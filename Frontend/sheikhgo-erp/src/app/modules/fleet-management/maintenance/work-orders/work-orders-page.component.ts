import {
  ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { MaintenanceService } from '../../../../core/services/maintenance.service';
import { VehicleService } from '../../../../core/services/vehicle.service';
import { MaintenanceContextService } from '../maintenance-context.service';
import {
  WorkOrderListItem, WorkOrderStats, WorkOrderStatusLabels, Workshop
} from '../../../../core/models/maintenance.model';
import { VehicleListItem } from '../../../../core/models/vehicle.model';
import { ExportService } from '../../../../core/services/export.service';
import { WorkOrderDetailDrawerComponent } from './work-order-detail-drawer.component';
import { WoStatsComponent, WoStatKey } from './components/wo-stats.component';
import { WoFiltersComponent, WoFilterState } from './components/wo-filters.component';
import { WoTableComponent } from './components/wo-table.component';
import { woActualCost, woEstimatedCost } from './utils/wo.util';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';

const DEFAULT_FILTERS: WoFilterState = { vehicleId: 0, workshopId: 0, status: '', priority: '' };
const PAGE_SIZE = 20;

const STAT_STATUS_MAP: Record<Exclude<WoStatKey, ''>, string> = {
  open: 'Draft,Open,Assigned',
  inProgress: 'InProgress,WaitingParts',
  completed: 'Completed,Closed',
  cancelled: 'Cancelled'
};

@Component({
  selector: 'app-work-orders-page',
  standalone: true,
  imports: [
    MatIconModule,
    WoStatsComponent,
    WoFiltersComponent,
    WoTableComponent,
    WorkOrderDetailDrawerComponent
  ],
  templateUrl: './work-orders-page.component.html',
  styleUrls: ['./work-orders-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class WorkOrdersPageComponent implements OnInit {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly vehicleService = inject(VehicleService);
  private readonly exportService = inject(ExportService);
  private readonly toast = inject(UiToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  readonly ctx = inject(MaintenanceContextService);

  readonly orders = signal<WorkOrderListItem[]>([]);
  readonly stats = signal<WorkOrderStats | null>(null);
  readonly vehicles = signal<VehicleListItem[]>([]);
  readonly workshops = signal<Workshop[]>([]);
  readonly loading = signal(false);
  readonly showCreate = signal(false);
  readonly selectedId = signal<number | null>(null);

  readonly draftFilters = signal<WoFilterState>({ ...DEFAULT_FILTERS });
  readonly appliedFilters = signal<WoFilterState>({ ...DEFAULT_FILTERS });
  readonly activeStat = signal<WoStatKey>('');

  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly pageSize = PAGE_SIZE;
  readonly totalPages = computed(() => Math.ceil(this.totalCount() / this.pageSize) || 1);

  readonly statusOptions = Object.entries(WorkOrderStatusLabels).map(([v, l]) => ({ value: v, label: l }));

  ngOnInit(): void {
    if (this.route.snapshot.queryParamMap.get('create') === 'true') this.showCreate.set(true);
    const wo = this.route.snapshot.queryParamMap.get('wo');
    if (wo) this.selectedId.set(+wo);

    this.vehicleService.getAll(1, 500).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => this.vehicles.set(r.items),
      error: () => this.vehicles.set([])
    });
    this.maintenanceService.getWorkshops().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: w => this.workshops.set(w),
      error: () => this.workshops.set([])
    });
    this.loadStats();
    this.load();
  }

  loadStats(): void {
    this.maintenanceService.getWorkOrderStats().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: s => this.stats.set(s),
      error: () => this.stats.set({ open: 0, inProgress: 0, completed: 0, cancelled: 0 })
    });
  }

  load(): void {
    this.loading.set(true);
    const f = this.appliedFilters();
    const stat = this.activeStat();
    const search = this.ctx.searchTerm() || undefined;
    this.maintenanceService.getWorkOrders(this.page(), this.pageSize, {
      statuses: stat ? STAT_STATUS_MAP[stat] : undefined,
      status: stat ? undefined : (f.status || undefined),
      search,
      vehicleId: f.vehicleId || undefined,
      workshopId: f.workshopId || undefined,
      priority: f.priority || undefined
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => {
        this.orders.set(r.items);
        this.totalCount.set(r.totalCount);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.toast.error(apiErrorMessage(err, 'Failed to load work orders'));
      }
    });
  }

  onStatSelect(key: WoStatKey): void {
    const next = this.activeStat() === key ? '' : key;
    this.activeStat.set(next);
    this.draftFilters.update(f => ({ ...f, status: '' }));
    this.appliedFilters.update(f => ({ ...f, status: '' }));
    this.page.set(1);
    this.load();
  }

  onDraftChange(f: WoFilterState): void {
    this.draftFilters.set(f);
  }

  onApply(): void {
    this.activeStat.set('');
    this.appliedFilters.set({ ...this.draftFilters() });
    this.page.set(1);
    this.load();
  }

  onReset(): void {
    this.activeStat.set('');
    this.draftFilters.set({ ...DEFAULT_FILTERS });
    this.appliedFilters.set({ ...DEFAULT_FILTERS });
    this.page.set(1);
    this.load();
  }

  goToPage(p: number): void {
    if (p < 1 || p > this.totalPages()) return;
    this.page.set(p);
    this.load();
  }

  rangeStart(): number {
    return this.totalCount() === 0 ? 0 : (this.page() - 1) * this.pageSize + 1;
  }

  rangeEnd(): number {
    return Math.min(this.page() * this.pageSize, this.totalCount());
  }

  onCreated(): void {
    this.showCreate.set(false);
    this.loadStats();
    this.load();
    this.toast.success('Work order created.');
  }

  onChanged(): void {
    this.loadStats();
    this.load();
  }

  exportOrders(): void {
    const rows = this.orders();
    if (!rows.length) {
      this.toast.warning('No work orders to export');
      return;
    }
    this.exportService.exportExcel(rows, [
      { header: 'Work Order No', accessor: r => r.workOrderNumber },
      { header: 'Vehicle', accessor: r => r.vehicleName ?? '' },
      { header: 'Service Type', accessor: r => r.serviceTypeName ?? '' },
      { header: 'Workshop', accessor: r => r.workshopName ?? '' },
      { header: 'Technician', accessor: r => r.technicianName ?? '' },
      { header: 'Estimated Cost', accessor: r => woEstimatedCost(r) },
      { header: 'Actual Cost', accessor: r => woActualCost(r) },
      { header: 'Start Date', accessor: r => r.startDate ?? '' },
      { header: 'Completion Date', accessor: r => r.completedAt ?? '' },
      { header: 'Status', accessor: r => r.status }
    ], { title: 'Work Orders', filename: 'work-orders' });
    this.toast.success('Excel exported');
  }
}
