import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MaintenanceService } from '../../../../core/services/maintenance.service';
import { ExportService, ExportColumn } from '../../../../core/services/export.service';
import { MaintenanceContextService } from '../maintenance-context.service';
import { MaintenanceDashboard } from '../../../../core/models/maintenance.model';
import { MaintenanceKpiGridComponent } from '../components/maintenance-kpi-grid.component';
import { MaintenanceCostTrendComponent } from '../components/maintenance-cost-trend.component';
import { MaintenanceQuickActionsComponent } from '../components/maintenance-quick-actions.component';
import { MaintenanceRecentWorkOrdersComponent } from '../components/maintenance-recent-work-orders.component';
import { MaintenanceCriticalAlertsComponent } from '../components/maintenance-critical-alerts.component';
import { MaintenancePendingRequestsComponent } from '../components/maintenance-pending-requests.component';
import { MaintenanceFuelSummaryComponent } from '../components/maintenance-fuel-summary.component';
import {
  MaintenanceUpcomingServicesComponent,
  MaintenanceVehicleHealthComponent
} from '../components/maintenance-health-widgets.component';
import { WorkOrderDetailDrawerComponent } from '../work-orders/work-order-detail-drawer.component';
import { AppBrandLoaderComponent } from '../../../../shared/components/app-brand-loader/app-brand-loader.component';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { maintenanceDashboardGranularity } from '../utils/maintenance-period.util';

@Component({
  selector: 'app-maintenance-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    CurrencyPipe,
    AppBrandLoaderComponent,
    MaintenanceKpiGridComponent,
    MaintenanceCostTrendComponent,
    MaintenanceQuickActionsComponent,
    MaintenanceRecentWorkOrdersComponent,
    MaintenanceCriticalAlertsComponent,
    MaintenanceVehicleHealthComponent,
    MaintenanceUpcomingServicesComponent,
    MaintenancePendingRequestsComponent,
    MaintenanceFuelSummaryComponent,
    WorkOrderDetailDrawerComponent
  ],
  templateUrl: './maintenance-dashboard.component.html',
  styleUrls: ['./maintenance-dashboard.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MaintenanceDashboardComponent {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly exportService = inject(ExportService);
  private readonly toast = inject(UiToastService);
  private readonly destroyRef = inject(DestroyRef);
  readonly ctx = inject(MaintenanceContextService);

  readonly loading = signal(true);
  readonly dashboard = signal<MaintenanceDashboard | null>(null);
  readonly selectedWorkOrderId = signal<number | null>(null);

  constructor() {
    effect(() => {
      this.ctx.period();
      this.load();
    }, { allowSignalWrites: true });

    this.ctx.exportRequested$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.exportReport());
  }

  onPeriodChange(period: string): void {
    this.ctx.period.set(period);
  }

  load(): void {
    const period = this.ctx.period();
    this.loading.set(true);
    this.maintenanceService.getDashboard(period, maintenanceDashboardGranularity(period)).subscribe({
      next: d => {
        this.dashboard.set(d);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.toast.error(apiErrorMessage(err, 'Failed to load maintenance dashboard'));
      }
    });
  }

  openWorkOrder(id: number): void {
    this.selectedWorkOrderId.set(id);
  }

  exportReport(): void {
    const d = this.dashboard();
    if (!d) return;

    const cols: ExportColumn<{ label: string; value: number }>[] = [
      { header: 'Metric', accessor: r => r.label },
      { header: 'Value', accessor: r => r.value }
    ];
    const rows = [
      { label: 'Total Vehicles', value: d.kpis.totalVehicles },
      { label: 'Due for Service', value: d.kpis.dueForService },
      { label: 'Under Maintenance', value: d.kpis.underMaintenance },
      { label: 'Overdue Services', value: d.kpis.overdueServices },
      { label: 'Monthly Cost', value: d.kpis.monthlyMaintenanceCost },
      { label: 'Active Work Orders', value: d.kpis.activeWorkOrders },
      { label: 'Pending Requests', value: d.kpis.pendingRequests ?? 0 }
    ];
    this.exportService.exportExcel(rows, cols, { title: 'Maintenance Dashboard', filename: 'maintenance-dashboard' });
    this.toast.success('Report exported');
  }
}
