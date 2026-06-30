import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { finalize } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { MaintenanceService } from '../../../../core/services/maintenance.service';
import { VehicleService } from '../../../../core/services/vehicle.service';
import { ExportService, ExportColumn } from '../../../../core/services/export.service';
import { MaintenanceContextService } from '../maintenance-context.service';
import {
  MaintenanceRequest,
  MaintenanceRequestStats,
  CreateMaintenanceRequestPayload
} from '../../../../core/models/maintenance.model';
import { VehicleListItem } from '../../../../core/models/vehicle.model';
import { RequestKpiChipsComponent } from './components/request-kpi-chips.component';
import { RequestTableComponent } from './components/request-table.component';
import { RequestMobileCardsComponent } from './components/request-mobile-cards.component';
import { RequestDetailDrawerComponent } from './components/request-detail-drawer.component';
import { RequestCreateFormComponent } from './components/request-create-form.component';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { isWithinMaintenancePeriod } from '../utils/maintenance-period.util';

@Component({
  selector: 'app-maintenance-requests-page',
  standalone: true,
  imports: [
    CommonModule,
    MatIconModule,
    RequestKpiChipsComponent,
    RequestTableComponent,
    RequestMobileCardsComponent,
    RequestDetailDrawerComponent,
    RequestCreateFormComponent
  ],
  templateUrl: './maintenance-requests-page.component.html',
  styleUrls: ['./maintenance-requests-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MaintenanceRequestsPageComponent implements OnInit {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly vehicleService = inject(VehicleService);
  private readonly exportService = inject(ExportService);
  private readonly toast = inject(UiToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly ctx = inject(MaintenanceContextService);
  private readonly destroyRef = inject(DestroyRef);

  readonly requests = signal<MaintenanceRequest[]>([]);
  readonly stats = signal<MaintenanceRequestStats | null>(null);
  readonly vehicles = signal<VehicleListItem[]>([]);
  readonly statusFilter = signal('');
  readonly showForm = signal(false);
  readonly formResetKey = signal(0);
  readonly saving = signal(false);
  readonly selectedId = signal<number | null>(null);

  constructor() {
    effect(() => {
      this.ctx.searchTerm();
      this.ctx.period();
      this.statusFilter();
      this.load();
    }, { allowSignalWrites: true });

    this.ctx.exportRequested$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.exportReport());
  }

  ngOnInit(): void {
    const status = this.route.snapshot.queryParamMap.get('status');
    if (status) this.statusFilter.set(status);
    if (this.route.snapshot.queryParamMap.get('create') === 'true') this.showForm.set(true);

    this.vehicleService.getAll(1, 500).subscribe({
      next: r => this.vehicles.set(r.items),
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to load vehicles'))
    });
  }

  load(): void {
    const search = this.ctx.searchTerm() || undefined;
    const status = this.statusFilter() || undefined;
    const period = this.ctx.period();

    this.maintenanceService.getRequests(1, 500, status, search).subscribe({
      next: r => {
        const filtered = r.items.filter(item =>
          isWithinMaintenancePeriod(item.createdAt || item.requestDate, period)
        );
        this.requests.set(filtered);
        this.stats.set(this.buildStats(filtered));
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to load requests'))
    });
  }

  private buildStats(items: MaintenanceRequest[]): MaintenanceRequestStats {
    return {
      open: items.filter(r => r.status === 'Open').length,
      approved: items.filter(r => r.status === 'Approved').length,
      inProgress: items.filter(r => r.status === 'InProgress' || r.status === 'Converted').length,
      pendingApproval: items.filter(r => r.status === 'PendingApproval').length
    };
  }

  exportReport(): void {
    const rows = this.requests();
    if (!rows.length) {
      this.toast.warning('No service requests to export for the selected period.');
      return;
    }

    const period = this.ctx.period();
    const status = this.statusFilter();
    const cols: ExportColumn<MaintenanceRequest>[] = [
      { header: 'Request #', accessor: r => r.requestNumber },
      { header: 'Vehicle', accessor: r => r.vehicleName ?? '' },
      { header: 'Registration', accessor: r => r.vehicleRegistration ?? '' },
      { header: 'Driver', accessor: r => r.driverName ?? '' },
      { header: 'Category', accessor: r => r.issueCategory },
      { header: 'Type', accessor: r => r.requestType },
      { header: 'Priority', accessor: r => r.priority },
      { header: 'Status', accessor: r => r.status },
      { header: 'Request Date', accessor: r => r.requestDate },
      { header: 'Description', accessor: r => r.description, excelWidth: 40 }
    ];

    const subtitle = [
      `Period: ${period}`,
      status ? `Status: ${status}` : 'Status: All',
      this.ctx.searchTerm() ? `Search: ${this.ctx.searchTerm()}` : null
    ].filter(Boolean).join(' · ');

    this.exportService.exportExcel(rows, cols, {
      title: 'Service Requests',
      subtitle,
      filename: `service-requests-${period.toLowerCase()}`,
      sheetName: 'Service Requests'
    });
    this.toast.success('Report exported');
  }

  onFilterChange(status: string): void {
    this.statusFilter.set(status);
  }

  onSelect(id: number): void {
    this.selectedId.set(id);
  }

  onChanged(): void {
    this.load();
  }

  submit(form: CreateMaintenanceRequestPayload): void {
    this.saving.set(true);
    this.maintenanceService.createRequest({
      ...form,
      description: form.description.trim()
    }).pipe(
      finalize(() => this.saving.set(false))
    ).subscribe({
      next: () => {
        this.showForm.set(false);
        this.formResetKey.update(k => k + 1);
        this.load();
        this.toast.success('Request created');
      },
      error: err => {
        this.toast.error(apiErrorMessage(err, 'Failed to create request'));
      }
    });
  }

  closeForm(): void {
    this.showForm.set(false);
    this.formResetKey.update(k => k + 1);
  }
}
