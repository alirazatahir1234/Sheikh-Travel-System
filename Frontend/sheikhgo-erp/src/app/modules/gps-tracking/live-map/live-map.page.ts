import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnDestroy,
  OnInit,
  ViewChild,
  effect,
  inject
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { ScrollingModule } from '@angular/cdk/scrolling';
import { ChartData } from 'chart.js';
import { interval } from 'rxjs';
import {
  FleetTrackStatus,
  GpsDashboardSummary,
  VehicleLocation
} from '../../../core/models/gps-tracking.model';
import { UiChartOptions } from '../../../shared/components/ui';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { SharedModule } from '../../../shared/shared.module';
import { GpsLiveFacade } from '../services/gps-live.facade';
import { ConnectionStatusBarComponent, RefreshRateMs } from './components/connection-status-bar/connection-status-bar.component';
import { FleetAnalyticsRowComponent } from './components/fleet-analytics-row/fleet-analytics-row.component';
import { FleetKpiRowComponent } from './components/fleet-kpi-row/fleet-kpi-row.component';
import { FleetTrackingHeaderComponent } from './components/fleet-tracking-header/fleet-tracking-header.component';
import { LiveMapPanelComponent } from './components/live-map-panel/live-map-panel.component';
import {
  ReplayPreset,
  RouteReplayData,
  RouteReplayPanelComponent
} from './components/route-replay-panel/route-replay-panel.component';
import { VehicleDetailPanelComponent } from './components/vehicle-detail-panel/vehicle-detail-panel.component';
import {
  VehicleListPanelComponent,
  VehicleListTab
} from './components/vehicle-list-panel/vehicle-list-panel.component';

type StatusFilter = 'all' | FleetTrackStatus;

@Component({
  selector: 'app-live-map-page',
  templateUrl: './live-map.page.html',
  styleUrls: ['./live-map.page.scss'],
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    SharedModule,
    ScrollingModule,
    FleetTrackingHeaderComponent,
    ConnectionStatusBarComponent,
    FleetKpiRowComponent,
    VehicleListPanelComponent,
    VehicleDetailPanelComponent,
    FleetAnalyticsRowComponent,
    RouteReplayPanelComponent,
    LiveMapPanelComponent
  ],
  providers: [GpsLiveFacade]
})
export class LiveMapPageComponent implements OnInit, OnDestroy {
  @ViewChild('mapPanel') mapPanel?: LiveMapPanelComponent;

  readonly facade = inject(GpsLiveFacade);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly toast = inject(UiToastService);

  private lastEventCount = 0;

  liveTracking = true;
  refreshRateMs: RefreshRateMs = 5000;
  searchInput = '';
  listTab: VehicleListTab = 'all';
  statusFilter: StatusFilter = 'all';
  batteryLowOnly = false;
  dashboardLoading = true;
  lastSyncLabel = '—';

  replayVisible = false;
  replayLoading = false;
  replayError = '';
  replayData: RouteReplayData | null = null;
  replayPreset: ReplayPreset = 'today';

  lineChartData: ChartData = { labels: [], datasets: [] };
  doughnutChartData: ChartData = { labels: [], datasets: [] };
  readonly chartOptions: UiChartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: {
      x: { grid: { display: false } },
      y: { beginAtZero: true, ticks: { precision: 0 } }
    }
  };

  readonly kpiHint = (key: string): string | undefined => {
    if (key === 'alertsToday' && this.facade.geofenceBreachCount() > 0) {
      return `${this.facade.geofenceBreachCount()} geofence breaches`;
    }
    return undefined;
  };

  private refreshInterval?: ReturnType<typeof setInterval>;

  constructor() {
    effect(() => {
      const summary = this.facade.dashboardSummary();
      const loading = this.facade.initialLoading();
      this.updateChartSnapshots(summary);
      this.dashboardLoading = loading && !summary;
      this.cdr.markForCheck();
    });

    effect(() => {
      const at = this.facade.lastSyncAt();
      this.lastSyncLabel = at ? this.formatSyncAgo(at) : '—';
      this.cdr.markForCheck();
    });

    effect(() => {
      const events = this.facade.events();
      if (events.length <= this.lastEventCount) return;
      const newest = events[0];
      this.lastEventCount = events.length;
      if (newest.type === 'alert') {
        this.toast.error(newest.message);
      } else if (newest.type === 'warning') {
        this.toast.warning(newest.message);
      }
    });
  }

  ngOnInit(): void {
    const vehicleIdParam = this.route.snapshot.queryParamMap.get('vehicleId');
    if (vehicleIdParam) {
      const id = Number(vehicleIdParam);
      if (Number.isFinite(id)) {
        this.facade.selectVehicle(id);
      }
    }

    const driverIdParam = this.route.snapshot.queryParamMap.get('driverId');
    if (driverIdParam) {
      // Driver focus handled after locations load via query param side-effect if needed
    }

    this.facade.connect();
    this.facade.loadAll();
    this.startAutoRefresh();

    interval(1000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        const at = this.facade.lastSyncAt();
        if (at) {
          this.lastSyncLabel = this.formatSyncAgo(at);
          this.cdr.markForCheck();
        }
      });
  }

  ngOnDestroy(): void {
    this.stopAutoRefresh();
    this.facade.disconnect();
  }

  onSelectVehicle(loc: VehicleLocation): void {
    this.facade.selectVehicle(loc.vehicleId);
    this.mapPanel?.focusVehicle(loc);
  }

  onTrackSelected(): void {
    const loc = this.facade.selectedLocation();
    if (loc) this.mapPanel?.focusVehicle(loc);
  }

  onMapReady(): void {
    this.mapPanel?.invalidateSize();
    this.mapPanel?.centerFleet();
  }

  onSearchChange(value: string): void {
    this.searchInput = value;
  }

  toggleLiveTracking(): void {
    this.liveTracking = !this.liveTracking;
    if (this.liveTracking) this.refreshNow();
    this.facade.pushEvent(
      this.liveTracking ? 'Live tracking resumed' : 'Live tracking paused',
      'info',
      this.liveTracking ? 'play_circle' : 'pause_circle'
    );
  }

  refreshNow(): void {
    this.facade.loadAll(true);
    this.mapPanel?.invalidateSize();
  }

  setRefreshRate(rate: RefreshRateMs): void {
    this.refreshRateMs = rate;
    this.startAutoRefresh();
  }

  openReplay(): void {
    this.replayVisible = true;
    this.replayError = '';
    this.replayData = null;
  }

  closeReplay(): void {
    this.replayVisible = false;
    this.replayLoading = false;
    this.replayError = '';
    this.replayData = null;
  }

  onReplayPresetChange(preset: ReplayPreset): void {
    this.replayPreset = preset;
  }

  loadReplay(): void {
    const loc = this.facade.selectedLocation();
    if (!loc) {
      this.replayError = 'Select a vehicle first.';
      return;
    }

    const { from, to } = this.resolvePresetRange(this.replayPreset);
    this.replayLoading = true;
    this.replayError = '';
    this.replayData = null;
    this.cdr.markForCheck();

    this.facade.loadTripReplay(loc.vehicleId, from, to).subscribe({
      next: result => {
        this.replayData = {
          routePoints: result.bundle.route ?? [],
          positions: result.playback,
          stops: result.bundle.stops ?? [],
          events: result.bundle.events ?? []
        };
        this.replayLoading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.replayLoading = false;
        this.replayError = 'Could not load route replay for this vehicle.';
        this.cdr.markForCheck();
      }
    });
  }

  goToHistory(): void {
    const id = this.facade.selectedVehicleId();
    this.router.navigate(['/gps-tracking/history'], {
      queryParams: id != null ? { vehicleId: id } : {}
    });
  }

  goToTrips(): void {
    const id = this.facade.selectedVehicleId();
    this.router.navigate(['/gps-tracking/trips'], {
      queryParams: id != null ? { vehicleId: id } : {}
    });
  }

  goToProfile(): void {
    const id = this.facade.selectedVehicleId();
    if (id != null) this.router.navigate(['/vehicles', id]);
  }

  goToCommands(): void {
    const id = this.facade.selectedVehicleId();
    this.router.navigate(['/gps-tracking/commands'], {
      queryParams: id != null ? { vehicleId: id } : {}
    });
  }

  onStatusFilterChange(filter: StatusFilter): void {
    this.statusFilter = filter;
  }

  onBatteryLowOnlyChange(active: boolean): void {
    this.batteryLowOnly = active;
  }

  private lastReconcileAt = 0;

  private startAutoRefresh(): void {
    this.stopAutoRefresh();
    if (this.refreshRateMs == null) return;
    this.refreshInterval = setInterval(() => {
      if (!this.liveTracking) return;
      if (this.facade.connectionState() === 'connected') {
        const reconcileMs = 60_000;
        if (Date.now() - this.lastReconcileAt < reconcileMs) return;
        this.lastReconcileAt = Date.now();
      }
      this.facade.loadAll(true);
    }, this.refreshRateMs);
  }

  private stopAutoRefresh(): void {
    if (this.refreshInterval) {
      clearInterval(this.refreshInterval);
      this.refreshInterval = undefined;
    }
  }

  private updateChartSnapshots(summary: GpsDashboardSummary | null): void {
    if (!summary) {
      this.lineChartData = { labels: [], datasets: [] };
      this.doughnutChartData = { labels: [], datasets: [] };
      return;
    }

    const labels = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
    const moving = summary.sparkline.moving.length
      ? summary.sparkline.moving
      : [summary.moving, summary.moving, summary.moving, summary.moving, summary.moving, summary.moving, summary.moving];

    this.lineChartData = {
      labels,
      datasets: [
        {
          label: 'Moving',
          data: moving.slice(-7),
          borderColor: '#10B981',
          backgroundColor: 'rgba(16, 185, 129, 0.15)',
          tension: 0.35,
          fill: true,
          pointRadius: 0
        },
        {
          label: 'Parked',
          data: (summary.sparkline.parked.length ? summary.sparkline.parked : [summary.parked]).slice(-7),
          borderColor: '#3B82F6',
          backgroundColor: 'rgba(59, 130, 246, 0.1)',
          tension: 0.35,
          fill: false,
          pointRadius: 0
        }
      ]
    };

    this.doughnutChartData = {
      labels: ['Moving', 'Idle', 'Parked', 'Offline'],
      datasets: [
        {
          data: [summary.moving, summary.idle, summary.parked, summary.offline],
          backgroundColor: ['#10B981', '#F59E0B', '#3B82F6', '#94A3B8'],
          borderWidth: 0
        }
      ]
    };
  }

  private resolvePresetRange(preset: ReplayPreset): { from: Date; to: Date } {
    const to = new Date();
    const from = new Date(to);
    if (preset === 'today') {
      from.setHours(0, 0, 0, 0);
    } else if (preset === '24h') {
      from.setTime(to.getTime() - 24 * 60 * 60 * 1000);
    } else if (preset === '7d') {
      from.setTime(to.getTime() - 7 * 24 * 60 * 60 * 1000);
    } else {
      from.setTime(to.getTime() - 24 * 60 * 60 * 1000);
    }
    return { from, to };
  }

  private formatSyncAgo(at: Date): string {
    const sec = Math.floor((Date.now() - at.getTime()) / 1000);
    if (sec < 5) return 'just now';
    if (sec < 60) return `${sec}s ago`;
    const min = Math.floor(sec / 60);
    if (min < 60) return `${min}m ago`;
    return `${Math.floor(min / 60)}h ago`;
  }
}
