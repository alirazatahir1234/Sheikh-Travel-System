import { Component, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription, timer } from 'rxjs';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { VehicleListItem, VehicleStatus } from '../../../core/models/vehicle.model';
import {
  GpsDevice,
  HistoryReplayBundle,
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

const RECENT_VEHICLES_KEY = 'gps_history_recent';
const MAX_RECENT = 5;
const LOAD_TIMEOUT_MS = 20_000;

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
    return (this.bundle?.stops ?? []).filter(s => s.durationMinutes >= 120).length;
  }

  get quickStats() {
    const stats = this.statistics;
    const summary = this.bundle?.summary;
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
    this.loading = true;
    this.loadingProgress = 0;
    this.loadingTimedOut = false;
    this.error = '';
    this.noData = false;
    this.bundle = null;
    this.selectedPosition = null;
    this.rawPositions = [];
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

    this.loadSub = this.gps.getHistoryReplay(this.vehicleId, fromDate, toDate).subscribe({
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
        this.loadRawPositions(fromDate, toDate);
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

  addressLabel(pos: TripReplayPosition | null): string {
    if (!pos) return '—';
    if (pos.address?.trim()) return pos.address;
    return 'Address not provided by device';
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
    const rows = this.rawPositions.length ? this.rawPositions : this.bundle?.route ?? [];
    if (!rows.length || !this.vehicleId) {
      this.toast.error('Load a route before exporting.');
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

  focusStop(stop: TripStop): void {
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

  private loadRawPositions(from: Date, to: Date): void {
    if (!this.vehicleId) return;
    this.gps.getHistory(this.vehicleId, from, to).subscribe({
      next: rows => {
        this.rawPositions = rows.map(r => ({
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
      },
      error: () => {}
    });
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
