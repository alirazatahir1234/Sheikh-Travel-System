import { Component, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ConnectedPosition } from '@angular/cdk/overlay';
import { Subscription, timer, Subject, mergeMap, catchError, of, tap, forkJoin, interval } from 'rxjs';
import { skip } from 'rxjs/operators';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { AuthService } from '../../../core/services/auth.service';
import { maskImei } from '../utils/imei-mask.util';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { VehicleListItem } from '../../../core/models/vehicle.model';
import { GpsScopeFilterService } from '../gps-scope-filter.service';
import {
  Geofence,
  GpsDevice,
  HistoryReplayBundle,
  HistoryTimeline,
  HistoryTimelineSegment,
  HistoryTimelineTotals,
  PositionDto,
  TripReplayPosition,
  VehicleLocation
} from '../../../core/models/gps-tracking.model';
import {
  HISTORY_DATE_PRESETS,
  HISTORY_PERIOD_BUTTONS,
  HISTORY_PERIOD_QUICK_PICKS,
  TripDatePreset,
  applyTripDatePreset,
  parseDatetimeLocalInput,
  toDatetimeLocalInput
} from '../utils/trip-date-preset.util';
import { resolveReplayStatus } from '../../../core/google-maps/fleet-vehicle-marker.gmap';
import { formatFleetDisplayAddress, isCoarseAddress, splitDisplayAddress } from '../utils/gps-address.util';
import {
  COMPARE_ROUTE_COLORS,
  formatStalenessLabel,
  googleMapsUrl,
  HISTORY_ALERT_NOTIFIED_KEY,
  HISTORY_COMPARE_MAX_VEHICLES,
  HISTORY_STATIONARY_ALERT_MINUTES,
  isOfflineOrCachedTracker,
  isVehicleStationaryNow,
  readHistoryPagePrefs,
  resolveLastKnownIso,
  stationaryMinutes,
  storeHistoryPagePrefs
} from '../utils/history-fleet-context.util';
import { formatInZone, toDate as toInstant } from '../../../core/utils/zoned-time.util';
import {
  buildKpis,
  buildTimelineRows,
  clockParts,
  DEFAULT_HIDE_STOPS_MINUTES,
  defaultOpenDay,
  drivenSoFarKm,
  formatClock,
  formatDay,
  formatDuration,
  formatRangeEdge,
  formatTimeRange,
  HIDE_STOP_OPTIONS,
  HistoryKpi,
  nextStandstill,
  placeLabel,
  previousStandstill,
  segmentAt,
  segmentEndMs,
  segmentStartMs,
  standstillWhere,
  TimelineFilter,
  TimelineRow,
  tripAtOrAfter,
  tripTitle,
  zoneShortLabel
} from '../utils/history-timeline.util';
import {
  ActivityBlock,
  activityBlocks,
  advancePlayhead,
  AxisTick,
  axisTicks,
  DEFAULT_PLAYBACK_RATE,
  DockZoom,
  dockRange,
  tripAfter,
  outsideBlocks,
  percentToTime,
  PLAYBACK_RATES,
  positionAt,
  speedPaths,
  TimeRange,
  timesOf,
  timeToPercent,
  weekZoomAvailable
} from '../utils/history-playback.util';
import {
  HistoryMapCompareRoute,
  HistoryMapComponent,
  HistoryMapLayers,
  HistoryMapType,
  HistoryMapVehicle
} from './history-map/history-map.component';
import { TimelinePanelState } from './history-timeline-panel/history-timeline-panel.component';
import { HistoryVehicleCard } from './history-vehicle-picker/history-vehicle-picker.component';

const RECENT_VEHICLES_KEY = 'gps_history_recent';
const WORKSPACE_PREFS_KEY = 'gps_history_workspace_prefs';
const MAX_RECENT = 5;
const LOAD_TIMEOUT_MS = 20_000;
const LAST7_ROUTE_MAX_POINTS = 3000;
const LAST7_PLAYBACK_MAX_POINTS = 1000;
const PLAY_TICK_MS = 66;
const ADDRESS_LOOKUP_DELAY_MS = 450;
const MAX_REPORT_ROWS = 500;

type LayerKey = keyof HistoryMapLayers;
type PanelKey = 'timeline' | 'dock' | 'details';

/** What the user left the workspace like, per browser. */
interface WorkspacePrefs {
  showTimeline: boolean;
  showDock: boolean;
  showDetails: boolean;
  layers: HistoryMapLayers;
  mapType: HistoryMapType;
  timelineFilter: TimelineFilter;
  hideStopsUnderMinutes: number;
  /** Multiple of real time. Saved before as `rate`, where 1 meant 60× — that key is ignored. */
  playbackRate: number;
  /** Day and Week playback skip stops and parking. */
  skipStops: boolean;
  follow: boolean;
  dockZoom: DockZoom;
}

const DEFAULT_WORKSPACE_PREFS: WorkspacePrefs = {
  showTimeline: true,
  showDock: true,
  showDetails: true,
  layers: { route: true, points: false, speed: false, stops: true, parking: true, geofences: true },
  mapType: 'street',
  timelineFilter: 'all',
  hideStopsUnderMinutes: DEFAULT_HIDE_STOPS_MINUTES,
  playbackRate: DEFAULT_PLAYBACK_RATE,
  skipStops: true,
  follow: true,
  dockZoom: 'day'
};

function readWorkspacePrefs(): WorkspacePrefs {
  try {
    const raw = localStorage.getItem(WORKSPACE_PREFS_KEY);
    const saved = raw ? (JSON.parse(raw) as Partial<WorkspacePrefs>) : {};
    const prefs: WorkspacePrefs = {
      ...DEFAULT_WORKSPACE_PREFS,
      ...saved,
      layers: { ...DEFAULT_WORKSPACE_PREFS.layers, ...(saved.layers ?? {}) }
    };
    if (!HIDE_STOP_OPTIONS.some(o => o.minutes === prefs.hideStopsUnderMinutes)) {
      prefs.hideStopsUnderMinutes = DEFAULT_HIDE_STOPS_MINUTES;
    }
    if (!PLAYBACK_RATES.includes(prefs.playbackRate)) prefs.playbackRate = DEFAULT_PLAYBACK_RATE;
    if (typeof prefs.skipStops !== 'boolean') prefs.skipStops = true;
    if (!['all', 'trips', 'stops'].includes(prefs.timelineFilter)) prefs.timelineFilter = 'all';
    if (!['trip', 'day', 'week'].includes(prefs.dockZoom)) prefs.dockZoom = 'day';
    if (prefs.mapType !== 'satellite') prefs.mapType = 'street';
    return prefs;
  } catch {
    return { ...DEFAULT_WORKSPACE_PREFS, layers: { ...DEFAULT_WORKSPACE_PREFS.layers } };
  }
}

function storeWorkspacePrefs(prefs: WorkspacePrefs): void {
  try {
    localStorage.setItem(WORKSPACE_PREFS_KEY, JSON.stringify(prefs));
  } catch {
    /* storage unavailable (private window) — the layout just does not persist */
  }
}

interface DockView {
  range: TimeRange | null;
  blocks: ActivityBlock[];
  outside: { leftPercent: number; widthPercent: number }[];
  speedLine: string;
  speedArea: string;
  ticks: AxisTick[];
  maxSpeedLabel: string;
  chartLabel: string;
}

const EMPTY_DOCK: DockView = {
  range: null,
  blocks: [],
  outside: [],
  speedLine: '',
  speedArea: '',
  ticks: [],
  maxSpeedLabel: '',
  chartLabel: 'Speed over time'
};

/** The vehicle at the playhead, as the Point details panel shows it. */
interface DetailView {
  atMs: number | null;
  latitude: number | null;
  longitude: number | null;
  speedKmh: number | null;
  headingLabel: string;
  fix: TripReplayPosition | null;
  address: string;
  addressSecondary: string | null;
  motion: string;
  drivenSoFarKm: number | null;
}

const EMPTY_DETAIL: DetailView = {
  atMs: null,
  latitude: null,
  longitude: null,
  speedKmh: null,
  headingLabel: '—',
  fix: null,
  address: '',
  addressSecondary: null,
  motion: '—',
  drivenSoFarKm: null
};

/**
 * Fleet Tracking → History workspace.
 *
 * One vehicle, one period: a summary strip, a day-by-day timeline of trips and standstills, the
 * map with a playback dock, and the vehicle at the playhead. Every figure comes from the API's
 * `timeline` (one calculation), so the strip, the day rows and the trip rows always agree.
 * The page owns the selection and the playhead; the five child components only display.
 */
@Component({
  standalone: false,
  selector: 'app-gps-history',
  templateUrl: './gps-history.component.html',
  styleUrls: ['./gps-history.component.scss']
})
export class GpsHistoryComponent implements OnInit, OnDestroy {
  @ViewChild(HistoryMapComponent) historyMap?: HistoryMapComponent;

  vehicles: VehicleListItem[] = [];
  devices: GpsDevice[] = [];
  vehicleId: number | null = null;
  vehicleSearch = '';
  from = '';
  to = '';
  preset: TripDatePreset = 'last7Days';
  bundle: HistoryReplayBundle | null = null;
  /** Gps.DeviceManage — the same permission that gates the Devices page. */
  readonly canViewFullImei: boolean;
  rawPositions: TripReplayPosition[] = [];
  loading = false;
  loadingTimedOut = false;
  loadAttempt = 0;
  error = '';
  noData = false;
  recentVehicleIds: number[] = [];

  readonly presets = HISTORY_DATE_PRESETS;
  readonly periodButtons = HISTORY_PERIOD_BUTTONS;
  readonly quickPicks = HISTORY_PERIOD_QUICK_PICKS;
  readonly compareMax = HISTORY_COMPARE_MAX_VEHICLES;
  readonly layerChips: { key: LayerKey; label: string }[] = [
    { key: 'route', label: 'Route' },
    { key: 'points', label: 'GPS points' },
    { key: 'speed', label: 'Speed' },
    { key: 'stops', label: 'Stops' },
    { key: 'parking', label: 'Parking' },
    { key: 'geofences', label: 'Geofences' }
  ];
  readonly rangePositions: ConnectedPosition[] = [
    { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 6 },
    { originX: 'end', originY: 'bottom', overlayX: 'end', overlayY: 'top', offsetY: 6 }
  ];

  // ---- workspace state ------------------------------------------------------------------------

  layers: HistoryMapLayers = { ...DEFAULT_WORKSPACE_PREFS.layers };
  mapType: HistoryMapType = 'street';
  showTimeline = true;
  showDock = true;
  showDetails = true;
  timelineFilter: TimelineFilter = 'all';
  hideStopsUnderMinutes = DEFAULT_HIDE_STOPS_MINUTES;
  openDays = new Set<string>();
  selectedId: string | null = null;
  /** The instant the dock, the map marker and the details panel show. */
  playheadMs: number | null = null;
  playing = false;
  rate = DEFAULT_PLAYBACK_RATE;
  skipStops = true;
  follow = true;
  dockZoom: DockZoom = 'day';

  /** Custom range picker. Drafts are company-zone wall time, like `from` and `to`. */
  rangeOpen = false;
  draftFrom = '';
  draftTo = '';
  rangeError = '';

  geofences: Geofence[] = [];

  // ---- what the template shows, rebuilt only when its inputs change ----------------------------

  /** Route drawn and played: the API's thinned route, in time order. */
  route: TripReplayPosition[] = [];
  windowRange: TimeRange | null = null;
  kpis: HistoryKpi[] = buildKpis(null);
  timelineRows: TimelineRow[] = [];
  dock: DockView = EMPTY_DOCK;
  playheadPercent: number | null = null;
  dockDateLabel = '';
  dockClockLabel = '—';
  dockSpeed: number | null = null;
  vehicleMarker: HistoryMapVehicle | null = null;
  detail: DetailView = EMPTY_DETAIL;
  activeDate: string | null = null;
  recentCards: HistoryVehicleCard[] = [];
  vehicleCards: HistoryVehicleCard[] = [];
  rangeFromLabel = '—';
  rangeToLabel = '—';
  zoneLabel = '';

  private routeTimes: number[] = [];
  private playTimer?: ReturnType<typeof setInterval>;
  private playEndMs = 0;
  /** Where the last playback ran out; the button reads Replay while the playhead is still there. */
  private finishedAtMs: number | null = null;
  private lastTickAt = 0;
  private addressTimer?: ReturnType<typeof setTimeout>;
  private geofenceScope: number | null | undefined | 'none' = 'none';

  private loadSub?: Subscription;
  private timeoutTimer?: ReturnType<typeof setTimeout>;
  private geocodeSub?: Subscription;
  private readonly geocodeRequests$ = new Subject<{
    lat: number;
    lng: number;
    forceRefresh: boolean;
    apply: (address: string) => void;
  }>();
  private readonly addressCache = new Map<string, string | null>();
  private readonly addressMetaCache = new Map<string, {
    primaryAddress?: string;
    nearbyPlaceName?: string;
    localityLine?: string;
    addressQuality?: 'exact' | 'nearby' | 'coarse' | string;
    formattedAddress?: string;
    placeName?: string;
  } | null>();
  private resolvingAddressKey: string | null = null;
  private readonly rawHistoryCache = new Map<string, TripReplayPosition[]>();
  private rawLoadSub?: Subscription;
  private rawCacheKey: string | null = null;

  /** Live fleet rows keyed by vehicle — refreshed for staleness & stationary filters. */
  liveLocations = new Map<number, VehicleLocation>();
  clockNow = Date.now();

  filterStationaryOverOneHour = false;
  alertStationary24h = false;
  /** Left over from the previous layout; written back unchanged so the stored prefs keep their shape. */
  private storedHideRecordsBefore: string | null = null;

  compareSelectedIds = new Set<number>();
  compareOverlays: HistoryMapCompareRoute[] = [];
  compareLoading = false;

  private liveLocSub?: Subscription;
  private clockSub?: Subscription;
  private compareLoadSub?: Subscription;
  private scopeSub?: Subscription;
  private vehiclesSub?: Subscription;
  private geofenceSub?: Subscription;

  constructor(
    private gps: GpsTrackingService,
    private scopeFilter: GpsScopeFilterService,
    private exportService: ExportService,
    private toast: UiToastService,
    private route$: ActivatedRoute,
    private router: Router,
    private auth: AuthService
  ) {
    this.canViewFullImei = this.auth.hasPermission('Gps.DeviceManage');
  }

  private get tenantScopeId(): number | null | undefined {
    return this.scopeFilter.tenantScopeId;
  }

  /** Super Admin with no company — empty fleet until a company is picked. */
  get needsCompanySelection(): boolean {
    return !this.scopeFilter.isFleetScopeReady;
  }

  ngOnInit(): void {
    const prefs = readHistoryPagePrefs();
    this.filterStationaryOverOneHour = prefs.stationaryOverOneHour;
    this.storedHideRecordsBefore = prefs.hideRecordsBefore ?? null;
    this.alertStationary24h = prefs.alertStationary24h;

    const workspace = readWorkspacePrefs();
    this.showTimeline = workspace.showTimeline;
    this.showDock = workspace.showDock;
    this.showDetails = workspace.showDetails;
    this.layers = workspace.layers;
    this.mapType = workspace.mapType;
    this.timelineFilter = workspace.timelineFilter;
    this.hideStopsUnderMinutes = workspace.hideStopsUnderMinutes;
    this.rate = workspace.playbackRate;
    this.skipStops = workspace.skipStops;
    this.follow = workspace.follow;
    this.dockZoom = workspace.dockZoom;

    this.loadRecentVehicles();
    this.applyPreset(this.preset, true);
    this.refreshRangeLabels();
    this.refreshLiveLocations();
    this.clockSub = interval(30_000).subscribe(tick => {
      this.clockNow = Date.now();
      this.refreshVehicleCards();
      if (tick > 0 && tick % 4 === 0) this.refreshLiveLocations();
    });

    this.vehiclesSub = this.scopeFilter.vehicles$.subscribe(list => {
      this.vehicles = list;
      if (this.vehicleId != null && !list.some(v => v.id === this.vehicleId)) {
        this.clearHistorySelection();
      }
      if (!this.vehicleId) this.readQueryParams();
      this.refreshVehicleCards();
    });

    this.scopeSub = this.scopeFilter.scope$.pipe(skip(1)).subscribe(state => {
      this.geofences = [];
      this.geofenceScope = 'none';
      if (!this.scopeFilter.isFleetScopeReady) {
        this.devices = [];
        this.liveLocations = new Map();
        this.clearHistorySelection();
        this.refreshVehicleCards();
        return;
      }
      this.loadDevices();
      this.refreshLiveLocations();
      if (state.vehicleId != null) {
        if (this.vehicleId !== state.vehicleId) {
          this.applyVehicleSelection(state.vehicleId, false);
        }
      } else {
        // Company cleared/changed with no vehicle — drop stale route for other tenant.
        this.clearHistorySelection();
      }
      this.refreshVehicleCards();
    });

    this.geocodeSub = this.geocodeRequests$.pipe(
      // Queue position geocodes (concurrency 2) instead of debounce+switchMap, which cancelled
      // all but the last request.
      mergeMap(req => {
        const key = this.addressCacheKey(req.lat, req.lng);
        if (!req.forceRefresh && this.addressCache.has(key)) {
          const cached = this.addressCache.get(key);
          if (cached) req.apply(cached);
          if (this.resolvingAddressKey === key) this.resolvingAddressKey = null;
          return of(null);
        }
        this.resolvingAddressKey = key;
        return this.gps.reverseGeocode(req.lat, req.lng, req.forceRefresh).pipe(
          tap(info => {
            const address = this.resolveAddressFromGeocode(info) || null;
            this.addressCache.set(key, address);
            this.addressMetaCache.set(key, info ?? null);
            if (address) req.apply(address);
            if (this.resolvingAddressKey === key) this.resolvingAddressKey = null;
          }),
          catchError(() => {
            if (!this.addressCache.has(key)) this.addressCache.set(key, null);
            if (this.resolvingAddressKey === key) this.resolvingAddressKey = null;
            return of(null);
          })
        );
      }, 2)
    ).subscribe(() => this.refreshPlayhead(false));

    this.vehicles = this.scopeFilter.vehicles;
    this.readQueryParams();

    if (this.scopeFilter.isFleetScopeReady) {
      this.loadDevices();
    } else {
      this.devices = [];
    }
    this.refreshVehicleCards();
  }

  ngOnDestroy(): void {
    this.stopPlayback();
    this.clearLoadTimers();
    if (this.addressTimer) clearTimeout(this.addressTimer);
    this.loadSub?.unsubscribe();
    this.rawLoadSub?.unsubscribe();
    this.geocodeSub?.unsubscribe();
    this.liveLocSub?.unsubscribe();
    this.clockSub?.unsubscribe();
    this.compareLoadSub?.unsubscribe();
    this.scopeSub?.unsubscribe();
    this.vehiclesSub?.unsubscribe();
    this.geofenceSub?.unsubscribe();
    this.geocodeRequests$.complete();
  }

  // ---- what is loaded ---------------------------------------------------------------------------

  get timeline(): HistoryTimeline | null {
    return this.bundle?.timeline ?? null;
  }

  /** Zone the vehicle's times are shown in: its branch → company → platform default. */
  get zone(): string | null {
    return this.bundle?.timeZoneId ?? null;
  }

  get selectedSegment(): HistoryTimelineSegment | null {
    if (!this.selectedId) return null;
    return this.timeline?.segments.find(s => s.id === this.selectedId) ?? null;
  }

  get selectedVehicle(): VehicleListItem | undefined {
    return this.vehicleId ? this.vehicles.find(v => v.id === this.vehicleId) : undefined;
  }

  get canLoad(): boolean {
    return !!this.vehicleId && !!this.from && !!this.to && !this.loading;
  }

  /** A vehicle is chosen but its route has not been asked for yet (plain page open). */
  get awaitingFirstLoad(): boolean {
    return this.canLoad && !this.bundle && !this.noData && !this.error;
  }

  get loadHint(): string {
    if (this.needsCompanySelection) return 'Select a company in the header to view vehicles.';
    if (!this.vehicleId) return 'Select a vehicle to see its route.';
    if (this.awaitingFirstLoad) return 'Press Load route to fetch this vehicle\'s GPS history.';
    return '';
  }

  get mapEmptyHint(): string {
    if (this.needsCompanySelection) return 'Select a company in the header to view vehicles.';
    if (!this.vehicleId) return 'Select a vehicle to see its route.';
    return 'Press Load route to fetch this vehicle\'s GPS history.';
  }

  get timelineState(): TimelinePanelState {
    if (this.loading) return 'loading';
    if (this.noData) return 'noData';
    if (!this.bundle) return 'idle';
    return this.timeline ? 'ready' : 'unavailable';
  }

  get timelineMessage(): string {
    switch (this.timelineState) {
      case 'noData':
        return 'No GPS history in this period.';
      case 'unavailable':
        return 'The timeline needs the updated API. The route and the figures above still work.';
      case 'idle':
        return 'Trips and stops appear here once a route is loaded.';
      default:
        return '';
    }
  }

  get weekZoomAvailable(): boolean {
    return !!this.windowRange && weekZoomAvailable(this.windowRange);
  }

  /** A trip to play; without a timeline (older API) the whole route plays. */
  get canPlay(): boolean {
    if (this.timeline) return this.timeline.totals.tripCount > 0;
    return this.route.length > 1;
  }

  /** A trip after the playhead for "Next trip". */
  get hasNextTrip(): boolean {
    return !!this.timeline && this.playheadMs != null && tripAfter(this.timeline, this.playheadMs) != null;
  }

  /** Playback ran to its end and the playhead has not moved since: Play starts over, shown as Replay. */
  get atPlayEnd(): boolean {
    return !this.playing && this.finishedAtMs != null && this.playheadMs === this.finishedAtMs;
  }

  get hasStandstills(): boolean {
    return !!this.timeline && this.timeline.totals.stopCount > 0;
  }

  /** Period figures the strip does not carry, shown small in Point details. */
  get engineHours(): number | null {
    return this.bundle?.statistics?.engineHours ?? this.bundle?.summary?.engineHours ?? null;
  }

  get gpsPointCount(): number | null {
    if (!this.bundle) return null;
    return this.rawPositions.length || this.route.length;
  }

  get showOfflineHistoryBanner(): boolean {
    const v = this.selectedVehicle;
    if (!v) return false;
    return isOfflineOrCachedTracker(
      v,
      this.liveLocations.get(v.id),
      !!(v.hasGpsDevice || v.gpsImei || this.deviceFor(v.id))
    );
  }

  get compareSelectionCount(): number {
    return this.compareSelectedIds.size;
  }

  /** The custom range picker works in company time, like every date picker in the ERP. */
  get companyZoneLabel(): string {
    return zoneShortLabel(null);
  }

  get summaryLabel(): string {
    return `Period summary, ${this.rangeFromLabel} to ${this.rangeToLabel}${this.zoneLabel ? ' ' + this.zoneLabel : ''}`;
  }

  get rangeButtonLabel(): string {
    return `Custom dates and time zone: ${this.rangeFromLabel} to ${this.rangeToLabel}${this.zoneLabel ? ', ' + this.zoneLabel : ''}`;
  }

  /**
   * Why the "All vehicles" list is empty. Never "no match" while the search box is empty:
   * with no search, an empty list means every vehicle is already shown under Recent.
   */
  get vehicleListEmptyMessage(): string | null {
    if (this.needsCompanySelection) return 'Select a company to view vehicles.';
    if (this.vehicleCards.length) return null;
    if (this.vehicleSearch.trim()) return 'No vehicles match your search.';
    if (this.filterStationaryOverOneHour) return 'No vehicles have been stationary for over an hour.';
    if (!this.vehicles.length) return 'No vehicles in this company yet.';
    return null;
  }

  get filteredVehicles(): VehicleListItem[] {
    const q = this.vehicleSearch.trim().toLowerCase();
    // Prefer GPS-linked vehicles; de-dupe by plate; don't repeat "Recent" entries.
    const recent = new Set(this.vehicleSearch ? [] : this.recentVehicleIds);
    const seenPlates = new Set<string>();
    const ranked = [...this.vehicles].sort((a, b) => {
      const aGps = a.hasGpsDevice || !!a.gpsImei || !!this.deviceFor(a.id) ? 0 : 1;
      const bGps = b.hasGpsDevice || !!b.gpsImei || !!this.deviceFor(b.id) ? 0 : 1;
      if (aGps !== bGps) return aGps - bGps;
      return a.name.localeCompare(b.name);
    });

    const result: VehicleListItem[] = [];
    for (const v of ranked) {
      if (recent.has(v.id)) continue;
      const plate = (v.registrationNumber || '').toLowerCase();
      if (plate && seenPlates.has(plate)) {
        // Keep the GPS-linked copy when plates collide.
        continue;
      }
      if (q && !this.vehicleSearchHaystack(v).includes(q)) continue;
      if (plate) seenPlates.add(plate);
      result.push(v);
    }
    if (this.filterStationaryOverOneHour) {
      return result.filter(v =>
        isVehicleStationaryNow(v, this.liveLocations.get(v.id), this.clockNow));
    }
    return result;
  }

  get recentVehicles(): VehicleListItem[] {
    const seenPlates = new Set<string>();
    return this.recentVehicleIds
      .map(id => this.vehicles.find(v => v.id === id))
      .filter((v): v is VehicleListItem => {
        if (!v) return false;
        const plate = (v.registrationNumber || '').toLowerCase();
        if (plate && seenPlates.has(plate)) return false;
        if (plate) seenPlates.add(plate);
        return true;
      });
  }

  // ---- vehicle and period -------------------------------------------------------------------------

  selectVehicle(id: number): void {
    this.applyVehicleSelection(id, true);
  }

  /** @param syncGlobal when true, push selection into the shared GPS scope. */
  private applyVehicleSelection(id: number, syncGlobal: boolean): void {
    this.vehicleId = id;
    this.compareSelectedIds.add(id);
    this.compareSelectedIds = new Set(this.compareSelectedIds);
    this.pushRecentVehicle(id);
    if (syncGlobal) this.scopeFilter.setVehicle(id);
    this.updateUrl();
    this.refreshVehicleCards();
    this.load();
  }

  onVehicleSearch(query: string): void {
    this.vehicleSearch = query;
    this.refreshVehicleCards();
  }

  setPreset(id: TripDatePreset): void {
    this.preset = id;
    this.applyPreset(id, true);
    this.rangeOpen = false;
    this.refreshRangeLabels();
    this.updateUrl();
    if (this.vehicleId) this.load();
  }

  toggleRange(): void {
    this.rangeOpen = !this.rangeOpen;
    if (this.rangeOpen) {
      this.draftFrom = this.from;
      this.draftTo = this.to;
      this.rangeError = '';
    }
  }

  closeRange(): void {
    this.rangeOpen = false;
  }

  onRangeKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.closeRange();
    }
  }

  applyCustomRange(): void {
    const from = parseDatetimeLocalInput(this.draftFrom);
    const to = parseDatetimeLocalInput(this.draftTo);
    if (Number.isNaN(from.getTime()) || Number.isNaN(to.getTime())) {
      this.rangeError = 'Choose both a start and an end.';
      return;
    }
    if (from >= to) {
      this.rangeError = 'The start must be before the end.';
      return;
    }
    if (to.getTime() - from.getTime() > 366 * 24 * 60 * 60 * 1000) {
      this.rangeError = 'The range cannot exceed 366 days.';
      return;
    }
    this.from = this.draftFrom;
    this.to = this.draftTo;
    this.preset = 'custom';
    this.rangeOpen = false;
    this.refreshRangeLabels();
    this.updateUrl();
    if (this.vehicleId) this.load();
  }

  load(): void {
    if (!this.vehicleId) return;
    const fromDate = parseDatetimeLocalInput(this.from);
    const toDate = parseDatetimeLocalInput(this.to);
    if (fromDate > toDate) {
      this.error = 'Start date must be before end date.';
      return;
    }
    if (toDate.getTime() - fromDate.getTime() > 366 * 24 * 60 * 60 * 1000) {
      this.error = 'Date range cannot exceed 366 days.';
      return;
    }

    this.stopPlayback();
    this.clearLoadTimers();
    this.loadSub?.unsubscribe();
    this.rawLoadSub?.unsubscribe();
    this.loading = true;
    this.loadingTimedOut = false;
    this.error = '';
    this.noData = false;
    this.resetLoaded();
    this.rawPositions = [];
    this.rawCacheKey = null;
    this.loadAttempt++;

    this.timeoutTimer = setTimeout(() => {
      if (this.loading) {
        this.loadingTimedOut = true;
      }
    }, LOAD_TIMEOUT_MS);

    this.loadSub = this.gps.getHistoryReplay(
      this.vehicleId,
      fromDate,
      toDate,
      {
        ...this.replayOptionsForWindow(fromDate, toDate),
        tenantScopeId: this.tenantScopeId
      }
    ).subscribe({
      next: bundle => {
        this.finishLoading();
        const hasRoute = (bundle.route?.length ?? 0) > 0 || (bundle.playback?.length ?? 0) > 0;
        if (!hasRoute) {
          this.noData = true;
          return;
        }
        this.showLoaded(bundle, fromDate, toDate);
        if (this.layers.points) void this.ensureRawPositionsLoaded(fromDate, toDate);
        if (this.layers.geofences) this.ensureGeofencesLoaded();
        this.reloadCompareRoutes(fromDate, toDate);
      },
      error: err => {
        this.finishLoading();
        const status = (err as { status?: number })?.status;
        if (status === 0) {
          this.error =
            'History request was interrupted or timed out. Large ranges can take up to a minute on first load — please Retry.';
          return;
        }
        this.error = err?.error?.message ?? 'Failed to load history.';
      }
    });
  }

  retryLoad(): void {
    this.loadingTimedOut = false;
    this.load();
  }

  private showLoaded(bundle: HistoryReplayBundle, fromDate: Date, toDate: Date): void {
    this.bundle = bundle;
    this.route = bundle.route?.length ? bundle.route : bundle.playback ?? [];
    this.routeTimes = timesOf(this.route);

    // The API clamps a range that ends in the future to "now"; its bounds are the period shown.
    const start = toInstant(bundle.from)?.getTime() ?? fromDate.getTime();
    const end = toInstant(bundle.to)?.getTime() ?? Math.min(toDate.getTime(), Date.now());
    this.windowRange = { startMs: start, endMs: Math.max(start, end) };

    const openDay = bundle.timeline ? defaultOpenDay(bundle.timeline) : null;
    this.openDays = new Set(openDay ? [openDay] : []);
    this.selectedId = null;

    // The playhead starts at the latest fix: where the vehicle was at the end of the period.
    const latest = this.routeTimes[this.routeTimes.length - 1];
    this.playheadMs = Number.isFinite(latest) ? latest : this.windowRange.endMs;

    this.refreshRangeLabels();
    this.refreshKpis();
    this.refreshRows();
    this.refreshDock(true);
    this.refreshPlayhead(true);
  }

  private resetLoaded(): void {
    this.bundle = null;
    this.route = [];
    this.routeTimes = [];
    this.windowRange = null;
    this.selectedId = null;
    this.playheadMs = null;
    this.finishedAtMs = null;
    this.openDays = new Set();
    this.timelineRows = [];
    this.kpis = buildKpis(null);
    this.dock = EMPTY_DOCK;
    this.playheadPercent = null;
    this.dockDateLabel = '';
    this.dockClockLabel = '—';
    this.dockSpeed = null;
    this.vehicleMarker = null;
    this.detail = EMPTY_DETAIL;
    this.activeDate = null;
  }

  private clearHistorySelection(): void {
    this.stopPlayback();
    this.vehicleId = null;
    this.resetLoaded();
    this.rawPositions = [];
    this.noData = false;
    this.error = '';
    this.compareOverlays = [];
  }

  // ---- strip, rows, dock, playhead: rebuilt on demand -----------------------------------------------

  private refreshKpis(): void {
    this.kpis = buildKpis(this.timeline?.totals ?? this.legacyTotals());
  }

  /**
   * An older API sends no timeline. The strip then shows the server's legacy totals, which add up
   * the same way (moving + non-moving = period).
   */
  private legacyTotals(): HistoryTimelineTotals | null {
    const b = this.bundle;
    if (!b) return null;
    const stats = b.statistics;
    const driving = stats?.drivingMinutes ?? b.summary?.drivingMinutes ?? 0;
    const stopped = Math.max(0, stats?.idleMinutes ?? 0);
    const parking = b.parking?.length ?? (b.stops ?? []).filter(s => s.durationMinutes >= 120).length;
    return {
      distanceKm: b.displaySummary?.distance ?? b.mileageKm ?? b.summary?.distanceKm ?? 0,
      drivingMinutes: driving,
      stoppedMinutes: stopped,
      windowMinutes: driving + stopped,
      tripCount: stats?.tripCount ?? 0,
      stopCount: b.displaySummary?.stops ?? stats?.stopCount ?? b.stops?.length ?? 0,
      parkingCount: b.displaySummary?.parking ?? parking,
      maxSpeedKmh: b.displaySummary?.maxSpeed ?? stats?.maxSpeedKmh ?? b.summary?.maxSpeedKmh ?? 0,
      avgSpeedKmh: stats?.avgSpeedKmh ?? b.summary?.avgSpeedKmh ?? 0,
      noSignalMinutes: 0
    };
  }

  private refreshRows(): void {
    const timeline = this.timeline;
    this.timelineRows = timeline
      ? buildTimelineRows(timeline, {
          filter: this.timelineFilter,
          hideStopsUnderMinutes: this.hideStopsUnderMinutes,
          openDays: this.openDays,
          selectedId: this.selectedId,
          zone: this.zone
        })
      : [];
  }

  /** Recomputes what the dock shows. Skipped when its range did not move (every playback tick). */
  private refreshDock(force = false): void {
    const window = this.windowRange;
    const playhead = this.playheadMs;
    if (!window || playhead == null || !this.route.length) {
      this.dock = EMPTY_DOCK;
      return;
    }

    const selected = this.selectedSegment;
    const selectedRange = selected ? { startMs: segmentStartMs(selected), endMs: segmentEndMs(selected) } : null;
    const range = dockRange(this.dockZoom, playhead, window, selectedRange, this.zone);
    const current = this.dock.range;
    if (!force && current && current.startMs === range.startMs && current.endMs === range.endMs) return;

    const timeline = this.timeline;
    const maxSpeed = Math.round(Number(timeline?.totals.maxSpeedKmh ?? this.legacyTotals()?.maxSpeedKmh ?? 0));
    // Without a timeline the whole route is charted as one stretch.
    const trips = timeline
      ? timeline.segments.filter(s => s.kind === 'trip')
      : [this.wholeRouteAsTrip()];
    const paths = speedPaths(this.route, this.routeTimes, trips, range, maxSpeed);

    this.dock = {
      range,
      blocks: timeline ? activityBlocks(timeline.segments, range, this.zone) : [],
      outside: outsideBlocks(range, window),
      speedLine: paths.line,
      speedArea: paths.area,
      ticks: axisTicks(range, this.zone),
      maxSpeedLabel: maxSpeed > 0 ? `${maxSpeed} km/h` : '',
      chartLabel:
        `Speed from ${formatDay(range.startMs, this.zone)} ${formatClock(range.startMs, this.zone)} ` +
        `to ${formatDay(range.endMs, this.zone)} ${formatClock(range.endMs, this.zone)}` +
        (maxSpeed > 0 ? `, peak in the period ${maxSpeed} km/h` : '')
    };
  }

  private wholeRouteAsTrip(): HistoryTimelineSegment {
    const first = this.route[0];
    const last = this.route[this.route.length - 1];
    return {
      id: 'route',
      kind: 'trip',
      date: '',
      startTime: first.timestamp,
      endTime: last.timestamp,
      durationMinutes: 0,
      startLatitude: first.latitude,
      startLongitude: first.longitude,
      endLatitude: last.latitude,
      endLongitude: last.longitude
    };
  }

  /**
   * Everything that follows the playhead: marker, clock, dock position and the details panel.
   * @param lookUpAddress ask for the address of the fix when it has none (not while playing).
   */
  private refreshPlayhead(lookUpAddress: boolean): void {
    const ms = this.playheadMs;
    const at = ms == null ? null : positionAt(this.route, this.routeTimes, ms);
    if (ms == null || !at) {
      this.playheadPercent = null;
      this.vehicleMarker = null;
      this.detail = EMPTY_DETAIL;
      return;
    }

    const timeline = this.timeline;
    const selected = this.selectedSegment;
    const here = timeline ? segmentAt(timeline, ms) : null;
    const fix = this.route[at.fixIndex] ?? null;
    // A selected standstill is shown where the vehicle stood, ringed on the map, not as a moving marker.
    const standing = selected && selected.kind !== 'trip' ? selected : null;
    const latitude = standing ? standing.startLatitude : at.latitude;
    const longitude = standing ? standing.startLongitude : at.longitude;
    const speed = standing || (here && here.kind !== 'trip') ? 0 : at.speedKmh;
    const heading = at.heading ?? fix?.heading ?? null;

    this.vehicleMarker = standing ? null : { latitude, longitude, heading };
    this.activeDate = clockParts(ms, this.zone)?.dateKey ?? null;
    this.dockDateLabel = formatDay(ms, this.zone);
    this.dockClockLabel = formatClock(ms, this.zone, true);
    this.dockSpeed = speed;
    const range = this.dock.range;
    this.playheadPercent = range && ms >= range.startMs && ms <= range.endMs ? timeToPercent(ms, range) : null;

    const place = standing ?? (here && here.kind !== 'trip' ? here : null);
    const address = place?.startAddress?.trim() || this.addressDuringTrip(here, fix, ms);
    const lines = splitDisplayAddress(address);
    this.detail = {
      atMs: ms,
      latitude,
      longitude,
      speedKmh: speed,
      headingLabel: speed > 0 ? this.headingLabel(heading) : '—',
      fix,
      address: lines.primary || address || 'Address unavailable',
      addressSecondary: lines.primary ? lines.secondary : null,
      motion: this.motionAt(here, fix, ms),
      drivenSoFarKm: timeline ? drivenSoFarKm(timeline, this.route, ms) : null
    };

    if (this.addressTimer) clearTimeout(this.addressTimer);
    if (lookUpAddress && fix && !place) {
      this.addressTimer = setTimeout(() => this.ensureAddress(fix), ADDRESS_LOOKUP_DELAY_MS);
    }
  }

  /**
   * The address of the fix; until it has one, a trip reads "Near <where it left from>" for its
   * first half and "Near <where it is going>" for the second.
   */
  private addressDuringTrip(
    here: HistoryTimelineSegment | null,
    fix: TripReplayPosition | null,
    ms: number
  ): string {
    const own = fix ? this.addressLabel(fix) : '';
    if (!here || here.kind !== 'trip' || (own && own !== 'Address unavailable' && own !== 'Resolving…')) {
      return own;
    }
    const half = (segmentStartMs(here) + segmentEndMs(here)) / 2;
    const near = placeLabel(ms < half ? here.startAddress : here.endAddress);
    return near ? `Near ${near}` : own;
  }

  private motionAt(
    here: HistoryTimelineSegment | null,
    fix: TripReplayPosition | null,
    ms: number
  ): string {
    if (!here) return this.motionLabel(fix);
    if (here.kind === 'parking') return 'Parked';
    if (here.kind === 'stop') return fix?.ignition === true ? 'Idling' : 'Stopped';
    const paused = (here.briefStops ?? []).some(b =>
      ms >= (toInstant(b.startTime)?.getTime() ?? 0) && ms <= (toInstant(b.endTime)?.getTime() ?? 0));
    return paused ? 'Paused' : 'Moving';
  }

  private refreshRangeLabels(): void {
    this.rangeFromLabel = this.from ? formatRangeEdge(parseDatetimeLocalInput(this.from), this.zone) : '—';
    this.rangeToLabel = this.to ? formatRangeEdge(parseDatetimeLocalInput(this.to), this.zone) : '—';
    this.zoneLabel = zoneShortLabel(this.zone);
  }

  private refreshVehicleCards(): void {
    this.recentCards = this.vehicleSearch ? [] : this.recentVehicles.map(v => this.toCard(v));
    this.vehicleCards = this.filteredVehicles.map(v => this.toCard(v));
  }

  private toCard(v: VehicleListItem): HistoryVehicleCard {
    const imei = this.imeiLabel(v.id);
    return {
      id: v.id,
      name: v.name,
      plate: v.registrationNumber || '',
      driver: this.driverLabel(v.id),
      statusLabel: this.vehicleStatusLabel(v),
      statusClass: this.vehicleStatusClass(v),
      lastKnown: this.lastKnownLabel(v.id),
      cached: this.isVehicleOfflineCached(v),
      tracker: this.trackerLabel(v.id),
      imei: imei === '—' ? null : imei,
      compare: this.compareSelectedIds.has(v.id),
      active: this.vehicleId === v.id
    };
  }

  // ---- timeline -----------------------------------------------------------------------------------

  setTimelineFilter(filter: TimelineFilter): void {
    this.timelineFilter = filter;
    this.persistWorkspace();
    this.refreshRows();
  }

  setHideStops(minutes: number): void {
    this.hideStopsUnderMinutes = minutes;
    this.persistWorkspace();
    this.refreshRows();
  }

  toggleDay(date: string): void {
    const days = new Set(this.openDays);
    if (!days.delete(date)) days.add(date);
    this.openDays = days;
    this.refreshRows();
  }

  /**
   * Select a trip or a standstill: the playhead goes to its start, and it is highlighted in the
   * list, on the dock and on the map.
   * @param focusMap bring it into view on the map (not when it was picked on the map itself).
   */
  selectSegment(id: string, focusMap = true): void {
    const segment = this.timeline?.segments.find(s => s.id === id);
    const window = this.windowRange;
    if (!segment || !window) return;

    this.stopPlayback();
    this.selectedId = id;
    if (!this.openDays.has(segment.date)) {
      this.openDays = new Set(this.openDays).add(segment.date);
    }
    this.playheadMs = Math.min(window.endMs, Math.max(window.startMs, segmentStartMs(segment)));
    this.refreshRows();
    this.refreshDock();
    this.refreshPlayhead(true);
    if (focusMap) this.historyMap?.focusSegment(id);
  }

  selectBriefStop(event: { tripId: string; atMs: number }): void {
    this.selectSegment(event.tripId);
    this.seekTo(event.atMs);
  }

  // ---- playback -------------------------------------------------------------------------------------

  togglePlay(): void {
    if (this.playing) {
      this.stopPlayback();
      return;
    }
    const timeline = this.timeline;
    const window = this.windowRange;
    if (!window || this.playheadMs == null) return;
    if (!timeline) {
      this.playWholeRoute();
      return;
    }
    if (this.dockZoom === 'trip') this.playTrip(timeline, window);
    else this.playPeriod(timeline);
  }

  /** Trip zoom: the selected trip; from a standstill (or nothing selected) the next trip, else the last one. */
  private playTrip(timeline: HistoryTimeline, window: TimeRange): void {
    if (this.playheadMs == null) return;
    const selected = this.selectedSegment;
    const trip = selected?.kind === 'trip'
      ? selected
      : tripAtOrAfter(timeline, this.playheadMs)
        ?? [...timeline.segments].reverse().find(s => s.kind === 'trip')
        ?? null;
    if (!trip) return;

    const start = segmentStartMs(trip);
    const end = Math.min(segmentEndMs(trip), window.endMs);
    let from = this.playheadMs;
    if (from < start || from >= end - 500) from = start;

    if (this.selectedId !== trip.id) {
      this.selectedId = trip.id;
      if (!this.openDays.has(trip.date)) this.openDays = new Set(this.openDays).add(trip.date);
      this.refreshRows();
      this.historyMap?.focusSegment(trip.id);
    }

    this.startTimer(from, end);
  }

  /** Day and Week zoom: every trip from the playhead to the end of the period; the dock follows day by day. */
  private playPeriod(timeline: HistoryTimeline): void {
    const end = this.playEnd();
    if (end == null || this.playheadMs == null) return;
    // Starting over begins at the first trip the dock shows (that day, or the week), else the period's first.
    const shown = this.dock.range;
    const restart =
      (shown ? tripAtOrAfter(timeline, shown.startMs) : null) ?? timeline.segments.find(s => s.kind === 'trip');
    if (!restart) return;

    let from = this.playheadMs >= end - 500 ? segmentStartMs(restart) : this.playheadMs;
    let start = advancePlayhead(from, 0, this.rate, timeline, this.skipStops, end);
    if (start.finished) {
      // Only standstill left before the end: start over.
      from = segmentStartMs(restart);
      start = advancePlayhead(from, 0, this.rate, timeline, this.skipStops, end);
    }
    this.startTimer(start.ms, end);
    this.selectSegmentAtPlayhead();
  }

  /** Where Day and Week playback stops: the period's last fix. */
  private playEnd(): number | null {
    const window = this.windowRange;
    if (!window) return null;
    const lastFix = this.routeTimes[this.routeTimes.length - 1];
    return Number.isFinite(lastFix) ? Math.min(lastFix, window.endMs) : window.endMs;
  }

  /** Older API without a timeline: play from the playhead to the last fix. */
  private playWholeRoute(): void {
    const first = this.routeTimes[0];
    const last = this.routeTimes[this.routeTimes.length - 1];
    if (!Number.isFinite(first) || !Number.isFinite(last) || last <= first || this.playheadMs == null) return;
    let from = this.playheadMs;
    if (from < first || from >= last - 500) from = first;
    this.startTimer(from, last);
  }

  private startTimer(from: number, end: number): void {
    this.playheadMs = from;
    this.playEndMs = end;
    this.playing = true;
    this.lastTickAt = performance.now();
    this.playTimer = setInterval(() => this.playTick(), PLAY_TICK_MS);
    this.refreshDock();
    this.refreshPlayhead(false);
  }

  private playTick(): void {
    if (this.playheadMs == null) {
      this.stopPlayback();
      return;
    }
    const now = performance.now();
    const elapsed = now - this.lastTickAt;
    this.lastTickAt = now;

    const period = this.dockZoom !== 'trip';
    // Stops are skipped only across a period; a single trip plays as recorded.
    const step = advancePlayhead(
      this.playheadMs, elapsed, this.rate, period ? this.timeline : null, this.skipStops, this.playEndMs);
    this.playheadMs = step.ms;
    if (step.finished) {
      this.stopPlayback();
      this.finishedAtMs = step.ms;
    }
    if (period) this.selectSegmentAtPlayhead();
    this.refreshDock();
    this.refreshPlayhead(step.finished);
  }

  /** Whatever the playhead is on becomes the selection, so the timeline row and the dock follow it. */
  private selectSegmentAtPlayhead(): void {
    const at = this.playheadMs;
    if (at == null) return;
    const here = this.timeline ? segmentAt(this.timeline, at) : null;
    const id = here?.id ?? null;
    if (id === this.selectedId) return;
    this.selectedId = id;
    if (here && !this.openDays.has(here.date)) this.openDays = new Set(this.openDays).add(here.date);
    this.refreshRows();
  }

  private stopPlayback(): void {
    this.playing = false;
    if (this.playTimer) {
      clearInterval(this.playTimer);
      this.playTimer = undefined;
    }
  }

  setRate(rate: number): void {
    if (!PLAYBACK_RATES.includes(rate)) return;
    this.rate = rate;
    this.persistWorkspace();
  }

  toggleSkipStops(): void {
    this.skipStops = !this.skipStops;
    this.persistWorkspace();
  }

  /** Select the first trip after the playhead; its start becomes the playhead. */
  nextTrip(): void {
    const timeline = this.timeline;
    if (!timeline || this.playheadMs == null) return;
    const trip = tripAfter(timeline, this.playheadMs);
    if (trip) this.selectSegment(trip.id);
  }

  toggleFollow(): void {
    this.follow = !this.follow;
    this.persistWorkspace();
  }

  setDockZoom(zoom: DockZoom): void {
    // What Play covers depends on the zoom, so a running playback stops rather than change course.
    this.stopPlayback();
    this.dockZoom = zoom;
    this.persistWorkspace();
    this.refreshDock(true);
    this.refreshPlayhead(false);
  }

  /** Previous / next standstill that the list shows at the current "hide stops" setting. */
  stepStandstill(direction: 1 | -1): void {
    const timeline = this.timeline;
    if (!timeline || this.playheadMs == null) return;
    const selected = this.selectedSegment;
    let from = selected && selected.kind !== 'trip' ? segmentStartMs(selected) : this.playheadMs;
    for (let guard = 0; guard < timeline.segments.length; guard++) {
      const target = direction > 0 ? nextStandstill(timeline, from) : previousStandstill(timeline, from);
      if (!target) return;
      if (target.durationMinutes >= this.hideStopsUnderMinutes) {
        this.selectSegment(target.id);
        return;
      }
      from = segmentStartMs(target);
    }
  }

  seekToPercent(percent: number): void {
    if (this.dock.range) this.seekTo(percentToTime(percent, this.dock.range));
  }

  /** Move the playhead; whatever is there becomes the selection. */
  seekTo(ms: number): void {
    const window = this.windowRange;
    if (!window || !Number.isFinite(ms)) return;
    this.stopPlayback();

    let at = Math.min(window.endMs, Math.max(window.startMs, ms));
    const selected = this.selectedSegment;
    if (this.dockZoom === 'trip' && selected) {
      // Zoomed to one segment: stay inside it, so the dock does not jump to its neighbour.
      at = Math.min(segmentEndMs(selected) - 1, Math.max(segmentStartMs(selected), at));
    }

    this.playheadMs = at;
    if (this.dockZoom !== 'trip' || !selected) this.selectSegmentAtPlayhead();
    this.refreshDock();
    this.refreshPlayhead(true);
  }

  // ---- map and panels --------------------------------------------------------------------------------

  toggleLayer(key: LayerKey): void {
    this.layers = { ...this.layers, [key]: !this.layers[key] };
    this.persistWorkspace();
    if (key === 'points' && this.layers.points) void this.ensureRawPositionsLoaded();
    if (key === 'geofences' && this.layers.geofences) this.ensureGeofencesLoaded();
  }

  setMapType(type: HistoryMapType): void {
    this.mapType = type;
    this.persistWorkspace();
  }

  togglePanel(panel: PanelKey): void {
    if (panel === 'timeline') this.showTimeline = !this.showTimeline;
    else if (panel === 'dock') this.showDock = !this.showDock;
    else this.showDetails = !this.showDetails;
    this.persistWorkspace();
  }

  onMapSegmentSelected(id: string): void {
    this.selectSegment(id, false);
  }

  private persistWorkspace(): void {
    storeWorkspacePrefs({
      showTimeline: this.showTimeline,
      showDock: this.showDock,
      showDetails: this.showDetails,
      layers: this.layers,
      mapType: this.mapType,
      timelineFilter: this.timelineFilter,
      hideStopsUnderMinutes: this.hideStopsUnderMinutes,
      playbackRate: this.rate,
      skipStops: this.skipStops,
      follow: this.follow,
      dockZoom: this.dockZoom
    });
  }

  /** Geofence outlines, loaded once per company the first time the layer is on. */
  private ensureGeofencesLoaded(): void {
    if (!this.scopeFilter.isFleetScopeReady || this.geofenceScope === this.tenantScopeId) return;
    this.geofenceScope = this.tenantScopeId;
    this.geofenceSub?.unsubscribe();
    this.geofenceSub = this.gps.getGeofences({ isActive: true }).subscribe({
      next: list => { this.geofences = list ?? []; },
      error: () => { this.geofences = []; }
    });
  }

  // ---- point details ---------------------------------------------------------------------------------

  openDetailInMaps(): void {
    if (this.detail.latitude != null && this.detail.longitude != null) {
      this.openExternalMap(this.detail.latitude, this.detail.longitude);
    }
  }

  copyCoordinates(text: string): void {
    void navigator.clipboard.writeText(text).then(
      () => this.toast.success('Coordinates copied'),
      () => this.toast.error('Could not copy coordinates')
    );
  }

  motionLabel(pos: TripReplayPosition | null): string {
    if (!pos) return '—';
    const status = resolveReplayStatus(Number(pos.speedKmh) || 0, pos.ignition);
    switch (status) {
      case 'moving': return 'Moving';
      case 'idle': return 'Idle';
      case 'parked': return 'Parked';
      default: return status;
    }
  }

  addressLabel(pos: TripReplayPosition | null): string {
    if (!pos) return '—';
    const key = this.addressCacheKey(pos.latitude, pos.longitude);
    const meta = this.addressMetaCache.get(key);
    const fromMeta = this.resolveAddressFromGeocode(meta ?? null);
    if (fromMeta && !isCoarseAddress(fromMeta)) return fromMeta;

    if (pos.address?.trim() && !isCoarseAddress(pos.address)) return pos.address;
    if (this.addressCache.has(key)) {
      const cached = this.addressCache.get(key);
      if (cached?.trim() && !isCoarseAddress(cached)) return cached.trim();
    }
    if (this.resolvingAddressKey === key) return 'Resolving…';

    const near =
      meta?.nearbyPlaceName?.trim() ||
      meta?.placeName?.trim() ||
      null;
    if (near) return `Near ${near}`;
    return 'Address unavailable';
  }

  private ensureAddress(pos: TripReplayPosition): void {
    const coarse = isCoarseAddress(pos.address);
    if (pos.address?.trim() && !coarse) return;
    const key = this.addressCacheKey(pos.latitude, pos.longitude);
    if (this.addressCache.has(key)) return;
    this.resolvingAddressKey = key;
    this.geocodeRequests$.next({
      lat: pos.latitude,
      lng: pos.longitude,
      forceRefresh: false,
      apply: addr => { pos.address = addr; }
    });
  }

  private addressCacheKey(lat: number, lng: number): string {
    return `${lat.toFixed(5)},${lng.toFixed(5)}`;
  }

  private resolveAddressFromGeocode(info: {
    formattedAddress?: string;
    placeName?: string;
    primaryAddress?: string;
    localityLine?: string;
  } | null): string | null {
    return formatFleetDisplayAddress(info) || null;
  }

  headingLabel(heading?: number | null): string {
    if (heading == null || !Number.isFinite(heading)) return '—';
    const dirs = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];
    const idx = Math.round(heading / 45) % 8;
    return `${Math.round(heading)}° ${dirs[idx]}`;
  }

  // ---- vehicles ---------------------------------------------------------------------------------------

  vehicleStatusLabel(v: VehicleListItem): string {
    if (v.gpsOnline) return 'Online';
    if (v.hasGpsDevice || v.gpsImei || this.deviceFor(v.id)) return 'Offline';
    return 'No GPS';
  }

  vehicleStatusClass(v: VehicleListItem): 'online' | 'offline' | 'none' {
    if (v.gpsOnline) return 'online';
    if (v.hasGpsDevice || v.gpsImei || this.deviceFor(v.id)) return 'offline';
    return 'none';
  }

  trackerLabel(vehicleId: number): string {
    const d = this.deviceFor(vehicleId);
    return d?.name ?? d?.trackerBrandName ?? '—';
  }

  imeiLabel(vehicleId: number): string {
    const v = this.vehicles.find(x => x.id === vehicleId);
    const d = this.deviceFor(vehicleId);
    const imei = d?.uniqueId ?? v?.gpsImei;
    if (!imei) return '—';
    // Full IMEI only for device managers; everyone else sees the last four digits.
    return this.canViewFullImei ? imei : maskImei(imei);
  }

  driverLabel(vehicleId: number): string {
    const v = this.vehicles.find(x => x.id === vehicleId);
    const d = this.deviceFor(vehicleId);
    const live = this.liveLocations.get(vehicleId);
    return live?.driverName ?? d?.driverName ?? v?.driverName ?? '—';
  }

  lastKnownLabel(vehicleId: number): string {
    const v = this.vehicles.find(x => x.id === vehicleId);
    if (!v) return '—';
    const iso = resolveLastKnownIso(v, this.liveLocations.get(vehicleId));
    return formatStalenessLabel(iso, this.clockNow);
  }

  toggleCompareVehicle(vehicleId: number): void {
    if (this.compareSelectedIds.has(vehicleId)) {
      this.compareSelectedIds.delete(vehicleId);
    } else if (this.compareSelectedIds.size >= HISTORY_COMPARE_MAX_VEHICLES) {
      this.toast.error(`Compare up to ${HISTORY_COMPARE_MAX_VEHICLES} vehicles at once.`);
      this.refreshVehicleCards();
      return;
    } else {
      this.compareSelectedIds.add(vehicleId);
    }
    this.compareSelectedIds = new Set(this.compareSelectedIds);
    this.refreshVehicleCards();
    if (this.vehicleId) {
      void this.reloadCompareRoutes();
    }
  }

  clearCompareSelection(): void {
    this.compareSelectedIds = new Set();
    this.compareOverlays = [];
    this.refreshVehicleCards();
  }

  onFilterStationaryChange(enabled: boolean): void {
    this.filterStationaryOverOneHour = enabled;
    this.persistHistoryPrefs();
    this.refreshVehicleCards();
  }

  openExternalMap(lat: number, lng: number): void {
    window.open(googleMapsUrl(lat, lng), '_blank', 'noopener,noreferrer');
  }

  openMapForVehicle(vehicleId: number): void {
    const live = this.liveLocations.get(vehicleId);
    const v = this.vehicles.find(x => x.id === vehicleId);
    const lat = live?.latitude ?? v?.locationLatitude;
    const lng = live?.longitude ?? v?.locationLongitude;
    if (lat == null || lng == null) {
      this.toast.error('No map coordinates for this vehicle yet.');
      return;
    }
    this.openExternalMap(lat, lng);
  }

  openLiveFleetMap(vehicleId: number): void {
    void this.router.navigate(['/gps-tracking/live'], {
      queryParams: { vehicleId: String(vehicleId) }
    });
  }

  isVehicleOfflineCached(v: VehicleListItem): boolean {
    return isOfflineOrCachedTracker(
      v,
      this.liveLocations.get(v.id),
      !!(v.hasGpsDevice || v.gpsImei || this.deviceFor(v.id))
    );
  }

  deviceFor(vehicleId: number): GpsDevice | undefined {
    return this.devices.find(d => d.vehicleId === vehicleId);
  }

  private loadDevices(): void {
    this.gps.getDevices(this.tenantScopeId).subscribe({
      next: d => {
        this.devices = d;
        this.refreshVehicleCards();
      },
      error: () => {}
    });
  }

  // ---- export and share -------------------------------------------------------------------------------

  /** The period as picked, for report headers. */
  get periodLabelDetailed(): string {
    if (!this.from || !this.to) return '—';
    // Picker values are company-zone wall time — show them as picked.
    const fmt = (raw: string) =>
      formatInZone(parseDatetimeLocalInput(raw), {
        day: '2-digit', month: 'short', year: 'numeric',
        hour: '2-digit', minute: '2-digit'
      });
    return `${fmt(this.from)} → ${fmt(this.to)}`;
  }

  /** The strip's figures as one line: the same numbers the screen shows. */
  private summaryLine(): string {
    return this.kpis
      .filter(k => k.value !== '—')
      .map(k => `${k.label}: ${k.value}${k.unit ? ' ' + k.unit : ''}`)
      .join(' · ');
  }

  exportHistoryReportPdf(): void {
    if (!this.vehicleId || !this.bundle) {
      this.toast.error('Load a route before exporting the report.');
      return;
    }
    const vehicle = this.selectedVehicle;
    const label = vehicle
      ? `${vehicle.name} (${vehicle.registrationNumber || '—'})`
      : `Vehicle #${this.vehicleId}`;
    const driver = this.driverLabel(this.vehicleId);

    type ReportRow = { type: string; window: string; duration: string; distance: string; place: string };
    const zone = this.zone;
    const timeline = this.timeline;
    const rows: ReportRow[] = timeline
      ? timeline.segments.slice(0, MAX_REPORT_ROWS).map(s => ({
          type: s.kind === 'trip' ? 'Trip' : s.kind === 'parking' ? 'Parking' : 'Stop',
          window: `${formatDay(s.startTime, zone)} · ${formatTimeRange(s.startTime, s.endTime, zone)}`,
          duration: formatDuration(s.durationMinutes),
          distance: s.kind === 'trip' ? `${(Number(s.distanceKm) || 0).toFixed(1)} km` : '',
          place: s.kind === 'trip' ? tripTitle(s) : standstillWhere(s)
        }))
      : (this.bundle.stops ?? []).slice(0, MAX_REPORT_ROWS).map(s => ({
          type: s.durationMinutes >= 120 ? 'Parking' : 'Stop',
          window: `${formatDay(s.startTime, zone)} · ${formatTimeRange(s.startTime, s.endTime, zone)}`,
          duration: formatDuration(s.durationMinutes),
          distance: '',
          place: s.address?.trim() || `${s.latitude.toFixed(4)}, ${s.longitude.toFixed(4)}`
        }));

    const columns: ExportColumn<ReportRow>[] = [
      { header: 'Type', accessor: r => r.type, excelWidth: 10 },
      { header: 'Time window', accessor: r => r.window, excelWidth: 30 },
      { header: 'Duration', accessor: r => r.duration, excelWidth: 10 },
      { header: 'Distance', accessor: r => r.distance, excelWidth: 10 },
      { header: 'Place', accessor: r => r.place, excelWidth: 40 }
    ];

    void this.exportService.exportPdf(rows, columns, {
      filename: `fleet-history-report-${this.vehicleId}`,
      title: `Fleet History Report — ${label}`,
      subtitle: `${this.periodLabelDetailed}\nDriver: ${driver} · ${this.summaryLine()}`,
      sheetName: 'History',
      orientation: 'portrait'
    });
  }

  exportClient(format: 'csv' | 'excel' | 'pdf'): void {
    if (!this.vehicleId || !this.bundle) {
      this.toast.error('Load a route before exporting.');
      return;
    }
    void this.ensureRawPositionsLoaded().then(() => {
      const rows = this.rawPositions.length ? this.rawPositions : this.bundle?.route ?? [];
      if (!rows.length) {
        this.toast.error('No route points available to export.');
        return;
      }
      const vehicle = this.selectedVehicle;
      const label = vehicle ? `${vehicle.name}-${vehicle.registrationNumber}` : `vehicle-${this.vehicleId}`;
      const columns: ExportColumn<TripReplayPosition>[] = [
        { header: 'Timestamp', accessor: r => formatInZone(r.timestamp, {}, this.bundle?.timeZoneId), excelWidth: 22 },
        { header: 'Latitude', accessor: r => r.latitude, excelWidth: 14 },
        { header: 'Longitude', accessor: r => r.longitude, excelWidth: 14 },
        { header: 'Speed (km/h)', accessor: r => r.speedKmh, excelWidth: 12 },
        { header: 'Heading', accessor: r => r.heading ?? '', excelWidth: 10 },
        { header: 'Ignition', accessor: r => r.ignition == null ? '' : r.ignition ? 'ON' : 'OFF', excelWidth: 10 },
        { header: 'Address', accessor: r => r.address ?? '', excelWidth: 36 },
        { header: 'Odometer (km)', accessor: r => r.totalDistanceKm ?? '', excelWidth: 14 }
      ];
      const meta = {
        filename: `gps-history-${label}`,
        title: `GPS History — ${label}`,
        subtitle: this.periodLabelDetailed,
        sheetName: 'Positions'
      };
      if (format === 'csv') this.exportService.exportCsv(rows, columns, meta);
      else if (format === 'excel') this.exportService.exportExcel(rows, columns, meta);
      else this.exportService.exportPdf(rows, columns, meta);
    });
  }

  exportServer(format: 'gpx' | 'geojson' | 'kml'): void {
    if (!this.vehicleId) return;
    const fromDate = parseDatetimeLocalInput(this.from);
    const toDate = parseDatetimeLocalInput(this.to);
    this.gps.exportHistorySignedUrl(this.vehicleId, fromDate, toDate, format, this.tenantScopeId).subscribe({
      next: result => {
        const fileName = result.fileName || `gps-history-${this.vehicleId}.${format}`;
        // Cross-origin Azure SAS: download attr is advisory; navigate so browser
        // honors blob Content-Disposition. Fallback open if popup blocked.
        const a = document.createElement('a');
        a.href = result.signedUrl;
        a.download = fileName;
        a.target = '_blank';
        a.rel = 'noopener noreferrer';
        document.body.appendChild(a);
        a.click();
        a.remove();
        this.toast.success('Export ready — download started.');
      },
      error: () => this.toast.error('Export failed.')
    });
  }

  printRoute(): void {
    window.print();
  }

  copyShareUrl(): void {
    const url = new URL(window.location.href);
    if (this.vehicleId) url.searchParams.set('vehicleId', String(this.vehicleId));
    url.searchParams.set('preset', this.preset);
    if (this.preset === 'custom') {
      url.searchParams.set('from', this.from);
      url.searchParams.set('to', this.to);
    } else {
      url.searchParams.delete('from');
      url.searchParams.delete('to');
    }
    void navigator.clipboard.writeText(url.toString()).then(
      () => this.toast.success('Share link copied to clipboard'),
      () => this.toast.error('Could not copy link')
    );
  }

  shareRouteText(): void {
    const v = this.selectedVehicle;
    const text = [
      `Route: ${v?.name ?? 'Vehicle'} (${v?.registrationNumber ?? ''})`,
      `Period: ${this.periodLabelDetailed}`,
      ...this.kpis
        .filter(k => k.value !== '—')
        .map(k => `${k.label}: ${k.value}${k.unit ? ' ' + k.unit : ''}`)
    ].join('\n');
    void navigator.clipboard.writeText(text).then(
      () => this.toast.success('Route summary copied'),
      () => this.toast.error('Could not copy summary')
    );
  }

  // ---- background data ----------------------------------------------------------------------------------

  private refreshLiveLocations(): void {
    this.liveLocSub?.unsubscribe();
    if (!this.scopeFilter.isFleetScopeReady) {
      this.liveLocations = new Map();
      return;
    }
    this.liveLocSub = this.gps.getAllVehicleLocations(this.tenantScopeId).subscribe({
      next: rows => {
        this.liveLocations = new Map(rows.map(r => [r.vehicleId, r]));
        this.clockNow = Date.now();
        this.refreshVehicleCards();
        this.evaluateStationary24hAlerts();
      },
      error: () => {}
    });
  }

  private persistHistoryPrefs(): void {
    storeHistoryPagePrefs({
      stationaryOverOneHour: this.filterStationaryOverOneHour,
      hideRecordsBefore: this.storedHideRecordsBefore,
      alertStationary24h: this.alertStationary24h
    });
  }

  private evaluateStationary24hAlerts(): void {
    if (!this.alertStationary24h) return;
    const offenders = this.vehicles.filter(v => {
      const mins = stationaryMinutes(v, this.liveLocations.get(v.id), this.clockNow);
      return mins >= HISTORY_STATIONARY_ALERT_MINUTES;
    });
    if (!offenders.length) return;

    const dayKey = new Date().toDateString();
    if (sessionStorage.getItem(HISTORY_ALERT_NOTIFIED_KEY) === dayKey) return;
    sessionStorage.setItem(HISTORY_ALERT_NOTIFIED_KEY, dayKey);

    const names = offenders.slice(0, 5).map(v => v.name).join(', ');
    const suffix = offenders.length > 5 ? ` (+${offenders.length - 5} more)` : '';
    const body = `${offenders.length} vehicle(s) stationary ≥24h: ${names}${suffix}`;
    this.toast.warning(body, undefined, 8000);

    if (typeof Notification !== 'undefined' && Notification.permission === 'granted') {
      try {
        new Notification('Fleet stationary alert', { body });
      } catch {
        /* ignore */
      }
    }
  }

  private reloadCompareRoutes(fromArg?: Date, toArg?: Date): void {
    const fromDate = fromArg ?? parseDatetimeLocalInput(this.from);
    const toDate = toArg ?? parseDatetimeLocalInput(this.to);
    const ids = [...this.compareSelectedIds].filter(id => id !== this.vehicleId);
    if (!ids.length) {
      this.compareOverlays = [];
      return;
    }

    this.compareLoadSub?.unsubscribe();
    this.compareLoading = true;
    this.compareLoadSub = forkJoin(
      ids.map(id =>
        this.gps.getHistoryReplay(id, fromDate, toDate, {
          ...this.replayOptionsForWindow(fromDate, toDate),
          tenantScopeId: this.tenantScopeId
        }).pipe(
          catchError(() => of(null))
        )
      )
    ).subscribe(bundles => {
      this.compareLoading = false;
      const overlays: HistoryMapCompareRoute[] = [];
      ids.forEach((id, idx) => {
        const bundle = bundles[idx];
        const route = bundle?.route?.length
          ? bundle.route
          : bundle?.playback ?? [];
        if (!route.length) return;
        const v = this.vehicles.find(x => x.id === id);
        overlays.push({
          label: v?.name ?? `Vehicle ${id}`,
          color: COMPARE_ROUTE_COLORS[idx % COMPARE_ROUTE_COLORS.length],
          route
        });
      });
      this.compareOverlays = overlays;
    });
  }

  private vehicleSearchHaystack(v: VehicleListItem): string {
    const device = this.deviceFor(v.id);
    return [
      v.name,
      v.registrationNumber,
      v.driverName,
      device?.driverName,
      device?.uniqueId,
      device?.name,
      v.gpsImei
    ].filter(Boolean).join(' ').toLowerCase();
  }

  private finishLoading(): void {
    this.loading = false;
    this.clearLoadTimers();
  }

  private clearLoadTimers(): void {
    if (this.timeoutTimer) {
      clearTimeout(this.timeoutTimer);
      this.timeoutTimer = undefined;
    }
  }

  private ensureRawPositionsLoaded(fromArg?: Date, toArg?: Date): Promise<void> {
    if (!this.vehicleId) return Promise.resolve();
    const from = fromArg ?? parseDatetimeLocalInput(this.from);
    const to = toArg ?? parseDatetimeLocalInput(this.to);
    const key = this.rawHistoryKey(this.vehicleId, from, to);

    if (this.rawCacheKey === key && this.rawPositions.length) {
      return Promise.resolve();
    }
    const cached = this.rawHistoryCache.get(key);
    if (cached) {
      this.rawPositions = cached;
      this.rawCacheKey = key;
      return Promise.resolve();
    }

    this.rawLoadSub?.unsubscribe();
    return new Promise(resolve => {
      this.rawLoadSub = this.gps.getHistory(this.vehicleId!, from, to, this.tenantScopeId).subscribe({
        next: rows => {
          const mapped = this.mapRawPositions(rows);
          this.rawHistoryCache.set(key, mapped);
          this.rawPositions = mapped;
          this.rawCacheKey = key;
          resolve();
        },
        error: () => resolve()
      });
    });
  }

  private replayOptionsForWindow(from: Date, to: Date): { routeMaxPoints: number; playbackMaxPoints: number; includeRaw: boolean } {
    const ms = to.getTime() - from.getTime();
    if (ms >= 6.5 * 24 * 60 * 60 * 1000) {
      return { routeMaxPoints: LAST7_ROUTE_MAX_POINTS, playbackMaxPoints: LAST7_PLAYBACK_MAX_POINTS, includeRaw: false };
    }
    return { routeMaxPoints: 5000, playbackMaxPoints: 2500, includeRaw: false };
  }

  private rawHistoryKey(vehicleId: number, from: Date, to: Date): string {
    return `${vehicleId}:${from.toISOString()}:${to.toISOString()}`;
  }

  private mapRawPositions(rows: PositionDto[]): TripReplayPosition[] {
    return rows.map(r => ({
      timestamp: r.timestamp,
      latitude: r.latitude,
      longitude: r.longitude,
      speedKmh: Number(r.speed) || 0,
      heading: r.heading,
      ignition: r.ignition,
      altitude: r.altitude,
      address: r.address,
      batteryLevel: r.batteryLevel,
      satellites: r.gsmSignal,
      totalDistanceKm: r.totalDistanceKm
    }));
  }

  private applyPreset(preset: TripDatePreset, updateFields: boolean): void {
    if (preset === 'custom') return;
    const { from, to } = applyTripDatePreset(preset);
    if (updateFields) {
      this.from = toDatetimeLocalInput(from);
      this.to = toDatetimeLocalInput(to);
    }
  }

  private readQueryParams(): void {
    const qp = this.route$.snapshot.queryParamMap;
    const vehicleParam = qp.get('vehicleId');
    const presetParam = qp.get('preset') as TripDatePreset | null;

    if (presetParam && this.presets.some(p => p.id === presetParam)) {
      this.preset = presetParam;
      if (presetParam !== 'custom') {
        this.applyPreset(presetParam, true);
      }
    }

    if (qp.get('from')) this.from = qp.get('from')!;
    if (qp.get('to')) this.to = qp.get('to')!;
    this.refreshRangeLabels();

    if (vehicleParam) {
      const id = Number(vehicleParam);
      if (Number.isFinite(id)) {
        this.vehicleId = id;
        this.pushRecentVehicle(id);
        timer(300).subscribe(() => this.load());
      }
    } else if (this.vehicles.length && !this.vehicleId) {
      this.vehicleId = this.recentVehicleIds[0] ?? this.vehicles[0].id;
    }
  }

  private updateUrl(): void {
    const queryParams: Record<string, string | null> = {
      vehicleId: this.vehicleId ? String(this.vehicleId) : null,
      preset: this.preset
    };
    if (this.preset === 'custom') {
      queryParams['from'] = this.from;
      queryParams['to'] = this.to;
    } else {
      queryParams['from'] = null;
      queryParams['to'] = null;
    }
    void this.router.navigate([], {
      relativeTo: this.route$,
      queryParams,
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }

  private loadRecentVehicles(): void {
    try {
      const raw = localStorage.getItem(RECENT_VEHICLES_KEY);
      this.recentVehicleIds = raw ? (JSON.parse(raw) as number[]) : [];
    } catch {
      this.recentVehicleIds = [];
    }
  }

  private pushRecentVehicle(id: number): void {
    this.recentVehicleIds = [id, ...this.recentVehicleIds.filter(x => x !== id)].slice(0, MAX_RECENT);
    localStorage.setItem(RECENT_VEHICLES_KEY, JSON.stringify(this.recentVehicleIds));
  }
}
