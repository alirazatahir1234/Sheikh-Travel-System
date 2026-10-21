import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { DecimalPipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { catchError, map, of, startWith } from 'rxjs';

import { UiModule } from '../../../shared/components/ui';
import { FleetService } from '../services/fleet.service';
import { FleetAssignmentService } from '../../../core/services/fleet-assignment.service';
import { MaintenanceService } from '../../../core/services/maintenance.service';
import { AiPlatformService } from '../../../core/services/ai-platform.service';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { FleetDashboardSummary } from '../models/fleet.model';
import { EMPTY_ASSIGNMENT_FILTERS, FleetAssignment } from '../../../core/models/fleet-assignment.model';
import { AssignmentItem, CriticalAlert, ActivityEvent, FleetKpi, FuelMaintenanceChart, UtilizationChart } from './fleet-dashboard.model';
import { FLEET_PRIMARY, QUICK_ACTIONS } from './fleet-dashboard.mock';

import { DashboardKpiRowComponent } from './widgets/dashboard-kpi-row.component';
import { UtilizationChartComponent } from './widgets/utilization-chart.component';
import { FuelMaintenanceChartComponent } from './widgets/fuel-maintenance-chart.component';
import { QuickActionsCardComponent } from './widgets/quick-actions-card.component';
import { CriticalAlertsCardComponent } from './widgets/critical-alerts-card.component';
import { RecentActivitiesCardComponent } from './widgets/recent-activities-card.component';
import { ActiveAssignmentsTableComponent } from './widgets/active-assignments-table.component';
import { FleetFabComponent } from './widgets/fleet-fab.component';

/**
 * Fleet dashboard content (bento grid only, no shell).
 * Primary entry: `/fleet/dashboard` (asset-health hub).
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
      <div class="fleet-dashboard__inner">
        <header class="fleet-dashboard__header">
          <div>
            <h2>Fleet Overview</h2>
            <p>Asset health, GPS, maintenance, and active assignments.</p>
          </div>
          <div class="fleet-dashboard__actions">
            <a routerLink="/ai" class="fleet-btn fleet-btn--ghost">AI Center</a>
            <a routerLink="/reports" class="fleet-btn fleet-btn--ghost">Reports</a>
            <a routerLink="/gps-tracking/live" class="fleet-btn fleet-btn--primary">Live map</a>
          </div>
        </header>

        @if (aiHealth(); as h) {
          <div class="fleet-health-banner">
            <div>
              <p class="fleet-health-banner__label">Fleet Health</p>
              <p class="fleet-health-banner__value">{{ h.healthPercent | number:'1.0-1' }}%</p>
            </div>
            <p class="fleet-health-banner__summary">{{ h.summary }}</p>
            <div class="fleet-health-banner__meta">
              <span>GPS {{ h.gpsOnlineRate | number:'1.0-0' }}%</span>
              <span>Drivers {{ h.driverScore | number:'1.0-0' }}</span>
              @if (h.criticalAlerts > 0) {
                <span class="fleet-health-banner__critical">{{ h.criticalAlerts }} critical</span>
              }
            </div>
          </div>
        }

        @if (recommendations().length) {
          <div class="fleet-recs">
            @for (r of recommendations().slice(0, 3); track r.id) {
              <div class="fleet-rec-card">
                <p class="fleet-rec-card__meta">{{ r.category }} · {{ r.severity }}</p>
                <p class="fleet-rec-card__title">{{ r.title }}</p>
                <p class="fleet-rec-card__action">{{ r.action }}</p>
              </div>
            }
          </div>
        }

        <fleet-dashboard-kpi-row [kpis]="kpis()"></fleet-dashboard-kpi-row>

        <div class="fleet-dashboard__grid">
          <div class="fleet-dashboard__span-8">
            <fleet-utilization-chart [data]="utilization()"></fleet-utilization-chart>
          </div>

          <div class="fleet-dashboard__span-4 fleet-dashboard__stack">
            <fleet-quick-actions-card [actions]="quickActions"></fleet-quick-actions-card>
            <fleet-critical-alerts-card [alerts]="alerts()"></fleet-critical-alerts-card>
          </div>

          <div class="fleet-dashboard__span-8">
            <fleet-fuel-maintenance-chart [data]="fuelMaintenance()"></fleet-fuel-maintenance-chart>
          </div>

          <div class="fleet-dashboard__span-4">
            <fleet-recent-activities-card [events]="activities()"></fleet-recent-activities-card>
          </div>

          <div class="fleet-dashboard__span-12">
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
    :host {
      display: block;
      /* Align fleet tokens with ops / stb shell while keeping fleet brand green */
      --fleet-surface: var(--stb-bg, #f4f7fa);
      --fleet-surface-alt: var(--stb-surface-alt, #f8fafc);
      --fleet-surface-muted: var(--stb-section-bg, #e8eef4);
      --fleet-border: var(--stb-border, #d8e0ea);
      --fleet-text: var(--stb-text, #0f172a);
      --fleet-text-muted: var(--stb-text-muted, #64748b);
      --fleet-card: var(--stb-surface, #fff);
      font-family: inherit;
      color: var(--stb-text, #0f172a);
    }

    .fleet-dashboard {
      min-height: 100%;
      background: var(--stb-bg, #f4f7fa);
    }

    .fleet-dashboard__inner {
      display: flex;
      flex-direction: column;
      gap: 20px;
      width: 100%;
      max-width: 1680px;
      margin: 0 auto;
      padding: clamp(12px, 3vw, 24px) clamp(12px, 3vw, 24px) 32px;
      box-sizing: border-box;
    }

    .fleet-dashboard__header {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-end;
      justify-content: space-between;
      gap: 16px;

      h2 {
        margin: 0 0 4px;
        font-size: 1.75rem;
        font-weight: 700;
        letter-spacing: -0.02em;
        color: var(--stb-text, #0f172a);
        line-height: 1.2;
      }

      p {
        margin: 0;
        font-size: 0.9375rem;
        color: var(--stb-text-muted, #64748b);
      }
    }

    .fleet-dashboard__actions {
      display: flex;
      flex-wrap: wrap;
      gap: 10px;
    }

    .fleet-btn {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      min-height: 40px;
      padding: 0 16px;
      border-radius: 10px;
      font-size: 0.875rem;
      font-weight: 600;
      text-decoration: none;
      border: 1px solid transparent;
    }

    .fleet-btn--ghost {
      background: #fff;
      border-color: var(--stb-border, #d8e0ea);
      color: var(--stb-text, #0f172a);
    }

    .fleet-btn--ghost:hover {
      border-color: var(--stb-primary, #0f766e);
      color: var(--stb-primary, #0f766e);
    }

    .fleet-btn--primary {
      background: var(--fleet-primary, #005f49);
      color: #fff;
    }

    .fleet-btn--primary:hover { opacity: 0.92; }

    .fleet-health-banner {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 1.25rem;
      padding: 1rem 1.1rem;
      border-radius: 14px;
      border: 1px solid color-mix(in srgb, var(--fleet-primary, #005f49) 25%, #e2e8f0);
      background: linear-gradient(90deg, color-mix(in srgb, var(--fleet-primary, #005f49) 8%, #fff), #fff);
    }

    .fleet-health-banner__label {
      margin: 0;
      font-size: 11px;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.06em;
      color: var(--fleet-primary, #005f49);
    }

    .fleet-health-banner__value {
      margin: 0;
      font-size: 1.75rem;
      font-weight: 700;
      color: var(--fleet-primary-dark, #00513e);
    }

    .fleet-health-banner__summary {
      margin: 0;
      flex: 1 1 200px;
      font-size: 0.875rem;
      color: var(--stb-text-muted, #64748b);
    }

    .fleet-health-banner__meta {
      display: flex;
      flex-wrap: wrap;
      gap: 1rem;
      font-size: 0.875rem;
      color: var(--stb-text-muted, #64748b);
    }

    .fleet-health-banner__critical { color: var(--fleet-error, #ba1a1a); font-weight: 600; }

    .fleet-recs {
      display: grid;
      grid-template-columns: repeat(1, minmax(0, 1fr));
      gap: 12px;
    }

    @media (min-width: 768px) {
      .fleet-recs { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    }

    @media (min-width: 1280px) {
      .fleet-recs { grid-template-columns: repeat(3, minmax(0, 1fr)); }
    }

    .fleet-rec-card {
      border-radius: 14px;
      border: 1px solid var(--stb-border, #d8e0ea);
      background: #fff;
      padding: 1rem;
    }

    .fleet-rec-card__meta {
      margin: 0;
      font-size: 11px;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      color: var(--stb-text-muted, #64748b);
    }

    .fleet-rec-card__title {
      margin: 0.35rem 0 0;
      font-weight: 600;
      color: var(--stb-text, #0f172a);
    }

    .fleet-rec-card__action {
      margin: 0.35rem 0 0;
      font-size: 0.875rem;
      color: var(--stb-text-muted, #64748b);
    }

    .fleet-dashboard__grid {
      display: grid;
      grid-template-columns: repeat(12, minmax(0, 1fr));
      gap: 16px;
    }

    .fleet-dashboard__span-4 { grid-column: span 12; }
    .fleet-dashboard__span-8 { grid-column: span 12; }
    .fleet-dashboard__span-12 { grid-column: span 12; }
    .fleet-dashboard__stack { display: flex; flex-direction: column; gap: 16px; }

    @media (min-width: 1024px) {
      .fleet-dashboard__span-4 { grid-column: span 4; }
      .fleet-dashboard__span-8 { grid-column: span 8; }
    }
  `]
})
export class FleetDashboardContentComponent {
  private readonly fleet = inject(FleetService);
  private readonly assignmentsApi = inject(FleetAssignmentService);
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly ai = inject(AiPlatformService);
  private readonly gps = inject(GpsTrackingService);
  private readonly router = inject(Router);

  private readonly summary = toSignal<FleetDashboardSummary | null>(this.fleet.getDashboard(), {
    initialValue: null
  });
  private readonly assignmentPage = toSignal(
    this.assignmentsApi
      .list({ ...EMPTY_ASSIGNMENT_FILTERS, status: 'Active' }, 1, 8)
      .pipe(
        map((page): FleetAssignment[] | null => page?.items ?? []),
        catchError(() => of([] as FleetAssignment[])),
        startWith(null as FleetAssignment[] | null)
      ),
    { initialValue: null as FleetAssignment[] | null }
  );
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

  readonly loadingAssignments = computed(() => this.assignmentPage() === null);

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
      if (keys.length >= 2) {
        return {
          subtitle: 'Fleet GPS online % over the last 7 days',
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

    // Honest empty / snapshot state — never paint a fake flat 7-day capacity line.
    return {
      labels: [],
      series: [],
      empty: true,
      subtitle: 'Fleet GPS online % over the last 7 days',
      emptyMessage: total
        ? `No GPS history yet. Current online rate: ${pct}% (${online}/${total}).`
        : 'No GPS history yet for this period.'
    };
  });

  readonly fuelMaintenance = computed<FuelMaintenanceChart>(() => {
    const fuel = this.maintenanceDashboard()?.fuelSummary;
    const labels = fuel?.labels ?? [];
    const fuelCosts = fuel?.fuelCosts ?? [];
    const maintCosts = fuel?.maintenanceCosts ?? [];
    const hasSeries = labels.length > 0 && (fuelCosts.some(v => v > 0) || maintCosts.some(v => v > 0));

    if (hasSeries) {
      return { labels, fuel: fuelCosts, maintenance: maintCosts };
    }

    return {
      labels: [],
      fuel: [],
      maintenance: [],
      empty: true,
      emptyMessage: 'No fuel or maintenance cost series for this period.'
    };
  });

  readonly quickActions = QUICK_ACTIONS;

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
        actionLabel: 'Open map',
        actionRoute: '/gps-tracking/live'
      });
    }
    if ((gps?.alertsToday ?? 0) > 0) {
      out.push({
        id: 'gps-alerts',
        icon: 'warning',
        tone: 'warning',
        title: `${gps!.alertsToday} GPS alert${gps!.alertsToday === 1 ? '' : 's'} today`,
        detail: 'Review offline, geofence, and overspeed events.',
        actionLabel: 'View alerts',
        actionRoute: '/gps-tracking/live'
      });
    }
    if ((maint?.overdueServices ?? 0) > 0) {
      out.push({
        id: 'maint-overdue',
        icon: 'build',
        tone: 'error',
        title: `${maint!.overdueServices} overdue service${maint!.overdueServices === 1 ? '' : 's'}`,
        detail: 'Schedule maintenance before the next dispatch.',
        actionLabel: 'Maintenance',
        actionRoute: '/fleet/maintenance'
      });
    }
    if ((this.summary()?.complianceAlerts ?? 0) > 0) {
      out.push({
        id: 'compliance',
        icon: 'policy',
        tone: 'warning',
        title: `${this.summary()!.complianceAlerts} compliance document${this.summary()!.complianceAlerts === 1 ? '' : 's'} expiring`,
        detail: 'Renew papers within 30 days.',
        actionLabel: 'Compliance',
        actionRoute: '/fleet/compliance'
      });
    }
    if ((health?.criticalAlerts ?? 0) > 0 && !out.some(a => a.id === 'sos')) {
      out.push({
        id: 'ai-critical',
        icon: 'error',
        tone: 'error',
        title: `${health!.criticalAlerts} critical fleet health issue${health!.criticalAlerts === 1 ? '' : 's'}`,
        detail: health?.summary || 'Review AI fleet health recommendations.',
        actionLabel: 'AI Center',
        actionRoute: '/ai'
      });
    }
    return out;
  });

  /** Real maintenance / service events only — never remapped assignment rows. */
  readonly activities = computed<ActivityEvent[]>(() => {
    const dash = this.maintenanceDashboard();
    const events: ActivityEvent[] = [];

    for (const wo of dash?.recentWorkOrders?.slice(0, 5) ?? []) {
      const vehicle = wo.vehicleRegistration || wo.vehicleName || 'Vehicle';
      const kind = wo.serviceTypeName || wo.maintenanceType || 'Work order';
      events.push({
        id: `wo-${wo.id}`,
        title: `${wo.workOrderNumber}: ${kind} · ${vehicle} (${wo.status})`,
        timeAgo: this.relativeTime(wo.completedAt || wo.createdAt),
        tone: wo.status === 'Completed' || wo.status === 'Closed' ? 'secondary' : 'primary'
      });
    }

    for (const svc of dash?.upcomingServices?.slice(0, 3) ?? []) {
      if (events.length >= 6) break;
      events.push({
        id: `svc-${svc.vehicleId}-${svc.dueDate || svc.serviceType}`,
        title: `Upcoming: ${svc.serviceType} · ${svc.vehicleRegistration || svc.vehicleName || 'Vehicle'}`,
        timeAgo: svc.dueDate ? `Due ${new Date(svc.dueDate).toLocaleDateString()}` : 'Scheduled',
        tone: 'muted'
      });
    }

    return events.slice(0, 6);
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
    const rows = this.assignmentPage();
    if (!rows?.length) {
      return [];
    }
    return rows.map((row) => ({
      id: row.id,
      vehicleId: row.vehicleName || row.vehicleRegistration || '—',
      vehicleModel: row.vehicleRegistration && row.vehicleName
        ? row.vehicleRegistration
        : (row.vehicleCode || '—'),
      driver: row.driverName || 'Unassigned',
      route: this.routeLabel(row),
      assignmentType: row.assignmentType || '—',
      status: row.displayStatus || row.status,
      eta: this.eta(row.endAt ?? undefined)
    }));
  });

  onFab(): void {
    this.router.navigate(['/vehicles/new']);
  }

  private routeLabel(row: FleetAssignment): string {
    const pickup = row.pickupLocation?.trim();
    const drop = row.dropLocation?.trim();
    if (pickup && drop) return `${pickup} → ${drop}`;
    if (pickup || drop) return pickup || drop || '—';
    if (row.purpose?.trim()) return row.purpose.trim();
    return '—';
  }

  private count(value: number | undefined, fallback: number): string {
    return (value != null ? value : fallback).toLocaleString();
  }

  private currency(value: number | undefined, fallback: number): string {
    const amount = value != null ? value : fallback;
    return 'PKR ' + Math.round(amount).toLocaleString();
  }

  private eta(endAt?: string | null): string {
    if (!endAt) {
      return '—';
    }
    const date = new Date(endAt);
    return isNaN(date.getTime())
      ? '—'
      : date.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
  }

  private relativeTime(iso?: string | null): string {
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
