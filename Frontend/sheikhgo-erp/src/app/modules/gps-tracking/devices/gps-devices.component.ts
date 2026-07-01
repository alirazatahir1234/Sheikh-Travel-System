import { Component, OnInit, OnDestroy, ViewChild, AfterViewInit, ChangeDetectorRef } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { MatPaginator } from '@angular/material/paginator';
import { MatTableDataSource } from '@angular/material/table';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import {
  GpsDevice,
  TraccarStatusDto,
  TraccarSyncStatusDto
} from '../../../core/models/gps-tracking.model';
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
  gsmSignalLabel,
  gsmSignalClass,
  ignitionDisplay,
  isTrackerIdle,
  isTrackerMoving,
  isTrackerNeverSeen,
  isTrackerOffline,
  isTrackerUnassigned,
  resolveTrackerStatus,
  trackerBrandLabel,
  trackerModelLabel,
  traccarLinkHint,
  vehicleDisplayLabel,
} from '../utils/tracker-status.util';

type DeviceFilter = 'all' | 'online' | 'moving' | 'idle' | 'parked' | 'offline' | 'available' | 'unassigned' | 'never';

@Component({
  selector: 'app-gps-devices',
  templateUrl: './gps-devices.component.html',
  styleUrls: ['./gps-devices.component.scss']
})
export class GpsDevicesComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild(MatPaginator) paginator?: MatPaginator;

  devices: GpsDevice[] = [];
  loading = false;
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
  private paginatorReady = false;

  constructor(
    private gps: GpsTrackingService,
    private toast: UiToastService,
    private cdr: ChangeDetectorRef,
    private router: Router,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    this.loadInitial();
    this.devicePollTimer = setInterval(() => this.load(true), 30_000);
    this.syncPollTimer   = setInterval(() => this.loadSyncStatus(), 5_000);
    this.clockTimer      = setInterval(() => {
      this.clockNow = Date.now();
      this.cdr.markForCheck();
    }, 1_000);
  }

  ngOnDestroy(): void {
    if (this.devicePollTimer) clearInterval(this.devicePollTimer);
    if (this.syncPollTimer)   clearInterval(this.syncPollTimer);
    if (this.clockTimer)      clearInterval(this.clockTimer);
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

  get connectedOnServer(): number { return this.traccarStatus?.deviceCount ?? 0; }
  get syncIntervalSeconds(): number { return this.traccarSyncStatus?.positionSyncIntervalSeconds ?? 5; }

  get lastSyncSecondsAgo(): number | null {
    const last = this.traccarSyncStatus?.lastPositionSyncAt;
    if (!last) return null;
    return Math.max(0, Math.floor((this.clockNow - new Date(last).getTime()) / 1000));
  }

  get lastSyncLabel(): string {
    const secs = this.lastSyncSecondsAgo;
    if (secs === null) return 'Awaiting first sync';
    if (secs < 60) return `${secs} sec ago`;
    return `${Math.floor(secs / 60)} min ago`;
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
    return !!(this.traccarSyncStatus?.enabled && this.traccarStatus?.connected && this.traccarSyncStatus.isRunning);
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
    this.gps.getTraccarSyncStatus().subscribe({
      next: s => { this.traccarSyncStatus = s; },
      error: () => {}
    });
    this.gps.getTraccarStatus().subscribe({
      next: s => { this.traccarStatus = s; },
      error: () => {}
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
    else if (this.deviceFilter === 'moving')   rows = rows.filter(d => isTrackerMoving(d));
    else if (this.deviceFilter === 'idle')     rows = rows.filter(d => isTrackerIdle(d));
    else if (this.deviceFilter === 'parked')   rows = rows.filter(d => resolveTrackerStatus(d).key === 'parked');
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
  ignitionView = ignitionDisplay;
  signalLabel = gsmSignalLabel;
  signalClass = gsmSignalClass;
  batteryLabel = batteryDisplayLabel;

  connectionLabel(d: GpsDevice): string {
    return resolveTrackerStatus(d).label;
  }

  connectionBadgeClass(d: GpsDevice): string {
    return resolveTrackerStatus(d).badgeClass;
  }

  rowStatusClass(d: GpsDevice): string {
    return resolveTrackerStatus(d).rowClass;
  }

  speedLabel(d: GpsDevice): string {
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
    if (!this.traccarStatus?.connected) return `${when} (last known — cached)`;
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
    return [
      { filter: 'all' as DeviceFilter,        label: 'Total',      value: this.devices.length,                                            icon: 'sensors',      color: '#64748b' },
      { filter: 'online' as DeviceFilter,     label: 'Online',     value: this.devices.filter(d => !!d.isOnline).length,                  icon: 'wifi',         color: '#22c55e' },
      { filter: 'moving' as DeviceFilter,     label: 'Moving',     value: this.devices.filter(d => isTrackerMoving(d)).length,            icon: 'speed',        color: '#3b82f6' },
      { filter: 'idle' as DeviceFilter,       label: 'Idle',       value: this.devices.filter(d => isTrackerIdle(d)).length,              icon: 'pause_circle', color: '#f59e0b' },
      { filter: 'parked' as DeviceFilter,     label: 'Parked',     value: this.devices.filter(d => resolveTrackerStatus(d).key === 'parked').length, icon: 'local_parking', color: '#10b981' },
      { filter: 'offline' as DeviceFilter,    label: 'Offline',    value: this.devices.filter(d => isTrackerOffline(d)).length,           icon: 'wifi_off',     color: '#ef4444' },
      { filter: 'available' as DeviceFilter,  label: 'In Stock',   value: this.devices.filter(d => isTrackerInInventory(d)).length,       icon: 'inventory_2',  color: '#8b5cf6' },
      { filter: 'never' as DeviceFilter,      label: 'Provisioned', value: this.devices.filter(d => isTrackerNeverSeen(d)).length,          icon: 'sensors_off',  color: '#94a3b8' },
      { filter: 'unassigned' as DeviceFilter, label: 'Unassigned', value: this.devices.filter(d => isTrackerUnassigned(d)).length,        icon: 'link_off',     color: '#a855f7' },
    ];
  }

  setRefreshInterval(ms: number): void {
    this.refreshIntervalMs = ms;
    if (this.devicePollTimer) clearInterval(this.devicePollTimer);
    if (ms > 0) this.devicePollTimer = setInterval(() => this.load(true), ms);
  }

  openView(d: GpsDevice): void {
    void this.router.navigate([d.id, 'edit'], { relativeTo: this.route });
  }

  openEdit(d: GpsDevice): void {
    void this.router.navigate([d.id, 'edit'], { relativeTo: this.route });
  }

  openInstall(d: GpsDevice): void {
    void this.router.navigate([d.id, 'install'], { relativeTo: this.route });
  }

  openReassign(d: GpsDevice): void {
    void this.router.navigate([d.id, 'install'], { relativeTo: this.route, queryParams: { reassign: 1 } });
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
