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
import {
  FleetAssignment,
  FleetAssignmentStats,
  FleetAssignmentFilters,
  EMPTY_ASSIGNMENT_FILTERS,
  ASSIGNMENT_TYPES,
  ASSIGNMENT_STATUSES
} from '../../../../core/models/fleet-assignment.model';
import { FleetUiModule } from '../../../../shared/fleet-ui/fleet-ui.module';

interface VehicleOption { id: number; name: string; registrationNumber: string; vehicleCode?: string | null }
interface DriverOption { id: number; fullName: string; driverCode?: string | null }

@Component({
  selector: 'app-assignment-board',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule, DatePipe, FleetUiModule],
  templateUrl: './assignment-board.component.html',
  styleUrls: ['./assignment-board.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AssignmentBoardComponent implements OnInit {
  private readonly assignmentService = inject(FleetAssignmentService);
  private readonly vehicleService = inject(VehicleService);
  private readonly driverService = inject(DriverService);
  private readonly toast = inject(UiToastService);

  readonly ASSIGNMENT_TYPES = ASSIGNMENT_TYPES;
  readonly ASSIGNMENT_STATUSES = ASSIGNMENT_STATUSES;

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
  readonly drawerMode = signal<'create' | 'transfer' | 'detail'>('create');
  readonly selectedAssignment = signal<FleetAssignment | null>(null);

  readonly vehicles = signal<VehicleOption[]>([]);
  readonly drivers = signal<DriverOption[]>([]);

  // Create form
  readonly form = signal({
    vehicleId: '',
    driverId: '',
    assignmentType: 'Permanent',
    startDate: new Date().toISOString().substring(0, 10),
    endDate: '',
    reason: '',
    notes: ''
  });

  // Transfer form
  readonly transferForm = signal({ newVehicleId: '', reason: '', notes: '' });

  // Action reason (complete / cancel)
  readonly actionReason = signal('');
  readonly saving = signal(false);

  ngOnInit(): void {
    this.loadStats();
    this.loadAssignments();
    this.loadPicklists();
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
      vehicles: this.vehicleService.getAll(1, 200).pipe(catchError(() => of({ items: [] as VehicleOption[] }))),
      drivers: this.driverService.getAll(1, 200).pipe(catchError(() => of({ items: [] as DriverOption[] })))
    }).subscribe(({ vehicles, drivers }) => {
      this.vehicles.set(vehicles.items as VehicleOption[]);
      this.drivers.set(drivers.items as DriverOption[]);
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

  goToPage(p: number): void {
    if (p < 1 || p > this.totalPages()) return;
    this.page.set(p);
    this.loadAssignments();
  }

  openCreate(): void {
    this.form.set({
      vehicleId: '',
      driverId: '',
      assignmentType: 'Permanent',
      startDate: new Date().toISOString().substring(0, 10),
      endDate: '',
      reason: '',
      notes: ''
    });
    this.drawerMode.set('create');
    this.drawerOpen.set(true);
  }

  openTransfer(a: FleetAssignment): void {
    this.selectedAssignment.set(a);
    this.transferForm.set({ newVehicleId: '', reason: '', notes: '' });
    this.drawerMode.set('transfer');
    this.drawerOpen.set(true);
  }

  openDetail(a: FleetAssignment): void {
    this.selectedAssignment.set(a);
    this.actionReason.set('');
    this.drawerMode.set('detail');
    this.drawerOpen.set(true);
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
    this.selectedAssignment.set(null);
  }

  saveCreate(): void {
    const f = this.form();
    if (!f.vehicleId || !f.driverId || !f.startDate) {
      this.toast.warning('Please fill in all required fields.');
      return;
    }
    this.saving.set(true);
    this.assignmentService.create({
      vehicleId: +f.vehicleId,
      driverId: +f.driverId,
      assignmentType: f.assignmentType,
      startDate: f.startDate,
      endDate: f.endDate || null,
      reason: f.reason || null,
      notes: f.notes || null
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.closeDrawer();
        this.toast.success('Assignment created successfully.');
        this.loadAssignments();
        this.loadStats();
      },
      error: (err) => {
        this.saving.set(false);
        this.toast.error(err?.error?.message || 'Failed to create assignment.');
      }
    });
  }

  saveTransfer(): void {
    const f = this.transferForm();
    const a = this.selectedAssignment();
    if (!a || !f.newVehicleId) {
      this.toast.warning('Please select a new vehicle.');
      return;
    }
    this.saving.set(true);
    this.assignmentService.transfer(a.id, {
      newVehicleId: +f.newVehicleId,
      reason: f.reason || null,
      notes: f.notes || null
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.closeDrawer();
        this.toast.success('Assignment transferred successfully.');
        this.loadAssignments();
        this.loadStats();
      },
      error: (err) => {
        this.saving.set(false);
        this.toast.error(err?.error?.message || 'Transfer failed.');
      }
    });
  }

  completeAssignment(a: FleetAssignment): void {
    this.saving.set(true);
    this.assignmentService.complete(a.id, { reason: this.actionReason() || null }).subscribe({
      next: () => {
        this.saving.set(false);
        this.closeDrawer();
        this.toast.success('Assignment completed.');
        this.loadAssignments();
        this.loadStats();
      },
      error: (err) => {
        this.saving.set(false);
        this.toast.error(err?.error?.message || 'Failed to complete assignment.');
      }
    });
  }

  cancelAssignment(a: FleetAssignment): void {
    this.saving.set(true);
    this.assignmentService.cancel(a.id, { reason: this.actionReason() || null }).subscribe({
      next: () => {
        this.saving.set(false);
        this.closeDrawer();
        this.toast.warning('Assignment cancelled.');
        this.loadAssignments();
        this.loadStats();
      },
      error: (err) => {
        this.saving.set(false);
        this.toast.error(err?.error?.message || 'Failed to cancel assignment.');
      }
    });
  }

  statusVariant(status: string): string {
    if (status === 'Active') return 'success';
    if (status === 'Completed') return 'info';
    if (status === 'Cancelled') return 'error';
    return 'warning';
  }

  typeIcon(type: string): string {
    if (type === 'Permanent') return 'lock';
    if (type === 'Temporary') return 'schedule';
    if (type === 'Trip') return 'route';
    if (type === 'Transfer') return 'swap_horiz';
    return 'assignment';
  }

  vehicleLabel(v: VehicleOption): string {
    return `${v.registrationNumber} · ${v.vehicleCode || v.name}`;
  }

  rangeStart(): number { return (this.page() - 1) * this.pageSize() + 1; }
  rangeEnd(): number { return Math.min(this.page() * this.pageSize(), this.totalCount()); }

  updateFilter(key: keyof FleetAssignmentFilters, value: string): void {
    this.filters.update(f => ({ ...f, [key]: value }));
  }

  updateForm(key: string, value: string): void {
    this.form.update(f => ({ ...f, [key]: value }));
  }

  updateTransferForm(key: string, value: string): void {
    this.transferForm.update(f => ({ ...f, [key]: value }));
  }
}
