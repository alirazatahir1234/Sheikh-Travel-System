import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { DecimalPipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';

import { UiModule } from '../../../shared/components/ui';
import { FleetService } from '../services/fleet.service';
import { MaintenanceService } from '../../../core/services/maintenance.service';
import { AiPlatformService } from '../../../core/services/ai-platform.service';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { AssignmentRow, FleetDashboardSummary } from '../models/fleet.model';
import { AssignmentItem, CriticalAlert, ActivityEvent, FleetKpi, FuelMaintenanceChart, UtilizationChart } from './fleet-dashboard.model';
import {
  FLEET_NEUTRAL,
  FLEET_PRIMARY,
  FUEL_MAINTENANCE_MOCK,
  QUICK_ACTIONS_MOCK
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
            <a
              routerLink="/reports"
              class="inline-flex items-center gap-1 rounded-lg border border-fleet-border bg-white px-3 py-2 text-sm font-medium text-fleet-text hover:border-fleet-primary">
              Reports
            </a>
            <a
              routerLink="/gps-tracking/live"
              class="inline-flex items-center gap-1 rounded-lg bg-fleet-primary px-3 py-2 text-sm font-medium text-white hover:opacity-90">
              Live map
            </a>
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
            <fleet-utilization-chart [data]="utilization()"></fleet-utilization-chart>
          </div>

          <div class="col-span-12 space-y-gutter lg:col-span-4">
            <fleet-quick-actions-card [actions]="quickActions"></fleet-quick-actions-card>
            <fleet-critical-alerts-card [alerts]="alerts()"></fleet-critical-alerts-card>
          </div>

          <div class="col-span-12 lg:col-span-8">
            <fleet-fuel-maintenance-chart [data]="fuelMaintenance()"></fleet-fuel-maintenance-chart>
          </div>

          <div class="col-span-12 lg:col-span-4">
            <fleet-recent-activities-card [events]="activities()"></fleet-recent-activities-card>
          </div>

          <div class="col-span-12">
            <fleet-active-assignments-table
              [rows]="assignments()"
              [loading]="loadingAssignments()"></fleet-active-assignments-table>
          </div>
        </div>
      </div>

      <fleet-fab ariaLabel="Add vehicle" (action)="onFab()"></fleet-fab>
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
  private readonly gps = inject(GpsTrackingService);
  private readonly router = inject(Router);

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
  private readonly gpsLocal = toSignal(
    this.gps.getFleetStatusLocal().pipe(catchError(() => of(null))),
    { initialValue: null }
  );
  private readonly gpsHistory = toSignal(
    this.gps.getFleetStatusHistory(
      new Date(Date.now() - 7 * 24 * 60 * 60 * 1000),
      new Date()
    ).pipe(catchError(() => of([]))),
    { initialValue: [] as import('../../../core/models/gps-tracking.model').GpsFleetStatusSnapshot[] }
  );
  readonly aiHealth = toSignal(this.ai.getHealth().pipe(catchError(() => of(null))), { initialValue: null });
  readonly recommendations = toSignal(this.ai.getRecommendations().pipe(catchError(() => of([]))), {
    initialValue: [] as import('../../../core/services/ai-platform.service').AiRecommendation[]
  });

  readonly loadingAssignments = computed(() => this.assignmentRows() === null);

  readonly utilization = computed<UtilizationChart>(() => {
    const history = this.gpsHistory() || [];
    if (history.length >= 2) {
      const byDay = new Map<string, { online: number; total: number }>();
      for (const row of history) {
        const key = (row.snapshotAt || '').slice(0, 10);
        if (!key) continue;
        const prev = byDay.get(key) || { online: 0, total: 0 };
        prev.online = Math.max(prev.online, row.online || 0);
        prev.total = Math.max(prev.total, row.totalVehicles || 0);
        byDay.set(key, prev);
      }
      const keys = [...byDay.keys()].sort().slice(-7);
      if (keys.length) {
        return {
          labels: keys.map(k => {
            const d = new Date(k + 'T00:00:00');
            return d.toLocaleDateString(undefined, { weekday: 'short' });
          }),
          series: [
            {
              label: 'Online %',
              values: keys.map(k => {
                const v = byDay.get(k)!;
                return v.total ? Math.round((v.online / v.total) * 100) : 0;
              }),
              color: FLEET_PRIMARY
            }
          ]
        };
      }
    }

    const local = this.gpsLocal();
    const s = this.summary();
    const total = local?.totalVehicles || s?.totalVehicles || 0;
    const online = local?.online ?? s?.activeVehicles ?? 0;
    const pct = total ? Math.round((online / total) * 100) : 0;
    const labels = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
    return {
      labels,
      series: [
        { label: 'Online %', values: labels.map(() => pct), color: FLEET_PRIMARY },
        { label: 'Baseline', values: labels.map(() => 0), color: FLEET_NEUTRAL }
      ]
    };
  });

  readonly fuelMaintenance = computed<FuelMaintenanceChart>(() => {
    const fuel = this.maintenanceDashboard()?.fuelSummary;
    if (fuel?.labels?.length) {
      return { labels: fuel.labels, fuel: fuel.fuelCosts, maintenance: fuel.maintenanceCosts };
    }
    // Honest empty chart — do not show invented US demo series.
    return { labels: FUEL_MAINTENANCE_MOCK.labels, fuel: FUEL_MAINTENANCE_MOCK.labels.map(() => 0), maintenance: FUEL_MAINTENANCE_MOCK.labels.map(() => 0) };
  });

  readonly quickActions = QUICK_ACTIONS_MOCK.map(a => {
    if (a.id === 'schedule-maint') return { ...a, route: '/fleet/maintenance', tone: 'orange' as const };
    if (a.id === 'assign-driver') return { ...a, route: '/fleet/assignments', tone: 'blue' as const };
    if (a.id === 'add-vehicle') return { ...a, tone: 'green' as const };
    if (a.id === 'add-driver') return { ...a, tone: 'blue' as const };
    return a;
  });

  readonly alerts = computed<CriticalAlert[]>(() => {
    const out: CriticalAlert[] = [];
    const gps = this.gpsLocal();
    const maint = this.maintenanceDashboard()?.kpis;
    const health = this.aiHealth();

    if ((gps?.sos ?? 0) > 0) {
      out.push({
        id: 'sos',
        icon: 'sos',
        tone: 'error',
        title: `${gps!.sos} SOS alert${gps!.sos === 1 ? '' : 's'}`,
        detail: 'Open live map and respond immediately.',
        actionLabel: 'Open map'
      });
    }
    if ((gps?.alertsToday ?? 0) > 0) {
      out.push({
        id: 'gps-alerts',
        icon: 'warning',
        tone: 'warning',
        title: `${gps!.alertsToday} GPS alert${gps!.alertsToday === 1 ? '' : 's'} today`,
        detail: 'Review offline, geofence, and overspeed events.',
        actionLabel: 'View alerts'
      });
    }
    if ((maint?.overdueServices ?? 0) > 0) {
      out.push({
        id: 'maint-overdue',
        icon: 'build',
        tone: 'error',
        title: `${maint!.overdueServices} overdue service${maint!.overdueServices === 1 ? '' : 's'}`,
        detail: 'Schedule maintenance before the next dispatch.',
        actionLabel: 'Maintenance'
      });
    }
    if ((this.summary()?.complianceAlerts ?? 0) > 0) {
      out.push({
        id: 'compliance',
        icon: 'policy',
        tone: 'warning',
        title: `${this.summary()!.complianceAlerts} compliance document${this.summary()!.complianceAlerts === 1 ? '' : 's'} expiring`,
        detail: 'Renew papers within 30 days.',
        actionLabel: 'Compliance'
      });
    }
    if ((health?.criticalAlerts ?? 0) > 0 && !out.some(a => a.id === 'sos')) {
      out.push({
        id: 'ai-critical',
        icon: 'error',
        tone: 'error',
        title: `${health!.criticalAlerts} critical fleet health issue${health!.criticalAlerts === 1 ? '' : 's'}`,
        detail: health?.summary || 'Review AI fleet health recommendations.'
      });
    }
    return out;
  });

  readonly activities = computed<ActivityEvent[]>(() => {
    const rows = this.assignmentRows() || [];
    return rows.slice(0, 6).map((row, i) => ({
      id: `a-${row.id}`,
      title: `${row.vehicleName} · ${row.assignmentType} · ${row.status}`,
      timeAgo: this.relativeTime(row.endAt || row.startAt),
      tone: (i % 2 === 0 ? 'primary' : 'secondary') as ActivityEvent['tone']
    }));
  });

  readonly kpis = computed<FleetKpi[]>(() => {
    const s = this.summary();
    const health = this.aiHealth();
    const gps = this.gpsLocal();
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
        trend: gps
          ? `GPS ${gps.online}/${gps.totalVehicles} online`
          : health
            ? `GPS ${Math.round(health.gpsOnlineRate)}% online`
            : undefined
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
      { id: 'fuel', label: 'Fuel Cost (MTD)', value: this.currency(s?.monthlyFuelCost, 0), icon: 'local_gas_station', tone: 'secondary' }
    ];
  });

  readonly assignments = computed<AssignmentItem[]>(() => {
    const rows = this.assignmentRows();
    if (!rows?.length) {
      return [];
    }
    return rows.map((row) => ({
      id: row.id,
      vehicleId: row.vehicleName,
      vehicleModel: row.assignmentType,
      driver: row.driverName || 'Unassigned',
      route: row.assignmentType,
      status: row.status,
      eta: this.eta(row.endAt)
    }));
  });

  onFab(): void {
    this.router.navigate(['/vehicles']);
  }

  private count(value: number | undefined, fallback: number): string {
    return (value != null ? value : fallback).toLocaleString();
  }

  private currency(value: number | undefined, fallback: number): string {
    const amount = value != null ? value : fallback;
    return 'PKR ' + Math.round(amount).toLocaleString();
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

  private relativeTime(iso?: string): string {
    if (!iso) return '—';
    const d = new Date(iso);
    if (isNaN(d.getTime())) return '—';
    const diffMs = Date.now() - d.getTime();
    const mins = Math.round(diffMs / 60000);
    if (mins < 60) return `${Math.max(0, mins)} min ago`;
    const hrs = Math.round(mins / 60);
    if (hrs < 48) return `${hrs} hour${hrs === 1 ? '' : 's'} ago`;
    const days = Math.round(hrs / 24);
    return `${days} day${days === 1 ? '' : 's'} ago`;
  }
}
