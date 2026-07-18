import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';

import { UiModule } from '../../../shared/components/ui';
import { FleetService } from '../services/fleet.service';
import { MaintenanceService } from '../../../core/services/maintenance.service';
import { AiPlatformService } from '../../../core/services/ai-platform.service';
import { AssignmentRow, FleetDashboardSummary } from '../models/fleet.model';
import { AssignmentItem, FleetKpi, FuelMaintenanceChart } from './fleet-dashboard.model';
import {
  ACTIVITIES_MOCK,
  ALERTS_MOCK,
  ASSIGNMENTS_MOCK,
  FUEL_MAINTENANCE_MOCK,
  QUICK_ACTIONS_MOCK,
  UTILIZATION_MOCK
} from './fleet-dashboard.mock';

import { DashboardKpiRowComponent } from './widgets/dashboard-kpi-row.component';
import { UtilizationChartComponent } from './widgets/utilization-chart.component';
import { FuelMaintenanceChartComponent } from './widgets/fuel-maintenance-chart.component';
import { QuickActionsCardComponent } from './widgets/quick-actions-card.component';
import { CriticalAlertsCardComponent } from './widgets/critical-alerts-card.component';
import { RecentActivitiesCardComponent } from './widgets/recent-activities-card.component';
import { ActiveAssignmentsTableComponent } from './widgets/active-assignments-table.component';
import { FleetFabComponent } from './widgets/fleet-fab.component';

/**
 * Fleet dashboard content (bento grid only, no shell). Shared by the `/fleet/dashboard`
 * hub and the `/dashboard` selector so KPI/chart/table logic lives in one place.
 */
@Component({
  selector: 'fleet-dashboard-content',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    UiModule,
    RouterLink,
    DecimalPipe,
    DashboardKpiRowComponent,
    UtilizationChartComponent,
    FuelMaintenanceChartComponent,
    QuickActionsCardComponent,
    CriticalAlertsCardComponent,
    RecentActivitiesCardComponent,
    ActiveAssignmentsTableComponent,
    FleetFabComponent
  ],
  template: `
    <div class="fleet-dashboard">
      <div class="mx-auto w-full max-w-[1440px] space-y-gutter px-page py-6">
        <div class="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <h2 class="text-3xl font-bold tracking-tight text-fleet-text">Fleet Overview</h2>
            <p class="text-sm text-fleet-text-muted">Real-time logistics performance and health metrics.</p>
          </div>
          <div class="flex flex-wrap gap-3">
            <a routerLink="/ai" class="inline-flex items-center gap-1 rounded-lg border border-fleet-border bg-white px-3 py-2 text-sm font-medium text-fleet-text hover:border-fleet-primary">
              AI Center
            </a>
            <ui-button variant="neutral" size="md" icon="calendar_today">Last 30 Days</ui-button>
            <ui-button variant="primary" size="md" icon="add">Export Report</ui-button>
          </div>
        </div>

        @if (aiHealth(); as h) {
          <div class="rounded-xl border border-teal-200 bg-gradient-to-r from-teal-50 to-white p-4 flex flex-wrap items-center gap-6">
            <div>
              <p class="text-[11px] font-bold uppercase tracking-wider text-teal-700">Fleet Health</p>
              <p class="text-3xl font-bold text-teal-800">{{ h.healthPercent | number:'1.0-1' }}%</p>
            </div>
            <p class="text-sm text-slate-600 flex-1 min-w-[200px]">{{ h.summary }}</p>
            <div class="flex gap-4 text-sm text-slate-600">
              <span>GPS {{ h.gpsOnlineRate | number:'1.0-0' }}%</span>
              <span>Drivers {{ h.driverScore | number:'1.0-0' }}</span>
              @if (h.criticalAlerts > 0) {
                <span class="text-red-600">{{ h.criticalAlerts }} critical</span>
              }
            </div>
          </div>
        }

        @if (recommendations().length) {
          <div class="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
            @for (r of recommendations().slice(0, 3); track r.id) {
              <div class="rounded-xl border border-fleet-border bg-white p-4">
                <p class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">{{ r.category }} · {{ r.severity }}</p>
                <p class="mt-1 font-semibold text-fleet-text">{{ r.title }}</p>
                <p class="mt-1 text-sm text-fleet-text-muted">{{ r.action }}</p>
              </div>
            }
          </div>
        }

        <fleet-dashboard-kpi-row [kpis]="kpis()"></fleet-dashboard-kpi-row>

        <div class="grid grid-cols-12 gap-gutter">
          <div class="col-span-12 lg:col-span-8">
            <fleet-utilization-chart [data]="utilization"></fleet-utilization-chart>
          </div>

          <div class="col-span-12 space-y-gutter lg:col-span-4">
            <fleet-quick-actions-card [actions]="quickActions"></fleet-quick-actions-card>
            <fleet-critical-alerts-card [alerts]="alerts"></fleet-critical-alerts-card>
          </div>

          <div class="col-span-12 lg:col-span-8">
            <fleet-fuel-maintenance-chart [data]="fuelMaintenance()"></fleet-fuel-maintenance-chart>
          </div>

          <div class="col-span-12 lg:col-span-4">
            <fleet-recent-activities-card [events]="activities"></fleet-recent-activities-card>
          </div>

          <div class="col-span-12">
            <fleet-active-assignments-table
              [rows]="assignments()"
              [loading]="loadingAssignments()"></fleet-active-assignments-table>
          </div>
        </div>
      </div>

      <fleet-fab></fleet-fab>
    </div>
  `,
  styles: [`
    :host { display: block; }
    .fleet-dashboard {
      min-height: 100%;
      background: var(--fleet-surface);
      font-family: 'Plus Jakarta Sans', 'Inter', sans-serif;
    }
  `]
})
export class FleetDashboardContentComponent {
  private readonly fleet = inject(FleetService);
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly ai = inject(AiPlatformService);

  private readonly summary = toSignal<FleetDashboardSummary | null>(this.fleet.getDashboard(), {
    initialValue: null
  });
  private readonly assignmentRows = toSignal<AssignmentRow[] | null>(this.fleet.getAssignments(), {
    initialValue: null
  });
  private readonly maintenanceDashboard = toSignal(
    this.maintenanceService.getDashboard('Month').pipe(catchError(() => of(null))),
    { initialValue: null }
  );
  readonly aiHealth = toSignal(this.ai.getHealth().pipe(catchError(() => of(null))), { initialValue: null });
  readonly recommendations = toSignal(this.ai.getRecommendations().pipe(catchError(() => of([]))), {
    initialValue: [] as import('../../../core/services/ai-platform.service').AiRecommendation[]
  });

  readonly loadingAssignments = computed(() => this.assignmentRows() === null);

  readonly utilization = UTILIZATION_MOCK;
  readonly fuelMaintenance = computed<FuelMaintenanceChart>(() => {
    const fuel = this.maintenanceDashboard()?.fuelSummary;
    if (fuel?.labels?.length) {
      return { labels: fuel.labels, fuel: fuel.fuelCosts, maintenance: fuel.maintenanceCosts };
    }
    return FUEL_MAINTENANCE_MOCK;
  });
  readonly quickActions = QUICK_ACTIONS_MOCK;
  readonly alerts = ALERTS_MOCK;
  readonly activities = ACTIVITIES_MOCK;

  readonly kpis = computed<FleetKpi[]>(() => {
    const s = this.summary();
    const health = this.aiHealth();
    return [
      {
        id: 'health',
        label: 'Fleet Health',
        value: health ? `${Math.round(health.healthPercent)}%` : '—',
        icon: 'monitor_heart',
        tone: 'primary',
        trend: health?.summary?.slice(0, 40) ?? 'AI health score',
        alert: (health?.criticalAlerts ?? 0) > 0
      },
      { id: 'total', label: 'Total Vehicles', value: this.count(s?.totalVehicles, 0), icon: 'local_shipping', tone: 'primary' },
      {
        id: 'active',
        label: 'Active',
        value: this.count(s?.activeVehicles, 0),
        icon: 'radar',
        tone: 'secondary',
        trend: health ? `GPS ${Math.round(health.gpsOnlineRate)}% online` : undefined
      },
      {
        id: 'maintenance',
        label: 'In Maintenance',
        value: this.count(this.maintenanceDashboard()?.kpis?.underMaintenance ?? s?.maintenanceDue, 0),
        icon: 'build',
        tone: 'error',
        alert: (this.maintenanceDashboard()?.kpis?.overdueServices ?? 0) > 0,
        trend: `${this.maintenanceDashboard()?.kpis?.overdueServices ?? 0} overdue service`,
        trendUp: false
      },
      {
        id: 'drivers',
        label: 'Active Drivers',
        value: this.count(s?.driversOnDuty, 0),
        icon: 'person',
        tone: 'primary',
        trend: health ? `Score ${Math.round(health.driverScore)}` : undefined
      },
      { id: 'fuel', label: 'Fuel Cost', value: this.currency(s?.monthlyFuelCost, 0), icon: 'local_gas_station', tone: 'secondary' }
    ];
  });

  readonly assignments = computed<AssignmentItem[]>(() => {
    const rows = this.assignmentRows();
    if (!rows?.length) {
      return ASSIGNMENTS_MOCK;
    }
    return rows.map((row, index) => ({
      id: row.id,
      vehicleId: row.vehicleName,
      vehicleModel: row.assignmentType,
      driver: row.driverName || 'Unassigned',
      route: ASSIGNMENTS_MOCK[index % ASSIGNMENTS_MOCK.length].route,
      status: row.status,
      eta: this.eta(row.endAt)
    }));
  });

  private count(value: number | undefined, fallback: number): string {
    return (value != null ? value : fallback).toLocaleString();
  }

  private currency(value: number | undefined, fallback: number): string {
    const amount = value != null && value > 0 ? value : fallback;
    return amount >= 1000 ? `$${Math.round(amount / 1000)}k` : `$${amount}`;
  }

  private eta(endAt?: string): string {
    if (!endAt) {
      return '—';
    }
    const date = new Date(endAt);
    return isNaN(date.getTime())
      ? '—'
      : date.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
  }
}
