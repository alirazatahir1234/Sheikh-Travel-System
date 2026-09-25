import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  OnInit,
  OnDestroy,
  AfterViewInit,
  ElementRef,
  ViewChild,
  HostListener,
  NgZone
} from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { ChartData } from 'chart.js';
import { UiChartOptions } from '../../../shared/components/ui/chart/ui-chart.component';
import { environment } from '../../../../environments/environment';
import {
  MAP_THEME_OPTIONS,
  MapTheme,
  readStoredMapTheme,
  storeMapTheme,
  applyGmapTheme,
  triggerGmapResize,
  type GmapThemeHandle
} from '../../../core/google-maps/gmap-theme';
import { createFleetMarkerClusterer, type FleetMarkerClusterer } from '../../../core/google-maps/gmap-cluster';
import {
  createFleetVehicleMarkerElement,
  buildFleetVehiclePopup
} from '../../../core/google-maps/fleet-vehicle-marker.gmap';
import {
  addGmapGeofenceBoundary,
  clearGmapGeofences,
  createGmapGeofenceLayer,
  type GmapGeofenceLayerHandle
} from '../../../core/google-maps/gmap-geofence-layer';
import { buildStreetViewStaticUrl } from '../../../core/google-maps/gmap-static-urls';
import { GoogleMapsLoaderService } from '../../../core/services/google-maps-loader.service';
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
  NearbyPlace,
  TraccarStatusDto,
  FleetVehicleHealth,
  FleetHealthSummary
} from '../../../core/models/gps-tracking.model';
import { VehicleListItem } from '../../../core/models/vehicle.model';
import {
  MOVING_THRESHOLD_KMH,
  resolveFleetStatus,
  tallyFleetStatusCounts
} from '../../../core/utils/gps-status.util';
import { parseGpsTimestamp } from '../../../core/utils/gps-timestamp.util';
import {
  mergeVehicleLocationsPreservingIdentity,
  preferRicherAddress
} from './live-map-state.util';
import {
  dualStatusLine,
  connectivityLabel,
  operationalLabel
} from '../utils/fleet-status-labels.util';
import {
  LiveKpiKey,
  LiveKpiTile
} from './fleet-kpi-strip/fleet-kpi-strip.component';
import { DetailTab } from './vehicle-detail-panel/vehicle-detail-panel.component';
import {
  breakdownFromSummary,
  fleetHealthChartData,
  aggregateHealthReasons,
  vehicleHealthById,
  FleetHealthBreakdown,
  FleetHealthReasonCount
} from '../utils/fleet-health.util';
import { isTraccarReachable } from '../utils/tracker-status.util';
import {
  buildLocalityLine,
  formatResolvedAddress,
  isCoarseAddress,
  shortAddressLine,
  splitDisplayAddress
} from '../utils/gps-address.util';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';

type StatusFilter = 'all' | 'online' | FleetTrackStatus;
type IgnitionFilter = 'all' | 'on' | 'off';
type BatteryFilter = 'all' | 'low';
type PrimaryStatusFilter = 'all' | 'moving' | 'idle' | 'parked' | 'offline' | 'never_seen';
type MoreStatusFilter = 'none' | 'sos' | 'delayed' | 'scheduled';
type RefreshRateMs = 5000 | 10000 | 120000 | null;

type FleetCounts = {
  total: number;
  online: number;
  offline: number;
  moving: number;
  idle: number;
  parked: number;
  neverSeen: number;
  sos: number;
  unknown: number;
};

const EMPTY_FLEET_COUNTS: FleetCounts = {
  total: 0,
  online: 0,
  offline: 0,
  moving: 0,
  idle: 0,
  parked: 0,
  neverSeen: 0,
  sos: 0,
  unknown: 0
};

const VALID_STATUS_FILTERS = new Set<StatusFilter>([
  'all',
  'online',
  'moving',
  'idle',
  'parked',
  'offline',
  'never_seen',
  'sos',
  'delayed',
  'scheduled'
]);

const PRIMARY_STATUS_FILTERS = new Set<PrimaryStatusFilter>([
  'all',
  'moving',
  'idle',
  'parked',
  'offline',
  'never_seen'
]);

const MORE_STATUS_FILTERS = new Set<Exclude<MoreStatusFilter, 'none'>>([
  'sos',
  'delayed',
  'scheduled'
]);

interface TrackEvent {
  time: Date;
  message: string;
  type: 'info' | 'alert' | 'success' | 'warning';
  icon: string;
}

const TRAIL_COLORS: Record<FleetTrackStatus, string> = {
  moving: '#2563EB',
  idle: '#F59E0B',
  parked: '#0D9488',
  unknown: '#F59E0B',
  delayed: '#EF4444',
  offline: '#EF4444',
  never_seen: '#94A3B8',
  sos: '#DC2626',
  scheduled: '#3B82F6'
};

@Component({
  standalone: false,
  selector: 'app-live-map',
  templateUrl: './live-map.component.html',
  styleUrls: ['./live-map.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LiveMapComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('mapHost') mapHost?: ElementRef<HTMLElement>;
  @ViewChild('mapContainer', { static: false }) mapContainer?: ElementRef<HTMLElement>;
  @ViewChild('vehicleSearchInput') vehicleSearchInput?: ElementRef<HTMLInputElement>;

  private map: google.maps.Map | null = null;
  private themeHandle: GmapThemeHandle | null = null;
  private mapResizeObserver?: ResizeObserver;
  private markerCluster: FleetMarkerClusterer | null = null;
  private markers = new Map<number, google.maps.marker.AdvancedMarkerElement>();
  private markerClickListeners = new Map<number, google.maps.MapsEventListener>();
  private markerAnimFrames = new Map<number, number>();
  private trailLayers = new Map<number, google.maps.Polyline>();
  private popupHtml = new Map<number, string>();
  private infoWindow: google.maps.InfoWindow | null = null;
  private geofenceHandle: GmapGeofenceLayerHandle = createGmapGeofenceLayer();
  private prevPositions = new Map<number, { lat: number; lng: number }>();
  private positionTrails = new Map<number, google.maps.LatLngLiteral[]>();
  /** Last marker visual signature — skip content rebuild when only position changed. */
  private markerVisualSig = new Map<number, string>();
  /** Vehicle IDs where Street View Static returned no coverage (HTTP error). */
  private streetViewUnavailable = new Set<number>();
  /** Cached Street View URL keyed by vehicleId + rounded coords. */
  private streetViewUrlCache = new Map<number, { key: string; url: string | null }>();
  private refreshInterval?: ReturnType<typeof setInterval>;
  private authFailSub?: { unsubscribe(): void };
  private readonly maxTrailPoints = 14;
  private readonly maxAnimateKm = 2;
  private readonly markerAnimMs = 500;

  locations: VehicleLocation[] = [];
  /** Filtered list cache — stable identity across GPS ticks when membership unchanged. */
  filteredLocationsCache: VehicleLocation[] = [];
  fleetCountsCache: FleetCounts = { ...EMPTY_FLEET_COUNTS };
  liveKpiTilesCache: LiveKpiTile[] = [];
  /** Selected row snapshot for the detail panel (updated only for that vehicle). */
  selectedLocation: VehicleLocation | null = null;
  emptyStateKind: 'loading' | 'no-data' | 'no-match' | null = 'loading';
  loading = true;
  /** Manual refresh UI — not set by silent auto-poll. */
  refreshing = false;
  syncError: string | null = null;
  mapError: string | null = null;
  searchQuery = '';
  statusFilter: StatusFilter = 'all';
  ignitionFilter: IgnitionFilter = 'all';
  batteryLowOnly = false;
  panelCollapsed = false;
  filterApplying = false;
  /** Vehicle id currently waiting on reverse-geocode for the detail panel. */
  addressResolvingVehicleId: number | null = null;
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
    { id: null, label: 'Realtime (SignalR)' },
    { id: 120000, label: '2 min REST sanity' },
    { id: 10000, label: '10 sec (force poll)' },
    { id: 5000, label: '5 sec (force poll)' }
  ];
  /** null = SignalR-only while connected; poll only when hub is down (or force/sanity selected). */
  refreshRateMs: RefreshRateMs = null;
  followSelected = false;
  connectionState: GpsConnectionState = 'disconnected';
  detailTab: DetailTab = 'overview';
  /** Mobile in-page bottom nav: map | vehicles | nearby | commands */
  mobileNav: 'map' | 'vehicles' | 'nearby' | 'commands' = 'map';
  private connectionStateSub?: { unsubscribe(): void };
  private sosSub?: { unsubscribe(): void };
  private readonly BATTERY_LOW_THRESHOLD = 20;

  vehicles: VehicleListItem[] = [];
  events: TrackEvent[] = [];

  readonly primaryStatusOptions: { id: PrimaryStatusFilter; label: string }[] = [
    { id: 'all', label: 'All Status' },
    { id: 'moving', label: 'Moving' },
    { id: 'idle', label: 'Idle' },
    { id: 'parked', label: 'Parked' },
    { id: 'offline', label: 'Offline' },
    { id: 'never_seen', label: 'Never Seen' }
  ];

  readonly moreStatusOptions: { id: MoreStatusFilter; label: string }[] = [
    { id: 'none', label: 'More Filters' },
    { id: 'sos', label: 'SOS' },
    { id: 'delayed', label: 'Delayed' },
    { id: 'scheduled', label: 'Scheduled' }
  ];

  readonly ignitionOptions: { id: IgnitionFilter; label: string }[] = [
    { id: 'all', label: 'All Ignition' },
    { id: 'on', label: 'Ignition ON' },
    { id: 'off', label: 'Ignition OFF' }
  ];

  readonly batteryOptions: { id: BatteryFilter; label: string }[] = [
    { id: 'all', label: 'All Battery' },
    { id: 'low', label: 'Battery Low' }
  ];

  geofenceBreachCount = 0;
  showGeofences = false;
  selectedEta: GpsEta | null = null;

  /** Places API (New) nearby amenities — map toolbar panel. */
  readonly nearbyCategories: { id: string; label: string; icon: string }[] = [
    { id: 'fuel', label: 'Petrol', icon: 'local_gas_station' },
    { id: 'restaurant', label: 'Food', icon: 'restaurant' },
    { id: 'hospital', label: 'Hospital', icon: 'local_hospital' },
    { id: 'parking', label: 'Parking', icon: 'local_parking' },
    { id: 'workshop', label: 'Workshop', icon: 'car_repair' },
    { id: 'hotel', label: 'Hotel', icon: 'hotel' },
    { id: 'atm', label: 'ATM', icon: 'local_atm' },
    { id: 'airport', label: 'Airport', icon: 'flight' },
    { id: 'more', label: 'More', icon: 'more_horiz' }
  ];
  nearbyCategory = 'fuel';
  nearbyRadiusMeters = 1500;
  nearbyPlaces: NearbyPlace[] = [];
  nearbyLoading = false;
  nearbyError: string | null = null;
  nearbyExpanded = false;
  nearbySearchCenterLabel = 'Map center';
  selectedNearbyPlace: NearbyPlace | null = null;
  private nearbyMarkers: google.maps.marker.AdvancedMarkerElement[] = [];
  private nearbyMarkerListeners: google.maps.MapsEventListener[] = [];
  private nearbyMarkerByPlaceId = new Map<string, google.maps.marker.AdvancedMarkerElement>();

  fleetStatusLocal: GpsFleetStatusLocal | null = null;
  fleetStatusHistory: GpsFleetStatusSnapshot[] = [];
  fleetOverviewRangeDays: 7 | 30 | number = 7;
  readonly fleetOverviewRangeOptions: { days: number; label: string }[] = [
    { days: 7, label: '7 Days' },
    { days: 30, label: '30 Days' },
    { days: -1, label: 'This Month' }
  ];

  private realtimeSub?: { unsubscribe(): void };
  private mapReady = false;
  private pendingMarkerLocations: VehicleLocation[] | null = null;
  private lastSyncSummaryKey = '';
  private _bootstrapping = false;
  private _bootstrapTimer?: ReturnType<typeof setTimeout>;

  /** Coord key for which a reverse-geocode was last requested; avoids re-fetching on small drift. */
  private lastEnrichedCoordKey = new Map<number, string>();

  private pendingFocusVehicleId: number | null = null;
  private isRefreshingLocations = false;
  /** When true, run another manual load after the in-flight request finishes. */
  private queuedManualRefresh = false;
  private refreshUiStartedAt = 0;
  private refreshUiClearTimer?: ReturnType<typeof setTimeout>;
  private static readonly REFRESH_UI_MIN_MS = 500;
  private userInteractionActive = false;
  private interactionPauseTimer?: ReturnType<typeof setTimeout>;

  constructor(
    private gpsService: GpsTrackingService,
    private realtime: GpsRealtimeService,
    private vehicleService: VehicleService,
    private driverService: DriverService,
    private router: Router,
    private route: ActivatedRoute,
    private googleMapsLoader: GoogleMapsLoaderService,
    private ngZone: NgZone,
    private cdr: ChangeDetectorRef
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
    void this.realtime.connect({ asDispatcher: true }).catch(() => {
      this.pushEvent('Realtime unavailable — using polling', 'warning', 'wifi_off');
    });
    // Hub events emit outside NgZone; mutate state outside, markForCheck only on real change.
    this.realtimeSub = this.realtime.locationUpdates$.subscribe(update => {
      if (this.applyRealtimeUpdate(update)) {
        this.ngZone.run(() => this.cdr.markForCheck());
      }
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
      this.cdr.markForCheck();
    });
    this.sosSub = this.realtime.sosAlerts$.subscribe(alert => {
      this.ngZone.run(() => {
        const idx = this.locations.findIndex(l => l.vehicleId === alert.vehicleId);
        if (idx >= 0) {
          this.locations[idx] = { ...this.locations[idx], status: 'sos', alarmType: 'sos' };
          this.patchFilteredCacheForVehicle(this.locations[idx], true);
          this.rebuildFleetCountsAndKpis();
          this.syncSelectedLocation();
          this.updateMarkers([this.locations[idx]], { resize: false, pruneMissing: false });
          this.pushEvent(`${this.locations[idx].vehicleName} — SOS / panic alarm!`, 'alert', 'sos');
        } else {
          this.pushEvent(`Vehicle #${alert.vehicleId} — SOS / panic alarm!`, 'alert', 'sos');
        }
        this.cdr.markForCheck();
      });
    });
    this.syncTick = setInterval(() => {
      // 5s tick — avoid re-rendering list/detail "Ns ago" every second.
      this.clockMs = Date.now();
      if (this.lastSyncAt) {
        this.secondsSinceSync = Math.floor((this.clockMs - this.lastSyncAt.getTime()) / 1000);
      }
      this.cdr.markForCheck();
    }, 5000);
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
      await this.waitForMapContainer();
      if (this.map) {
        void this.setMapTheme(this.mapTheme);
        this.scheduleMapResize();
        return;
      }
      await this.initMap();
      if (!this.map) return;
      this.loadLocations();
      this.startAutoRefresh();
    } catch (err) {
      console.error('[LiveMap] Map bootstrap failed:', err);
      this.mapError =
        this.googleMapsLoader.failureMessage ||
        'Map could not be initialized. Refresh the page or tap Retry map.';
      this.cdr.markForCheck();
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
    if (this.refreshUiClearTimer) clearTimeout(this.refreshUiClearTimer);
    this.markerAnimFrames.forEach(id => cancelAnimationFrame(id));
    this.markerAnimFrames.clear();
    this.clearNearbyMarkers();
    this.authFailSub?.unsubscribe();
    this.mapResizeObserver?.disconnect();
    this.realtimeSub?.unsubscribe();
    this.connectionStateSub?.unsubscribe();
    this.sosSub?.unsubscribe();
    void this.realtime.releaseDispatcher();
    this.teardownMap();
  }

  private teardownMap(): void {
    this.markerClickListeners.forEach(l => l.remove());
    this.markerClickListeners.clear();
    this.infoWindow?.close();
    this.infoWindow = null;
    this.markers.forEach(m => {
      m.map = null;
    });
    this.markers.clear();
    this.trailLayers.forEach(line => line.setMap(null));
    this.trailLayers.clear();
    this.popupHtml.clear();
    this.positionTrails.clear();
    this.prevPositions.clear();
    this.markerVisualSig.clear();
    clearGmapGeofences(this.geofenceHandle);
    this.themeHandle?.trafficLayer?.setMap(null);
    this.themeHandle = null;
    this.markerCluster?.clearMarkers();
    this.markerCluster = null;
    this.map = null;
    this.mapReady = false;
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.mapThemeMenuOpen) return;
    const target = event.target as HTMLElement | null;
    if (target?.closest('.map-theme-control')) return;
    this.mapThemeMenuOpen = false;
    this.cdr.markForCheck();
  }

  @HostListener('document:fullscreenchange')
  onFullscreenChange(): void {
    this.isMapFullscreen = !!document.fullscreenElement;
    this.cdr.markForCheck();
    setTimeout(() => this.scheduleMapResize(), 200);
  }

  get filteredLocations(): VehicleLocation[] {
    return this.filteredLocationsCache;
  }

  trackByVehicleId = (_: number, loc: VehicleLocation): number => loc.vehicleId;

  trackByNearbyPlace = (_: number, place: NearbyPlace): string => place.placeId;

  /** Status select value — primary statuses only; More Filters / Online leave this at All. */
  get primaryStatusSelect(): PrimaryStatusFilter {
    return PRIMARY_STATUS_FILTERS.has(this.statusFilter as PrimaryStatusFilter)
      ? (this.statusFilter as PrimaryStatusFilter)
      : 'all';
  }

  /** More Filters select value for SOS / Delayed / Scheduled. */
  get moreStatusSelect(): MoreStatusFilter {
    return MORE_STATUS_FILTERS.has(this.statusFilter as Exclude<MoreStatusFilter, 'none'>)
      ? (this.statusFilter as Exclude<MoreStatusFilter, 'none'>)
      : 'none';
  }

  get batterySelect(): BatteryFilter {
    return this.batteryLowOnly ? 'low' : 'all';
  }

  statusLabelForFilter(id: StatusFilter): string {
    switch (id) {
      case 'all':
        return 'All';
      case 'online':
        return 'Online';
      case 'never_seen':
        return 'Never Seen';
      case 'sos':
        return 'SOS';
      default:
        return id.charAt(0).toUpperCase() + id.slice(1);
    }
  }

  isKpiStatusSelected(id: StatusFilter): boolean {
    return this.statusFilter === id;
  }

  private matchesStatusFilter(status: FleetTrackStatus): boolean {
    if (this.statusFilter === 'all') return true;
    if (this.statusFilter === 'online') {
      return (
        status === 'moving' ||
        status === 'idle' ||
        status === 'parked' ||
        status === 'sos' ||
        status === 'unknown'
      );
    }
    return status === this.statusFilter;
  }

  get hasActiveFilters(): boolean {
    return (
      this.searchQuery.trim().length > 0 ||
      this.statusFilter !== 'all' ||
      this.ignitionFilter !== 'all' ||
      this.batteryLowOnly
    );
  }

  private matchesVehicleFilters(loc: VehicleLocation): boolean {
    const q = this.searchQuery.trim().toLowerCase();
    if (!this.matchesStatusFilter(loc.status)) return false;
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
  }

  private rebuildFilteredLocations(): void {
    this.filteredLocationsCache = this.locations.filter(loc => this.matchesVehicleFilters(loc));
    this.updateEmptyStateKind();
  }

  /** Patch one row in the filtered cache, or rebuild if filter membership changed. */
  private patchFilteredCacheForVehicle(loc: VehicleLocation, statusMayAffectFilter: boolean): void {
    const matches = this.matchesVehicleFilters(loc);
    const idx = this.filteredLocationsCache.findIndex(l => l.vehicleId === loc.vehicleId);
    if (matches && idx >= 0) {
      this.filteredLocationsCache[idx] = loc;
      return;
    }
    if (!matches && idx < 0 && !statusMayAffectFilter) {
      return;
    }
    // Membership of the filtered list changed — rebuild once.
    this.rebuildFilteredLocations();
  }

  private syncSelectedLocation(): void {
    if (this.selectedVehicleId == null) {
      this.selectedLocation = null;
      return;
    }
    this.selectedLocation =
      this.locations.find(l => l.vehicleId === this.selectedVehicleId) ?? null;
  }

  private updateEmptyStateKind(): void {
    if (this.loading && this.locations.length === 0) {
      this.emptyStateKind = 'loading';
    } else if (this.locations.length === 0) {
      this.emptyStateKind = 'no-data';
    } else if (this.filteredLocationsCache.length === 0) {
      this.emptyStateKind = 'no-match';
    } else {
      this.emptyStateKind = null;
    }
  }

  private rebuildFleetCountsAndKpis(forceNewTileArray = false): void {
    const tallied = tallyFleetStatusCounts(this.locations);
    const next: FleetCounts = {
      total: tallied.total,
      online: tallied.online,
      offline: tallied.offline,
      moving: tallied.moving,
      idle: tallied.idle,
      parked: tallied.parked,
      neverSeen: tallied.neverSeen,
      sos: tallied.sos,
      unknown: tallied.unknown
    };

    const countsChanged =
      forceNewTileArray ||
      this.fleetCountsCache.total !== next.total ||
      this.fleetCountsCache.online !== next.online ||
      this.fleetCountsCache.offline !== next.offline ||
      this.fleetCountsCache.moving !== next.moving ||
      this.fleetCountsCache.idle !== next.idle ||
      this.fleetCountsCache.parked !== next.parked ||
      this.fleetCountsCache.neverSeen !== next.neverSeen ||
      this.fleetCountsCache.sos !== next.sos ||
      this.fleetCountsCache.unknown !== next.unknown;

    this.fleetCountsCache = next;
    if (countsChanged || this.liveKpiTilesCache.length === 0) {
      this.rebuildLiveKpiTiles();
    } else {
      // Selection highlight may have changed without counts.
      this.refreshKpiSelectionFlags();
    }
  }

  private refreshKpiSelectionFlags(): void {
    let changed = false;
    const tiles = this.liveKpiTilesCache;
    for (const tile of tiles) {
      const selected =
        tile.key === 'alerts'
          ? false
          : tile.key === 'total'
            ? this.isKpiStatusSelected('all')
            : tile.key === 'never_seen'
              ? this.isKpiStatusSelected('never_seen')
              : this.isKpiStatusSelected(tile.key as StatusFilter);
      if (tile.selected !== selected) {
        tile.selected = selected;
        changed = true;
      }
    }
    if (changed) {
      // New array ref so OnPush KPI strip picks up selection toggle.
      this.liveKpiTilesCache = tiles.map(t => ({ ...t }));
    }
  }

  private rebuildLiveKpiTiles(): void {
    const c = this.fleetCountsCache;
    const total = Math.max(c.total, 1);
    const pct = (n: number) => `${Math.round((n / total) * 100)}%`;
    const spark = (key: string) => this.kpiTiles[key]?.sparkline;
    const trend = (key: string) => this.kpiTiles[key]?.trend;
    const trendUp = (key: string) => this.kpiTiles[key]?.trendUp;
    const next: LiveKpiTile[] = [
      {
        key: 'total',
        label: 'Total Vehicles',
        value: c.total,
        icon: 'directions_car',
        color: 'teal',
        hint: 'All vehicles',
        selected: this.isKpiStatusSelected('all')
      },
      {
        key: 'online',
        label: 'Online',
        value: c.online,
        icon: 'wifi_tethering',
        color: 'sky',
        hint: pct(c.online),
        trend: trend('online'),
        trendUp: trendUp('online'),
        sparkline: spark('online'),
        selected: this.isKpiStatusSelected('online')
      },
      {
        key: 'moving',
        label: 'Moving',
        value: c.moving,
        icon: 'near_me',
        color: 'blue',
        hint: pct(c.moving),
        trend: trend('moving'),
        trendUp: trendUp('moving'),
        sparkline: spark('moving'),
        selected: this.isKpiStatusSelected('moving')
      },
      {
        key: 'parked',
        label: 'Parked',
        value: c.parked,
        icon: 'local_parking',
        color: 'green',
        hint: pct(c.parked),
        trend: trend('parked'),
        trendUp: trendUp('parked'),
        sparkline: spark('parked'),
        selected: this.isKpiStatusSelected('parked')
      },
      {
        key: 'idle',
        label: 'Idle',
        value: c.idle,
        icon: 'pause_circle',
        color: 'amber',
        hint: pct(c.idle),
        trend: trend('idle'),
        trendUp: trendUp('idle'),
        sparkline: spark('idle'),
        selected: this.isKpiStatusSelected('idle')
      },
      {
        key: 'offline',
        label: 'Offline',
        value: c.offline,
        icon: 'cloud_off',
        color: 'rose',
        hint: pct(c.offline),
        trend: trend('offline'),
        trendUp: trendUp('offline'),
        sparkline: spark('offline'),
        selected: this.isKpiStatusSelected('offline')
      },
      {
        key: 'never_seen',
        label: 'Never Seen',
        value: c.neverSeen,
        icon: 'help_outline',
        color: 'purple',
        hint: pct(c.neverSeen),
        trend: trend('neverSeen'),
        trendUp: trendUp('neverSeen'),
        sparkline: spark('neverSeen'),
        selected: this.isKpiStatusSelected('never_seen')
      },
      {
        key: 'alerts',
        label: 'Alerts Today',
        value: this.fleetStatusLocal?.alertsToday ?? 0,
        icon: 'notifications_active',
        color: 'orange',
        hint: 'Open alerts',
        trend: trend('alertsToday'),
        trendUp: trendUp('alertsToday'),
        sparkline: spark('alertsToday')
      }
    ];

    // Reuse tile object identity when values unchanged (OnPush KPI strip).
    if (this.liveKpiTilesCache.length === next.length) {
      const reused: LiveKpiTile[] = [];
      let anyNew = false;
      for (let i = 0; i < next.length; i++) {
        const prev = this.liveKpiTilesCache[i];
        const n = next[i];
        if (
          prev &&
          prev.key === n.key &&
          prev.value === n.value &&
          prev.hint === n.hint &&
          prev.selected === n.selected &&
          prev.trend === n.trend &&
          prev.trendUp === n.trendUp &&
          prev.sparkline === n.sparkline
        ) {
          reused.push(prev);
        } else {
          reused.push(n);
          anyNew = true;
        }
      }
      this.liveKpiTilesCache = anyNew ? reused : this.liveKpiTilesCache;
    } else {
      this.liveKpiTilesCache = next;
    }
  }

  private rebuildListDerivedState(opts: { rebuildFilter?: boolean; forceKpis?: boolean } = {}): void {
    if (opts.rebuildFilter !== false) {
      this.rebuildFilteredLocations();
    } else {
      this.updateEmptyStateKind();
    }
    this.rebuildFleetCountsAndKpis(opts.forceKpis === true);
    this.syncSelectedLocation();
  }

  setIgnitionFilter(id: IgnitionFilter): void {
    this.ignitionFilter = id;
    this.persistFilters();
    this.markUserActive();
    this.applyVisibleMarkers();
    this.cdr.markForCheck();
  }

  setBatteryFilter(id: BatteryFilter): void {
    this.batteryLowOnly = id === 'low';
    this.persistFilters();
    this.markUserActive();
    this.applyVisibleMarkers();
    this.cdr.markForCheck();
  }

  onPrimaryStatusSelect(id: PrimaryStatusFilter): void {
    this.setStatusFilter(id);
  }

  onMoreStatusSelect(id: MoreStatusFilter): void {
    this.setStatusFilter(id === 'none' ? 'all' : id);
  }

  onKpiStatusClick(id: StatusFilter): void {
    if (this.statusFilter === id) {
      this.setStatusFilter('all');
      return;
    }
    this.setStatusFilter(id);
  }

  onTotalFleetKpiClick(): void {
    this.setStatusFilter('all');
  }

  goToAlerts(): void {
    this.router.navigate(['../alerts'], { relativeTo: this.route });
  }

  get statusCounts(): Record<FleetTrackStatus | 'all', number> {
    const c = this.fleetCountsCache;
    return {
      all: c.total,
      moving: c.moving,
      idle: c.idle,
      parked: c.parked,
      unknown: c.unknown,
      offline: c.offline,
      never_seen: c.neverSeen,
      sos: c.sos,
      delayed: this.locations.filter(l => l.status === 'delayed').length,
      scheduled: this.locations.filter(l => l.status === 'scheduled').length
    };
  }

  /** Online/Offline/Moving/Idle/Parked/Never-Seen counts for the top stat row. */
  get fleetCounts(): FleetCounts {
    return this.fleetCountsCache;
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
    return dualStatusLine(status);
  }

  operationalStatusLabel(status: FleetTrackStatus): string {
    return operationalLabel(status);
  }

  connectivityStatusLabel(status: FleetTrackStatus): string {
    return connectivityLabel(status);
  }

  /** Compact badge for detail header (operational / connectivity). */
  statusBadgeLabel(status: FleetTrackStatus): string {
    const conn = connectivityLabel(status);
    if (conn !== 'Online') return conn;
    return operationalLabel(status);
  }

  statusIcon(status: FleetTrackStatus): string {
    const icons: Record<FleetTrackStatus, string> = {
      moving: 'directions_car',
      idle: 'pause_circle',
      parked: 'local_parking',
      unknown: 'help_outline',
      offline: 'cloud_off',
      never_seen: 'help_outline',
      sos: 'sos',
      scheduled: 'schedule',
      delayed: 'warning'
    };
    return icons[status];
  }

  /** KPI strip — cached; rebuilt only when fleet counts / selection change. */
  get liveKpiTiles(): LiveKpiTile[] {
    return this.liveKpiTilesCache;
  }

  onKpiStripClick(key: LiveKpiKey): void {
    switch (key) {
      case 'total':
        this.onTotalFleetKpiClick();
        break;
      case 'alerts':
        this.goToAlerts();
        break;
      case 'never_seen':
        this.onKpiStatusClick('never_seen');
        break;
      default:
        this.onKpiStatusClick(key);
        break;
    }
  }

  setDetailTab(tab: DetailTab): void {
    this.detailTab = tab;
    this.cdr.markForCheck();
  }

  setMobileNav(tab: 'map' | 'vehicles' | 'nearby' | 'commands'): void {
    this.mobileNav = tab;
    if (tab === 'vehicles') {
      this.listSheetOpen = true;
      this.panelCollapsed = false;
    } else if (tab === 'nearby') {
      if (!this.nearbyExpanded) this.toggleNearbyPlaces();
    } else if (tab === 'commands') {
      this.goToCommands();
    } else {
      this.listSheetOpen = false;
    }
    this.cdr.markForCheck();
  }

  /** Quick chip filters for vehicle list (reference design). */
  readonly listStatusChips: { id: StatusFilter; label: string }[] = [
    { id: 'all', label: 'All' },
    { id: 'online', label: 'Online' },
    { id: 'moving', label: 'Moving' },
    { id: 'parked', label: 'Parked' },
    { id: 'idle', label: 'Idle' },
    { id: 'offline', label: 'Offline' }
  ];

  chipCount(id: StatusFilter): number {
    const c = this.fleetCounts;
    switch (id) {
      case 'all':
        return c.total;
      case 'online':
        return c.online;
      case 'moving':
        return c.moving;
      case 'parked':
        return c.parked;
      case 'idle':
        return c.idle;
      case 'offline':
        return c.offline;
      default:
        return 0;
    }
  }

  selectListChip(id: StatusFilter): void {
    if (id === 'all') {
      this.onTotalFleetKpiClick();
      return;
    }
    this.onKpiStatusClick(id);
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

  /**
   * Popup / list GPS line from position freshness — never "No live GPS" when a marker is shown.
   * Live: update within 2 minutes. Available: valid coords but older. No data: no valid position.
   */
  formatLastPing(loc: VehicleLocation): string {
    if (!this.isValidCoord(loc.latitude, loc.longitude)) {
      return 'No GPS data';
    }

    if (!loc.lastUpdated) {
      return 'GPS position available';
    }

    let sec = Math.floor((this.clockMs - parseGpsTimestamp(loc.lastUpdated)) / 1000);
    if (!Number.isFinite(sec)) {
      return 'GPS position available';
    }
    // Fresh SignalR can land slightly ahead of the 1s clock tick — treat as just updated.
    if (sec < 0) sec = 0;

    const age =
      sec < 60
        ? `${sec}s ago`
        : (() => {
            const min = Math.floor(sec / 60);
            if (min < 60) return `${min}m ago`;
            return `${Math.floor(min / 60)}h ago`;
          })();

    // Align with "recent" UX: ~2 minutes is Live; older last-known still has position.
    if (sec <= 120) {
      return `Live GPS · Last update: ${age}`;
    }
    return `GPS position available · Last update: ${age}`;
  }

  /**
   * Display speed for list/popup. Ignition OFF / Parked: hide GPS drift (1–9 km/h) as movement.
   */
  speedLabel(loc: VehicleLocation): string {
    if (!this.isValidCoord(loc.latitude, loc.longitude) && !loc.hasGps) return '—';
    const display = this.displaySpeedKmh(loc);
    if (display > 0) return `${Math.round(display)} km/h`;
    if (loc.status === 'idle') return '0 km/h · idle';
    if (loc.status === 'parked' || loc.ignition === false) return '0 km/h · stationary';
    return 'Stationary';
  }

  /** Raw telemetry speed with ignition-OFF drift zeroed for UI. */
  displaySpeedKmh(loc: VehicleLocation): number {
    const speed = Number(loc.speed) || 0;
    if (loc.ignition === false || loc.status === 'parked') {
      // Matches resolveFleetStatus: under MOVING_THRESHOLD with ACC off is GPS noise.
      return speed >= MOVING_THRESHOLD_KMH ? speed : 0;
    }
    return speed;
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
    return shortAddressLine(loc.address);
  }

  /** Primary street / locality line for the Current Location card (not Nearby POI). */
  locationPrimaryLine(loc: VehicleLocation): string | null {
    const fromAddress = splitDisplayAddress(loc.address).primary;
    if (fromAddress) return fromAddress;
    // Locality-only geocode (tehsil/city) — still show a place name, not lat/lng.
    const short = shortAddressLine(loc.address);
    if (short) return short;
    const locality = loc.addressLocality?.trim();
    if (locality) return locality;
    return null;
  }

  /** Locality under the primary address plus nearest place/shop when available. */
  locationSecondaryLine(loc: VehicleLocation): string | null {
    const place = loc.placeName?.trim();
    const locality = loc.addressLocality?.trim();
    const primary = this.locationPrimaryLine(loc)?.toLowerCase() ?? '';
    const near =
      place && !primary.includes(place.toLowerCase()) ? `Near: ${place}` : null;

    if (locality) {
      if (!primary.includes(locality.toLowerCase())) {
        return near ? `${near} · ${locality}` : locality;
      }
    }
    const secondary = splitDisplayAddress(loc.address).secondary;
    if (near && secondary && !near.toLowerCase().includes(secondary.toLowerCase())) {
      return `${near} · ${secondary}`;
    }
    return near || secondary;
  }

  isAddressResolving(loc: VehicleLocation): boolean {
    return this.addressResolvingVehicleId === loc.vehicleId && !loc.address?.trim();
  }

  googleMapsUrl(loc: VehicleLocation): string {
    return `https://maps.google.com/?q=${loc.latitude},${loc.longitude}`;
  }

  /** Street View still — round coords so micro GPS drift does not reload the image. */
  streetViewUrl(loc: VehicleLocation): string | null {
    if (this.streetViewUnavailable.has(loc.vehicleId)) return null;
    if (!this.isValidCoord(loc.latitude, loc.longitude)) return null;
    const key = `${loc.latitude.toFixed(4)},${loc.longitude.toFixed(4)}`;
    const cached = this.streetViewUrlCache.get(loc.vehicleId);
    if (cached && cached.key === key) return cached.url;
    const url = buildStreetViewStaticUrl({
      lat: Number(loc.latitude.toFixed(4)),
      lng: Number(loc.longitude.toFixed(4)),
      width: 280,
      height: 140,
      returnErrorCode: true
    });
    this.streetViewUrlCache.set(loc.vehicleId, { key, url });
    return url;
  }

  onStreetViewError(vehicleId: number): void {
    this.streetViewUnavailable.add(vehicleId);
    this.streetViewUrlCache.set(vehicleId, { key: '', url: null });
    this.cdr.markForCheck();
  }

  /** Operational status line for the detail panel (e.g. "Parked · Ignition off"). */
  operationalStatusLine(loc: VehicleLocation): string {
    const status = operationalLabel(loc.status);
    if (loc.ignition === false) return `${status} · Ignition off`;
    if (loc.ignition === true) return `${status} · Ignition on`;
    return status;
  }

  private refreshTraccarStatus(): void {
    this.gpsService.getTraccarStatus().pipe(
      catchError(() => of({ connected: false, serverVersion: null, deviceCount: 0, lastError: 'Unavailable' } as TraccarStatusDto))
    ).subscribe(status => {
      this.traccarStatus = status;
      this.cdr.markForCheck();
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
      this.cdr.markForCheck();
    }, 300);
    this.cdr.markForCheck();
  }

  onSearchEnter(): void {
    if (this.searchDebounceTimer) {
      clearTimeout(this.searchDebounceTimer);
      this.searchDebounceTimer = undefined;
    }
    this.persistFilters();
    this.applyVisibleMarkers();
    this.filterApplying = false;
    const first = this.filteredLocationsCache[0];
    if (first) this.selectVehicle(first);
    this.cdr.markForCheck();
  }

  setStatusFilter(id: StatusFilter): void {
    this.statusFilter = id;
    this.persistFilters();
    this.markUserActive();
    this.applyVisibleMarkers();
    this.cdr.markForCheck();
  }

  clearFilters(): void {
    this.searchQuery = '';
    this.statusFilter = 'all';
    this.ignitionFilter = 'all';
    this.batteryLowOnly = false;
    this.persistFilters();
    this.markUserActive();
    this.applyVisibleMarkers();
    this.cdr.markForCheck();
  }

  togglePanelCollapsed(): void {
    this.panelCollapsed = !this.panelCollapsed;
    this.cdr.markForCheck();
    setTimeout(() => {
      this.scheduleMapResize();
    }, 220);
  }

  private applyVisibleMarkers(): void {
    this.rebuildListDerivedState({ rebuildFilter: true, forceKpis: true });
    this.updateMarkers(this.mappableLocations(this.filteredLocationsCache));
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
      if (parsed.statusFilter && VALID_STATUS_FILTERS.has(parsed.statusFilter)) {
        this.statusFilter = parsed.statusFilter;
      }
      if (parsed.ignitionFilter === 'all' || parsed.ignitionFilter === 'on' || parsed.ignitionFilter === 'off') {
        this.ignitionFilter = parsed.ignitionFilter;
      }
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
    this.beginRefreshUi();
    this.loadLocations(true, true);
    this.loadRecentAlertEvents();
    this.loadFleetStatus();
  }

  zoomIn(): void {
    if (!this.map) return;
    const z = this.map.getZoom() ?? 6;
    this.map.setZoom(z + 1);
  }

  zoomOut(): void {
    if (!this.map) return;
    const z = this.map.getZoom() ?? 6;
    this.map.setZoom(z - 1);
  }

  private beginRefreshUi(): void {
    this.refreshing = true;
    this.refreshUiStartedAt = Date.now();
    if (this.refreshUiClearTimer) {
      clearTimeout(this.refreshUiClearTimer);
      this.refreshUiClearTimer = undefined;
    }
  }

  /** Keep spin/chip visible at least REFRESH_UI_MIN_MS so fast APIs still feel responsive. */
  private endRefreshUi(): void {
    if (!this.refreshing) return;
    const elapsed = Date.now() - this.refreshUiStartedAt;
    const remain = LiveMapComponent.REFRESH_UI_MIN_MS - elapsed;
    if (remain <= 0) {
      this.refreshing = false;
      return;
    }
    if (this.refreshUiClearTimer) clearTimeout(this.refreshUiClearTimer);
    this.refreshUiClearTimer = setTimeout(() => {
      this.refreshing = false;
      this.refreshUiClearTimer = undefined;
    }, remain);
  }

  private loadFleetStatus(): void {
    this.gpsService.getFleetStatusLocal().subscribe({
      next: s => {
        this.fleetStatusLocal = s;
        this.rebuildLiveKpiTiles();
        this.cdr.markForCheck();
      },
      error: () => {}
    });
    this.loadFleetStatusHistory();
    this.loadFleetHealth();
  }

  /** Explainable health from BE — not recomputed from operational status on SignalR ticks. */
  private loadFleetHealth(): void {
    this.gpsService.getFleetHealth().subscribe({
      next: summary => {
        this.applyFleetHealthSummary(summary);
        this.cdr.markForCheck();
      },
      error: () => {
        this.applyFleetHealthSummary(null);
        this.cdr.markForCheck();
      }
    });
  }

  private applyFleetHealthSummary(summary: FleetHealthSummary | null): void {
    this.fleetHealth = breakdownFromSummary(summary);
    this.fleetHealthChartData = fleetHealthChartData(this.fleetHealth);
    this.fleetHealthReasons = aggregateHealthReasons(summary?.vehicles);
    this.fleetHealthByVehicleId = vehicleHealthById(summary?.vehicles);
  }

  /** Selected vehicle health for the detail panel (null when Unknown / missing). */
  get selectedVehicleHealth(): FleetVehicleHealth | null {
    if (this.selectedVehicleId == null) return null;
    return this.fleetHealthByVehicleId.get(this.selectedVehicleId) ?? null;
  }

  private loadFleetStatusHistory(): void {
    const to = new Date();
    let from: Date;
    if (this.fleetOverviewRangeDays === -1) {
      from = new Date(to.getFullYear(), to.getMonth(), 1);
    } else {
      from = new Date(to.getTime() - this.fleetOverviewRangeDays * 24 * 60 * 60 * 1000);
    }
    this.gpsService.getFleetStatusHistory(from, to).subscribe({
      next: rows => {
        this.fleetStatusHistory = rows;
        this.recomputeKpiTiles();
        this.recomputeFleetOverviewChart();
        this.rebuildLiveKpiTiles();
        this.cdr.markForCheck();
      },
      error: () => {
        this.fleetStatusHistory = [];
        this.recomputeFleetOverviewChart();
        this.cdr.markForCheck();
      }
    });
  }

  setFleetOverviewRange(days: number): void {
    this.fleetOverviewRangeDays = days;
    this.loadFleetStatusHistory();
    this.cdr.markForCheck();
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
  fleetHealth: FleetHealthBreakdown = {
    optimal: 0,
    healthy: 0,
    attention: 0,
    critical: 0,
    unknown: 0,
    total: 0,
    assessed: 0,
    assessedPercent: null
  };
  fleetHealthReasons: FleetHealthReasonCount[] = [];
  fleetHealthByVehicleId = new Map<number, FleetVehicleHealth>();
  fleetHealthChartData: ChartData = { labels: [], datasets: [] };
  fleetOverviewChartData: ChartData = { labels: [], datasets: [] };
  fleetOverviewChartOptions: UiChartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: true,
        position: 'bottom',
        labels: { boxWidth: 10, boxHeight: 10, usePointStyle: true, padding: 12 }
      },
      tooltip: {
        mode: 'index',
        intersect: false,
        callbacks: {
          title: (items: { label?: string }[]) => items[0]?.label ? `Fleet status · ${items[0].label}` : 'Fleet status',
          label: (ctx: { dataset: { label?: string }; parsed: { y: number | null } }) =>
            ` ${ctx.dataset.label ?? ''}: ${ctx.parsed.y ?? 0}`
        }
      }
    },
    scales: {
      x: { grid: { display: false }, title: { display: true, text: 'Day' } },
      y: {
        beginAtZero: true,
        ticks: { precision: 0 },
        title: { display: true, text: 'Vehicles' },
        grid: { color: 'rgba(15, 23, 42, 0.06)' }
      }
    }
  };

  private recomputeFleetHealth(): void {
    // Health is loaded from GET /gps/fleet-health (real factors). Do not invent from locations.
    this.loadFleetHealth();
  }

  private recomputeFleetOverviewChart(): void {
    const daily = this.rollupFleetStatusByDay(this.fleetStatusHistory);
    const labels = daily.map(s =>
      new Date(s.snapshotAt).toLocaleDateString(undefined, { day: '2-digit', month: 'short' }));
    this.fleetOverviewChartData = {
      labels,
      datasets: [
        {
          label: 'Moving',
          data: daily.map(s => s.moving),
          borderColor: '#2563EB',
          backgroundColor: '#2563EB33',
          tension: 0.35,
          pointRadius: 2
        },
        {
          label: 'Idle',
          data: daily.map(s => s.idle),
          borderColor: '#F59E0B',
          backgroundColor: '#F59E0B33',
          tension: 0.35,
          pointRadius: 2
        },
        {
          label: 'Parked',
          data: daily.map(s => s.parked),
          borderColor: '#0D9488',
          backgroundColor: '#0D948833',
          tension: 0.35,
          pointRadius: 2
        },
        {
          label: 'Offline',
          data: daily.map(s => s.offline),
          borderColor: '#EF4444',
          backgroundColor: '#EF444433',
          tension: 0.35,
          pointRadius: 2
        }
      ]
    };
  }

  /** Last snapshot per local calendar day (history assumed ascending by SnapshotAt). */
  private rollupFleetStatusByDay(rows: GpsFleetStatusSnapshot[]): GpsFleetStatusSnapshot[] {
    const byDay = new Map<string, GpsFleetStatusSnapshot>();
    for (const s of rows) {
      const d = new Date(s.snapshotAt);
      if (Number.isNaN(d.getTime())) continue;
      const key = `${d.getFullYear()}-${d.getMonth() + 1}-${d.getDate()}`;
      byDay.set(key, s);
    }
    return [...byDay.values()];
  }

  toggleLiveTracking(): void {
    this.liveTracking = !this.liveTracking;
    if (this.liveTracking) this.refreshNow();
    this.pushEvent(
      this.liveTracking ? 'Live tracking resumed' : 'Live tracking paused',
      'info',
      this.liveTracking ? 'play_circle' : 'pause_circle'
    );
    this.cdr.markForCheck();
  }

  private startAutoRefresh(): void {
    if (this.refreshInterval) clearInterval(this.refreshInterval);
    const intervalMs = this.effectivePollIntervalMs();
    if (intervalMs == null) return;
    this.refreshInterval = setInterval(() => {
      if (this.liveTracking && !this.userInteractionActive) this.loadLocations(true);
    }, intervalMs);
  }

  /** SignalR connected → no REST poll (or optional ≥2 min sanity); disconnected → 5–10s fallback. */
  private effectivePollIntervalMs(): number | null {
    if (this.connectionState === 'connected') {
      // Realtime mode: no HTTP poll. Optional REST sanity only when user picked ≥ 2 min.
      if (this.refreshRateMs == null) return null;
      if (this.refreshRateMs >= 120_000) return this.refreshRateMs;
      // "Force poll" modes still poll while connected when explicitly selected.
      return this.refreshRateMs;
    }
    // Hub down: always poll for continuity (5–10s).
    if (this.refreshRateMs == null) return 10_000;
    return Math.min(this.refreshRateMs, 10_000);
  }

  setRefreshRate(rate: RefreshRateMs): void {
    this.refreshRateMs = rate;
    this.markUserActive();
    this.startAutoRefresh();
    const label =
      rate == null
        ? 'Realtime (SignalR only while connected)'
        : rate >= 120_000
          ? `REST sanity every ${rate / 1000}s`
          : `Force poll every ${rate / 1000}s`;
    this.pushEvent(label, 'info', rate == null ? 'sensors' : 'autorenew');
  }

  toggleFollowSelected(): void {
    this.followSelected = !this.followSelected;
    this.pushEvent(
      this.followSelected ? 'Follow vehicle enabled' : 'Follow vehicle disabled',
      'info',
      this.followSelected ? 'my_location' : 'location_disabled'
    );
    this.cdr.markForCheck();
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
      clearGmapGeofences(this.geofenceHandle);
      return;
    }
    this.gpsService.getGeofences({ isActive: true }).subscribe({
      next: fences => {
        if (!this.map) return;
        clearGmapGeofences(this.geofenceHandle);
        for (const g of fences) {
          addGmapGeofenceBoundary(this.map, this.geofenceHandle, g, {
            fillOpacity: 0.08,
            weight: 2
          });
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
    this.mapError = null;
    this.themeHandle = applyGmapTheme(this.map, theme, this.themeHandle);
    this.scheduleMapResize();
  }

  private scheduleMapResize(): void {
    if (!this.map) return;
    const resize = () => triggerGmapResize(this.map);
    resize();
    requestAnimationFrame(resize);
    setTimeout(resize, 150);
    setTimeout(resize, 400);
  }

  private observeMapResize(): void {
    const el = this.mapContainer?.nativeElement;
    if (!el || typeof ResizeObserver === 'undefined') return;
    this.mapResizeObserver?.disconnect();
    this.mapResizeObserver = new ResizeObserver(() => triggerGmapResize(this.map));
    this.mapResizeObserver.observe(el);
  }

  centerMap(): void {
    if (!this.map) return;
    const gps = this.locations.filter(l => l.hasGps && l.latitude && l.longitude);
    if (!gps.length) {
      this.map.panTo({ lat: 30.3753, lng: 69.3451 });
      this.map.setZoom(6);
      return;
    }
    const bounds = new google.maps.LatLngBounds();
    gps.forEach(l => bounds.extend({ lat: l.latitude, lng: l.longitude }));
    this.map.fitBounds(bounds, { top: 48, right: 48, bottom: 48, left: 48 });
    const z = this.map.getZoom();
    if (z != null && z > 12) this.map.setZoom(12);
  }

  resetZoom(): void {
    if (!this.map) return;
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
    const previousId = this.selectedVehicleId;
    this.selectedVehicleId = loc.vehicleId;
    this.detailTab = 'overview';
    // Clear the coord-dedup key so selecting a vehicle always fetches a fresh address.
    this.lastEnrichedCoordKey.delete(loc.vehicleId);
    this.clearNearbyPlaces();
    this.markUserActive();
    void this.realtime.subscribeVehicle(loc.vehicleId);
    this.syncSelectedLocation();
    this.refreshKpiSelectionFlags();
    // Only refresh selection styling on previous + new marker — not the whole fleet.
    this.refreshMarkerSelection(previousId, loc.vehicleId);
    if (loc.hasGps && this.isValidCoord(loc.latitude, loc.longitude)) {
      this.focusVehicle(loc);
      this.enrichSelectedAddress(loc);
      if (this.nearbyExpanded) this.runNearbySearch();
    } else {
      this.pushEvent(`${loc.vehicleName} has no GPS coordinates yet`, 'warning', 'gps_off');
    }

    this.selectedEta = null;
    if (loc.bookingId != null) {
      this.gpsService.getEta(loc.bookingId).subscribe({
        next: eta => {
          this.selectedEta = eta;
          this.cdr.markForCheck();
        },
        error: () => {
          this.selectedEta = null;
          this.cdr.markForCheck();
        }
      });
    }
    this.cdr.markForCheck();
  }

  /** Resolve street + nearby shop/POI for the selected vehicle detail panel. */
  private enrichSelectedAddress(loc: VehicleLocation): void {
    const coarse = isCoarseAddress(loc.address);
    const hasPlace = !!loc.placeName?.trim();
    if (!coarse && loc.address?.trim() && hasPlace) return;

    // Deduplicate: don't re-request if we already fetched for these coordinates.
    const coordKey = `${loc.latitude.toFixed(4)},${loc.longitude.toFixed(4)}`;
    if (this.lastEnrichedCoordKey.get(loc.vehicleId) === coordKey) return;
    this.lastEnrichedCoordKey.set(loc.vehicleId, coordKey);

    this.addressResolvingVehicleId = loc.vehicleId;
    this.cdr.markForCheck();
    this.gpsService.reverseGeocode(loc.latitude, loc.longitude, true).subscribe({
      next: info => {
        if (this.selectedVehicleId !== loc.vehicleId) {
          if (this.addressResolvingVehicleId === loc.vehicleId) {
            this.addressResolvingVehicleId = null;
          }
          this.cdr.markForCheck();
          return;
        }
        this.addressResolvingVehicleId = null;
        if (!info?.formattedAddress) {
          this.cdr.markForCheck();
          return;
        }

        const idx = this.locations.findIndex(l => l.vehicleId === loc.vehicleId);
        if (idx < 0) return;

        const nextAddress =
          formatResolvedAddress(info.formattedAddress, info.placeName) ||
          (info.road
            ? [info.road, info.city, info.state, info.country].filter(Boolean).join(', ')
            : null) ||
          info.formattedAddress.trim();
        const nextPlace = info.placeName?.trim() || undefined;
        const nextType = info.placeType?.trim() || undefined;
        const nextLocality =
          buildLocalityLine({
            city: info.city,
            state: info.state,
            country: info.country
          }) || undefined;
        const cur = this.locations[idx];
        if (
          cur.address === nextAddress &&
          cur.placeName === nextPlace &&
          cur.placeType === nextType &&
          cur.addressLocality === nextLocality
        ) {
          this.cdr.markForCheck();
          return;
        }
        this.locations[idx] = {
          ...cur,
          address: nextAddress,
          placeName: nextPlace,
          placeType: nextType,
          addressLocality: nextLocality
        };
        this.patchFilteredCacheForVehicle(this.locations[idx], false);
        this.syncSelectedLocation();
        this.cdr.markForCheck();
      },
      error: () => {
        if (this.addressResolvingVehicleId === loc.vehicleId) {
          this.addressResolvingVehicleId = null;
        }
        this.cdr.markForCheck();
      }
    });
  }

  private isCoarseAddress(address?: string | null): boolean {
    return isCoarseAddress(address);
  }

  goToCommands(): void {
    this.router.navigate(['/gps-tracking/commands']);
  }

  onVehicleCardEnter(loc: VehicleLocation): void {
    if (!loc.hasGps) return;
    const marker = this.markers.get(loc.vehicleId);
    if (marker) marker.zIndex = 2000;
    this.trailLayers.get(loc.vehicleId)?.setOptions({ strokeWeight: 5, strokeOpacity: 1 });
  }

  onVehicleCardLeave(loc: VehicleLocation): void {
    const marker = this.markers.get(loc.vehicleId);
    if (marker && this.selectedVehicleId !== loc.vehicleId) {
      marker.zIndex = 0;
    }
    const line = this.trailLayers.get(loc.vehicleId);
    if (line) line.setOptions({ strokeWeight: 3, strokeOpacity: 0.75 });
  }

  private async initMap(): Promise<void> {
    const host = this.mapContainer?.nativeElement;
    if (!host) {
      throw new Error('Map container element not found');
    }
    if (this.map) return;

    if (!this.googleMapsLoader.isConfigured) {
      this.mapError = 'Google Maps API key is not configured. Set environment.googleMapsApiKey in environment.ts.';
      return;
    }

    this.authFailSub?.unsubscribe();
    this.authFailSub = this.googleMapsLoader.authFailures$.subscribe(msg => {
      this.ngZone.run(() => {
        this.mapError = msg;
      });
    });

    const bootstrapped = await this.googleMapsLoader.load();
    if (!bootstrapped) {
      this.mapError =
        this.googleMapsLoader.failureMessage || 'Google Maps failed to load.';
      return;
    }

    await this.googleMapsLoader.importLibrary('maps');
    await this.googleMapsLoader.importLibrary('marker');

    if (this.googleMapsLoader.authFailed) {
      this.mapError =
        this.googleMapsLoader.failureMessage || 'Google Maps authentication failed.';
      return;
    }

    const mapOptions: google.maps.MapOptions = {
      center: { lat: 30.3753, lng: 69.3451 },
      zoom: 6,
      maxZoom: 20,
      disableDefaultUI: false,
      mapTypeControl: false,
      streetViewControl: false,
      fullscreenControl: false,
      zoomControl: true,
      zoomControlOptions: {
        position: google.maps.ControlPosition.RIGHT_BOTTOM
      },
      // Advanced Markers require a Map ID; DEMO_MAP_ID works for local/dev without Cloud Map Management.
      mapId: (environment as { googleMapsMapId?: string }).googleMapsMapId?.trim() || 'DEMO_MAP_ID'
    };

    this.map = new google.maps.Map(host, mapOptions);
    this.infoWindow = new google.maps.InfoWindow();
    this.markerCluster = createFleetMarkerClusterer(this.map);
    void this.setMapTheme(this.mapTheme);
    this.observeMapResize();
    this.scheduleMapResize();
    this.mapReady = true;
    if (this.pendingMarkerLocations) {
      this.updateMarkers(this.pendingMarkerLocations);
      this.pendingMarkerLocations = null;
    } else if (this.locations.length) {
      this.updateMarkers(this.mappableLocations(this.filteredLocationsCache));
    }
  }

  private mappableLocations(locs: VehicleLocation[]): VehicleLocation[] {
    return locs.filter(l => l.hasGps && this.isValidCoord(l.latitude, l.longitude));
  }

  private loadLocations(silent = false, manual = false): void {
    if (this.isRefreshingLocations) {
      if (manual) this.queuedManualRefresh = true;
      return;
    }
    this.isRefreshingLocations = true;
    if (!silent) this.loading = true;
    this.gpsService.getAllVehicleLocations().subscribe({
      next: locs => {
        const prevMoving = new Set(
          this.locations.filter(l => l.status === 'moving').map(l => l.vehicleId)
        );
        const { locations, membershipChanged } = mergeVehicleLocationsPreservingIdentity(
          this.locations,
          locs
        );
        this.locations = locations;
        this.rebuildListDerivedState({ rebuildFilter: true, forceKpis: true });
        this.recomputeFleetHealth();
        this.loading = false;
        this.syncError = null;
        this.lastSyncAt = new Date();
        this.secondsSinceSync = 0;
        const gpsLocs = this.mappableLocations(this.filteredLocationsCache);
        this.updateMarkers(gpsLocs, {
          resize: !silent || membershipChanged,
          pruneMissing: true
        });
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
        this.finishLocationLoad(manual);
        this.cdr.markForCheck();
      },
      error: () => {
        this.loading = false;
        this.isRefreshingLocations = false;
        this.syncError = 'Could not reach tracking service.';
        this.pushEvent('Tracking sync failed', 'alert', 'cloud_off');
        this.updateEmptyStateKind();
        this.finishLocationLoad(manual);
        this.cdr.markForCheck();
      }
    });
  }

  private finishLocationLoad(manual: boolean): void {
    if (this.queuedManualRefresh) {
      this.queuedManualRefresh = false;
      this.beginRefreshUi();
      this.loadLocations(true, true);
      return;
    }
    if (manual || this.refreshing) {
      this.endRefreshUi();
    }
  }

  private emitSyncSummary(gpsLocs: VehicleLocation[], announce: boolean): void {
    if (!announce) return;
    const live = gpsLocs.filter(l => l.isLive).length;
    const lastKnown = gpsLocs.length - live;
    const summary = `${gpsLocs.length} on map (${live} live${lastKnown ? `, ${lastKnown} last known` : ''})`;
    if (summary === this.lastSyncSummaryKey) return;
    this.lastSyncSummaryKey = summary;
    // Intentionally no Live Events feed on this page — Alerts module owns event history.
  }

  private loadRecentAlertEvents(): void {
    // Alerts tab owns event history; do not mirror into Live Map UI.
  }

  private emitTelemetryEvents(_gpsLocs: VehicleLocation[], _prevMoving: Set<number>): void {
    // Status transitions are visible on markers/list; skip noisy event spam.
  }

  private pushEvent(_message: string, _type: TrackEvent['type'], _icon: string): void {
    // Live Events panel removed — keep method as no-op for remaining call sites.
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

  private createMarkerContent(
    status: FleetTrackStatus,
    bearing = 0,
    vehicleType?: string | null,
    ignition?: boolean | null,
    vehicleId?: number
  ): HTMLElement {
    const selected = vehicleId != null && vehicleId === this.selectedVehicleId;
    const badge =
      status === 'sos' ? 'sos' :
      status === 'offline' || status === 'never_seen' ? 'offline' :
      ignition === true && status !== 'moving' ? 'ignition' :
      status === 'parked' ? 'parked' :
      null;
    return createFleetVehicleMarkerElement({
      status,
      heading: bearing,
      vehicleType,
      badge,
      size: selected ? 34 : 30,
      selected,
      pulse: selected
    });
  }

  /** Visual signature for marker icon — position-only updates when unchanged. */
  private markerVisualSignature(
    loc: VehicleLocation,
    bearing: number,
    selected: boolean
  ): string {
    const badge =
      loc.status === 'sos' ? 'sos' :
      loc.status === 'offline' || loc.status === 'never_seen' ? 'offline' :
      loc.ignition === true && loc.status !== 'moving' ? 'ignition' :
      loc.status === 'parked' ? 'parked' :
      'none';
    const bearingBucket = Math.round(((bearing % 360) + 360) % 360 / 15) * 15;
    return `${loc.status}|${selected ? 1 : 0}|${badge}|${bearingBucket}|${loc.vehicleType ?? ''}`;
  }

  /** Update selection styling on previous + newly selected markers only. */
  private refreshMarkerSelection(
    previousId: number | null,
    nextId: number | null
  ): void {
    const unique = [...new Set([previousId, nextId].filter((id): id is number => id != null))];
    const locs: VehicleLocation[] = [];
    for (const id of unique) {
      const loc = this.locations.find(l => l.vehicleId === id);
      if (loc && loc.hasGps && this.isValidCoord(loc.latitude, loc.longitude)) {
        locs.push(loc);
      }
    }
    if (!locs.length) return;
    // Force content rebuild by clearing visual sig for these ids.
    for (const loc of locs) {
      this.markerVisualSig.delete(loc.vehicleId);
    }
    this.updateMarkers(locs, { resize: false, pruneMissing: false });
  }

  private updateTrail(loc: VehicleLocation): void {
    if (!this.map) return;
    const pts = [
      ...(this.positionTrails.get(loc.vehicleId) ?? []),
      { lat: loc.latitude, lng: loc.longitude }
    ].slice(-this.maxTrailPoints);
    this.positionTrails.set(loc.vehicleId, pts);
    if (pts.length < 2) return;

    const color = TRAIL_COLORS[loc.status];
    const existing = this.trailLayers.get(loc.vehicleId);
    if (existing) {
      existing.setPath(pts);
      existing.setOptions({ strokeColor: color });
    } else {
      const line = new google.maps.Polyline({
        map: this.map,
        path: pts,
        strokeColor: color,
        strokeWeight: 3,
        strokeOpacity: 0.8,
        clickable: false,
        zIndex: 1
      });
      this.trailLayers.set(loc.vehicleId, line);
    }
  }

  private updateMarkers(
    locs: VehicleLocation[],
    opts: { resize?: boolean; pruneMissing?: boolean } = {}
  ): void {
    const resize = opts.resize !== false;
    const pruneMissing = opts.pruneMissing !== false;

    if (!this.mapReady || !this.map || !this.markerCluster) {
      this.pendingMarkerLocations = locs;
      return;
    }

    this.ngZone.runOutsideAngular(() => {
      const mappable = locs.filter(
        loc => loc.hasGps && this.isValidCoord(loc.latitude, loc.longitude)
      );
      const currentIds = new Set(mappable.map(l => l.vehicleId));

      if (pruneMissing) {
        this.markers.forEach((marker, vehicleId) => {
          if (!currentIds.has(vehicleId)) {
            this.cancelMarkerAnim(vehicleId);
            this.markerClickListeners.get(vehicleId)?.remove();
            this.markerClickListeners.delete(vehicleId);
            this.markerCluster?.removeMarker(marker);
            marker.map = null;
            this.markers.delete(vehicleId);
            this.trailLayers.get(vehicleId)?.setMap(null);
            this.trailLayers.delete(vehicleId);
            this.positionTrails.delete(vehicleId);
            this.prevPositions.delete(vehicleId);
            this.popupHtml.delete(vehicleId);
            this.markerVisualSig.delete(vehicleId);
          }
        });
      }

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
          this.locationPrimaryLine(loc) ||
          shortAddressLine(loc.address) ||
          `${loc.latitude.toFixed(5)}, ${loc.longitude.toFixed(5)}`;
        const popupAddr = this.locationSecondaryLine(loc)
          ? `${addr} · ${this.locationSecondaryLine(loc)}`
          : addr;
        const popupContent =
          buildFleetVehiclePopup({
            name: loc.vehicleName,
            plate: loc.registrationNumber,
            driver: loc.driverName,
            tracker: loc.trackerName ?? loc.imei,
            ignition: loc.ignition,
            speedKmh: this.displaySpeedKmh(loc),
            headingLabel: headingText || null,
            address: popupAddr,
            mapsUrl: this.googleMapsUrl(loc),
            gpsStatus: this.formatLastPing(loc),
            statusLabel: this.statusLabel(loc.status)
          }) + `<a href="#" class="map-popup-link" data-vid="${loc.vehicleId}">View details →</a>`;
        this.popupHtml.set(loc.vehicleId, popupContent);

        const selected = this.selectedVehicleId === loc.vehicleId;
        const visualSig = this.markerVisualSignature(loc, bearing, selected);
        const existingMarker = this.markers.get(loc.vehicleId);

        if (existingMarker) {
          this.animateMarkerTo(loc.vehicleId, existingMarker, loc.latitude, loc.longitude);
          if (this.markerVisualSig.get(loc.vehicleId) !== visualSig) {
            existingMarker.content = this.createMarkerContent(
              loc.status,
              bearing,
              loc.vehicleType,
              loc.ignition,
              loc.vehicleId
            );
            this.markerVisualSig.set(loc.vehicleId, visualSig);
          }
          if (selected) {
            existingMarker.zIndex = 1500;
          } else if (existingMarker.zIndex === 1500) {
            existingMarker.zIndex = null;
          }
        } else {
          const content = this.createMarkerContent(
            loc.status,
            bearing,
            loc.vehicleType,
            loc.ignition,
            loc.vehicleId
          );
          const marker = new google.maps.marker.AdvancedMarkerElement({
            position: { lat: loc.latitude, lng: loc.longitude },
            content,
            gmpClickable: true,
            title: loc.vehicleName
          });
          const listener = marker.addListener('click', () => {
            this.ngZone.run(() => this.openVehicleInfoWindow(loc.vehicleId, marker));
          });
          this.markerClickListeners.set(loc.vehicleId, listener);
          this.markerCluster!.addMarker(marker);
          this.markers.set(loc.vehicleId, marker);
          this.markerVisualSig.set(loc.vehicleId, visualSig);
          if (selected) {
            marker.zIndex = 1500;
          }
        }
      });

      if (resize) {
        this.scheduleMapResize();
      }
    });
  }

  private openVehicleInfoWindow(
    vehicleId: number,
    marker: google.maps.marker.AdvancedMarkerElement
  ): void {
    if (!this.infoWindow || !this.map) return;
    const html = this.popupHtml.get(vehicleId);
    if (!html) return;
    this.infoWindow.setContent(html);
    this.infoWindow.open({ map: this.map, anchor: marker });
    google.maps.event.addListenerOnce(this.infoWindow, 'domready', () => {
      document.querySelector(`a[data-vid="${vehicleId}"]`)?.addEventListener(
        'click',
        e => {
          e.preventDefault();
          this.ngZone.run(() => this.goToVehicleProfile(vehicleId));
        },
        { once: true }
      );
    });
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
    marker: google.maps.marker.AdvancedMarkerElement,
    lat: number,
    lng: number
  ): void {
    const pos = marker.position;
    const fromLat =
      typeof pos === 'object' && pos && 'lat' in pos
        ? typeof pos.lat === 'function'
          ? pos.lat()
          : Number(pos.lat)
        : lat;
    const fromLng =
      typeof pos === 'object' && pos && 'lng' in pos
        ? typeof pos.lng === 'function'
          ? pos.lng()
          : Number(pos.lng)
        : lng;
    const distKm = this.haversineKm(fromLat, fromLng, lat, lng);
    this.cancelMarkerAnim(vehicleId);

    if (distKm <= 0.0005 || distKm > this.maxAnimateKm) {
      marker.position = { lat, lng };
      return;
    }

    const start = performance.now();
    const duration = this.markerAnimMs;
    const step = (now: number) => {
      const t = Math.min(1, (now - start) / duration);
      const eased = t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2;
      marker.position = {
        lat: fromLat + (lat - fromLat) * eased,
        lng: fromLng + (lng - fromLng) * eased
      };
      if (t < 1) {
        this.markerAnimFrames.set(vehicleId, requestAnimationFrame(step));
      } else {
        this.markerAnimFrames.delete(vehicleId);
        marker.position = { lat, lng };
      }
    };
    this.markerAnimFrames.set(vehicleId, requestAnimationFrame(step));
  }

  focusVehicle(loc: VehicleLocation): void {
    if (!loc.hasGps || !this.isValidCoord(loc.latitude, loc.longitude) || !this.map) return;
    this.map.panTo({ lat: loc.latitude, lng: loc.longitude });
    this.map.setZoom(14);
    const marker = this.markers.get(loc.vehicleId);
    if (marker) {
      this.openVehicleInfoWindow(loc.vehicleId, marker);
    } else {
      setTimeout(() => {
        const m = this.markers.get(loc.vehicleId);
        if (m) this.openVehicleInfoWindow(loc.vehicleId, m);
      }, 250);
    }
    this.pushEvent(
      `Focused ${loc.vehicleName} (${loc.isLive ? 'live' : 'last known'})`,
      'info',
      'my_location'
    );
  }

  toggleNearbyPlaces(): void {
    this.nearbyExpanded = !this.nearbyExpanded;
    if (!this.nearbyExpanded) {
      this.clearNearbyPlaces();
      return;
    }
    this.runNearbySearch();
  }

  selectNearbyCategory(category: string): void {
    if (this.nearbyCategory === category && this.nearbyPlaces.length) return;
    this.nearbyCategory = category;
    if (this.nearbyExpanded) this.runNearbySearch();
  }

  setNearbyRadius(meters: number): void {
    this.nearbyRadiusMeters = meters;
    if (this.nearbyExpanded) this.runNearbySearch();
  }

  /** Resolve search center: selected vehicle GPS, else current map center. */
  private resolveNearbyCenter(): { lat: number; lng: number; label: string } | null {
    const selected = this.locations.find(l => l.vehicleId === this.selectedVehicleId);
    if (selected?.hasGps && this.isValidCoord(selected.latitude, selected.longitude)) {
      return {
        lat: selected.latitude,
        lng: selected.longitude,
        label: selected.vehicleName
      };
    }
    const center = this.map?.getCenter();
    if (center) {
      return {
        lat: center.lat(),
        lng: center.lng(),
        label: 'Map center'
      };
    }
    return null;
  }

  runNearbySearch(): void {
    const center = this.resolveNearbyCenter();
    if (!center) {
      this.nearbyError = 'Select a vehicle or wait for the map to load.';
      this.nearbyPlaces = [];
      return;
    }
    this.nearbySearchCenterLabel = center.label;
    this.nearbyLoading = true;
    this.nearbyError = null;
    this.selectedNearbyPlace = null;
    this.gpsService
      .getNearbyPlaces(center.lat, center.lng, this.nearbyCategory, this.nearbyRadiusMeters, 10)
      .subscribe({
        next: places => {
          this.nearbyLoading = false;
          this.nearbyPlaces = places;
          if (!places.length) {
            const cat =
              this.nearbyCategories.find(c => c.id === this.nearbyCategory)?.label
              ?? this.nearbyCategory;
            const radiusLabel = this.formatNearbyDistance(this.nearbyRadiusMeters) || 'the selected radius';
            this.nearbyError = `No ${cat.toLowerCase()} found within ${radiusLabel}.`;
          }
          void this.renderNearbyMarkers(places);
        },
        error: (err: unknown) => {
          this.nearbyLoading = false;
          this.nearbyPlaces = [];
          this.selectedNearbyPlace = null;
          // gpsService.getNearbyPlaces always maps failures to Error with a clear message.
          this.nearbyError =
            err instanceof Error && err.message.trim()
              ? err.message.trim()
              : 'Nearby places unavailable. Check Places API (New) on the backend key.';
          this.clearNearbyMarkers();
        }
      });
  }

  loadNearbyPlaces(loc: VehicleLocation): void {
    this.selectedVehicleId = loc.vehicleId;
    this.runNearbySearch();
  }

  focusNearbyPlace(place: NearbyPlace): void {
    this.selectedNearbyPlace = place;
    this.applyNearbyMarkerHighlight(place.placeId);
    if (!this.map) return;
    this.map.panTo({ lat: place.latitude, lng: place.longitude });
    if ((this.map.getZoom() ?? 0) < 15) this.map.setZoom(15);
  }

  nearbyMapsUrl(place: NearbyPlace): string {
    if (place.googleMapsUri?.trim()) return place.googleMapsUri.trim();
    const id = place.placeId.startsWith('places/')
      ? place.placeId.slice('places/'.length)
      : place.placeId;
    return `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(place.name)}&query_place_id=${encodeURIComponent(id)}`;
  }

  /** Google Maps directions from selected vehicle (or map) to the place. */
  nearbyDirectionsUrl(place: NearbyPlace): string {
    const destId = place.placeId.startsWith('places/')
      ? place.placeId.slice('places/'.length)
      : place.placeId;
    const params = new URLSearchParams({
      api: '1',
      destination: `${place.latitude},${place.longitude}`,
      destination_place_id: destId,
      travelmode: 'driving'
    });
    const origin = this.resolveNearbyCenter();
    if (origin) {
      params.set('origin', `${origin.lat},${origin.lng}`);
    }
    return `https://www.google.com/maps/dir/?${params.toString()}`;
  }

  formatNearbyDistance(meters?: number | null): string {
    if (meters == null || !Number.isFinite(meters)) return '';
    if (meters < 1000) return `${Math.round(meters)} m`;
    return `${(meters / 1000).toFixed(1)} km`;
  }

  nearbyOpeningLabel(place: NearbyPlace): string {
    if (place.openingStatus?.trim()) return place.openingStatus.trim();
    if (place.openNow === true) return 'Open';
    if (place.openNow === false) return 'Closed';
    return '';
  }

  private clearNearbyPlaces(): void {
    this.nearbyPlaces = [];
    this.nearbyError = null;
    this.nearbyLoading = false;
    this.selectedNearbyPlace = null;
    this.clearNearbyMarkers();
  }

  private clearNearbyMarkers(): void {
    this.nearbyMarkerListeners.forEach(l => l.remove());
    this.nearbyMarkerListeners = [];
    this.nearbyMarkers.forEach(m => {
      m.map = null;
    });
    this.nearbyMarkers = [];
    this.nearbyMarkerByPlaceId.clear();
  }

  private applyNearbyMarkerHighlight(placeId: string): void {
    this.nearbyMarkerByPlaceId.forEach((marker, id) => {
      const el = marker.content as HTMLElement | null;
      if (!el) return;
      el.classList.toggle('nearby-place-marker--selected', id === placeId);
    });
  }

  private async renderNearbyMarkers(places: NearbyPlace[]): Promise<void> {
    this.clearNearbyMarkers();
    if (!this.map || !places.length) return;
    try {
      await this.googleMapsLoader.importLibrary('marker');
    } catch {
      return;
    }
    const { AdvancedMarkerElement } = google.maps.marker;
    for (const place of places) {
      const el = document.createElement('div');
      el.className = 'nearby-place-marker';
      el.title = place.name;
      el.innerHTML = `<span class="nearby-place-marker__dot"></span>`;
      const marker = new AdvancedMarkerElement({
        map: this.map,
        position: { lat: place.latitude, lng: place.longitude },
        content: el,
        title: place.name,
        zIndex: 500
      });
      const listener = marker.addListener('gmp-click', () => this.focusNearbyPlace(place));
      this.nearbyMarkerListeners.push(listener);
      this.nearbyMarkers.push(marker);
      this.nearbyMarkerByPlaceId.set(place.placeId, marker);
    }
    if (this.selectedNearbyPlace) {
      this.applyNearbyMarkerHighlight(this.selectedNearbyPlace.placeId);
    }
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

  /**
   * Apply a single-vehicle SignalR fix. Returns true when UI should markForCheck.
   * Runs outside Angular; caller marks CD only on real changes.
   */
  private applyRealtimeUpdate(update: PositionDto): boolean {
    const idx = this.locations.findIndex(l => l.vehicleId === update.vehicleId);
    if (idx < 0) {
      return false;
    }

    const speed = Number(update.speed) || 0;
    const status = resolveFleetStatus({
      speed,
      ignition: update.ignition,
      lastUpdated: update.timestamp,
      hasGps: true,
      alarmType: update.alarmType
    });
    const prev = this.locations[idx];
    // Skip no-op updates — do not touch lastSyncAt or trigger CD.
    if (
      prev.latitude === update.latitude &&
      prev.longitude === update.longitude &&
      prev.speed === speed &&
      prev.status === status &&
      prev.ignition === update.ignition &&
      prev.lastUpdated === update.timestamp
    ) {
      return false;
    }

    const prevStatus = prev.status;
    const incomingAddr = update.address?.trim() || null;
    const keptAddr = preferRicherAddress(prev.address, incomingAddr);
    this.locations[idx] = {
      ...prev,
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
      // Never clobber an enriched street address with a coarse Traccar string.
      address: keptAddr,
      alarmType: update.alarmType,
      temperature: update.temperature,
      routeHint: this.realtimeRouteHint(status, speed)
    };

    const patched = this.locations[idx];
    const statusChanged = prevStatus !== status;
    this.patchFilteredCacheForVehicle(patched, statusChanged);
    // Always retally KPIs from the same status field the cards use (cheap O(n)).
    this.rebuildFleetCountsAndKpis();

    // Only refresh selected snapshot when this vehicle is selected.
    if (this.selectedVehicleId === update.vehicleId) {
      this.selectedLocation = patched;
    }

    // Surgical marker update — do not rebuild the whole fleet every GPS tick.
    this.updateMarkers([patched], { resize: false, pruneMissing: false });
    if (this.followSelected && this.selectedVehicleId === update.vehicleId) {
      this.map?.panTo({ lat: update.latitude, lng: update.longitude });
    }
    if (
      this.selectedVehicleId === update.vehicleId &&
      isCoarseAddress(patched.address)
    ) {
      this.enrichSelectedAddress(patched);
    }

    this.lastSyncAt = new Date();
    this.secondsSinceSync = 0;
    return true;
  }

  private realtimeRouteHint(status: FleetTrackStatus, speed: number): string {
    if (status === 'moving') return `${Math.round(speed)} km/h`;
    if (status === 'idle') return 'Idle • awaiting movement';
    if (status === 'parked') return 'Parked • ignition off';
    if (status === 'unknown') return 'Unknown telemetry';
    if (status === 'sos') return 'SOS / panic alarm';
    return `${Math.round(speed)} km/h`;
  }
}
