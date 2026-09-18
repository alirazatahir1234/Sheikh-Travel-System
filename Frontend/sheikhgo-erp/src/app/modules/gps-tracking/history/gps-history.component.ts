import { Component, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription, timer, Subject, mergeMap, catchError, of, tap } from 'rxjs';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { VehicleListItem, VehicleStatus } from '../../../core/models/vehicle.model';
import {
  GpsDevice,
  HistoryReplayBundle,
  PositionDto,
  TripAnalyticsSummary,
  TripReplayPosition,
  TripStop
} from '../../../core/models/gps-tracking.model';
import {
  HISTORY_DATE_PRESETS,
  TripDatePreset,
  applyTripDatePreset,
  formatPeriodLabel,
  toDatetimeLocalInput
} from '../utils/trip-date-preset.util';
import { TripReplayMapComponent } from '../shared/trip-replay-map/trip-replay-map.component';
import { resolveReplayStatus } from '../../../core/leaflet/fleet-vehicle-marker';
import {
  formatFleetDisplayAddress,
  isCoarseAddress,
  looksLikeRoadSegment,
  splitDisplayAddress
} from '../utils/gps-address.util';

const RECENT_VEHICLES_KEY = 'gps_history_recent';
const MAX_RECENT = 5;
const LOAD_TIMEOUT_MS = 20_000;
const LAST7_ROUTE_MAX_POINTS = 3000;
const LAST7_PLAYBACK_MAX_POINTS = 1000;

export type HistoryRouteFilter =
  | 'route'
  | 'gpsPoints'
  | 'stops'
  | 'parking'
  | 'geofences'
  | 'heatmap';

@Component({
  standalone: false,
  selector: 'app-gps-history',
  templateUrl: './gps-history.component.html',
  styleUrls: ['./gps-history.component.scss']
})
export class GpsHistoryComponent implements OnInit, OnDestroy {
  @ViewChild(TripReplayMapComponent) replayMap?: TripReplayMapComponent;
  vehicles: VehicleListItem[] = [];
  devices: GpsDevice[] = [];
  vehicleId: number | null = null;
  vehicleSearch = '';
  from = '';
  to = '';
  preset: TripDatePreset = 'last7Days';
  bundle: HistoryReplayBundle | null = null;
  selectedPosition: TripReplayPosition | null = null;
  rawPositions: TripReplayPosition[] = [];
  loading = false;
  loadingProgress = 0;
  loadingTimedOut = false;
  loadAttempt = 0;
  error = '';
  noData = false;
  recentVehicleIds: number[] = [];

  readonly presets = HISTORY_DATE_PRESETS;
  readonly routeFilters: { id: HistoryRouteFilter; label: string; icon: string }[] = [
    { id: 'route', label: 'Route', icon: 'timeline' },
    { id: 'gpsPoints', label: 'GPS points', icon: 'scatter_plot' },
    { id: 'heatmap', label: 'Speed heatmap', icon: 'gradient' },
    { id: 'stops', label: 'Stops', icon: 'pause_circle' },
    { id: 'parking', label: 'Parking', icon: 'local_parking' },
    { id: 'geofences', label: 'Geofences', icon: 'fence' }
  ];
  activeRouteFilters = new Set<HistoryRouteFilter>([
    'route', 'stops', 'parking', 'geofences'
  ]);

  private loadSub?: Subscription;
  private progressTimer?: ReturnType<typeof setInterval>;
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
  selectedStopIndex: number | null = null;

  constructor(
    private gps: GpsTrackingService,
    private vehicleService: VehicleService,
    private exportService: ExportService,
    private toast: UiToastService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadRecentVehicles();
    this.applyPreset(this.preset, true);

    this.geocodeSub = this.geocodeRequests$.pipe(
      // Queue all visible stop/position geocodes (concurrency 2) instead of
      // debounce+switchMap which cancelled all but the last request.
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
            const address =
              this.resolveAddressFromGeocode(info) || null;
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
    ).subscribe();

    this.vehicleService.getAll(1, 500).subscribe({
      next: r => {
        this.vehicles = r.items.filter(v => v.status !== VehicleStatus.Retired);
        this.readQueryParams();
      }
    });

    this.gps.getDevices().subscribe({
      next: d => { this.devices = d; },
      error: () => {}
    });
  }

  ngOnDestroy(): void {
    this.clearLoadTimers();
    this.loadSub?.unsubscribe();
    this.rawLoadSub?.unsubscribe();
    this.geocodeSub?.unsubscribe();
    this.geocodeRequests$.complete();
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

  get selectedVehicle(): VehicleListItem | undefined {
    return this.vehicleId ? this.vehicles.find(v => v.id === this.vehicleId) : undefined;
  }

  get canLoad(): boolean {
    return !!this.vehicleId && !!this.from && !!this.to && !this.loading;
  }

  get loadHint(): string {
    if (!this.vehicleId) return 'Select a vehicle from the list to begin.';
    if (!this.from || !this.to) return 'Choose a date range, then load history.';
    if (this.loading) return 'Loading route from Traccar…';
    if (this.noData) return 'No historical data for this period — try another range.';
    if (!this.bundle) return 'Press Load route to fetch GPS history.';
    return '';
  }

  get loadButtonLabel(): string {
    return this.bundle ? 'Reload route' : 'Load route';
  }

  get periodLabel(): string {
    return formatPeriodLabel(this.from, this.to);
  }

  get periodLabelDetailed(): string {
    if (!this.from || !this.to) return '—';
    const fmt = (raw: string) => {
      const d = new Date(raw);
      return d.toLocaleString(undefined, {
        day: '2-digit', month: 'short', year: 'numeric',
        hour: '2-digit', minute: '2-digit'
      });
    };
    return `${fmt(this.from)} → ${fmt(this.to)}`;
  }

  get vehicleContext() {
    return this.bundle?.vehicle ?? null;
  }

  get statistics(): TripAnalyticsSummary | null {
    return this.bundle?.statistics ?? null;
  }

  get playbackPositions(): TripReplayPosition[] {
    const b = this.bundle;
    if (!b) return [];
    return b.playback?.length ? b.playback : b.route ?? [];
  }

  get mileageKm(): number | null {
    if (this.bundle?.mileageKm != null) return this.bundle.mileageKm;
    return this.bundle?.summary?.distanceKm ?? null;
  }

  get parkingCount(): number {
    if (this.bundle?.parking?.length) return this.bundle.parking.length;
    return (this.bundle?.stops ?? []).filter(s => s.durationMinutes >= 120).length;
  }

  get quickStats() {
    const display = this.bundle?.displaySummary;
    const stats = this.statistics;
    const summary = this.bundle?.summary;
    if (display) {
      return {
        distanceKm: display.distance ?? this.mileageKm,
        moving: display.movingTime,
        idle: display.nonMovingTime,
        stops: display.stops,
        parking: display.parking,
        maxSpeed: display.maxSpeed,
        avgSpeed: stats?.avgSpeedKmh ?? summary?.avgSpeedKmh ?? null,
        engineHours: stats?.engineHours ?? summary?.engineHours ?? null,
        points: this.rawPositions.length || this.bundle?.route.length || 0
      };
    }
    return {
      distanceKm: this.mileageKm,
      moving: this.formatDuration(this.movingMinutes),
      idle: this.formatDuration(stats?.idleMinutes ?? 0),
      stops: stats?.stopCount ?? this.bundle?.stops.length ?? 0,
      parking: this.parkingCount,
      maxSpeed: stats?.maxSpeedKmh ?? summary?.maxSpeedKmh ?? null,
      avgSpeed: stats?.avgSpeedKmh ?? summary?.avgSpeedKmh ?? null,
      engineHours: stats?.engineHours ?? summary?.engineHours ?? null,
      points: this.rawPositions.length || this.bundle?.route.length || 0
    };
  }

  get showTodayMileageChip(): boolean {
    const { from, to } = applyTripDatePreset('today');
    const rangeFrom = new Date(this.from).getTime();
    const rangeTo = new Date(this.to).getTime();
    return rangeFrom <= to.getTime() && rangeTo >= from.getTime();
  }

  get periodRangeLabel(): string {
    const short = (raw: string) =>
      new Date(raw).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
    if (this.preset === 'custom') {
      return `${short(this.from)} – ${short(this.to)}`;
    }
    const base = this.presets.find(p => p.id === this.preset)?.label || 'Custom range';
    return this.showTodayMileageChip ? `${base} · Includes today` : base;
  }

  get stoppedMinutes(): number {
    const stats = this.statistics;
    if (!stats) return 0;
    return Math.max(0, stats.idleMinutes);
  }

  get movingMinutes(): number {
    const stats = this.statistics;
    if (!stats) return this.bundle?.summary?.drivingMinutes ?? 0;
    return stats.drivingMinutes;
  }

  get showGpsPoints(): boolean {
    return this.activeRouteFilters.has('gpsPoints');
  }

  get showStopsLayer(): boolean {
    return this.activeRouteFilters.has('stops');
  }

  get showParkingLayer(): boolean {
    return this.activeRouteFilters.has('parking');
  }

  get showGeofencesLayer(): boolean {
    return this.activeRouteFilters.has('geofences');
  }

  get showHeatmap(): boolean {
    return this.activeRouteFilters.has('heatmap');
  }

  get showRouteLayer(): boolean {
    return this.activeRouteFilters.has('route');
  }

  selectVehicle(id: number): void {
    this.vehicleId = id;
    this.pushRecentVehicle(id);
    this.updateUrl();
    this.load();
  }

  setPreset(id: TripDatePreset): void {
    this.preset = id;
    this.applyPreset(id, true);
    this.updateUrl();
  }

  onCustomDatesChange(): void {
    this.preset = 'custom';
    this.updateUrl();
  }

  toggleRouteFilter(id: HistoryRouteFilter): void {
    if (this.activeRouteFilters.has(id)) {
      if (id === 'route' && this.activeRouteFilters.size <= 1) return;
      this.activeRouteFilters.delete(id);
    } else {
      this.activeRouteFilters.add(id);
    }
    this.activeRouteFilters = new Set(this.activeRouteFilters);
    if (id === 'gpsPoints' && this.activeRouteFilters.has('gpsPoints')) {
      void this.ensureRawPositionsLoaded();
    }
  }

  isRouteFilterActive(id: HistoryRouteFilter): boolean {
    return this.activeRouteFilters.has(id);
  }

  load(): void {
    if (!this.vehicleId) return;
    const fromDate = new Date(this.from);
    const toDate = new Date(this.to);
    if (fromDate > toDate) {
      this.error = 'Start date must be before end date.';
      return;
    }
    if (toDate.getTime() - fromDate.getTime() > 366 * 24 * 60 * 60 * 1000) {
      this.error = 'Date range cannot exceed 366 days.';
      return;
    }

    this.clearLoadTimers();
    this.loadSub?.unsubscribe();
    this.rawLoadSub?.unsubscribe();
    this.loading = true;
    this.loadingProgress = 0;
    this.loadingTimedOut = false;
    this.error = '';
    this.noData = false;
    this.bundle = null;
    this.selectedPosition = null;
    this.rawPositions = [];
    this.rawCacheKey = null;
    this.loadAttempt++;

    this.progressTimer = setInterval(() => {
      if (this.loadingProgress < 92) {
        this.loadingProgress = Math.min(92, this.loadingProgress + 4 + Math.random() * 6);
      }
    }, 400);

    this.timeoutTimer = setTimeout(() => {
      if (this.loading) {
        this.loadingTimedOut = true;
      }
    }, LOAD_TIMEOUT_MS);

    this.loadSub = this.gps.getHistoryReplay(
      this.vehicleId,
      fromDate,
      toDate,
      this.replayOptionsForWindow(fromDate, toDate)
    ).subscribe({
      next: bundle => {
        this.finishLoading();
        const hasRoute = (bundle.route?.length ?? 0) > 0 || (bundle.playback?.length ?? 0) > 0;
        if (!hasRoute) {
          this.noData = true;
          this.bundle = null;
          return;
        }
        this.bundle = bundle;
        const playback = this.playbackPositions;
        this.selectedPosition = playback[0] ?? bundle.route[0] ?? null;
        this.selectedStopIndex = null;
        if (this.selectedPosition) this.ensureAddress(this.selectedPosition);
        this.enrichStopAddresses(bundle.stops);
        if (this.showGpsPoints) {
          void this.ensureRawPositionsLoaded(fromDate, toDate);
        }
      },
      error: err => {
        this.finishLoading();
        this.error = err?.error?.message ?? 'Failed to load history.';
      }
    });
  }

  retryLoad(): void {
    this.loadingTimedOut = false;
    this.load();
  }

  refreshRoute(): void {
    if (this.canLoad || this.vehicleId) this.load();
  }

  onPositionSelected(pos: TripReplayPosition): void {
    this.selectedPosition = pos;
    this.ensureAddress(pos);
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

  addressPrimaryLine(address: string | null | undefined): string | null {
    return splitDisplayAddress(address).primary;
  }

  addressSecondaryLine(address: string | null | undefined): string | null {
    return splitDisplayAddress(address).secondary;
  }

  stopAddressLabel(stop: TripStop): string {
    if (stop.address?.trim() && !isCoarseAddress(stop.address)) return stop.address;
    const key = this.addressCacheKey(stop.latitude, stop.longitude);
    if (this.addressCache.has(key)) {
      const cached = this.addressCache.get(key);
      return cached?.trim() || stop.address?.trim() || 'Address unavailable';
    }
    if (this.resolvingAddressKey === key) return 'Resolving…';
    return stop.address?.trim() || 'Address unavailable';
  }

  /** "Parking — Circular Road, Sialkot" */
  stopHeadline(stop: TripStop): string {
    return this.stopLabel(stop);
  }

  isParkingStop(stop: TripStop): boolean {
    return stop.durationMinutes >= 120;
  }

  stopPrimaryLine(stop: TripStop): string {
    const key = this.addressCacheKey(stop.latitude, stop.longitude);
    const meta = this.addressMetaCache.get(key);
    const near =
      meta?.nearbyPlaceName?.trim() ||
      meta?.placeName?.trim() ||
      null;

    if (meta?.primaryAddress?.trim()) {
      const primary = meta.primaryAddress.trim();
      // Never show the POI/business name as the street line.
      if (near && primary.toLowerCase() === near.toLowerCase()) {
        return (
          meta.localityLine?.trim() ||
          this.addressPrimaryLine(meta.formattedAddress) ||
          'Approximate location'
        );
      }
      return primary;
    }

    if (meta?.localityLine?.trim()) return meta.localityLine.trim();

    const full = this.stopAddressLabel(stop);
    if (full === 'Resolving…') return full;
    if (full === 'Address unavailable') {
      return near ? 'Approximate location' : full;
    }
    const split = splitDisplayAddress(full);
    if (split.primary) return split.primary;
    // POI-only / business-name-only → do not promote as the street line.
    return near || split.secondary ? 'Approximate location' : full;
  }

  stopNearbyLine(stop: TripStop): string | null {
    const key = this.addressCacheKey(stop.latitude, stop.longitude);
    const meta = this.addressMetaCache.get(key);
    let near =
      meta?.nearbyPlaceName?.trim() ||
      meta?.placeName?.trim() ||
      null;

    // Cached stop.address may be a lone POI until reverse-geocode refresh completes.
    if (!near) {
      const raw = stop.address?.trim() || this.addressCache.get(key)?.trim() || '';
      if (raw && isCoarseAddress(raw)) {
        const split = splitDisplayAddress(raw);
        if (split.secondary && !looksLikeRoadSegment(split.secondary)) {
          near = split.secondary;
        } else {
          const parts = raw.split(',').map(p => p.trim()).filter(Boolean);
          if (parts.length === 1 && !looksLikeRoadSegment(parts[0])) {
            near = parts[0];
          }
        }
      }
    }

    if (!near) return null;
    const primary = this.stopPrimaryLine(stop).toLowerCase();
    if (primary.includes(near.toLowerCase())) return null;
    return `Near ${near}`;
  }

  stopLocalityLine(stop: TripStop): string | null {
    const key = this.addressCacheKey(stop.latitude, stop.longitude);
    const meta = this.addressMetaCache.get(key);
    if (meta?.localityLine?.trim()) {
      const loc = meta.localityLine.trim();
      const primary = this.stopPrimaryLine(stop).toLowerCase();
      if (primary.includes(loc.toLowerCase())) return null;
      return loc;
    }
    const secondary = this.addressSecondaryLine(this.stopAddressLabel(stop));
    if (!secondary) return null;
    // Do not treat a POI/business name as the locality line.
    if (!looksLikeRoadSegment(secondary) && secondary.split(/\s+/).length >= 3) {
      return null;
    }
    const primary = this.stopPrimaryLine(stop).toLowerCase();
    if (primary.includes(secondary.toLowerCase())) return null;
    return secondary;
  }

  formatCompactDuration(minutes: number): string {
    if (!minutes || minutes < 1) return '0m';
    const h = Math.floor(minutes / 60);
    const m = minutes % 60;
    if (h === 0) return `${m}m`;
    if (m === 0) return `${h}h`;
    return `${h}h ${m}m`;
  }

  stopTimeWindowLabel(stop: TripStop): string {
    const start = new Date(stop.startTime);
    const end = new Date(stop.endTime);
    const day = start.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
    const startTime = start.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
    if (!(end instanceof Date) || Number.isNaN(end.getTime()) || end <= start) {
      return `${day} · ${startTime}`;
    }
    const endTime = end.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
    const sameDay = start.toDateString() === end.toDateString();
    if (sameDay) return `${day} · ${startTime} – ${endTime}`;
    const endDay = end.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
    return `${day} ${startTime} – ${endDay} ${endTime}`;
  }

  private ensureAddress(pos: TripReplayPosition): void {
    const coarse = isCoarseAddress(pos.address);
    if (pos.address?.trim() && !coarse) return;
    const key = this.addressCacheKey(pos.latitude, pos.longitude);
    if (!coarse && this.addressCache.has(key)) {
      const cached = this.addressCache.get(key);
      if (cached) this.patchPositionAddress(pos, cached);
      return;
    }
    this.resolvingAddressKey = key;
    this.geocodeRequests$.next({
      lat: pos.latitude,
      lng: pos.longitude,
      forceRefresh: coarse && !!pos.address?.trim(),
      apply: addr => this.patchPositionAddress(pos, addr)
    });
  }

  private enrichStopAddresses(stops: TripStop[]): void {
    for (const stop of stops.slice(0, 15)) {
      this.ensureStopAddress(stop);
    }
  }

  private ensureStopAddress(stop: TripStop): void {
    const coarse = isCoarseAddress(stop.address);
    if (stop.address?.trim() && !coarse) return;
    const key = this.addressCacheKey(stop.latitude, stop.longitude);
    if (!coarse && this.addressCache.has(key)) {
      const cached = this.addressCache.get(key);
      if (cached) stop.address = cached;
      return;
    }
    this.resolvingAddressKey = key;
    this.geocodeRequests$.next({
      lat: stop.latitude,
      lng: stop.longitude,
      forceRefresh: coarse && !!stop.address?.trim(),
      apply: addr => {
        stop.address = addr;
        if (this.bundle) {
          this.bundle = { ...this.bundle, stops: [...this.bundle.stops] };
        }
      }
    });
  }

  private patchPositionAddress(pos: TripReplayPosition, address: string): void {
    pos.address = address;
    if (
      this.selectedPosition &&
      this.selectedPosition.latitude === pos.latitude &&
      this.selectedPosition.longitude === pos.longitude &&
      this.selectedPosition.timestamp === pos.timestamp
    ) {
      this.selectedPosition = { ...this.selectedPosition, address };
    }
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

  formatDuration(minutes: number): string {
    if (!minutes || minutes < 1) return '0 min';
    const h = Math.floor(minutes / 60);
    const m = minutes % 60;
    if (h === 0) return `${m} min`;
    if (m === 0) return `${h} hr`;
    return `${h} hr ${m} min`;
  }

  vehicleStatusLabel(v: VehicleListItem): string {
    if (v.gpsOnline) return 'Online';
    if (v.hasGpsDevice || v.gpsImei || this.deviceFor(v.id)) return 'Offline';
    return 'No GPS';
  }

  vehicleStatusClass(v: VehicleListItem): string {
    if (v.gpsOnline) return 'online';
    if (v.hasGpsDevice || v.gpsImei || this.deviceFor(v.id)) return 'offline';
    return 'none';
  }

  gsmLabel(pos: TripReplayPosition | null): string {
    if (pos?.satellites == null) return '—';
    return `${pos.satellites} (GSM / RSSI)`;
  }

  trackerLabel(vehicleId: number): string {
    const d = this.deviceFor(vehicleId);
    return d?.name ?? d?.trackerBrandName ?? '—';
  }

  imeiLabel(vehicleId: number): string {
    const v = this.vehicles.find(x => x.id === vehicleId);
    const d = this.deviceFor(vehicleId);
    return d?.uniqueId ?? v?.gpsImei ?? '—';
  }

  driverLabel(vehicleId: number): string {
    const v = this.vehicles.find(x => x.id === vehicleId);
    const d = this.deviceFor(vehicleId);
    return d?.driverName ?? v?.driverName ?? '—';
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
        { header: 'Timestamp', accessor: r => new Date(r.timestamp).toLocaleString(), excelWidth: 22 },
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
    const fromDate = new Date(this.from);
    const toDate = new Date(this.to);
    this.gps.exportHistory(this.vehicleId, fromDate, toDate, format).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `gps-history-${this.vehicleId}.${format}`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.toast.error('Export failed.')
    });
  }

  focusStop(stop: TripStop, index?: number): void {
    this.selectedStopIndex = index ?? null;
    this.ensureStopAddress(stop);
    this.replayMap?.focusStop(stop.latitude, stop.longitude, stop.startTime);
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
    const stats = this.quickStats;
    const text = [
      `Route: ${v?.name ?? 'Vehicle'} (${v?.registrationNumber ?? ''})`,
      `Period: ${this.periodLabelDetailed}`,
      stats.distanceKm != null ? `Distance: ${stats.distanceKm} km` : '',
      `Moving: ${stats.moving}`,
      `Stops: ${stats.stops}`
    ].filter(Boolean).join('\n');
    void navigator.clipboard.writeText(text).then(
      () => this.toast.success('Route summary copied'),
      () => this.toast.error('Could not copy summary')
    );
  }

  deviceFor(vehicleId: number): GpsDevice | undefined {
    return this.devices.find(d => d.vehicleId === vehicleId);
  }

  stopLabel(stop: TripStop): string {
    return stop.durationMinutes >= 120 ? 'Parking' : 'Stop';
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
    this.loadingProgress = 100;
    this.clearLoadTimers();
  }

  private clearLoadTimers(): void {
    if (this.progressTimer) {
      clearInterval(this.progressTimer);
      this.progressTimer = undefined;
    }
    if (this.timeoutTimer) {
      clearTimeout(this.timeoutTimer);
      this.timeoutTimer = undefined;
    }
  }

  private ensureRawPositionsLoaded(fromArg?: Date, toArg?: Date): Promise<void> {
    if (!this.vehicleId) return Promise.resolve();
    const from = fromArg ?? new Date(this.from);
    const to = toArg ?? new Date(this.to);
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
      this.rawLoadSub = this.gps.getHistory(this.vehicleId!, from, to).subscribe({
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
    const qp = this.route.snapshot.queryParamMap;
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
      relativeTo: this.route,
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
