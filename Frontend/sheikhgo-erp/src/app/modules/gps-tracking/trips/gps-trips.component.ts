import { Component, OnInit } from '@angular/core';
import { forkJoin, of, TimeoutError } from 'rxjs';
import { catchError, timeout } from 'rxjs/operators';
import { HttpErrorResponse } from '@angular/common/http';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { ExportService } from '../../../core/services/export.service';
import { PlatformService } from '../../../core/services/platform.service';
import { DriverService } from '../../../core/services/driver.service';
import {
  GpsTrip,
  TripAnalyticsBundle,
  TripAnalyticsSummary,
  TripDeviceContext,
  TripEvent,
  TripReplayPosition,
  TripReplayBundle,
  TripStop,
  TripVehicleOption
} from '../../../core/models/gps-tracking.model';
import { Branch, Department } from '../../../core/models/platform.model';
import {
  TripDatePreset,
  TRIP_DATE_PRESETS,
  applyTripDatePreset,
  formatPeriodLabel,
  toDatetimeLocalInput
} from '../utils/trip-date-preset.util';
import { eventIconsForTrip } from '../utils/trip-event.util';

const EMPTY_EVENTS: TripEvent[] = [];
const EMPTY_REPLAY: TripReplayBundle = { route: [], playback: [], stops: [], events: [], summary: null };
const TRIPS_TIMEOUT_MS = 90_000;
const ANALYTICS_TIMEOUT_MS = 90_000;
const CONTEXT_TIMEOUT_MS = 20_000;
const REPLAY_TIMEOUT_MS = 60_000;

interface FleetDriverOption {
  id: number;
  fullName: string;
}

@Component({
  standalone: false,
  selector: 'app-gps-trips',
  templateUrl: './gps-trips.component.html',
  styleUrls: ['./gps-trips.component.scss']
})
export class GpsTripsComponent implements OnInit {
  readonly datePresets = TRIP_DATE_PRESETS;
  readonly pageSize = 10;

  datePreset: TripDatePreset = 'thisWeek';

  vehicleOptions: TripVehicleOption[] = [];
  vehicleId: number | null = null;
  driverFilter = '';
  from = '';
  to = '';
  logSearch = '';

  // Fleet-wide filters (server-side query params) — only meaningful when vehicleId is null.
  branches: Branch[] = [];
  departments: Department[] = [];
  fleetDrivers: FleetDriverOption[] = [];
  branchId: number | null = null;
  departmentId: number | null = null;
  fleetDriverId: number | null = null;

  // Client-side range filters, applied over whatever page of trips is loaded.
  distanceMin: number | null = null;
  distanceMax: number | null = null;
  speedMin: number | null = null;
  speedMax: number | null = null;

  trips: GpsTrip[] = [];
  filteredTrips: GpsTrip[] = [];
  paginatedTrips: GpsTrip[] = [];
  pageIndex = 0;

  analytics: TripAnalyticsBundle | null = null;
  fleetSummary: TripAnalyticsSummary | null = null;
  context: TripDeviceContext | null = null;
  replayRoute: TripReplayPosition[] = [];
  replayPlayback: TripReplayPosition[] = [];
  replayStops: TripStop[] = [];
  replayEvents: TripEvent[] = [];
  replayLoading = false;

  loading = false;
  analyticsLoading = false;
  error = '';
  apiWarning = '';
  shareMessage = '';
  lastRefreshedAt: Date | null = null;

  selectedTrip: GpsTrip | null = null;

  /** Cached to avoid new array refs on every change detection. */
  analyticsEvents: TripEvent[] = EMPTY_EVENTS;
  distanceSparklineData: number[] = [];
  driverOptionsList: string[] = [];
  private tripEventIconMap = new Map<string, string[]>();

  get isFleetWide(): boolean {
    return !this.vehicleId;
  }

  get summary(): TripAnalyticsSummary | null {
    return this.isFleetWide ? this.fleetSummary : (this.analytics?.summary ?? null);
  }

  get periodLabel(): string {
    return formatPeriodLabel(this.from, this.to);
  }

  get selectedVehicle(): TripVehicleOption | null {
    if (!this.vehicleId) return null;
    return this.vehicleOptions.find(v => v.vehicleId === this.vehicleId) ?? null;
  }

  get driverOptions(): string[] {
    return this.driverOptionsList;
  }

  get distanceSparkline(): number[] {
    return this.distanceSparklineData;
  }

  get idlePercentLabel(): string {
    if (!this.summary) return '—';
    const total = this.summary.drivingMinutes + this.summary.idleMinutes;
    if (!total) return '0%';
    const pct = Math.round((this.summary.idleMinutes / total) * 100);
    return `${pct}%`;
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.filteredTrips.length / this.pageSize));
  }

  get distanceValue(): string | number {
    return this.summary ? this.summary.distanceKm.toFixed(1) : '—';
  }

  get maxSpeedValue(): string | number {
    return this.summary ? Math.round(this.summary.maxSpeedKmh) : '—';
  }

  get fuelValue(): string | number {
    if (!this.summary || this.summary.fuelLiters == null) return '—';
    return this.summary.fuelLiters.toFixed(1);
  }

  get avgSpeedValue(): string | number {
    return this.summary ? Math.round(this.summary.avgSpeedKmh) : '—';
  }

  get stopCountValue(): string | number {
    return this.summary ? this.summary.stopCount : '—';
  }

  get overspeedCountValue(): string | number {
    return this.summary ? this.summary.overspeedCount : '—';
  }

  get engineHoursValue(): string | number {
    if (!this.summary || this.summary.engineHours == null) return '—';
    return this.summary.engineHours.toFixed(1);
  }

  get driveTimeValue(): string {
    return this.summary ? this.formatDuration(this.summary.drivingMinutes) : '—';
  }

  get emptyReasons(): string[] {
    if (this.isFleetWide) {
      return ['No trips match the selected filters and date range.'];
    }
    const reasons: string[] = [];
    if (this.context && !this.context.hasTraccarLink) reasons.push('Tracker not assigned or not linked to Traccar');
    if (this.context && !this.context.isOnline) reasons.push('GPS device appears offline');
    if (this.context?.lastIgnition === false) reasons.push('Ignition may not have been turned ON during this period');
    if (!this.trips.length && this.context?.isOnline) reasons.push('Vehicle may have remained stationary');
    if (!this.trips.length) reasons.push('No GPS trip data for the selected date range');
    return [...new Set(reasons)];
  }

  constructor(
    private gps: GpsTrackingService,
    private exportService: ExportService,
    private platform: PlatformService,
    private driverService: DriverService
  ) {}

  ngOnInit(): void {
    this.applyPreset('thisWeek');
    this.gps.getDevices().subscribe({
      next: devices => {
        this.vehicleOptions = devices
          .filter(d => d.vehicleId)
          .map(d => ({
            vehicleId: d.vehicleId!,
            vehicleName: d.vehicleName ?? `Vehicle #${d.vehicleId}`,
            plateNumber: d.plateNumber ?? '—',
            deviceName: d.name,
            uniqueId: d.uniqueId,
            isOnline: !!d.isOnline
          }))
          .sort((a, b) => a.vehicleName.localeCompare(b.vehicleName));
      }
    });
    this.platform.getBranches().subscribe({ next: b => { this.branches = b; }, error: () => {} });
    this.platform.getDepartments().subscribe({ next: d => { this.departments = d; }, error: () => {} });
    this.driverService.getAll(1, 500).subscribe({
      next: r => {
        this.fleetDrivers = r.items.map(d => ({ id: d.id, fullName: d.fullName }));
      },
      error: () => {}
    });
    this.load();
  }

  applyPreset(preset: TripDatePreset): void {
    this.datePreset = preset;
    if (preset === 'custom') return;
    const range = applyTripDatePreset(preset);
    this.from = toDatetimeLocalInput(range.from);
    this.to = toDatetimeLocalInput(range.to);
  }

  onPresetChange(preset: TripDatePreset): void {
    this.applyPreset(preset);
    if (preset !== 'custom') this.load();
  }

  onVehicleChange(): void {
    this.driverFilter = '';
    this.load();
  }

  onFleetFilterChange(): void {
    this.load();
  }

  formatLastUpdated(): string {
    if (!this.lastRefreshedAt) return 'Never';
    const secs = Math.floor((Date.now() - this.lastRefreshedAt.getTime()) / 1000);
    if (secs < 5) return 'Just now';
    if (secs < 60) return `${secs} seconds ago`;
    const mins = Math.floor(secs / 60);
    if (mins < 60) return `${mins} minute${mins === 1 ? '' : 's'} ago`;
    return this.lastRefreshedAt.toLocaleTimeString();
  }

  statusLabel(option: TripVehicleOption): string {
    return option.isOnline ? 'Online' : 'Offline';
  }

  load(): void {
    const fromDate = this.from ? new Date(this.from) : undefined;
    const toDate = this.to ? new Date(this.to) : undefined;
    if (!fromDate || !toDate || fromDate > toDate) {
      this.error = 'End Date cannot be earlier than Start Date.';
      return;
    }

    this.loading = true;
    this.analyticsLoading = true;
    this.error = '';
    this.apiWarning = '';
    this.selectedTrip = null;
    this.replayRoute = [];
    this.replayPlayback = [];
    this.replayStops = [];
    this.replayEvents = [];
    this.analytics = null;
    this.fleetSummary = null;
    this.analyticsEvents = EMPTY_EVENTS;

    const fleetFilters = { branchId: this.branchId, departmentId: this.departmentId, driverId: this.fleetDriverId };

    forkJoin({
      trips: this.gps.getTrips(this.vehicleId, fromDate, toDate, fleetFilters).pipe(
        timeout(TRIPS_TIMEOUT_MS),
        catchError((err: HttpErrorResponse | TimeoutError) => {
          if (err instanceof TimeoutError) {
            this.error = 'Trip list request timed out. Try a shorter date range.';
          } else if ((err as HttpErrorResponse).status !== 404) {
            this.error = (err as HttpErrorResponse).error?.message ?? 'Failed to load trips.';
          }
          return of([] as GpsTrip[]);
        })
      ),
      context: this.vehicleId
        ? this.gps.getTripContext(this.vehicleId).pipe(
            timeout(CONTEXT_TIMEOUT_MS),
            catchError((err: HttpErrorResponse | TimeoutError) => {
              if (err instanceof TimeoutError) {
                this.apiWarning = 'Device context timed out — showing trips without live status.';
              } else if ((err as HttpErrorResponse).status === 404) {
                this.apiWarning = 'Trip analytics API unavailable — restart backend with latest build.';
              }
              return of(null);
            })
          )
        : of(null)
    }).subscribe({
      next: ({ trips, context }) => {
        this.trips = trips;
        this.context = context;
        this.applyFilters();
        this.loading = false;
        this.lastRefreshedAt = new Date();
      },
      error: err => {
        console.error('Failed to load trips', err);
        this.error = err?.error?.message ?? 'Failed to load trips.';
        this.loading = false;
        this.analyticsLoading = false;
      }
    });

    if (this.vehicleId) {
      this.gps.getTripAnalytics(this.vehicleId, fromDate, toDate).pipe(
        timeout(ANALYTICS_TIMEOUT_MS),
        catchError((err: HttpErrorResponse | TimeoutError) => {
          if (err instanceof TimeoutError) {
            this.apiWarning = 'Trip analytics timed out — Traccar may be slow. Trips are still shown below.';
          } else if ((err as HttpErrorResponse).status === 404) {
            this.apiWarning = 'Trip analytics API unavailable — restart backend with latest build.';
          }
          return of(null);
        })
      ).subscribe(analytics => {
        this.analytics = analytics;
        this.analyticsEvents = analytics?.events ?? EMPTY_EVENTS;
        this.rebuildTripCaches();
        this.analyticsLoading = false;
      });
    } else {
      this.gps.getFleetTripSummary(fromDate, toDate, fleetFilters).pipe(
        timeout(ANALYTICS_TIMEOUT_MS),
        catchError((err: HttpErrorResponse | TimeoutError) => {
          if (err instanceof TimeoutError) {
            this.apiWarning = 'Fleet summary timed out — Traccar may be slow. Trips are still shown below.';
          }
          return of(null);
        })
      ).subscribe(summary => {
        this.fleetSummary = summary;
        this.analyticsEvents = EMPTY_EVENTS;
        this.rebuildTripCaches();
        this.analyticsLoading = false;
      });
    }
  }

  private rebuildTripCaches(): void {
    const names = this.trips
      .map(t => t.driverName?.trim())
      .filter((n): n is string => !!n);
    this.driverOptionsList = [...new Set(names)].sort((a, b) => a.localeCompare(b));
    this.distanceSparklineData = this.trips.slice(0, 12).map(t => Number(t.distanceKm) || 0);

    this.tripEventIconMap.clear();
    const events = this.analyticsEvents;
    for (const trip of this.trips) {
      const key = this.tripKey(trip);
      this.tripEventIconMap.set(key, eventIconsForTrip(events, trip.startTime, trip.endTime));
    }
  }

  private tripKey(trip: GpsTrip): string {
    return `${trip.vehicleId}|${trip.startTime}|${trip.endTime}`;
  }

  applyFilters(): void {
    const q = this.logSearch.trim().toLowerCase();
    const opt = this.vehicleId ? this.vehicleOptions.find(v => v.vehicleId === this.vehicleId) : undefined;
    let rows = [...this.trips];

    // Driver-name filter only applies in single-vehicle mode (fleet-wide driver filtering already
    // happened server-side via fleetDriverId).
    if (!this.isFleetWide && this.driverFilter) {
      rows = rows.filter(t => t.driverName === this.driverFilter);
    }

    if (this.distanceMin != null) rows = rows.filter(t => t.distanceKm >= this.distanceMin!);
    if (this.distanceMax != null) rows = rows.filter(t => t.distanceKm <= this.distanceMax!);
    if (this.speedMin != null) rows = rows.filter(t => t.maxSpeedKmh >= this.speedMin!);
    if (this.speedMax != null) rows = rows.filter(t => t.maxSpeedKmh <= this.speedMax!);

    if (q) {
      rows = rows.filter(t =>
        [t.vehicleName, t.plateNumber, t.deviceName, t.driverName, t.startAddress, t.endAddress,
          opt?.plateNumber, opt?.uniqueId, String(t.vehicleId)]
          .some(v => v?.toLowerCase().includes(q))
      );
    }

    this.filteredTrips = rows;
    this.pageIndex = 0;
    this.updatePage();
    this.rebuildTripCaches();
  }

  updatePage(): void {
    const start = this.pageIndex * this.pageSize;
    this.paginatedTrips = this.filteredTrips.slice(start, start + this.pageSize);
  }

  prevPage(): void {
    if (this.pageIndex > 0) {
      this.pageIndex--;
      this.updatePage();
    }
  }

  nextPage(): void {
    if (this.pageIndex < this.totalPages - 1) {
      this.pageIndex++;
      this.updatePage();
    }
  }

  selectTrip(trip: GpsTrip): void {
    this.selectedTrip = trip;
    this.replayLoading = true;
    this.replayRoute = [];
    this.replayPlayback = [];
    this.replayStops = [];
    this.replayEvents = [];

    // Always use the trip row's own vehicleId — in fleet-wide mode this.vehicleId is null, and
    // even in single-vehicle mode it's the more precise source of truth for this specific row.
    this.gps.getTripReplay(trip.vehicleId, new Date(trip.startTime), new Date(trip.endTime)).pipe(
      timeout(REPLAY_TIMEOUT_MS),
      catchError((err: HttpErrorResponse | TimeoutError) => {
        console.error('Replay load failed', err);
        if (err instanceof TimeoutError) {
          this.apiWarning = 'Route replay timed out — try a shorter trip window.';
        } else if ((err as HttpErrorResponse).status === 404) {
          this.apiWarning = 'Trip replay API unavailable — restart backend with latest build.';
        }
        return of(EMPTY_REPLAY);
      })
    ).subscribe({
      next: bundle => {
        this.replayRoute = bundle.route ?? [];
        this.replayPlayback = bundle.playback?.length ? bundle.playback : bundle.route ?? [];
        this.replayStops = bundle.stops ?? [];
        this.replayEvents = bundle.events?.length ? bundle.events : this.filterPeriodEvents(trip);
        this.replayLoading = false;
      },
      error: () => {
        this.replayLoading = false;
      }
    });
  }

  private filterPeriodEvents(trip: GpsTrip): TripEvent[] {
    const start = new Date(trip.startTime).getTime();
    const end = new Date(trip.endTime).getTime();
    return this.analyticsEvents.filter(e => {
      const t = new Date(e.time).getTime();
      return t >= start && t <= end;
    });
  }

  viewTrip(trip: GpsTrip, event: Event): void {
    event.stopPropagation();
    this.selectTrip(trip);
  }

  tripEventIcons(trip: GpsTrip): string[] {
    return this.tripEventIconMap.get(this.tripKey(trip)) ?? [];
  }

  vehicleLabel(trip: GpsTrip): string {
    const name = trip.vehicleName ?? `Vehicle #${trip.vehicleId}`;
    return trip.plateNumber ? `${name} (${trip.plateNumber})` : name;
  }

  shareReport(): void {
    const url = window.location.href;
    const title = 'GPS Trip Analytics';
    const text = `Trip analytics for ${this.isFleetWide ? 'fleet' : (this.selectedVehicle?.vehicleName ?? 'vehicle')} — ${this.periodLabel}`;
    if (navigator.share) {
      navigator.share({ title, text, url }).catch(() => this.copyShareUrl(url));
    } else {
      this.copyShareUrl(url);
    }
  }

  private copyShareUrl(url: string): void {
    navigator.clipboard?.writeText(url).then(() => {
      this.shareMessage = 'Link copied to clipboard';
      setTimeout(() => { this.shareMessage = ''; }, 2500);
    }).catch(() => {
      this.shareMessage = 'Could not copy link';
    });
  }

  exportExcel(): void {
    this.exportRows('excel');
  }

  exportPdf(): void {
    this.exportRows('pdf');
  }

  exportCsv(): void {
    this.exportRows('csv');
  }

  private exportRows(kind: 'excel' | 'pdf' | 'csv'): void {
    const rows = this.filteredTrips.length ? this.filteredTrips : this.trips;
    if (!rows.length) return;
    const cols = [
      { header: 'Vehicle', accessor: (t: GpsTrip) => this.vehicleLabel(t) },
      { header: 'Device', accessor: (t: GpsTrip) => t.deviceName ?? '-' },
      { header: 'Start', accessor: (t: GpsTrip) => t.startTime },
      { header: 'End', accessor: (t: GpsTrip) => t.endTime },
      { header: 'Start Address', accessor: (t: GpsTrip) => t.startAddress ?? '-' },
      { header: 'End Address', accessor: (t: GpsTrip) => t.endAddress ?? '-' },
      { header: 'Distance (km)', accessor: (t: GpsTrip) => t.distanceKm },
      { header: 'Avg speed', accessor: (t: GpsTrip) => t.avgSpeedKmh },
      { header: 'Max speed', accessor: (t: GpsTrip) => t.maxSpeedKmh },
      { header: 'Duration (min)', accessor: (t: GpsTrip) => t.durationMinutes },
      { header: 'Fuel (L)', accessor: (t: GpsTrip) => t.fuelLiters ?? '-' },
      { header: 'Driver', accessor: (t: GpsTrip) => t.driverName ?? '-' }
    ];
    const scopeLabel = this.isFleetWide ? 'Fleet Trips' : (this.selectedVehicle?.vehicleName ?? 'Trip Analytics');
    const meta = {
      filename: `${scopeLabel.toLowerCase().replace(/\s+/g, '-')}-${this.periodLabel.toLowerCase().replace(/\s+/g, '-')}`,
      sheetName: 'Trips',
      title: `${scopeLabel} — ${this.periodLabel}`
    };
    if (kind === 'pdf') this.exportService.exportPdf(rows, cols, meta);
    else if (kind === 'csv') this.exportService.exportCsv(rows, cols, meta);
    else this.exportService.exportExcel(rows, cols, meta);
  }

  formatDuration(minutes: number): string {
    if (minutes < 60) return `${minutes} min`;
    const h = Math.floor(minutes / 60);
    const m = minutes % 60;
    return m ? `${h}h ${m}m` : `${h}h`;
  }

  formatTripDate(trip: GpsTrip): string {
    const start = new Date(trip.startTime);
    const end = new Date(trip.endTime);
    const fmt = (d: Date) => d.toLocaleString(undefined, { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' });
    return `${fmt(start)} – ${fmt(end)}`;
  }

  formatLocation(trip: GpsTrip): string {
    const start = trip.startAddress?.trim() || 'Unknown';
    const end = trip.endAddress?.trim() || 'Unknown';
    return `${start} → ${end}`;
  }
}
