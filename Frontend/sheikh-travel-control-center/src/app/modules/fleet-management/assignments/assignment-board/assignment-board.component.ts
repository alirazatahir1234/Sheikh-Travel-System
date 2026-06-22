import {
  ChangeDetectionStrategy, Component, computed, inject, OnInit, signal
} from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { FleetAssignmentService } from '../../../../core/services/fleet-assignment.service';
import { VehicleService } from '../../../../core/services/vehicle.service';
import { DriverService } from '../../../../core/services/driver.service';
import { PlatformService } from '../../../../core/services/platform.service';
import { ExportService, ExportColumn } from '../../../../core/services/export.service';
import {
  ASSIGNMENT_TYPES,
  AssignmentValidationIssue,
  assignmentEffectiveStatus,
  EMPTY_ASSIGNMENT_FILTERS,
  FleetAssignment,
  FleetAssignmentFilters,
  FleetAssignmentStats,
  TRANSFER_TYPES
} from '../../../../core/models/fleet-assignment.model';
import { FleetUiModule } from '../../../../shared/fleet-ui/fleet-ui.module';
import { UiSelectOption } from '../../../../shared/components/ui/types/ui.types';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { AssignmentKpiGridComponent } from '../components/assignment-kpi-grid.component';
import { AssignmentFiltersComponent } from '../components/assignment-filters.component';
import { AssignmentTableComponent } from '../components/assignment-table.component';
import { AssignmentCreateWizardComponent, AssignmentWizardForm } from '../components/assignment-create-wizard.component';
import { AssignmentHistoryPanelComponent } from '../components/assignment-history-panel.component';

@Component({
  selector: 'app-assignment-board',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    MatIconModule,
    FleetUiModule,
    AssignmentKpiGridComponent,
    AssignmentFiltersComponent,
    AssignmentTableComponent,
    AssignmentCreateWizardComponent,
    AssignmentHistoryPanelComponent
  ],
  templateUrl: './assignment-board.component.html',
  styleUrls: ['./assignment-board.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AssignmentBoardComponent implements OnInit {
  private readonly assignmentService = inject(FleetAssignmentService);
  private readonly vehicleService = inject(VehicleService);
  private readonly driverService = inject(DriverService);
  private readonly platformService = inject(PlatformService);
  private readonly exportService = inject(ExportService);
  private readonly toast = inject(UiToastService);

  readonly TRANSFER_TYPES = TRANSFER_TYPES;
  readonly ASSIGNMENT_TYPES = ASSIGNMENT_TYPES;

  readonly loading = signal(true);
  readonly statsLoading = signal(true);
  readonly stats = signal<FleetAssignmentStats | null>(null);
  readonly assignments = signal<FleetAssignment[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(20);
  readonly totalPages = computed(() => Math.ceil(this.totalCount() / this.pageSize()) || 1);

  readonly filters = signal<FleetAssignmentFilters>({ ...EMPTY_ASSIGNMENT_FILTERS });
  readonly drawerOpen = signal(false);
  readonly drawerMode = signal<'create' | 'transfer' | 'detail' | 'history'>('create');
  readonly selectedAssignment = signal<FleetAssignment | null>(null);
  readonly selectedIds = signal<Set<number>>(new Set());
  readonly bulkMode = signal(false);

  readonly vehicleOptions = signal<UiSelectOption[]>([]);
  readonly driverOptions = signal<UiSelectOption[]>([]);
  readonly branchOptions = signal<UiSelectOption[]>([]);

  readonly wizardForm = signal<AssignmentWizardForm>(this.emptyWizardForm());
  readonly validationIssues = signal<AssignmentValidationIssue[]>([]);
  readonly canSubmitWizard = signal(true);

  readonly transferForm = signal({
    transferType: 'Vehicle',
    newVehicleId: '',
    newDriverId: '',
    reason: '',
    notes: ''
  });

  readonly actionReason = signal('');
  readonly odometerEnd = signal('');
  readonly saving = signal(false);

  readonly allSelected = computed(() => {
    const rows = this.assignments();
    const sel = this.selectedIds();
    return rows.length > 0 && rows.every(r => sel.has(r.id));
  });

  ngOnInit(): void {
    this.loadStats();
    this.loadAssignments();
    this.loadPicklists();
  }

  private emptyWizardForm(): AssignmentWizardForm {
    return {
      vehicleIdStr: '',
      driverIdStr: '',
      vehicleId: 0,
      driverId: 0,
      assignmentType: 'Permanent',
      purpose: 'Permanent',
      startDate: new Date().toISOString().substring(0, 10),
      endDate: '',
      odometerStart: null,
      reason: '',
      notes: ''
    };
  }

  private loadStats(): void {
    this.statsLoading.set(true);
    this.assignmentService.stats().pipe(catchError(() => of(null))).subscribe(s => {
      this.stats.set(s);
      this.statsLoading.set(false);
    });
  }

  private loadAssignments(): void {
    this.loading.set(true);
    this.assignmentService.list(this.filters(), this.page(), this.pageSize()).pipe(
      catchError(() => of({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 }))
    ).subscribe(r => {
      this.assignments.set(r.items);
      this.totalCount.set(r.totalCount);
      this.loading.set(false);
    });
  }

  private loadPicklists(): void {
    forkJoin({
      vehicles: this.vehicleService.getAll(1, 200).pipe(catchError(() => of({ items: [] }))),
      drivers: this.driverService.getAll(1, 200).pipe(catchError(() => of({ items: [] }))),
      branches: this.platformService.getBranches().pipe(catchError(() => of([])))
    }).subscribe(({ vehicles, drivers, branches }) => {
      this.vehicleOptions.set(vehicles.items.map(v => ({
        value: String(v.id),
        label: `${v.registrationNumber} | ${[v.make, v.model].filter(Boolean).join(' ') || v.name}`
      })));
      this.driverOptions.set(drivers.items.map(d => ({
        value: String(d.id),
        label: `${d.fullName}${d.driverCode ? ' · ' + d.driverCode : ''}`
      })));
      this.branchOptions.set(branches.map(b => ({ value: String(b.id), label: b.name })));
    });
  }

  applyFilters(): void {
    this.page.set(1);
    this.loadAssignments();
  }

  clearFilters(): void {
    this.filters.set({ ...EMPTY_ASSIGNMENT_FILTERS });
    this.page.set(1);
    this.loadAssignments();
  }

  onKpiClick(status: string): void {
    if (!status) return;
    this.filters.update(f => ({ ...f, status }));
    this.applyFilters();
  }

  goToPage(p: number): void {
    if (p < 1 || p > this.totalPages()) return;
    this.page.set(p);
    this.loadAssignments();
  }

  openCreate(): void {
    this.wizardForm.set(this.emptyWizardForm());
    this.validationIssues.set([]);
    this.canSubmitWizard.set(true);
    this.drawerMode.set('create');
    this.drawerOpen.set(true);
  }

  openTransfer(a: FleetAssignment): void {
    this.selectedAssignment.set(a);
    this.transferForm.set({ transferType: 'Vehicle', newVehicleId: '', newDriverId: '', reason: '', notes: '' });
    this.drawerMode.set('transfer');
    this.drawerOpen.set(true);
  }

  openDetail(a: FleetAssignment): void {
    this.selectedAssignment.set(a);
    this.actionReason.set('');
    this.odometerEnd.set('');
    this.drawerMode.set('detail');
    this.drawerOpen.set(true);
  }

  openHistory(a: FleetAssignment): void {
    this.selectedAssignment.set(a);
    this.drawerMode.set('history');
    this.drawerOpen.set(true);
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
    this.selectedAssignment.set(null);
  }

  validateWizard(): void {
    const f = this.wizardForm();
    const vehicleId = Number(f.vehicleIdStr);
    const driverId = Number(f.driverIdStr);
    if (!vehicleId || !driverId) return;

    this.assignmentService.validate({
      vehicleId,
      driverId,
      startDate: f.startDate,
      assignmentType: f.assignmentType
    }).subscribe({
      next: result => {
        this.validationIssues.set(result.issues);
        this.canSubmitWizard.set(result.canProceed);
      },
      error: err => {
        this.canSubmitWizard.set(false);
        const message = apiErrorMessage(err, 'Could not validate assignment. Please try again.');
        this.validationIssues.set([{
          code: 'ValidateFailed',
          message,
          severity: 'Error'
        }]);
        this.toast.error(message);
      }
    });
  }

  saveCreate(): void {
    const f = this.wizardForm();
    const vehicleId = Number(f.vehicleIdStr);
    const driverId = Number(f.driverIdStr);
    if (!vehicleId || !driverId || !f.startDate) {
      this.toast.warning('Please complete all required fields.');
      return;
    }

    this.saving.set(true);
    this.assignmentService.create({
      vehicleId,
      driverId,
      assignmentType: f.assignmentType,
      startDate: f.startDate,
      endDate: f.endDate || null,
      purpose: f.purpose || f.assignmentType,
      odometerStart: f.odometerStart,
      reason: f.reason || null,
      notes: f.notes || null
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.closeDrawer();
        this.toast.success('Assignment created.');
        this.loadAssignments();
        this.loadStats();
      },
      error: err => {
        this.saving.set(false);
        this.toast.error(apiErrorMessage(err, 'Failed to create assignment.'));
      }
    });
  }

  saveTransfer(): void {
    const f = this.transferForm();
    const a = this.selectedAssignment();
    if (!a) return;

    const body = {
      transferType: f.transferType,
      newVehicleId: f.newVehicleId ? +f.newVehicleId : null,
      newDriverId: f.newDriverId ? +f.newDriverId : null,
      reason: f.reason || null,
      notes: f.notes || null
    };

    if (f.transferType === 'Vehicle' && !body.newVehicleId) {
      this.toast.warning('Select a new vehicle.');
      return;
    }
    if (f.transferType === 'Driver' && !body.newDriverId) {
      this.toast.warning('Select a new driver.');
      return;
    }

    this.saving.set(true);
    this.assignmentService.transfer(a.id, body).subscribe({
      next: () => {
        this.saving.set(false);
        this.closeDrawer();
        this.toast.success('Assignment transferred.');
        this.loadAssignments();
        this.loadStats();
      },
      error: err => {
        this.saving.set(false);
        this.toast.error(apiErrorMessage(err, 'Transfer failed.'));
      }
    });
  }

  completeAssignment(a: FleetAssignment): void {
    this.saving.set(true);
    this.assignmentService.complete(a.id, {
      reason: this.actionReason() || null,
      odometerEnd: this.odometerEnd() ? +this.odometerEnd() : null
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.closeDrawer();
        this.toast.success('Assignment completed.');
        this.loadAssignments();
        this.loadStats();
      },
      error: err => {
        this.saving.set(false);
        this.toast.error(apiErrorMessage(err, 'Failed to complete.'));
      }
    });
  }

  cancelAssignment(a: FleetAssignment): void {
    this.saving.set(true);
    this.assignmentService.cancel(a.id, { reason: this.actionReason() || null }).subscribe({
      next: () => {
        this.saving.set(false);
        this.closeDrawer();
        this.toast.success('Assignment cancelled.');
        this.loadAssignments();
        this.loadStats();
      },
      error: err => {
        this.saving.set(false);
        this.toast.error(apiErrorMessage(err, 'Failed to cancel.'));
      }
    });
  }

  approveAssignment(a: FleetAssignment): void {
    this.assignmentService.approve(a.id).subscribe({
      next: () => {
        this.toast.success('Assignment approved.');
        this.loadAssignments();
        this.loadStats();
        this.closeDrawer();
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Approval failed.'))
    });
  }

  toggleBulkMode(): void {
    this.bulkMode.update(v => !v);
    this.selectedIds.set(new Set());
  }

  toggleRow(id: number): void {
    this.selectedIds.update(set => {
      const next = new Set(set);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  toggleAll(checked: boolean): void {
    if (!checked) {
      this.selectedIds.set(new Set());
      return;
    }
    this.selectedIds.set(new Set(this.assignments().map(r => r.id)));
  }

  bulkComplete(): void {
    const ids = [...this.selectedIds()];
    if (!ids.length) return;
    this.saving.set(true);
    this.assignmentService.bulkComplete({ assignmentIds: ids, reason: this.actionReason() || null }).subscribe({
      next: r => {
        this.saving.set(false);
        this.toast.success(`Completed ${r.succeeded} assignment(s).`);
        this.selectedIds.set(new Set());
        this.loadAssignments();
        this.loadStats();
      },
      error: err => {
        this.saving.set(false);
        this.toast.error(apiErrorMessage(err, 'Bulk complete failed.'));
      }
    });
  }

  bulkCancel(): void {
    const ids = [...this.selectedIds()];
    if (!ids.length) return;
    this.saving.set(true);
    this.assignmentService.bulkCancel({ assignmentIds: ids, reason: this.actionReason() || null }).subscribe({
      next: r => {
        this.saving.set(false);
        this.toast.success(`Cancelled ${r.succeeded} assignment(s).`);
        this.selectedIds.set(new Set());
        this.loadAssignments();
        this.loadStats();
      },
      error: err => {
        this.saving.set(false);
        this.toast.error(apiErrorMessage(err, 'Bulk cancel failed.'));
      }
    });
  }

  exportExcel(): void {
    this.exportRows('excel');
  }

  exportPdf(): void {
    this.exportRows('pdf');
  }

  private exportRows(format: 'excel' | 'pdf'): void {
    const rows = this.assignments();
    const columns: ExportColumn<FleetAssignment>[] = [
      { header: 'Assignment #', accessor: r => r.assignmentNo },
      { header: 'Vehicle', accessor: r => r.vehicleName },
      { header: 'Plate', accessor: r => r.vehicleRegistration },
      { header: 'Driver', accessor: r => r.driverName },
      { header: 'Type', accessor: r => r.assignmentType },
      { header: 'Purpose', accessor: r => r.purpose },
      { header: 'Status', accessor: r => assignmentEffectiveStatus(r) },
      { header: 'Start', accessor: r => r.startAt?.slice(0, 10) },
      { header: 'End', accessor: r => r.endAt?.slice(0, 10) ?? '' },
      { header: 'GPS', accessor: r => r.gpsOnline ? 'Online' : 'Offline' },
      { header: 'Created By', accessor: r => r.createdBy }
    ];
    const meta = { filename: 'assignments', title: 'Assignment Report', sheetName: 'Assignments' };
    if (format === 'excel') this.exportService.exportExcel(rows, columns, meta);
    else this.exportService.exportPdf(rows, columns, meta);
  }

  rangeStart(): number { return (this.page() - 1) * this.pageSize() + 1; }
  rangeEnd(): number { return Math.min(this.page() * this.pageSize(), this.totalCount()); }

  effectiveStatus = assignmentEffectiveStatus;

  updateTransferForm(field: 'transferType' | 'newVehicleId' | 'newDriverId' | 'reason' | 'notes', value: string): void {
    this.transferForm.update(f => ({ ...f, [field]: value }));
  }
}
