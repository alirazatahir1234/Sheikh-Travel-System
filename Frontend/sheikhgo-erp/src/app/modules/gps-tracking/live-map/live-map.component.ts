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
import { MAP_TILE_STACKS, MAP_THEME_OPTIONS, MapTheme, readStoredMapTheme, storeMapTheme } from '../../../core/leaflet/leaflet-map-tiles';
import { GoogleTrafficBasemap } from '../../../core/leaflet/google-traffic-basemap';
import { GoogleMapsLoaderService } from '../../../core/services/google-maps-loader.service';
import {
  createFleetVehicleDivIcon,
  buildFleetVehiclePopup
} from '../../../core/leaflet/fleet-vehicle-marker';
import {
  addGeofenceBoundary,
  clearLayerGroup
} from '../../../core/leaflet/geofence-layer';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { GpsRealtimeService, GpsConnectionState } from '../../../core/services/gps-realtime.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { DriverService } from '../../../core/services/driver.service';
import {
  VehicleLocation,
  PositionDto,
  FleetTrackStatus,
  SosAlertPayload,
  GpsFleetStatusLocal,
  GpsFleetStatusSnapshot,
  GpsEta,
  TraccarStatusDto
} from '../../../core/models/gps-tracking.model';
import { VehicleListItem } from '../../../core/models/vehicle.model';
import { resolveFleetStatus } from '../../../core/utils/gps-status.util';
import { parseGpsTimestamp } from '../../../core/utils/gps-timestamp.util';
import { mergeVehicleLocations } from './live-map-state.util';
import { computeFleetHealth, FleetHealthBreakdown } from '../utils/fleet-health.util';
import { isTraccarReachable } from '../utils/tracker-status.util';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';

type StatusFilter = 'all' | FleetTrackStatus;
type IgnitionFilter = 'all' | 'on' | 'off';
type RefreshRateMs = 5000 | 10000 | 30000 | 60000 | null;

interface TrackEvent {
  time: Date;
  message: string;
  type: 'info' | 'alert' | 'success' | 'warning';
  icon: string;
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
  @ViewChild('vehicleSearchInput') vehicleSearchInput?: ElementRef<HTMLInputElement>;

  private map!: LeafletTypes.Map;
  private tileLayer?: LeafletTypes.TileLayer;
  private readonly trafficBasemap = new GoogleTrafficBasemap();
  private mapResizeObserver?: ResizeObserver;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private markerCluster!: any;
  private markers = new Map<number, LeafletTypes.Marker>();
  private markerAnimFrames = new Map<number, number>();
  private trailLayers = new Map<number, LeafletTypes.Polyline>();
  private geofenceLayer: LeafletTypes.LayerGroup | null = null;
  private prevPositions = new Map<number, { lat: number; lng: number }>();
  private positionTrails = new Map<number, [number, number][]>();
  private refreshInterval?: ReturnType<typeof setInterval>;
  private readonly maxTrailPoints = 14;
  private readonly maxAnimateKm = 2;
  private readonly markerAnimMs = 500;

  locations: VehicleLocation[] = [];
  loading = true;
  syncError: string | null = null;
  mapError: string | null = null;
  searchQuery = '';
  statusFilter: StatusFilter = 'all';
  ignitionFilter: IgnitionFilter = 'all';
  batteryLowOnly = false;
  panelCollapsed = false;
  filterApplying = false;
  private searchDebounceTimer?: ReturnType<typeof setTimeout>;
  private static readonly FILTER_STORAGE_KEY = 'stb-live-map-filters';
  mapTheme: MapTheme = readStoredMapTheme();
  mapThemeMenuOpen = false;
  readonly mapThemeOptions = MAP_THEME_OPTIONS;
  liveTracking = true;
  listSheetOpen = true;
  selectedVehicleId: number | null = null;
  lastSyncAt: Date | null = null;
  secondsSinceSync = 0;
  /**
   * Wall-clock snapshot updated once per second. Template helpers (Last ping, signal bars)
   * must use this instead of Date.now() so Angular's CD verify pass does not see a
   * second tick mid-cycle (NG0100 ExpressionChangedAfterItHasBeenCheckedError).
   */
  clockMs = Date.now();
  isMapFullscreen = false;
  private syncTick?: ReturnType<typeof setInterval>;
  traccarStatus: TraccarStatusDto | null = null;
  private traccarStatusPoll?: ReturnType<typeof setInterval>;

  readonly refreshRateOptions: { id: RefreshRateMs; label: string }[] = [
    { id: 30000, label: '30 sec' },
    { id: 60000, label: '1 min (SignalR primary)' },
    { id: 10000, label: '10 sec (fallback)' },
    { id: null, label: 'Pause' }
  ];
  /** User-selected cap; effective poll interval adapts to SignalR connection state. */
  refreshRateMs: RefreshRateMs = 60000;
  followSelected = false;
  connectionState: GpsConnectionState = 'disconnected';
  private connectionStateSub?: { unsubscribe(): void };
  private sosSub?: { unsubscribe(): void };
  private readonly BATTERY_LOW_THRESHOLD = 20;

  vehicles: VehicleListItem[] = [];
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

  geofenceBreachCount = 0;
  showGeofences = false;
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
    private route: ActivatedRoute,
    private googleMapsLoader: GoogleMapsLoaderService
  ) {}

  ngOnInit(): void {
    this.restoreFilters();

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
    this.refreshTraccarStatus();
    this.traccarStatusPoll = setInterval(() => this.refreshTraccarStatus(), 60_000);
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
      this.startAutoRefresh();
    });
    this.sosSub = this.realtime.sosAlerts$.subscribe(alert => {
      const idx = this.locations.findIndex(l => l.vehicleId === alert.vehicleId);
      if (idx >= 0) {
        this.locations[idx] = { ...this.locations[idx], status: 'sos', alarmType: 'sos' };
        this.updateMarkers(this.mappableLocations(this.filteredLocations));
        this.pushEvent(`${this.locations[idx].vehicleName} — SOS / panic alarm!`, 'alert', 'sos');
      } else {
        this.pushEvent(`Vehicle #${alert.vehicleId} — SOS / panic alarm!`, 'alert', 'sos');
      }
    });
    this.syncTick = setInterval(() => {
      this.clockMs = Date.now();
      if (this.lastSyncAt) {
        this.secondsSinceSync = Math.floor((this.clockMs - this.lastSyncAt.getTime()) / 1000);
      }
    }, 1000);
    this.pushEvent('Tracking console ready', 'info', 'gps_fixed');
  }

  ngAfterViewInit(): void {
    // Defer until the routed view and map container dimensions are ready.
    this._bootstrapTimer = setTimeout(() => void this.bootstrapMap(), 100);
    setTimeout(() => this.vehicleSearchInput?.nativeElement?.focus(), 250);
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
        void this.setMapTheme(this.mapTheme);
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
    if (this.searchDebounceTimer) clearTimeout(this.searchDebounceTimer);
    if (this.refreshInterval) clearInterval(this.refreshInterval);
    if (this.syncTick) clearInterval(this.syncTick);
    if (this.traccarStatusPoll) clearInterval(this.traccarStatusPoll);
    if (this.interactionPauseTimer) clearTimeout(this.interactionPauseTimer);
    this.markerAnimFrames.forEach(id => cancelAnimationFrame(id));
    this.markerAnimFrames.clear();
    this.trafficBasemap.detach();
    this.mapResizeObserver?.disconnect();
    this.realtimeSub?.unsubscribe();
    this.connectionStateSub?.unsubscribe();
    this.sosSub?.unsubscribe();
    void this.realtime.disconnect();
    if (this.map) this.map.remove();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.mapThemeMenuOpen) return;
    const target = event.target as HTMLElement | null;
    if (target?.closest('.map-theme-control')) return;
    this.mapThemeMenuOpen = false;
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

  get hasActiveFilters(): boolean {
    return (
      this.searchQuery.trim().length > 0 ||
      this.statusFilter !== 'all' ||
      this.ignitionFilter !== 'all' ||
      this.batteryLowOnly
    );
  }

  get emptyStateKind(): 'loading' | 'no-data' | 'no-match' | null {
    if (this.loading && this.locations.length === 0) return 'loading';
    if (this.locations.length === 0) return 'no-data';
    if (this.filteredLocations.length === 0) return 'no-match';
    return null;
  }

  setIgnitionFilter(id: IgnitionFilter): void {
    this.ignitionFilter = this.ignitionFilter === id ? 'all' : id;
    this.persistFilters();
    this.markUserActive();
    this.applyVisibleMarkers();
  }

  toggleBatteryLowOnly(): void {
    this.batteryLowOnly = !this.batteryLowOnly;
    this.persistFilters();
    this.markUserActive();
    this.applyVisibleMarkers();
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
    if (this.traccarStatus?.syncEnabled === false) {
      return this.isTraccarOnline
        ? 'Traccar sync disabled — enable Traccar:Enabled'
        : 'Traccar sync disabled';
    }
    if (!this.isTraccarOnline) return 'Traccar unreachable — showing last known';
    if (this.connectionState === 'reconnecting') return 'Connection lost — reconnecting…';
    if (this.connectionState === 'disconnected') return 'Polling only (realtime offline)';
    if (this.syncError) return 'Sync issue — tap Refresh';
    return 'Connected';
  }

  get trackingActive(): boolean {
    return this.liveTracking
      && !this.syncError
      && this.isTraccarOnline
      && this.traccarStatus?.syncEnabled !== false
      && this.connectionState === 'connected';
  }

  get isTraccarOnline(): boolean {
    return this.traccarStatus == null || isTraccarReachable(this.traccarStatus.connected);
  }

  get gpsHealthy(): boolean {
    if (this.traccarStatus?.syncEnabled === false) return false;
    return this.isTraccarOnline && this.locations.some(
      l => l.hasGps && this.isValidCoord(l.latitude, l.longitude)
    );
  }

  get gpsStatusPillLabel(): string {
    if (this.traccarStatus?.syncEnabled === false) return 'Sync disabled';
    if (!this.isTraccarOnline) return 'Traccar offline';
    return this.gpsHealthy ? 'GPS healthy' : 'GPS limited';
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

  signalBars(loc: VehicleLocation): number {
    if (!loc.hasGps) return 0;
    if (!loc.lastUpdated) return 1;
    const ageMin = (this.clockMs - parseGpsTimestamp(loc.lastUpdated)) / 60000;
    if (!Number.isFinite(ageMin)) return 1;
    if (ageMin < 2 && loc.status === 'moving') return 4;
    if (ageMin < 10) return 3;
    if (ageMin < 30) return 2;
    return 1;
  }

  /** Freshness bars — not satellite lock; GSM dBm is shown separately when present. */
  signalFreshnessLabel(loc: VehicleLocation): string {
    const bars = this.signalBars(loc);
    if (bars >= 4) return 'Fresh';
    if (bars >= 3) return 'Good';
    if (bars >= 2) return 'Aging';
    return 'Stale';
  }

  formatLastPing(loc: VehicleLocation): string {
    if (!loc.hasGps || !loc.lastUpdated) return 'No live GPS';
    const sec = Math.floor((this.clockMs - parseGpsTimestamp(loc.lastUpdated)) / 1000);
    if (!Number.isFinite(sec) || sec < 0) return 'No live GPS';
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

  headingLabel(loc: VehicleLocation): string | null {
    const h = loc.heading;
    if (h == null || !Number.isFinite(h)) return null;
    const deg = ((Math.round(h) % 360) + 360) % 360;
    const dirs = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];
    const idx = Math.round(deg / 45) % 8;
    return `${deg}° ${dirs[idx]}`;
  }

  temperatureLabel(loc: VehicleLocation): string | null {
    if (loc.temperature == null || !Number.isFinite(loc.temperature)) return null;
    return `${loc.temperature.toFixed(1)} °C`;
  }

  /** First 1–2 segments of a reverse-geocoded address for compact cards. */
  shortAddress(loc: VehicleLocation): string | null {
    const raw = loc.address?.trim();
    if (!raw) return null;
    const parts = raw.split(',').map(p => p.trim()).filter(Boolean);
    if (parts.length === 0) return null;
    if (parts.length === 1) return parts[0];
    return `${parts[0]}, ${parts[1]}`;
  }

  googleMapsUrl(loc: VehicleLocation): string {
    return `https://maps.google.com/?q=${loc.latitude},${loc.longitude}`;
  }

  private refreshTraccarStatus(): void {
    this.gpsService.getTraccarStatus().pipe(
      catchError(() => of({ connected: false, serverVersion: null, deviceCount: 0, lastError: 'Unavailable' } as TraccarStatusDto))
    ).subscribe(status => {
      this.traccarStatus = status;
    });
  }

  onSearchQueryChanged(value: string): void {
    this.searchQuery = value;
    this.markUserActive();
    this.filterApplying = true;
    if (this.searchDebounceTimer) clearTimeout(this.searchDebounceTimer);
    this.searchDebounceTimer = setTimeout(() => {
      this.persistFilters();
      this.applyVisibleMarkers();
      this.filterApplying = false;
    }, 300);
  }

  onSearchEnter(): void {
    if (this.searchDebounceTimer) {
      clearTimeout(this.searchDebounceTimer);
      this.searchDebounceTimer = undefined;
    }
    this.persistFilters();
    this.applyVisibleMarkers();
    this.filterApplying = false;
    const first = this.filteredLocations[0];
    if (first) this.selectVehicle(first);
  }

  setStatusFilter(id: StatusFilter): void {
    this.statusFilter = id;
    this.persistFilters();
    this.markUserActive();
    this.applyVisibleMarkers();
  }

  clearFilters(): void {
    this.searchQuery = '';
    this.statusFilter = 'all';
    this.ignitionFilter = 'all';
    this.batteryLowOnly = false;
    this.persistFilters();
    this.markUserActive();
    this.applyVisibleMarkers();
  }

  togglePanelCollapsed(): void {
    this.panelCollapsed = !this.panelCollapsed;
    setTimeout(() => {
      this.map?.invalidateSize();
      this.scheduleMapResize();
    }, 220);
  }

  private applyVisibleMarkers(): void {
    this.updateMarkers(this.mappableLocations(this.filteredLocations));
  }

  private persistFilters(): void {
    try {
      localStorage.setItem(
        LiveMapComponent.FILTER_STORAGE_KEY,
        JSON.stringify({
          searchQuery: this.searchQuery,
          statusFilter: this.statusFilter,
          ignitionFilter: this.ignitionFilter,
          batteryLowOnly: this.batteryLowOnly
        })
      );
    } catch {
      /* ignore */
    }
  }

  private restoreFilters(): void {
    try {
      const raw = localStorage.getItem(LiveMapComponent.FILTER_STORAGE_KEY);
      if (!raw) return;
      const parsed = JSON.parse(raw) as {
        searchQuery?: string;
        statusFilter?: StatusFilter;
        ignitionFilter?: IgnitionFilter;
        batteryLowOnly?: boolean;
      };
      if (typeof parsed.searchQuery === 'string') this.searchQuery = parsed.searchQuery;
      if (parsed.statusFilter) this.statusFilter = parsed.statusFilter;
      if (parsed.ignitionFilter) this.ignitionFilter = parsed.ignitionFilter;
      if (typeof parsed.batteryLowOnly === 'boolean') this.batteryLowOnly = parsed.batteryLowOnly;
    } catch {
      /* ignore */
    }
  }

  openHistoryForSelected(): void {
    this.openFullHistory('today');
  }

  openFullHistory(preset = 'today'): void {
    const queryParams: Record<string, string | number> = { preset };
    if (this.selectedVehicleId) {
      queryParams['vehicleId'] = this.selectedVehicleId;
    }
    void this.router.navigate(['/gps-tracking/history'], { queryParams });
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
    const intervalMs = this.effectivePollIntervalMs();
    if (intervalMs == null) return;
    this.refreshInterval = setInterval(() => {
      if (this.liveTracking && !this.userInteractionActive) this.loadLocations(true);
    }, intervalMs);
  }

  /** SignalR connected → slow REST sanity poll; disconnected → faster fallback. */
  private effectivePollIntervalMs(): number | null {
    if (this.refreshRateMs == null) return null;
    if (this.connectionState === 'connected') {
      return Math.max(this.refreshRateMs, 60_000);
    }
    return Math.min(this.refreshRateMs, 10_000);
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
    const order: MapTheme[] = ['street', 'satellite', 'dark', 'traffic'];
    const i = order.indexOf(this.mapTheme);
    void this.setMapTheme(order[(i + 1) % order.length]);
  }

  toggleMapThemeMenu(): void {
    this.mapThemeMenuOpen = !this.mapThemeMenuOpen;
  }

  toggleGeofenceLayer(): void {
    this.showGeofences = !this.showGeofences;
    if (!this.map) return;
    if (!this.showGeofences) {
      if (this.geofenceLayer) {
        this.map.removeLayer(this.geofenceLayer);
        clearLayerGroup(this.geofenceLayer);
      }
      return;
    }
    this.gpsService.getGeofences({ isActive: true }).subscribe({
      next: fences => {
        if (!this.geofenceLayer) {
          this.geofenceLayer = L.layerGroup();
        }
        clearLayerGroup(this.geofenceLayer);
        for (const g of fences) {
          const layer = addGeofenceBoundary(this.geofenceLayer!, g, { fillOpacity: 0.08, weight: 2 });
          layer?.bindTooltip(g.name);
        }
        if (!this.map.hasLayer(this.geofenceLayer)) {
          this.map.addLayer(this.geofenceLayer);
        }
      },
      error: () => {
        this.showGeofences = false;
      }
    });
  }

  selectMapTheme(theme: MapTheme): void {
    this.mapThemeMenuOpen = false;
    void this.setMapTheme(theme);
  }

  async setMapTheme(theme: MapTheme): Promise<void> {
    this.mapTheme = theme;
    storeMapTheme(theme);
    if (!this.map) return;
    this.tileFallbackIndex = 0;
    this.tileErrorCount = 0;
    this.mapError = null;
    await this.applyTileLayer(theme);
  }

  private async applyTileLayer(theme: MapTheme): Promise<void> {
    if (!this.map) return;

    this.trafficBasemap.detach();

    if (this.tileLayer) {
      this.tileLayer.off();
      this.map.removeLayer(this.tileLayer);
      this.tileLayer = undefined;
    }

    if (theme === 'traffic') {
      const ok = await this.trafficBasemap.attach(this.map, this.googleMapsLoader);
      if (!ok) {
        this.pushEvent('Traffic map unavailable — showing Street', 'warning', 'traffic');
        this.mapTheme = 'street';
        storeMapTheme('street');
        await this.applyTileLayer('street');
        return;
      }
      this.scheduleMapResize();
      return;
    }

    const stack = MAP_TILE_STACKS[theme];
    const cfg = stack[this.tileFallbackIndex] ?? stack[0];
    if (!cfg) return;

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
        void this.applyTileLayer(theme);
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
      void this.setMapTheme(this.mapTheme);
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
        const size = count < 10 ? 36 : count < 50 ? 42 : 48;
        return L.divIcon({
          html: `<div class="fv-cluster" style="width:${size}px;height:${size}px"><span>${count}</span></div>`,
          className: 'fv-marker-host',
          iconSize: [size, size],
          iconAnchor: [size / 2, size / 2]
        });
      }
    });
    this.map.addLayer(this.markerCluster);
    void this.setMapTheme(this.mapTheme);
    this.observeMapResize();
    this.map.whenReady(() => this.scheduleMapResize());
    this.scheduleMapResize();
    this.mapReady = true;
    if (this.pendingMarkerLocations) {
      this.updateMarkers(this.pendingMarkerLocations);
      this.pendingMarkerLocations = null;
    } else if (this.locations.length) {
      this.updateMarkers(this.mappableLocations(this.filteredLocations));
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
        const gpsLocs = this.mappableLocations(this.filteredLocations);
        this.updateMarkers(gpsLocs);
        this.emitTelemetryEvents(this.mappableLocations(this.locations), prevMoving);
        this.emitSyncSummary(this.mappableLocations(this.locations), manual || !silent);
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

  private createMarkerIcon(status: FleetTrackStatus, bearing = 0, vehicleType?: string | null, ignition?: boolean | null): LeafletTypes.DivIcon {
    const badge =
      status === 'sos' ? 'sos' :
      status === 'offline' || status === 'never_seen' ? 'offline' :
      ignition === true && status !== 'moving' ? 'ignition' :
      status === 'parked' ? 'parked' :
      null;
    return createFleetVehicleDivIcon({
      status,
      heading: bearing,
      vehicleType,
      badge,
      size: 34
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
        this.cancelMarkerAnim(vehicleId);
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
      let bearing =
        loc.heading != null && Number.isFinite(loc.heading)
          ? loc.heading
          : 0;
      if ((!bearing || bearing === 0) && prev) {
        bearing = this.bearingFrom(prev, loc.latitude, loc.longitude);
      }
      this.prevPositions.set(loc.vehicleId, { lat: loc.latitude, lng: loc.longitude });
      this.updateTrail(loc);

      const headingText = this.headingLabel(loc);
      const addr =
        loc.address?.trim() ||
        `${loc.latitude.toFixed(5)}, ${loc.longitude.toFixed(5)}`;
      const popupContent = buildFleetVehiclePopup({
        name: loc.vehicleName,
        plate: loc.registrationNumber,
        driver: loc.driverName,
        tracker: loc.trackerName ?? loc.imei,
        ignition: loc.ignition,
        speedKmh: loc.speed,
        headingLabel: headingText || null,
        address: addr,
        mapsUrl: this.googleMapsUrl(loc),
        lastPing: this.formatLastPing(loc),
        statusLabel: this.statusLabel(loc.status)
      }) + `<a href="#" class="map-popup-link" data-vid="${loc.vehicleId}">View details →</a>`;
      const icon = this.createMarkerIcon(loc.status, bearing, loc.vehicleType, loc.ignition);

      if (this.markers.has(loc.vehicleId)) {
        const m = this.markers.get(loc.vehicleId)!;
        this.animateMarkerTo(loc.vehicleId, m, loc.latitude, loc.longitude);
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

  private escapeHtml(value: string): string {
    return value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  private cancelMarkerAnim(vehicleId: number): void {
    const frame = this.markerAnimFrames.get(vehicleId);
    if (frame != null) {
      cancelAnimationFrame(frame);
      this.markerAnimFrames.delete(vehicleId);
    }
  }

  private animateMarkerTo(
    vehicleId: number,
    marker: LeafletTypes.Marker,
    lat: number,
    lng: number
  ): void {
    const from = marker.getLatLng();
    const distKm = this.haversineKm(from.lat, from.lng, lat, lng);
    this.cancelMarkerAnim(vehicleId);

    if (distKm <= 0.0005 || distKm > this.maxAnimateKm) {
      marker.setLatLng([lat, lng]);
      return;
    }

    const start = performance.now();
    const duration = this.markerAnimMs;
    const step = (now: number) => {
      const t = Math.min(1, (now - start) / duration);
      const eased = t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2;
      marker.setLatLng([
        from.lat + (lat - from.lat) * eased,
        from.lng + (lng - from.lng) * eased
      ]);
      if (t < 1) {
        this.markerAnimFrames.set(vehicleId, requestAnimationFrame(step));
      } else {
        this.markerAnimFrames.delete(vehicleId);
        marker.setLatLng([lat, lng]);
      }
    };
    this.markerAnimFrames.set(vehicleId, requestAnimationFrame(step));
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
        address: update.address?.trim() || this.locations[idx].address,
        alarmType: update.alarmType,
        temperature: update.temperature,
        routeHint: this.realtimeRouteHint(status, speed)
      };
      this.updateMarkers(this.mappableLocations(this.filteredLocations));
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
