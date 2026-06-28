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

type DeviceFilter = 'all' | 'online' | 'moving' | 'idle' | 'offline' | 'unlinked' | 'never';

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
    { id: 'all',      label: 'All' },
    { id: 'online',   label: 'Online' },
    { id: 'moving',   label: 'Moving' },
    { id: 'idle',     label: 'Idle' },
    { id: 'offline',  label: 'Offline' },
    { id: 'unlinked', label: 'Unlinked' },
    { id: 'never',    label: 'Never Seen' }
  ];

  readonly displayedColumns = [
    'vehicle', 'plate', 'driver', 'trackerModel', 'imei',
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

  // ── Pagination helpers ───────────────────────────────────────────────────────

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

  get nextSyncSeconds(): number | null {
    if (this.lastSyncSecondsAgo === null) return null;
    return Math.max(0, this.syncIntervalSeconds - this.lastSyncSecondsAgo);
  }

  get lastSyncLabel(): string {
    const secs = this.lastSyncSecondsAgo;
    if (secs === null) return 'Awaiting first sync';
    if (secs < 60) return `${secs} sec ago`;
    return `${Math.floor(secs / 60)} min ago`;
  }

  get nextSyncLabel(): string {
    const secs = this.nextSyncSeconds;
    if (secs === null) return '—';
    return `~${secs} sec`;
  }

  private attachPaginator(): void {
    if (this.paginator) this.dataSource.paginator = this.paginator;
  }

  // ── Data loading ─────────────────────────────────────────────────────────────

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
    else if (this.deviceFilter === 'moving')   rows = rows.filter(d => this.isMoving(d));
    else if (this.deviceFilter === 'idle')     rows = rows.filter(d => this.isIdle(d));
    else if (this.deviceFilter === 'offline')  rows = rows.filter(d => !!d.lastSeenAt && !d.isOnline);
    else if (this.deviceFilter === 'unlinked') rows = rows.filter(d => !d.vehicleId || !d.vehicleName);
    else if (this.deviceFilter === 'never')    rows = rows.filter(d => !d.lastSeenAt);

    if (q) {
      rows = rows.filter(d =>
        (d.vehicleName?.toLowerCase().includes(q) ?? false) ||
        (d.plateNumber?.toLowerCase().includes(q) ?? false) ||
        (d.driverName?.toLowerCase().includes(q) ?? false) ||
        (d.model?.toLowerCase().includes(q) ?? false) ||
        d.uniqueId.toLowerCase().includes(q) ||
        d.name.toLowerCase().includes(q)
      );
    }

    this.dataSource.data = rows;
    if (this.paginatorReady) this.paginator?.firstPage();
  }

  // ── Display helpers ──────────────────────────────────────────────────────────

  vehicleLabel(d: GpsDevice): string {
    if (d.vehicleName) return d.vehicleName;
    if (d.vehicleId)   return 'Invalid link';
    return 'Unlinked';
  }

  trackerModelLabel(d: GpsDevice): string { return d.model || d.vendor || '—'; }

  ignitionLabel(d: GpsDevice): string {
    if (!d.lastSeenAt)       return '—';
    if (d.lastIgnition == null) return 'Unknown';
    return d.lastIgnition ? 'On' : 'Off';
  }

  ignitionBadgeClass(d: GpsDevice): string {
    if (!d.lastSeenAt)          return 'badge-gray';
    if (d.lastIgnition === true)  return 'badge-green';
    if (d.lastIgnition === false) return 'badge-red';
    return 'badge-gray';
  }

  speedLabel(d: GpsDevice): string {
    if (d.lastSpeed == null || !d.lastSeenAt) return '—';
    return `${Math.round(d.lastSpeed)} km/h`;
  }

  signalLabel(d: GpsDevice): string {
    if (d.lastRssi == null) return '—';
    if (d.lastRssi >= -70)  return 'Strong';
    if (d.lastRssi >= -85)  return 'Good';
    if (d.lastRssi >= -100) return 'Weak';
    return 'Poor';
  }

  batteryLabel(d: GpsDevice): string {
    if (d.lastBatteryLevel == null) return '—';
    return `${Math.round(d.lastBatteryLevel)}%`;
  }

  lastSeenLabel(d: GpsDevice): string {
    if (!d.lastSeenAt) {
      return this.traccarStatus?.connected ? 'Awaiting ping' : 'Never';
    }
    const diff = this.clockNow - new Date(d.lastSeenAt).getTime();
    const mins = Math.floor(diff / 60000);
    if (mins < 1)  return 'Just now';
    if (mins < 60) return `${mins}m ago`;
    const hrs = Math.floor(mins / 60);
    if (hrs < 24)  return `${hrs}h ago`;
    return `${Math.floor(hrs / 24)}d ago`;
  }

  lastSeenTooltip(d: GpsDevice): string {
    if (!d.lastSeenAt) return 'No telemetry received yet';
    const when = new Date(d.lastSeenAt).toLocaleString();
    if (!this.traccarStatus?.connected) return `${when} (last known — cached)`;
    return when;
  }

  private isMoving(d: GpsDevice): boolean {
    return !!d.isOnline && (d.lastSpeed ?? 0) > 5;
  }

  private isIdle(d: GpsDevice): boolean {
    return !!d.isOnline && !!d.lastSeenAt && (d.lastSpeed ?? 0) <= 5;
  }

  connectionLabel(d: GpsDevice): string {
    if (!d.lastSeenAt) return 'Never Seen';
    if (!d.isOnline)   return 'Offline';
    if (this.isMoving(d)) return 'Moving';
    if (this.isIdle(d))   return 'Idle';
    return 'Online';
  }

  connectionBadgeClass(d: GpsDevice): string {
    switch (this.connectionLabel(d)) {
      case 'Moving':    return 'badge-blue';
      case 'Idle':      return 'badge-amber';
      case 'Online':    return 'badge-green';
      case 'Offline':   return 'badge-red';
      default:          return 'badge-gray';
    }
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
      { filter: 'all' as DeviceFilter,      label: 'Total',      value: this.devices.length,                                            icon: 'sensors',      color: '#64748b' },
      { filter: 'online' as DeviceFilter,   label: 'Online',     value: this.devices.filter(d => !!d.isOnline).length,                  icon: 'wifi',         color: '#22c55e' },
      { filter: 'moving' as DeviceFilter,   label: 'Moving',     value: this.devices.filter(d => this.isMoving(d)).length,              icon: 'speed',        color: '#3b82f6' },
      { filter: 'idle' as DeviceFilter,     label: 'Idle',       value: this.devices.filter(d => this.isIdle(d)).length,                icon: 'pause_circle', color: '#f59e0b' },
      { filter: 'offline' as DeviceFilter,  label: 'Offline',    value: this.devices.filter(d => !!d.lastSeenAt && !d.isOnline).length, icon: 'wifi_off',     color: '#ef4444' },
      { filter: 'never' as DeviceFilter,    label: 'Never Seen', value: this.devices.filter(d => !d.lastSeenAt).length,                 icon: 'sensors_off',  color: '#94a3b8' },
      { filter: 'unlinked' as DeviceFilter, label: 'Unlinked',   value: this.devices.filter(d => !d.vehicleId).length,                  icon: 'link_off',     color: '#a855f7' },
    ];
  }

  setRefreshInterval(ms: number): void {
    this.refreshIntervalMs = ms;
    if (this.devicePollTimer) clearInterval(this.devicePollTimer);
    if (ms > 0) this.devicePollTimer = setInterval(() => this.load(true), ms);
  }

  goToLiveMap(d: GpsDevice): void {
    this.router.navigate(['../live'], { relativeTo: this.route, queryParams: { vehicleId: d.vehicleId } });
  }

  goToHistory(d: GpsDevice): void {
    this.router.navigate(['../history'], { relativeTo: this.route, queryParams: { vehicleId: d.vehicleId } });
  }

  goToCommands(d: GpsDevice): void {
    this.router.navigate(['../commands'], { relativeTo: this.route, queryParams: { deviceId: d.id } });
  }

  // ── Navigation ───────────────────────────────────────────────────────────────

  openCreate(): void {
    void this.router.navigate(['register'], { relativeTo: this.route });
  }

  openEdit(d: GpsDevice): void {
    void this.router.navigate([d.id, 'edit'], { relativeTo: this.route });
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
