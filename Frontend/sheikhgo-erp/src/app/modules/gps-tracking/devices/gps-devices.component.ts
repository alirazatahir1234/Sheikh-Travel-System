import { Component, OnInit, OnDestroy, ViewChild, AfterViewInit, ChangeDetectorRef, NgZone } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { MatPaginator } from '@angular/material/paginator';
import { MatTableDataSource } from '@angular/material/table';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import {
  GpsDevice,
  PositionDto,
  TraccarStatusDto,
  TraccarSyncStatusDto
} from '../../../core/models/gps-tracking.model';
import { GpsRealtimeService } from '../../../core/services/gps-realtime.service';
import {
  assignmentLabel,
  assignmentBadgeClass,
  assignmentTooltip,
  isTrackerInInventory,
  isTrackerInstalled,
  batteryDisplayLabel,
  deviceMatchesSearch,
  formatLastSeenLabel,
  formatLastSeenTooltip,
  gsmSignalLabelForView,
  gsmSignalClassForView,
  ignitionDisplayForView,
  isTrackerIdle,
  isTrackerMoving,
  isTrackerNeverSeen,
  isTrackerOffline,
  isTrackerUnassigned,
  isTelemetryStale,
  isTraccarReachable,
  resolveDisplayTrackerStatus,
  resolveTrackerStatus,
  trackerBrandLabel,
  trackerModelLabel,
  traccarLinkHint,
  vehicleDisplayLabel,
} from '../utils/tracker-status.util';

type DeviceFilter = 'all' | 'online' | 'moving' | 'idle' | 'parked' | 'offline' | 'available' | 'unassigned' | 'never';

@Component({
  standalone: false,
  selector: 'app-gps-devices',
  templateUrl: './gps-devices.component.html',
  styleUrls: ['./gps-devices.component.scss']
})
export class GpsDevicesComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild(MatPaginator) paginator?: MatPaginator;

  devices: GpsDevice[] = [];
  loading = false;
  syncing = false;
  traccarStatus: TraccarStatusDto | null = null;
  traccarSyncStatus: TraccarSyncStatusDto | null = null;
  clockNow = Date.now();
  refreshIntervalMs = 30_000;
  readonly refreshOptions = [
    { label: 'Live (30s)', value: 30_000 },
    { label: '1 min',      value: 60_000 },
    { label: '5 min',      value: 300_000 },
    { label: 'Manual',     value: 0 },
  ];

  searchQuery = '';
  deviceFilter: DeviceFilter = 'all';

  readonly filterOptions: { id: DeviceFilter; label: string }[] = [
    { id: 'all',        label: 'All' },
    { id: 'online',     label: 'Online' },
    { id: 'moving',     label: 'Moving' },
    { id: 'idle',       label: 'Idle' },
    { id: 'parked',     label: 'Parked' },
    { id: 'offline',    label: 'Offline' },
    { id: 'available',  label: 'In Stock' },
    { id: 'unassigned', label: 'Unassigned' },
    { id: 'never',      label: 'Provisioned' }
  ];

  readonly displayedColumns = [
    'vehicle', 'assignment', 'plate', 'driver', 'trackerModel', 'imei',
    'status', 'ignition', 'speed', 'lastSeen', 'signal', 'battery', 'actions'
  ];

  dataSource = new MatTableDataSource<GpsDevice>([]);

  private devicePollTimer?: ReturnType<typeof setInterval>;
  private syncPollTimer?: ReturnType<typeof setInterval>;
  private clockTimer?: ReturnType<typeof setInterval>;
  private realtimeSub?: { unsubscribe(): void };
  private connectionSub?: { unsubscribe(): void };
  private paginatorReady = false;
  realtimeConnected = false;

  constructor(
    private gps: GpsTrackingService,
    private realtime: GpsRealtimeService,
    private toast: UiToastService,
    private cdr: ChangeDetectorRef,
    private router: Router,
    private route: ActivatedRoute,
    private ngZone: NgZone
  ) {}

  ngOnInit(): void {
    this.loadInitial();
    void this.realtime.connect({ asDispatcher: true }).catch(() => {});
    this.realtimeSub = this.realtime.locationUpdates$.subscribe(update => {
      // Events emit outside Angular Zone — re-enter only when mutating UI state.
      this.ngZone.run(() => this.applyRealtimeUpdate(update));
    });
    this.connectionSub = this.realtime.connectionState$.subscribe(state => {
      this.realtimeConnected = state === 'connected';
      this.applyDevicePollInterval();
    });
    this.syncPollTimer = setInterval(() => this.loadSyncStatus(), 5_000);
    this.clockTimer = setInterval(() => {
      this.clockNow = Date.now();
      this.cdr.markForCheck();
    }, 1_000);
  }

  ngOnDestroy(): void {
    if (this.devicePollTimer) clearInterval(this.devicePollTimer);
    if (this.syncPollTimer)   clearInterval(this.syncPollTimer);
    if (this.clockTimer)      clearInterval(this.clockTimer);
    this.realtimeSub?.unsubscribe();
    this.connectionSub?.unsubscribe();
    void this.realtime.releaseDispatcher();
  }

  ngAfterViewInit(): void {
    this.attachPaginator();
    this.paginatorReady = true;
    if (this.devices.length > 0) this.applyFilters();
  }

  get pageDevices(): GpsDevice[] {
    const data = this.dataSource.filteredData;
    const p = this.paginator;
    if (!p) return data;
    const start = p.pageIndex * p.pageSize;
    return data.slice(start, start + p.pageSize);
  }

  get pageRangeStart(): number {
    if (this.filteredCount === 0) return 0;
    const p = this.paginator;
    if (!p) return 1;
    return p.pageIndex * p.pageSize + 1;
  }

  get pageRangeEnd(): number {
    const p = this.paginator;
    if (!p) return this.filteredCount;
    return Math.min((p.pageIndex + 1) * p.pageSize, this.filteredCount);
  }

  get filteredCount(): number { return this.dataSource.filteredData.length; }
  get totalCount():    number { return this.devices.length; }

  /** Single source of truth for Traccar connectivity — drives stale UI across the page. */
  get isTraccarReachable(): boolean {
    return isTraccarReachable(this.traccarStatus?.connected);
  }

  get isTraccarSyncEnabled(): boolean {
    return this.traccarStatus?.syncEnabled !== false
      && this.traccarSyncStatus?.enabled !== false;
  }

  get lastSyncLabel(): string {
    if (this.traccarStatus?.syncEnabled === false || this.traccarSyncStatus?.enabled === false) {
      return 'Sync disabled';
    }
    if (!this.isTraccarReachable) return 'Traccar unreachable';
    const secs = this.lastSyncSecondsAgo;
    if (secs === null) return 'Awaiting first sync';
    if (secs < 60) return `${secs} sec ago`;
    return `${Math.floor(secs / 60)} min ago`;
  }

  get lastSyncHintClass(): string {
    return this.isTraccarReachable && this.isTraccarSyncEnabled ? '' : 'sync-stat-hint--offline';
  }

  get kpiStaleHint(): string | null {
    if (this.traccarStatus?.syncEnabled === false || this.traccarSyncStatus?.enabled === false) {
      return 'Sync disabled';
    }
    if (this.isTraccarReachable) return null;
    const latest = this.latestTelemetryAt;
    if (!latest) return 'Cached';
    return `as of ${formatLastSeenLabel(latest, this.clockNow)}`;
  }

  get connectedOnServer(): number | null {
    if (!this.isTraccarReachable) return null;
    return this.traccarStatus?.deviceCount ?? 0;
  }

  get traccarRegistrationLabel(): string {
    if (!this.isTraccarReachable) return 'Unavailable';
    return String(this.connectedOnServer ?? 0);
  }
  get syncIntervalSeconds(): number { return this.traccarSyncStatus?.positionSyncIntervalSeconds ?? 5; }

  get lastSyncSecondsAgo(): number | null {
    const last = this.traccarSyncStatus?.lastPositionSyncAt;
    if (!last) return null;
    return Math.max(0, Math.floor((this.clockNow - new Date(last).getTime()) / 1000));
  }

  get erpPollLabel(): string {
    if (!this.traccarSyncStatus) return '—';
    if (!this.traccarSyncStatus.enabled) return 'Disabled';
    return `Every ${this.syncIntervalSeconds}s`;
  }

  private get latestTelemetryAt(): string | undefined {
    let latest: number | null = null;
    let iso: string | undefined;
    for (const d of this.devices) {
      if (!d.lastSeenAt) continue;
      const t = new Date(d.lastSeenAt).getTime();
      if (latest == null || t > latest) {
        latest = t;
        iso = d.lastSeenAt;
      }
    }
    return iso;
  }

  get autoSyncLabel(): string {
    if (!this.traccarSyncStatus) return '—';
    return this.traccarSyncStatus.enabled ? 'Enabled' : 'Disabled';
  }

  get autoSyncDetail(): string | null {
    if (!this.traccarSyncStatus?.enabled) return null;
    return `Every ${this.syncIntervalSeconds}s`;
  }

  get liveSyncRunning(): boolean {
    if (!this.traccarSyncStatus?.enabled || !this.traccarStatus?.connected) return false;
    if (this.traccarSyncStatus.isRunning) return true;
    const secs = this.lastSyncSecondsAgo;
    return secs !== null && secs <= this.syncIntervalSeconds * 2;
  }

  private attachPaginator(): void {
    if (this.paginator) this.dataSource.paginator = this.paginator;
  }

  private loadInitial(): void {
    this.loading = true;
    forkJoin({
      devices: this.gps.getDevices().pipe(catchError(() => of([] as GpsDevice[]))),
      traccar: this.gps.getTraccarStatus().pipe(
        catchError(() => of({ connected: false, deviceCount: 0 } as TraccarStatusDto))
      ),
      sync: this.gps.getTraccarSyncStatus().pipe(catchError(() => of(null)))
    }).subscribe(({ devices, traccar, sync }) => {
      this.devices = devices;
      this.traccarStatus = traccar;
      this.traccarSyncStatus = sync;
      this.applyFilters();
      this.loading = false;
      this.cdr.detectChanges();
    });
  }

  load(silent = false): void {
    if (!silent) this.loading = true;
    this.gps.getDevices().subscribe({
      next: d => {
        this.devices = d;
        this.applyFilters();
        this.loading = false;
      },
      error: () => { this.loading = false; }
    });
  }

  loadSyncStatus(): void {
    const prevSyncAt = this.traccarSyncStatus?.lastPositionSyncAt;
    this.gps.getTraccarSyncStatus().subscribe({
      next: s => {
        this.traccarSyncStatus = s;
        if (s?.lastPositionSyncAt && s.lastPositionSyncAt !== prevSyncAt && this.isTraccarReachable) {
          this.load(true);
        }
      },
      error: () => {}
    });
    this.gps.getTraccarStatus().subscribe({
      next: s => { this.traccarStatus = s; },
      error: () => {
        this.traccarStatus = {
          connected: false,
          deviceCount: this.traccarStatus?.deviceCount ?? 0,
          serverVersion: null,
          lastError: 'Traccar server unreachable.',
        };
      }
    });
  }

  syncNow(): void {
    if (this.syncing) return;
    this.syncing = true;
    this.gps.runTraccarSync().subscribe({
      next: () => {
        this.toast.success('Traccar sync completed');
        this.loadSyncStatus();
        this.load(true);
        this.syncing = false;
      },
      error: err => {
        this.toast.error(err?.error?.message ?? 'Sync failed');
        this.syncing = false;
      }
    });
  }

  setDeviceFilter(filter: DeviceFilter): void {
    this.deviceFilter = filter;
    this.applyFilters();
  }

  onSearchChange(): void { this.applyFilters(); }

  private applyFilters(): void {
    const q = this.searchQuery.trim().toLowerCase();
    let rows = [...this.devices];

    if (this.deviceFilter === 'online')        rows = rows.filter(d => !!d.isOnline);
    else if (this.deviceFilter === 'moving')   rows = rows.filter(d => isTrackerMoving(d, this.clockNow));
    else if (this.deviceFilter === 'idle')     rows = rows.filter(d => isTrackerIdle(d, this.clockNow));
    else if (this.deviceFilter === 'parked')   rows = rows.filter(d => resolveTrackerStatus(d, this.clockNow).key === 'parked');
    else if (this.deviceFilter === 'offline')  rows = rows.filter(d => isTrackerOffline(d));
    else if (this.deviceFilter === 'available') rows = rows.filter(d => isTrackerInInventory(d));
    else if (this.deviceFilter === 'unassigned') rows = rows.filter(d => isTrackerUnassigned(d));
    else if (this.deviceFilter === 'never')    rows = rows.filter(d => isTrackerNeverSeen(d));

    if (q) {
      rows = rows.filter(d => deviceMatchesSearch(d, q));
    }

    this.dataSource.data = rows;
    if (this.paginatorReady) this.paginator?.firstPage();
  }

  vehicleLabel = vehicleDisplayLabel;
  traccarHint = traccarLinkHint;
  trackerBrand = trackerBrandLabel;
  trackerModel = trackerModelLabel;
  assignmentText = assignmentLabel;
  assignmentClass = assignmentBadgeClass;
  assignmentHint = assignmentTooltip;
  batteryLabel = batteryDisplayLabel;

  connectionLabel(d: GpsDevice): string {
    return resolveDisplayTrackerStatus(d, this.clockNow, this.isTraccarReachable).label;
  }

  connectionBadgeClass(d: GpsDevice): string {
    return resolveDisplayTrackerStatus(d, this.clockNow, this.isTraccarReachable).badgeClass;
  }

  rowStatusClass(d: GpsDevice): string {
    return resolveDisplayTrackerStatus(d, this.clockNow, this.isTraccarReachable).rowClass;
  }

  ignitionView(d: GpsDevice) {
    return ignitionDisplayForView(d, this.clockNow, this.isTraccarReachable);
  }

  signalLabel(d: GpsDevice): string {
    return gsmSignalLabelForView(d, this.clockNow, this.isTraccarReachable);
  }

  signalClass(d: GpsDevice): string {
    return gsmSignalClassForView(d, this.clockNow, this.isTraccarReachable);
  }

  isRowStale(d: GpsDevice): boolean {
    return isTelemetryStale(d, this.clockNow, this.isTraccarReachable);
  }

  speedLabel(d: GpsDevice): string {
    if (this.isRowStale(d)) {
      if (d.lastSpeed == null || !d.lastSeenAt || d.lastSpeed <= 0) return '—';
      return `${Math.round(d.lastSpeed)} km/h (cached)`;
    }
    if (d.lastSpeed == null || !d.lastSeenAt || d.lastSpeed <= 0) return '—';
    return `${Math.round(d.lastSpeed)} km/h`;
  }

  imeiTooltip(d: GpsDevice): string {
    const parts = [`IMEI: ${d.uniqueId}`];
    if (d.traccarDeviceId) parts.push(`Traccar ID: ${d.traccarDeviceId}`);
    if (d.serialNumber)    parts.push(`Serial: ${d.serialNumber}`);
    return parts.join('\n');
  }

  lastSeenLabel(d: GpsDevice): string {
    if (!d.lastSeenAt) {
      return this.traccarStatus?.connected ? 'Awaiting ping' : 'Never';
    }
    return formatLastSeenLabel(d.lastSeenAt, this.clockNow);
  }

  lastSeenTooltip(d: GpsDevice): string {
    const when = formatLastSeenTooltip(d.lastSeenAt);
    if (!d.lastSeenAt) return when;
    if (this.isRowStale(d)) return `${when} (last known — not verified live)`;
    return when;
  }

  lastSeenClass(d: GpsDevice): string {
    if (!d.lastSeenAt) return 'seen-never';
    const age = this.clockNow - new Date(d.lastSeenAt).getTime();
    if (age < 2 * 60_000)  return 'seen-live';
    if (age < 10 * 60_000) return 'seen-recent';
    if (age < 30 * 60_000) return 'seen-stale';
    return 'seen-old';
  }

  get kpiTiles() {
    const stale = !this.isTraccarReachable;
    const hint = this.kpiStaleHint;
    return [
      { filter: 'all' as DeviceFilter,        label: 'Total',      value: this.devices.length,                                            icon: 'sensors',      color: stale ? '#94a3b8' : '#64748b', stale, hint },
      { filter: 'online' as DeviceFilter,     label: 'Online',     value: this.devices.filter(d => !!d.isOnline).length,                  icon: 'wifi',         color: stale ? '#94a3b8' : '#22c55e', stale, hint },
      { filter: 'moving' as DeviceFilter,     label: 'Moving',     value: this.devices.filter(d => isTrackerMoving(d, this.clockNow)).length,            icon: 'speed',        color: stale ? '#94a3b8' : '#3b82f6', stale, hint },
      { filter: 'idle' as DeviceFilter,       label: 'Idle',       value: this.devices.filter(d => isTrackerIdle(d, this.clockNow)).length,              icon: 'pause_circle', color: stale ? '#94a3b8' : '#f59e0b', stale, hint },
      { filter: 'parked' as DeviceFilter,     label: 'Parked',     value: this.devices.filter(d => resolveTrackerStatus(d, this.clockNow).key === 'parked').length, icon: 'local_parking', color: stale ? '#94a3b8' : '#10b981', stale, hint },
      { filter: 'offline' as DeviceFilter,    label: 'Offline',    value: this.devices.filter(d => isTrackerOffline(d)).length,           icon: 'wifi_off',     color: stale ? '#94a3b8' : '#ef4444', stale, hint },
      { filter: 'available' as DeviceFilter,  label: 'In Stock',   value: this.devices.filter(d => isTrackerInInventory(d)).length,       icon: 'inventory_2',  color: stale ? '#94a3b8' : '#8b5cf6', stale, hint },
      { filter: 'never' as DeviceFilter,      label: 'Provisioned', value: this.devices.filter(d => isTrackerNeverSeen(d)).length,          icon: 'sensors_off',  color: '#94a3b8', stale, hint },
      { filter: 'unassigned' as DeviceFilter, label: 'Unassigned', value: this.devices.filter(d => isTrackerUnassigned(d)).length,        icon: 'link_off',     color: stale ? '#94a3b8' : '#a855f7', stale, hint },
    ];
  }

  setRefreshInterval(ms: number): void {
    this.refreshIntervalMs = ms;
    if (this.devicePollTimer) clearInterval(this.devicePollTimer);
    if (ms > 0) this.devicePollTimer = setInterval(() => this.load(true), ms);
  }

  private applyDevicePollInterval(): void {
    const ms = this.realtimeConnected ? 60_000 : 30_000;
    this.setRefreshInterval(ms);
  }

  private applyRealtimeUpdate(update: PositionDto): void {
    const idx = this.devices.findIndex(d => d.vehicleId === update.vehicleId);
    if (idx < 0) return;

    const current = this.devices[idx];
    this.devices[idx] = {
      ...current,
      lastSeenAt: update.timestamp,
      lastSpeed: Number(update.speed) || 0,
      lastIgnition: update.ignition ?? current.lastIgnition,
      isOnline: true,
    };
    this.applyFilters();
    this.cdr.markForCheck();
  }

  openView(d: GpsDevice): void {
    if (isTrackerInstalled(d)) {
      void this.router.navigate([d.id], { relativeTo: this.route });
      return;
    }
    if (this.canInstall(d)) {
      void this.router.navigate([d.id, 'install'], { relativeTo: this.route });
      return;
    }
    void this.router.navigate([d.id], { relativeTo: this.route });
  }

  openEdit(d: GpsDevice): void {
    void this.router.navigate([d.id, 'edit'], { relativeTo: this.route });
  }

  openInstall(d: GpsDevice): void {
    if (isTrackerInstalled(d)) {
      this.toast.warning(
        `This tracker is already installed on ${d.vehicleName ?? 'a vehicle'} (${d.plateNumber ?? ''}). Remove or transfer first.`
      );
      void this.router.navigate([d.id], { relativeTo: this.route });
      return;
    }
    void this.router.navigate([d.id, 'install'], { relativeTo: this.route });
  }

  openReassign(d: GpsDevice): void {
    void this.router.navigate([d.id, 'transfer'], { relativeTo: this.route });
  }

  uninstallTracker(d: GpsDevice): void {
    const label = d.name || d.uniqueId;
    if (!confirm(`Return "${label}" to inventory? This will unlink the vehicle assignment.`)) return;
    this.gps.uninstallTracker(d.id).subscribe({
      next: () => {
        this.toast.success('Tracker returned to inventory');
        this.load();
      },
      error: err => this.toast.error(err?.error?.message ?? 'Uninstall failed')
    });
  }

  canInstall(d: GpsDevice): boolean {
    return isTrackerInInventory(d);
  }

  canReassign(d: GpsDevice): boolean {
    return isTrackerInstalled(d);
  }

  canUninstall(d: GpsDevice): boolean {
    return isTrackerInstalled(d);
  }

  goToLiveMap(d: GpsDevice): void {
    this.router.navigate(['../live'], { relativeTo: this.route, queryParams: { vehicleId: d.vehicleId } });
  }

  goToHistory(d: GpsDevice): void {
    this.router.navigate(['../history'], { relativeTo: this.route, queryParams: { vehicleId: d.vehicleId } });
  }

  goToCommands(d: GpsDevice, command?: string): void {
    const queryParams: Record<string, string | number> = { deviceId: d.id };
    if (command) queryParams['command'] = command;
    this.router.navigate(['../commands'], { relativeTo: this.route, queryParams });
  }

  goToTrips(d: GpsDevice): void {
    this.router.navigate(['../trips'], { relativeTo: this.route, queryParams: { vehicleId: d.vehicleId } });
  }

  goToAlerts(d: GpsDevice): void {
    this.router.navigate(['../alerts'], { relativeTo: this.route, queryParams: { vehicleId: d.vehicleId } });
  }

  goToGeofences(_d: GpsDevice): void {
    this.router.navigate(['../geofences'], { relativeTo: this.route });
  }

  openCreate(): void {
    void this.router.navigate(['register'], { relativeTo: this.route });
  }

  deleteDevice(d: GpsDevice): void {
    const label = d.vehicleName || d.name;
    if (!confirm(`Remove tracker for "${label}"?`)) return;
    this.gps.deleteTracker(d.id).subscribe({
      next: () => { this.toast.success('Device removed'); this.load(); },
      error: err => this.toast.error(err?.error?.message ?? 'Delete failed')
    });
  }
}
