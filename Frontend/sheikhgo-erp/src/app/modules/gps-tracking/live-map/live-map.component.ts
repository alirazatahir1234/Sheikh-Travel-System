import {
  Component,
  OnInit,
  OnDestroy,
  AfterViewInit,
  ElementRef,
  ViewChild,
  HostListener
} from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { ChartData } from 'chart.js';
import type * as LeafletTypes from 'leaflet';
import {
  createMarkerClusterGroup,
  L,
  loadMarkerClusterPlugin
} from '../../../core/leaflet/leaflet-cluster';
import { MAP_TILE_STACKS, MapTheme } from '../../../core/leaflet/leaflet-map-tiles';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { GpsRealtimeService, GpsConnectionState } from '../../../core/services/gps-realtime.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { DriverService } from '../../../core/services/driver.service';
import {
  VehicleLocation,
  PositionDto,
  TrackingDto,
  FleetTrackStatus,
  SosAlertPayload,
  GpsFleetStatusLocal,
  GpsFleetStatusSnapshot,
  GpsEta
} from '../../../core/models/gps-tracking.model';
import { VehicleListItem } from '../../../core/models/vehicle.model';
import { resolveFleetStatus } from '../../../core/utils/gps-status.util';
import { mergeVehicleLocations } from './live-map-state.util';
import { computeFleetHealth, FleetHealthBreakdown } from '../utils/fleet-health.util';

type StatusFilter = 'all' | FleetTrackStatus;
type TimePreset = 'today' | '24h' | '7d' | 'custom';
type IgnitionFilter = 'all' | 'on' | 'off';
type RefreshRateMs = 5000 | 10000 | 30000 | 60000 | null;

interface TrackEvent {
  time: Date;
  message: string;
  type: 'info' | 'alert' | 'success' | 'warning';
  icon: string;
}

interface TripSummary {
  distanceKm: number;
  avgSpeed: number;
  stopMinutes: number;
  durationMinutes: number;
  pointCount: number;
}

const TRAIL_COLORS: Record<FleetTrackStatus, string> = {
  moving: '#10B981',
  idle: '#F59E0B',
  parked: '#3B82F6',
  delayed: '#EF4444',
  offline: '#64748B',
  never_seen: '#CBD5E1',
  sos: '#DC2626',
  scheduled: '#3B82F6'
};

@Component({
  standalone: false,
  selector: 'app-live-map',
  templateUrl: './live-map.component.html',
  styleUrls: ['./live-map.component.scss']
})
export class LiveMapComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('mapHost') mapHost?: ElementRef<HTMLElement>;
  @ViewChild('mapContainer', { static: false }) mapContainer?: ElementRef<HTMLElement>;

  private map!: LeafletTypes.Map;
  private tileLayer?: LeafletTypes.TileLayer;
  private mapResizeObserver?: ResizeObserver;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private markerCluster!: any;
  private markers = new Map<number, LeafletTypes.Marker>();
  private trailLayers = new Map<number, LeafletTypes.Polyline>();
  private prevPositions = new Map<number, { lat: number; lng: number }>();
  private positionTrails = new Map<number, [number, number][]>();
  private historyPolyline?: LeafletTypes.Polyline;
  private historyMarker?: LeafletTypes.Marker;
  private refreshInterval?: ReturnType<typeof setInterval>;
  private replayTimer?: ReturnType<typeof setInterval>;
  private replayIndex = 0;
  private readonly maxTrailPoints = 14;

  locations: VehicleLocation[] = [];
  loading = true;
  syncError: string | null = null;
  mapError: string | null = null;
  searchQuery = '';
  statusFilter: StatusFilter = 'all';
  ignitionFilter: IgnitionFilter = 'all';
  batteryLowOnly = false;
  timePreset: TimePreset = 'today';
  mapTheme: MapTheme = 'light';
  liveTracking = true;
  listSheetOpen = true;
  selectedVehicleId: number | null = null;
  lastSyncAt: Date | null = null;
  secondsSinceSync = 0;
  isMapFullscreen = false;
  tripSummary: TripSummary | null = null;
  private syncTick?: ReturnType<typeof setInterval>;

  readonly refreshRateOptions: { id: RefreshRateMs; label: string }[] = [
    { id: 5000, label: '5 sec' },
    { id: 10000, label: '10 sec' },
    { id: 30000, label: '30 sec' },
    { id: 60000, label: '1 min' },
    { id: null, label: 'Pause' }
  ];
  refreshRateMs: RefreshRateMs = 5000;
  followSelected = false;
  connectionState: GpsConnectionState = 'disconnected';
  private connectionStateSub?: { unsubscribe(): void };
  private sosSub?: { unsubscribe(): void };
  private readonly BATTERY_LOW_THRESHOLD = 20;

  vehicles: VehicleListItem[] = [];
  historyFrom = '';
  historyTo = '';
  historyRows: TrackingDto[] = [];
  loadingHistory = false;
  historyError = '';
  showHistory = false;
  replayPlaying = false;
  replaySpeed = 1;
  events: TrackEvent[] = [];

  readonly statusFilters: { id: StatusFilter; label: string }[] = [
    { id: 'all', label: 'All' },
    { id: 'moving', label: 'Moving' },
    { id: 'idle', label: 'Idle' },
    { id: 'parked', label: 'Parked' },
    { id: 'offline', label: 'Offline' },
    { id: 'never_seen', label: 'Never Seen' },
    { id: 'sos', label: 'SOS' },
    { id: 'delayed', label: 'Delayed' },
    { id: 'scheduled', label: 'Scheduled' }
  ];

  readonly timePresets: { id: TimePreset; label: string }[] = [
    { id: 'today', label: 'Today' },
    { id: '24h', label: 'Last 24h' },
    { id: '7d', label: 'Last 7d' },
    { id: 'custom', label: 'Custom' }
  ];

  geofenceBreachCount = 0;
  selectedEta: GpsEta | null = null;

  fleetStatusLocal: GpsFleetStatusLocal | null = null;
  fleetStatusHistory: GpsFleetStatusSnapshot[] = [];
  fleetOverviewRangeDays: 7 | 30 = 7;
  readonly fleetOverviewRangeOptions: { days: 7 | 30; label: string }[] = [
    { days: 7, label: '7 Days' },
    { days: 30, label: '30 Days' }
  ];

  private realtimeSub?: { unsubscribe(): void };
  private mapReady = false;
  private pendingMarkerLocations: VehicleLocation[] | null = null;
  private lastSyncSummaryKey = '';
  private tileFallbackIndex = 0;
  private tileErrorCount = 0;
  private _bootstrapping = false;
  private _bootstrapTimer?: ReturnType<typeof setTimeout>;
  private _switchingTiles = false;

  private pendingFocusVehicleId: number | null = null;
  private isRefreshingLocations = false;
  private userInteractionActive = false;
  private interactionPauseTimer?: ReturnType<typeof setTimeout>;

  constructor(
    private gpsService: GpsTrackingService,
    private realtime: GpsRealtimeService,
    private vehicleService: VehicleService,
    private driverService: DriverService,
    private router: Router,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    const vehicleIdParam = this.route.snapshot.queryParamMap.get('vehicleId');
    if (vehicleIdParam) {
      const id = Number(vehicleIdParam);
      if (Number.isFinite(id)) {
        this.pendingFocusVehicleId = id;
        this.selectedVehicleId = id;
      }
    }

    const driverIdParam = this.route.snapshot.queryParamMap.get('driverId');
    if (driverIdParam) {
      const driverId = Number(driverIdParam);
      if (Number.isFinite(driverId)) {
        this.driverService.getById(driverId).subscribe({
          next: driver => {
            if (driver.assignedVehicleId) {
              this.pendingFocusVehicleId = driver.assignedVehicleId;
              this.selectedVehicleId = driver.assignedVehicleId;
              this.pushEvent(`Tracking ${driver.fullName}`, 'info', 'person_pin_circle');
            } else {
              this.pushEvent('Driver has no assigned vehicle — GPS unavailable', 'warning', 'person_off');
            }
          },
          error: () => {
            this.pushEvent('Could not load driver for tracking', 'warning', 'error');
          }
        });
      }
    }

    this.applyTimePreset('today');
    this.vehicleService.getAll(1, 500).subscribe({
      next: r => { this.vehicles = r.items; },
      error: () => {}
    });
    this.gpsService.getGeofenceBreachCount().subscribe({
      next: c => { this.geofenceBreachCount = c; },
      error: () => {}
    });
    this.loadRecentAlertEvents();
    this.loadFleetStatus();
    void this.realtime.connect().catch(() => {
      this.pushEvent('Realtime unavailable — using polling', 'warning', 'wifi_off');
    });
    this.realtimeSub = this.realtime.locationUpdates$.subscribe(update => {
      this.applyRealtimeUpdate(update);
    });
    this.connectionStateSub = this.realtime.connectionState$.subscribe(state => {
      const wasDisconnected = this.connectionState === 'disconnected';
      this.connectionState = state;
      if (state === 'reconnecting') {
        this.pushEvent('Connection lost — reconnecting…', 'warning', 'wifi_off');
      } else if (state === 'connected' && wasDisconnected) {
        this.pushEvent('Realtime connection restored', 'success', 'wifi');
      }
    });
    this.sosSub = this.realtime.sosAlerts$.subscribe(alert => {
      const idx = this.locations.findIndex(l => l.vehicleId === alert.vehicleId);
      if (idx >= 0) {
        this.locations[idx] = { ...this.locations[idx], status: 'sos', alarmType: 'sos' };
        this.updateMarkers(this.mappableLocations(this.locations));
        this.pushEvent(`${this.locations[idx].vehicleName} — SOS / panic alarm!`, 'alert', 'sos');
      } else {
        this.pushEvent(`Vehicle #${alert.vehicleId} — SOS / panic alarm!`, 'alert', 'sos');
      }
    });
    this.syncTick = setInterval(() => {
      if (this.lastSyncAt) {
        this.secondsSinceSync = Math.floor((Date.now() - this.lastSyncAt.getTime()) / 1000);
      }
    }, 1000);
    this.pushEvent('Tracking console ready', 'info', 'gps_fixed');
  }

  ngAfterViewInit(): void {
    // Defer until the routed view and map container dimensions are ready.
    this._bootstrapTimer = setTimeout(() => void this.bootstrapMap(), 100);
  }

  @HostListener('window:resize')
  onWindowResize(): void {
    this.scheduleMapResize();
  }

  private async bootstrapMap(): Promise<void> {
    if (this._bootstrapping) return;
    this._bootstrapping = true;
    try {
      await loadMarkerClusterPlugin();
      await this.waitForMapContainer();
      if (this.map) {
        this.setMapTheme(this.mapTheme);
        this.scheduleMapResize();
        return;
      }
      this.initMap();
      this.loadLocations();
      this.startAutoRefresh();
    } catch (err) {
      console.error('[LiveMap] Map bootstrap failed:', err);
      this.mapError = 'Map could not be initialized. Refresh the page or tap Retry map.';
    } finally {
      this._bootstrapping = false;
    }
  }

  private waitForMapContainer(): Promise<void> {
    return new Promise((resolve, reject) => {
      const attempt = (frame: number) => {
        const host = this.mapContainer?.nativeElement;
        if (host) {
          if (host.offsetWidth >= 50 && host.offsetHeight >= 50) {
            resolve();
            return;
          }
          if (frame >= 100) {
            resolve();
            return;
          }
        } else if (frame >= 100) {
          reject(new Error('Map container element not found'));
          return;
        }
        requestAnimationFrame(() => attempt(frame + 1));
      };
      attempt(0);
    });
  }

  ngOnDestroy(): void {
    if (this._bootstrapTimer) clearTimeout(this._bootstrapTimer);
    if (this.refreshInterval) clearInterval(this.refreshInterval);
    if (this.syncTick) clearInterval(this.syncTick);
    if (this.interactionPauseTimer) clearTimeout(this.interactionPauseTimer);
    this.mapResizeObserver?.disconnect();
    this.realtimeSub?.unsubscribe();
    this.connectionStateSub?.unsubscribe();
    this.sosSub?.unsubscribe();
    void this.realtime.disconnect();
    this.stopReplay();
    if (this.map) this.map.remove();
  }

  @HostListener('document:fullscreenchange')
  onFullscreenChange(): void {
    this.isMapFullscreen = !!document.fullscreenElement;
    setTimeout(() => this.map?.invalidateSize(), 200);
  }

  get filteredLocations(): VehicleLocation[] {
    const q = this.searchQuery.trim().toLowerCase();
    return this.locations.filter(loc => {
      if (this.statusFilter !== 'all' && loc.status !== this.statusFilter) return false;
      if (this.ignitionFilter === 'on' && loc.ignition !== true) return false;
      if (this.ignitionFilter === 'off' && loc.ignition !== false) return false;
      if (this.batteryLowOnly && !(loc.batteryLevel != null && loc.batteryLevel < this.BATTERY_LOW_THRESHOLD)) {
        return false;
      }
      if (!q) return true;
      return (
        loc.vehicleName.toLowerCase().includes(q) ||
        loc.registrationNumber.toLowerCase().includes(q) ||
        (loc.driverName?.toLowerCase().includes(q) ?? false) ||
        (loc.imei?.toLowerCase().includes(q) ?? false) ||
        (loc.trackerName?.toLowerCase().includes(q) ?? false)
      );
    });
  }

  setIgnitionFilter(id: IgnitionFilter): void {
    this.ignitionFilter = this.ignitionFilter === id ? 'all' : id;
  }

  toggleBatteryLowOnly(): void {
    this.batteryLowOnly = !this.batteryLowOnly;
  }

  get statusCounts(): Record<FleetTrackStatus | 'all', number> {
    const gps = this.locations.filter(l => l.hasGps);
    return {
      all: this.locations.length,
      moving: gps.filter(l => l.status === 'moving').length,
      idle: gps.filter(l => l.status === 'idle').length,
      parked: gps.filter(l => l.status === 'parked').length,
      offline: this.locations.filter(l => l.status === 'offline').length,
      never_seen: this.locations.filter(l => l.status === 'never_seen').length,
      sos: this.locations.filter(l => l.status === 'sos').length,
      delayed: gps.filter(l => l.status === 'delayed').length,
      scheduled: this.locations.filter(l => l.status === 'scheduled').length
    };
  }

  /** Online/Offline/Moving/Idle/Parked/Never-Seen counts for the top stat row, per the spec's dashboard cards. */
  get fleetCounts() {
    const c = this.statusCounts;
    return {
      total: this.locations.length,
      online: c.moving + c.idle,
      offline: c.offline,
      moving: c.moving,
      idle: c.idle,
      parked: c.parked,
      neverSeen: c.never_seen,
      sos: c.sos
    };
  }

  get trackingStatusLabel(): string {
    if (!this.liveTracking) return 'Tracking paused';
    if (this.connectionState === 'reconnecting') return 'Connection lost — reconnecting…';
    if (this.connectionState === 'disconnected') return 'Connection lost';
    if (this.syncError) return 'Sync issue — tap Refresh';
    return 'Connected';
  }

  get trackingActive(): boolean {
    return this.liveTracking && !this.syncError && this.connectionState === 'connected';
  }

  get gpsHealthy(): boolean {
    return this.locations.some(
      l => l.hasGps && this.isValidCoord(l.latitude, l.longitude)
    );
  }

  get replayProgress(): number {
    if (!this.historyRows.length) return 0;
    return Math.round((this.replayIndex / this.historyRows.length) * 100);
  }

  get historyVehicleOptions(): VehicleListItem[] {
    return [...this.vehicles].sort((a, b) => a.name.localeCompare(b.name));
  }

  get selectedHistoryVehicleLabel(): string {
    if (this.selectedVehicleId == null) return 'Select vehicle';
    const vehicle = this.vehicles.find(v => v.id === this.selectedVehicleId);
    if (!vehicle) return 'Select vehicle';
    return `${vehicle.name} (${vehicle.registrationNumber})`;
  }

  compareVehicleId = (a: number | null, b: number | null): boolean => a === b;

  onHistoryVehicleSelected(vehicleId: number | null): void {
    if (vehicleId == null) return;
    const loc = this.locations.find(l => l.vehicleId === vehicleId);
    if (loc) {
      this.selectVehicle(loc);
      return;
    }
    this.selectedVehicleId = vehicleId;
    void this.realtime.subscribeVehicle(vehicleId);
  }

  statusLabel(status: FleetTrackStatus): string {
    const labels: Record<FleetTrackStatus, string> = {
      moving: 'Moving',
      idle: 'Idle',
      parked: 'Parked',
      offline: 'Offline',
      never_seen: 'Never Seen',
      sos: 'SOS',
      scheduled: 'Scheduled',
      delayed: 'Delayed'
    };
    return labels[status];
  }

  statusIcon(status: FleetTrackStatus): string {
    const icons: Record<FleetTrackStatus, string> = {
      moving: 'directions_bus',
      idle: 'local_shipping',
      parked: 'local_parking',
      offline: 'signal_wifi_off',
      never_seen: 'help_outline',
      sos: 'sos',
      scheduled: 'schedule',
      delayed: 'warning'
    };
    return icons[status];
  }

  /** Vehicle-category glyph independent of live status; production VehicleType values are free
   * text, so this matches by substring — treat as a first draft to refine against real data. */
  private vehicleCategoryGlyph(vehicleType?: string | null): string {
    const type = (vehicleType ?? '').toLowerCase();
    if (!type) return '&#128652;'; // 🚌 default
    if (type.includes('ambulance')) return '&#128657;';
    if (type.includes('bike') || type.includes('motorcycle')) return '&#127949;';
    if (type.includes('construction') || type.includes('excavator') || type.includes('crane')) return '&#128667;';
    if (type.includes('truck') || type.includes('trailer')) return '&#128666;';
    if (type.includes('bus') || type.includes('coaster') || type.includes('van')) return '&#128652;';
    if (type.includes('car') || type.includes('sedan') || type.includes('suv')) return '&#128663;';
    return '&#128652;';
  }

  signalBars(loc: VehicleLocation): number {
    if (!loc.hasGps) return 0;
    if (!loc.lastUpdated) return 1;
    const ageMin = (Date.now() - new Date(loc.lastUpdated).getTime()) / 60000;
    if (ageMin < 2 && loc.status === 'moving') return 4;
    if (ageMin < 10) return 3;
    if (ageMin < 30) return 2;
    return 1;
  }

  formatLastPing(loc: VehicleLocation): string {
    if (!loc.hasGps || !loc.lastUpdated) return 'No live GPS';
    const sec = Math.floor((Date.now() - new Date(loc.lastUpdated).getTime()) / 1000);
    if (sec < 60) return `Last ping: ${sec}s ago`;
    const min = Math.floor(sec / 60);
    if (min < 60) return `Last ping: ${min}m ago`;
    const hr = Math.floor(min / 60);
    return `Last ping: ${hr}h ago`;
  }

  speedLabel(loc: VehicleLocation): string {
    if (!loc.hasGps) return '—';
    if (loc.speed > 0) return `${Math.round(loc.speed)} km/h`;
    return loc.status === 'idle' ? '0 km/h · idle' : 'Stationary';
  }

  onSearchQueryChanged(value: string): void {
    this.searchQuery = value;
    this.markUserActive();
  }

  setStatusFilter(id: StatusFilter): void {
    this.statusFilter = id;
    this.markUserActive();
  }

  setTimePreset(id: TimePreset): void {
    this.timePreset = id;
    this.markUserActive();
    if (id !== 'custom') this.applyTimePreset(id);
  }

  private applyTimePreset(preset: TimePreset): void {
    const now = new Date();
    const to = new Date(now);
    let from = new Date(now);
    if (preset === 'today') {
      from.setHours(0, 0, 0, 0);
    } else if (preset === '24h') {
      from = new Date(now.getTime() - 24 * 60 * 60 * 1000);
    } else if (preset === '7d') {
      from = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
    }
    this.historyFrom = this.toLocalInput(from);
    this.historyTo = this.toLocalInput(to);
  }

  private toLocalInput(d: Date): string {
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  refreshNow(): void {
    this.pushEvent('Manual refresh requested', 'info', 'refresh');
    this.loadLocations(true, true);
    this.loadRecentAlertEvents();
    this.loadFleetStatus();
  }

  private loadFleetStatus(): void {
    this.gpsService.getFleetStatusLocal().subscribe({
      next: s => { this.fleetStatusLocal = s; },
      error: () => {}
    });
    this.loadFleetStatusHistory();
  }

  private loadFleetStatusHistory(): void {
    const to = new Date();
    const from = new Date(to.getTime() - this.fleetOverviewRangeDays * 24 * 60 * 60 * 1000);
    this.gpsService.getFleetStatusHistory(from, to).subscribe({
      next: rows => {
        this.fleetStatusHistory = rows;
        this.recomputeKpiTiles();
        this.recomputeFleetOverviewChart();
      },
      error: () => {}
    });
  }

  setFleetOverviewRange(days: 7 | 30): void {
    this.fleetOverviewRangeDays = days;
    this.loadFleetStatusHistory();
  }

  /**
   * Pre-computed sparkline/trend data per KPI metric, recomputed only when fleetStatusHistory
   * actually changes — NOT exposed as a template-callable method. Binding a method call directly
   * in a template (e.g. [sparkline]="sparklineFor('online')") makes Angular invoke it, and allocate
   * a brand-new array/object, on every single change-detection cycle; stb-stat-tile treats a new
   * array reference as a change and redraws its canvas in response, and with this page's 1-second
   * sync timer driving frequent CD cycles that turned into a runaway redraw loop that froze the tab.
   */
  kpiTiles: Record<string, { trend?: string; trendUp?: boolean; sparkline: number[] }> = {};

  private recomputeKpiTiles(): void {
    const metrics: (keyof GpsFleetStatusSnapshot)[] =
      ['online', 'moving', 'idle', 'parked', 'offline', 'neverSeen', 'alertsToday'];
    const tiles: Record<string, { trend?: string; trendUp?: boolean; sparkline: number[] }> = {};
    for (const metric of metrics) {
      tiles[metric] = {
        ...this.computeTrend(metric),
        sparkline: this.fleetStatusHistory.slice(-24).map(s => Number(s[metric]) || 0)
      };
    }
    this.kpiTiles = tiles;
  }

  /** Day-over-day delta (latest snapshot vs. ~24h-ago) for a given metric's trend indicator. */
  private computeTrend(metric: keyof GpsFleetStatusSnapshot): { trend?: string; trendUp?: boolean } {
    if (this.fleetStatusHistory.length < 2) return {};

    const latest = this.fleetStatusHistory[this.fleetStatusHistory.length - 1];
    const latestTime = new Date(latest.snapshotAt).getTime();
    const dayAgoTarget = latestTime - 24 * 60 * 60 * 1000;

    let compare = this.fleetStatusHistory[0];
    for (const s of this.fleetStatusHistory) {
      if (new Date(s.snapshotAt).getTime() <= dayAgoTarget) compare = s;
    }

    const latestVal = Number(latest[metric]) || 0;
    const compareVal = Number(compare[metric]) || 0;
    if (compareVal === 0) return {};

    const pct = ((latestVal - compareVal) / compareVal) * 100;
    return { trend: `${pct >= 0 ? '+' : ''}${pct.toFixed(1)}%`, trendUp: pct >= 0 };
  }

  /** Pre-computed, stable fields — see kpiTiles doc comment for why these aren't template getters. */
  fleetHealth: FleetHealthBreakdown = { optimal: 0, attention: 0, critical: 0, unknown: 0, total: 0 };
  fleetHealthChartData: ChartData = { labels: [], datasets: [] };
  fleetOverviewChartData: ChartData = { labels: [], datasets: [] };

  private recomputeFleetHealth(): void {
    this.fleetHealth = computeFleetHealth(this.locations);
    const h = this.fleetHealth;
    this.fleetHealthChartData = {
      labels: ['Optimal', 'Attention', 'Critical', 'Unknown'],
      datasets: [{
        data: [h.optimal, h.attention, h.critical, h.unknown],
        backgroundColor: ['#10B981', '#F59E0B', '#EF4444', '#94A3B8'],
        borderWidth: 0,
        hoverOffset: 6
      }]
    };
  }

  private recomputeFleetOverviewChart(): void {
    const labels = this.fleetStatusHistory.map(s =>
      new Date(s.snapshotAt).toLocaleDateString(undefined, { day: '2-digit', month: 'short' }));
    this.fleetOverviewChartData = {
      labels,
      datasets: [
        { label: 'Moving', data: this.fleetStatusHistory.map(s => s.moving), borderColor: '#10B981', backgroundColor: '#10B98133', tension: 0.4, pointRadius: 0 },
        { label: 'Idle', data: this.fleetStatusHistory.map(s => s.idle), borderColor: '#8B5CF6', backgroundColor: '#8B5CF633', tension: 0.4, pointRadius: 0 },
        { label: 'Parked', data: this.fleetStatusHistory.map(s => s.parked), borderColor: '#F97316', backgroundColor: '#F9731633', tension: 0.4, pointRadius: 0 },
        { label: 'Offline', data: this.fleetStatusHistory.map(s => s.offline), borderColor: '#EF4444', backgroundColor: '#EF444433', tension: 0.4, pointRadius: 0 }
      ]
    };
  }

  toggleLiveTracking(): void {
    this.liveTracking = !this.liveTracking;
    if (this.liveTracking) this.refreshNow();
    this.pushEvent(
      this.liveTracking ? 'Live tracking resumed' : 'Live tracking paused',
      'info',
      this.liveTracking ? 'play_circle' : 'pause_circle'
    );
  }

  private startAutoRefresh(): void {
    if (this.refreshInterval) clearInterval(this.refreshInterval);
    if (this.refreshRateMs == null) return;
    this.refreshInterval = setInterval(() => {
      if (this.liveTracking && !this.userInteractionActive) this.loadLocations(true);
    }, this.refreshRateMs);
  }

  setRefreshRate(rate: RefreshRateMs): void {
    this.refreshRateMs = rate;
    this.markUserActive();
    this.startAutoRefresh();
    this.pushEvent(
      rate == null ? 'Auto-refresh paused' : `Auto-refresh set to ${rate / 1000}s`,
      'info',
      rate == null ? 'pause_circle' : 'autorenew'
    );
  }

  toggleFollowSelected(): void {
    this.followSelected = !this.followSelected;
    this.pushEvent(
      this.followSelected ? 'Follow vehicle enabled' : 'Follow vehicle disabled',
      'info',
      this.followSelected ? 'my_location' : 'location_disabled'
    );
  }

  cycleMapTheme(): void {
    const order: MapTheme[] = ['dark', 'light', 'satellite'];
    const i = order.indexOf(this.mapTheme);
    this.setMapTheme(order[(i + 1) % order.length]);
  }

  setMapTheme(theme: MapTheme): void {
    this.mapTheme = theme;
    if (!this.map) return;
    this.tileFallbackIndex = 0;
    this.tileErrorCount = 0;
    this.mapError = null;
    this.applyTileLayer(theme);
  }

  private applyTileLayer(theme: MapTheme): void {
    if (!this.map) return;

    const stack = MAP_TILE_STACKS[theme];
    const cfg = stack[this.tileFallbackIndex] ?? stack[0];
    if (!cfg) return;

    if (this.tileLayer) {
      this.tileLayer.off();
      this.map.removeLayer(this.tileLayer);
    }

    this.tileLayer = L.tileLayer(cfg.url, {
      maxZoom: cfg.maxZoom ?? 19,
      attribution: cfg.attribution,
      ...(cfg.subdomains ? { subdomains: cfg.subdomains } : {})
    }).addTo(this.map);

    this.tileLayer.on('tileerror', () => {
      if (this._switchingTiles) return;
      this.tileErrorCount += 1;
      if (this.tileErrorCount < 4) return;

      if (this.tileFallbackIndex < stack.length - 1) {
        this._switchingTiles = true;
        this.tileFallbackIndex += 1;
        this.tileErrorCount = 0;
        this.applyTileLayer(theme);
        this._switchingTiles = false;
        return;
      }

      this.mapError = 'Map tiles could not be loaded. Check your network or try another map style.';
    });

    this.scheduleMapResize();
  }

  private scheduleMapResize(): void {
    if (!this.map) return;
    const resize = () => this.map.invalidateSize(true);
    resize();
    requestAnimationFrame(resize);
    setTimeout(resize, 150);
    setTimeout(resize, 400);
  }

  private observeMapResize(): void {
    const el = this.mapContainer?.nativeElement;
    if (!el || typeof ResizeObserver === 'undefined') return;
    this.mapResizeObserver?.disconnect();
    this.mapResizeObserver = new ResizeObserver(() => this.map?.invalidateSize(true));
    this.mapResizeObserver.observe(el);
  }

  centerMap(): void {
    const gps = this.locations.filter(l => l.hasGps && l.latitude && l.longitude);
    if (!gps.length) {
      this.map.setView([30.3753, 69.3451], 6);
      return;
    }
    const bounds = L.latLngBounds(gps.map(l => [l.latitude, l.longitude] as [number, number]));
    this.map.fitBounds(bounds, { padding: [48, 48], maxZoom: 12 });
  }

  resetZoom(): void {
    this.map.setZoom(6);
    this.centerMap();
  }

  retryMap(): void {
    this.mapError = null;
    if (this.map) {
      this.setMapTheme(this.mapTheme);
      this.scheduleMapResize();
      return;
    }
    this._bootstrapTimer = setTimeout(() => void this.bootstrapMap(), 100);
  }

  toggleFullscreen(): void {
    const el = this.mapHost?.nativeElement;
    if (!el) return;
    if (!document.fullscreenElement) {
      el.requestFullscreen?.();
    } else {
      document.exitFullscreen?.();
    }
  }

  selectVehicle(loc: VehicleLocation): void {
    this.selectedVehicleId = loc.vehicleId;
    this.markUserActive();
    void this.realtime.subscribeVehicle(loc.vehicleId);
    if (loc.hasGps && this.isValidCoord(loc.latitude, loc.longitude)) {
      this.focusVehicle(loc);
    } else {
      this.pushEvent(`${loc.vehicleName} has no GPS coordinates yet`, 'warning', 'gps_off');
    }

    this.selectedEta = null;
    if (loc.bookingId != null) {
      this.gpsService.getEta(loc.bookingId).subscribe({
        next: eta => { this.selectedEta = eta; },
        error: () => { this.selectedEta = null; }
      });
    }
  }

  goToCommands(): void {
    this.router.navigate(['/gps-tracking/commands']);
  }

  onVehicleCardEnter(loc: VehicleLocation): void {
    if (!loc.hasGps) return;
    const marker = this.markers.get(loc.vehicleId);
    marker?.setZIndexOffset(2000);
    this.trailLayers.get(loc.vehicleId)?.setStyle({ weight: 5, opacity: 1 });
  }

  onVehicleCardLeave(loc: VehicleLocation): void {
    const marker = this.markers.get(loc.vehicleId);
    if (this.selectedVehicleId !== loc.vehicleId) {
      marker?.setZIndexOffset(0);
    }
    const line = this.trailLayers.get(loc.vehicleId);
    if (line) line.setStyle({ weight: 3, opacity: 0.75 });
  }

  private configureLeafletDefaults(): void {
    if (!L.Icon?.Default?.mergeOptions) {
      return;
    }
    const iconBase = 'https://unpkg.com/leaflet@1.9.4/dist/images/';
    L.Icon.Default.mergeOptions({
      iconRetinaUrl: `${iconBase}marker-icon-2x.png`,
      iconUrl: `${iconBase}marker-icon.png`,
      shadowUrl: `${iconBase}marker-shadow.png`
    });
  }

  private initMap(): void {
    const host = this.mapContainer?.nativeElement;
    if (!host) {
      throw new Error('Map container element not found');
    }

    if ((host as HTMLElement & { _leaflet_id?: number })._leaflet_id != null) {
      return;
    }

    this.configureLeafletDefaults();

    this.map = L.map(host, {
      center: [30.3753, 69.3451],
      zoom: 6,
      maxZoom: 20,
      zoomControl: false,
      preferCanvas: false
    });
    L.control.zoom({ position: 'bottomright' }).addTo(this.map);
    this.markerCluster = createMarkerClusterGroup({
      maxClusterRadius: 55,
      disableClusteringAtZoom: 14,
      spiderfyOnMaxZoom: true,
      showCoverageOnHover: false,
      animateAddingMarkers: true,
      iconCreateFunction: (cluster: { getChildCount: () => number }) => {
        const count = cluster.getChildCount();
        return L.divIcon({
          html: `<div class="fleet-cluster"><span>${count}</span></div>`,
          className: 'fleet-cluster-host',
          iconSize: [44, 44],
          iconAnchor: [22, 22]
        });
      }
    });
    this.map.addLayer(this.markerCluster);
    this.setMapTheme(this.mapTheme);
    this.observeMapResize();
    this.map.whenReady(() => this.scheduleMapResize());
    this.scheduleMapResize();
    this.mapReady = true;
    if (this.pendingMarkerLocations) {
      this.updateMarkers(this.pendingMarkerLocations);
      this.pendingMarkerLocations = null;
    } else if (this.locations.length) {
      this.updateMarkers(this.mappableLocations(this.locations));
    }
  }

  private mappableLocations(locs: VehicleLocation[]): VehicleLocation[] {
    return locs.filter(l => l.hasGps && this.isValidCoord(l.latitude, l.longitude));
  }

  private loadLocations(silent = false, manual = false): void {
    if (this.isRefreshingLocations) return;
    this.isRefreshingLocations = true;
    if (!silent) this.loading = true;
    this.gpsService.getAllVehicleLocations().subscribe({
      next: locs => {
        const prevMoving = new Set(
          this.locations.filter(l => l.status === 'moving').map(l => l.vehicleId)
        );
        const previousLocations = this.locations;
        this.locations = mergeVehicleLocations(previousLocations, locs);
        this.recomputeFleetHealth();
        this.loading = false;
        this.syncError = null;
        this.lastSyncAt = new Date();
        this.secondsSinceSync = 0;
        const gpsLocs = this.mappableLocations(this.locations);
        this.updateMarkers(gpsLocs);
        this.emitTelemetryEvents(gpsLocs, prevMoving);
        this.emitSyncSummary(gpsLocs, manual || !silent);
        if (this.pendingFocusVehicleId != null) {
          const target = this.locations.find(l => l.vehicleId === this.pendingFocusVehicleId);
          if (target) {
            this.focusVehicle(target);
            this.pendingFocusVehicleId = null;
          }
        } else if (!silent && gpsLocs.length) {
          this.centerMap();
        }
        if (manual) {
          this.scheduleMapResize();
        }
        this.isRefreshingLocations = false;
      },
      error: () => {
        this.loading = false;
        this.isRefreshingLocations = false;
        this.syncError = 'Could not reach tracking service.';
        this.pushEvent('Tracking sync failed', 'alert', 'cloud_off');
      }
    });
  }

  private emitSyncSummary(gpsLocs: VehicleLocation[], announce: boolean): void {
    if (!announce) return;
    const live = gpsLocs.filter(l => l.isLive).length;
    const lastKnown = gpsLocs.length - live;
    const summary = `${gpsLocs.length} on map (${live} live${lastKnown ? `, ${lastKnown} last known` : ''})`;
    if (summary === this.lastSyncSummaryKey) return;
    this.lastSyncSummaryKey = summary;
    this.pushEvent(`Fleet synced — ${summary}`, 'success', 'sync');
    gpsLocs.slice(0, 3).forEach(loc => {
      const ignition = loc.ignition === true ? 'Ignition on' : loc.ignition === false ? 'Ignition off' : null;
      const detail = [
        loc.isLive ? 'Live signal' : 'Last known position',
        ignition,
        loc.speed > 0 ? `${Math.round(loc.speed)} km/h` : null
      ].filter(Boolean).join(' · ');
      this.pushEvent(`${loc.vehicleName}: ${detail}`, loc.isLive ? 'success' : 'info', 'place');
    });
  }

  private loadRecentAlertEvents(): void {
    this.gpsService.getAlertEvents(undefined, true).subscribe({
      next: events => {
        events.slice(0, 5).forEach(ev => {
          this.pushEvent(ev.message || `${ev.eventType} alert`, 'warning', 'warning');
        });
      },
      error: () => {}
    });
  }

  private emitTelemetryEvents(gpsLocs: VehicleLocation[], prevMoving: Set<number>): void {
    gpsLocs
      .filter(l => l.status === 'moving' && l.speed > 5)
      .slice(0, 2)
      .forEach(l => {
        if (!prevMoving.has(l.vehicleId)) {
          this.pushEvent(`${l.vehicleName} is now en route (${Math.round(l.speed)} km/h)`, 'success', 'directions_bus');
        }
      });
    gpsLocs
      .filter(l => l.status === 'delayed')
      .slice(0, 1)
      .forEach(l => this.pushEvent(`${l.vehicleName} — delayed telemetry`, 'warning', 'warning'));
  }

  private pushEvent(message: string, type: TrackEvent['type'], icon: string): void {
    this.events = [{ time: new Date(), message, type, icon }, ...this.events].slice(0, 30);
  }

  private bearingFrom(prev: { lat: number; lng: number }, lat: number, lng: number): number {
    const dLng = ((lng - prev.lng) * Math.PI) / 180;
    const lat1 = (prev.lat * Math.PI) / 180;
    const lat2 = (lat * Math.PI) / 180;
    const y = Math.sin(dLng) * Math.cos(lat2);
    const x =
      Math.cos(lat1) * Math.sin(lat2) -
      Math.sin(lat1) * Math.cos(lat2) * Math.cos(dLng);
    return ((Math.atan2(y, x) * 180) / Math.PI + 360) % 360;
  }

  private createMarkerIcon(status: FleetTrackStatus, bearing = 0, vehicleType?: string | null): LeafletTypes.DivIcon {
    const showArrow = status === 'moving' || status === 'delayed';
    const arrow = showArrow
      ? `<span class="fleet-marker-arrow" style="transform:rotate(${bearing}deg)"></span>`
      : '';
    const vehicleGlyph =
      status === 'sos'
        ? '&#128680;'
        : status === 'delayed'
          ? '&#9888;'
          : status === 'offline' || status === 'never_seen'
            ? '&#9679;'
            : this.vehicleCategoryGlyph(vehicleType);
    return L.divIcon({
      className: 'fleet-marker-host',
      html: `
        <div class="fleet-marker fleet-marker--${status}">
          <span class="fleet-marker-ring"></span>
          <span class="fleet-marker-pulse"></span>
          ${arrow}
          <span class="fleet-marker-glyph">${vehicleGlyph}</span>
        </div>`,
      iconSize: [40, 40],
      iconAnchor: [20, 20],
      popupAnchor: [0, -22]
    });
  }

  private updateTrail(loc: VehicleLocation): void {
    const pts = [...(this.positionTrails.get(loc.vehicleId) ?? []), [loc.latitude, loc.longitude] as [number, number]]
      .slice(-this.maxTrailPoints);
    this.positionTrails.set(loc.vehicleId, pts);
    if (pts.length < 2) return;

    const color = TRAIL_COLORS[loc.status];
    const existing = this.trailLayers.get(loc.vehicleId);
    if (existing) {
      existing.setLatLngs(pts);
      existing.setStyle({ color });
    } else {
      const line = L.polyline(pts, {
        color,
        weight: 3,
        opacity: 0.8,
        className: `live-trail live-trail--${loc.status}`
      }).addTo(this.map);
      this.trailLayers.set(loc.vehicleId, line);
    }
  }

  private updateMarkers(locs: VehicleLocation[]): void {
    if (!this.mapReady || !this.map || !this.markerCluster) {
      this.pendingMarkerLocations = locs;
      return;
    }

    const mappable = locs.filter(
      loc => loc.hasGps && this.isValidCoord(loc.latitude, loc.longitude)
    );
    const currentIds = new Set(mappable.map(l => l.vehicleId));

    this.markers.forEach((marker, vehicleId) => {
      if (!currentIds.has(vehicleId)) {
        this.markerCluster.removeLayer(marker);
        marker.remove();
        this.markers.delete(vehicleId);
        this.trailLayers.get(vehicleId)?.remove();
        this.trailLayers.delete(vehicleId);
        this.positionTrails.delete(vehicleId);
        this.prevPositions.delete(vehicleId);
      }
    });

    mappable.forEach(loc => {
      const prev = this.prevPositions.get(loc.vehicleId);
      let bearing = 0;
      if (prev) {
        bearing = this.bearingFrom(prev, loc.latitude, loc.longitude);
      }
      this.prevPositions.set(loc.vehicleId, { lat: loc.latitude, lng: loc.longitude });
      this.updateTrail(loc);

      const popupContent = `
        <div class="map-popup">
          <strong>${loc.vehicleName}</strong>
          <span class="map-popup-reg">${loc.registrationNumber}</span>
          ${loc.driverName ? `<span>Driver: ${loc.driverName}</span>` : ''}
          <span>${this.statusLabel(loc.status)} · ${this.speedLabel(loc)}</span>
          <small>${this.formatLastPing(loc)}</small>
          <a href="#" class="map-popup-link" data-vid="${loc.vehicleId}">View vehicle →</a>
        </div>
      `;
      const icon = this.createMarkerIcon(loc.status, bearing, loc.vehicleType);

      if (this.markers.has(loc.vehicleId)) {
        const m = this.markers.get(loc.vehicleId)!;
        m.setLatLng([loc.latitude, loc.longitude]);
        m.setIcon(icon);
        m.setPopupContent(popupContent);
      } else {
        const marker = L.marker([loc.latitude, loc.longitude], { icon })
          .bindPopup(popupContent);
        marker.on('popupopen', () => {
          document.querySelector(`a[data-vid="${loc.vehicleId}"]`)?.addEventListener('click', e => {
            e.preventDefault();
            this.goToVehicleProfile(loc.vehicleId);
          }, { once: true });
        });
        this.markerCluster.addLayer(marker);
        this.markers.set(loc.vehicleId, marker);
      }
      if (this.selectedVehicleId === loc.vehicleId) {
        this.markers.get(loc.vehicleId)?.setZIndexOffset(1500);
      }
    });

    if (typeof this.markerCluster.refreshClusters === 'function') {
      this.markerCluster.refreshClusters();
    }
    this.scheduleMapResize();
  }

  focusVehicle(loc: VehicleLocation): void {
    if (!loc.hasGps || !this.isValidCoord(loc.latitude, loc.longitude)) return;
    this.map.setView([loc.latitude, loc.longitude], 14, { animate: true });
    const marker = this.markers.get(loc.vehicleId);
    if (marker) {
      marker.openPopup();
    } else {
      setTimeout(() => this.markers.get(loc.vehicleId)?.openPopup(), 250);
    }
    this.pushEvent(
      `Focused ${loc.vehicleName} (${loc.isLive ? 'live' : 'last known'})`,
      'info',
      'my_location'
    );
  }

  private isValidCoord(lat: number, lng: number): boolean {
    return Number.isFinite(lat) && Number.isFinite(lng) && !(lat === 0 && lng === 0);
  }

  goToVehicleProfile(vehicleId: number): void {
    this.router.navigate(['/vehicles', vehicleId]);
  }

  goToVehicles(): void {
    this.router.navigate(['/vehicles']);
  }

  loadHistory(): void {
    if (!this.selectedVehicleId) return;
    this.loadingHistory = true;
    this.historyError = '';
    this.historyRows = [];
    this.tripSummary = null;
    this.showHistory = true;
    this.clearHistoryOverlay();
    this.gpsService
      .getHistory(this.selectedVehicleId, new Date(this.historyFrom), new Date(this.historyTo))
      .subscribe({
        next: rows => {
          this.historyRows = [...rows].sort(
            (a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime()
          );
          this.loadingHistory = false;
          this.tripSummary = this.computeTripSummary(this.historyRows);
          this.drawHistoryRoute(this.historyRows);
          this.buildHistoryEvents(this.historyRows);
          if (this.historyRows.length) {
            this.pushEvent(`Route loaded — ${this.tripSummary?.distanceKm} km`, 'info', 'route');
          }
        },
        error: () => {
          this.historyError = 'Could not load tracking history.';
          this.loadingHistory = false;
        }
      });
  }

  private computeTripSummary(rows: TrackingDto[]): TripSummary | null {
    if (!rows.length) return null;
    let distanceKm = 0;
    let stopMs = 0;
    const speeds: number[] = [];
    for (let i = 1; i < rows.length; i++) {
      const a = rows[i - 1];
      const b = rows[i];
      distanceKm += this.haversineKm(a.latitude, a.longitude, b.latitude, b.longitude);
      const dt = new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime();
      const sp = Number(b.speed) || 0;
      speeds.push(sp);
      if (sp < 2 && dt > 0) stopMs += dt;
    }
    const t0 = new Date(rows[0].timestamp).getTime();
    const t1 = new Date(rows[rows.length - 1].timestamp).getTime();
    const durationMinutes = Math.max(1, Math.round((t1 - t0) / 60000));
    const avgSpeed =
      speeds.length > 0
        ? Math.round(speeds.reduce((s, v) => s + v, 0) / speeds.length)
        : 0;
    return {
      distanceKm: Math.round(distanceKm * 10) / 10,
      avgSpeed,
      stopMinutes: Math.round(stopMs / 60000),
      durationMinutes,
      pointCount: rows.length
    };
  }

  private haversineKm(lat1: number, lon1: number, lat2: number, lon2: number): number {
    const R = 6371;
    const dLat = ((lat2 - lat1) * Math.PI) / 180;
    const dLon = ((lon2 - lon1) * Math.PI) / 180;
    const a =
      Math.sin(dLat / 2) ** 2 +
      Math.cos((lat1 * Math.PI) / 180) *
        Math.cos((lat2 * Math.PI) / 180) *
        Math.sin(dLon / 2) ** 2;
    return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  }

  private buildHistoryEvents(rows: TrackingDto[]): void {
    rows.forEach((r, i) => {
      const speed = Number(r.speed) || 0;
      if (speed > 80) {
        this.pushEvent(`Speed alert — ${Math.round(speed)} km/h`, 'alert', 'speed');
      }
      if (i > 0 && speed < 2 && (Number(rows[i - 1].speed) || 0) > 10) {
        this.pushEvent('Vehicle stopped', 'info', 'pause_circle');
      }
      if (i > 0 && speed > 10 && (Number(rows[i - 1].speed) || 0) < 2) {
        this.pushEvent('Vehicle departed', 'success', 'play_circle');
      }
    });
  }

  private drawHistoryRoute(rows: TrackingDto[]): void {
    if (!rows.length) return;
    const latlngs: LeafletTypes.LatLngExpression[] = rows.map(r => [r.latitude, r.longitude]);
    this.historyPolyline = L.polyline(latlngs, {
      color: '#2DD4BF',
      weight: 5,
      opacity: 0.9,
      className: 'history-route-glow'
    }).addTo(this.map);
    const last = rows[rows.length - 1];
    let bearing = 0;
    if (rows.length > 1) {
      const p = rows[rows.length - 2];
      bearing = this.bearingFrom(
        { lat: p.latitude, lng: p.longitude },
        last.latitude,
        last.longitude
      );
    }
    this.historyMarker = L.marker([last.latitude, last.longitude], {
      icon: this.createMarkerIcon('moving', bearing)
    }).addTo(this.map);
    this.map.fitBounds(this.historyPolyline.getBounds(), { padding: [48, 48] });
  }

  private clearHistoryOverlay(): void {
    this.stopReplay();
    this.replayIndex = 0;
    if (this.historyPolyline) {
      this.map.removeLayer(this.historyPolyline);
      this.historyPolyline = undefined;
    }
    if (this.historyMarker) {
      this.map.removeLayer(this.historyMarker);
      this.historyMarker = undefined;
    }
  }

  toggleReplay(): void {
    if (this.replayPlaying) {
      this.stopReplay();
      return;
    }
    if (!this.historyRows.length) return;
    this.replayPlaying = true;
    this.replayIndex = 0;
    const stepMs = 350 / this.replaySpeed;
    this.replayTimer = setInterval(() => this.advanceReplay(), stepMs);
    this.pushEvent('Route playback started', 'info', 'play_arrow');
  }

  setReplaySpeed(mult: number): void {
    this.replaySpeed = mult;
    if (this.replayPlaying) {
      this.stopReplay();
      this.toggleReplay();
    }
  }

  onReplayScrub(event: Event): void {
    const val = Number((event.target as HTMLInputElement).value);
    if (!this.historyRows.length) return;
    this.replayIndex = Math.min(
      this.historyRows.length - 1,
      Math.floor((val / 100) * this.historyRows.length)
    );
    const row = this.historyRows[this.replayIndex];
    if (row && this.historyMarker) {
      let bearing = 0;
      if (this.replayIndex > 0) {
        const p = this.historyRows[this.replayIndex - 1];
        bearing = this.bearingFrom(
          { lat: p.latitude, lng: p.longitude },
          row.latitude,
          row.longitude
        );
      }
      this.historyMarker.setLatLng([row.latitude, row.longitude]);
      this.historyMarker.setIcon(this.createMarkerIcon('moving', bearing));
    }
  }

  private advanceReplay(): void {
    if (this.replayIndex >= this.historyRows.length) {
      this.stopReplay();
      this.pushEvent('Route playback complete', 'success', 'flag');
      return;
    }
    const row = this.historyRows[this.replayIndex];
    let bearing = 0;
    if (this.replayIndex > 0) {
      const p = this.historyRows[this.replayIndex - 1];
      bearing = this.bearingFrom(
        { lat: p.latitude, lng: p.longitude },
        row.latitude,
        row.longitude
      );
    }
    this.historyMarker?.setLatLng([row.latitude, row.longitude]);
    this.historyMarker?.setIcon(this.createMarkerIcon('moving', bearing));
    this.map.setView([row.latitude, row.longitude], Math.max(this.map.getZoom(), 12), {
      animate: true
    });
    this.replayIndex++;
  }

  stopReplay(): void {
    this.replayPlaying = false;
    if (this.replayTimer) {
      clearInterval(this.replayTimer);
      this.replayTimer = undefined;
    }
  }

  formatSyncAgo(): string {
    if (!this.lastSyncAt) return '—';
    if (this.secondsSinceSync < 60) return `${this.secondsSinceSync} sec ago`;
    return `${Math.floor(this.secondsSinceSync / 60)} min ago`;
  }

  private markUserActive(): void {
    this.userInteractionActive = true;
    if (this.interactionPauseTimer) clearTimeout(this.interactionPauseTimer);
    this.interactionPauseTimer = setTimeout(() => {
      this.userInteractionActive = false;
      this.startAutoRefresh();
    }, 2000);
  }

  private applyRealtimeUpdate(update: PositionDto): void {
    const idx = this.locations.findIndex(l => l.vehicleId === update.vehicleId);
    if (idx >= 0) {
      const speed = Number(update.speed) || 0;
      const prevStatus = this.locations[idx].status;
      const status = resolveFleetStatus({
        speed,
        ignition: update.ignition,
        lastUpdated: update.timestamp,
        hasGps: true,
        alarmType: update.alarmType
      });
      this.locations[idx] = {
        ...this.locations[idx],
        latitude: update.latitude,
        longitude: update.longitude,
        speed,
        lastUpdated: update.timestamp,
        status,
        hasGps: true,
        isLive: true,
        ignition: update.ignition,
        heading: update.heading,
        fuelLevel: update.fuelLevel,
        batteryLevel: update.batteryLevel,
        gsmSignal: update.gsmSignal,
        totalDistanceKm: update.totalDistanceKm,
        address: update.address,
        alarmType: update.alarmType,
        routeHint: this.realtimeRouteHint(status, speed)
      };
      this.updateMarkers(this.mappableLocations(this.locations));
      if (this.followSelected && this.selectedVehicleId === update.vehicleId) {
        this.map?.panTo([update.latitude, update.longitude], { animate: true });
      }
      if (status === 'sos' && prevStatus !== 'sos') {
        this.pushEvent(`${this.locations[idx].vehicleName} — SOS / panic alarm!`, 'alert', 'sos');
      } else {
        this.pushEvent(
          `${this.locations[idx].vehicleName} location updated (${Math.round(speed)} km/h)`,
          'success',
          'gps_fixed'
        );
      }
    }
    this.lastSyncAt = new Date();
    this.secondsSinceSync = 0;
  }

  private realtimeRouteHint(status: FleetTrackStatus, speed: number): string {
    if (status === 'moving') return `${Math.round(speed)} km/h`;
    if (status === 'idle') return 'Idle • awaiting movement';
    if (status === 'parked') return 'Parked • ignition off';
    if (status === 'sos') return 'SOS / panic alarm';
    return `${Math.round(speed)} km/h`;
  }
}
