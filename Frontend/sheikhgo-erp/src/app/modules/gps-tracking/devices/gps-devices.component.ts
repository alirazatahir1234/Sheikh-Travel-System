import { Component, OnInit, OnDestroy, ViewChild, AfterViewInit, ChangeDetectorRef } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MatPaginator } from '@angular/material/paginator';
import { MatTableDataSource } from '@angular/material/table';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import {
  GpsDevice,
  TraccarStatusDto,
  TraccarSyncStatusDto
} from '../../../core/models/gps-tracking.model';
import { Vehicle, VehicleStatus } from '../../../core/models/vehicle.model';

type DeviceFilter = 'all' | 'online' | 'offline' | 'unlinked' | 'never';

@Component({
  selector: 'app-gps-devices',
  templateUrl: './gps-devices.component.html',
  styleUrls: ['./gps-devices.component.scss']
})
export class GpsDevicesComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild(MatPaginator) paginator?: MatPaginator;

  devices: GpsDevice[] = [];
  vehicles: Vehicle[] = [];
  loading = false;
  showForm = false;
  editDevice: GpsDevice | null = null;
  traccarStatus: TraccarStatusDto | null = null;
  traccarSyncStatus: TraccarSyncStatusDto | null = null;
  clockNow = Date.now();

  searchQuery = '';
  deviceFilter: DeviceFilter = 'all';

  readonly filterOptions: { id: DeviceFilter; label: string }[] = [
    { id: 'all', label: 'All' },
    { id: 'online', label: 'Online' },
    { id: 'offline', label: 'Offline' },
    { id: 'unlinked', label: 'Unlinked' },
    { id: 'never', label: 'Never seen' }
  ];

  readonly displayedColumns = [
    'vehicle', 'plate', 'driver', 'trackerModel', 'imei',
    'status', 'ignition', 'speed', 'lastSeen', 'signal', 'battery', 'actions'
  ];

  dataSource = new MatTableDataSource<GpsDevice>([]);

  form!: ReturnType<FormBuilder['group']>;

  private readonly imeiPattern = /^\d{14,20}$/;
  private devicePollTimer?: ReturnType<typeof setInterval>;
  private syncPollTimer?: ReturnType<typeof setInterval>;
  private clockTimer?: ReturnType<typeof setInterval>;
  private paginatorReady = false;

  constructor(
    private gps: GpsTrackingService,
    private vehicleService: VehicleService,
    private fb: FormBuilder,
    private toast: UiToastService,
    private cdr: ChangeDetectorRef
  ) {
    this.form = this.fb.group({
      uniqueId: ['', [Validators.required, Validators.pattern(this.imeiPattern)]],
      name: ['', [Validators.required, Validators.minLength(3), Validators.pattern(/[A-Za-z]/)]],
      vehicleId: [null as number | null],
      protocol: [''],
      supportsEngineCutoff: [false]
    });
  }

  ngOnInit(): void {
    this.vehicleService.getAll(1, 500).subscribe({
      next: r => { this.vehicles = r.items; }
    });
    this.loadInitial();
    this.devicePollTimer = setInterval(() => this.load(true), 30_000);
    this.syncPollTimer = setInterval(() => this.loadSyncStatus(), 5_000);
    this.clockTimer = setInterval(() => {
      this.clockNow = Date.now();
      this.cdr.markForCheck();
    }, 1_000);
  }

  ngOnDestroy(): void {
    if (this.devicePollTimer) clearInterval(this.devicePollTimer);
    if (this.syncPollTimer) clearInterval(this.syncPollTimer);
    if (this.clockTimer) clearInterval(this.clockTimer);
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

  get filteredCount(): number {
    return this.dataSource.filteredData.length;
  }

  get totalCount(): number {
    return this.devices.length;
  }

  get connectedOnServer(): number {
    return this.traccarStatus?.deviceCount ?? 0;
  }

  get syncIntervalSeconds(): number {
    return this.traccarSyncStatus?.positionSyncIntervalSeconds ?? 5;
  }

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

  get linkableVehicles(): Vehicle[] {
    return this.vehicles.filter(
      v => v.status !== VehicleStatus.Draft && v.name.trim().toLowerCase() !== 'draft vehicle'
    );
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

  onSearchChange(): void {
    this.applyFilters();
  }

  private applyFilters(): void {
    const q = this.searchQuery.trim().toLowerCase();
    let rows = [...this.devices];

    if (this.deviceFilter === 'online') rows = rows.filter(d => !!d.isOnline);
    else if (this.deviceFilter === 'offline') rows = rows.filter(d => !!d.lastSeenAt && !d.isOnline);
    else if (this.deviceFilter === 'unlinked') rows = rows.filter(d => !d.vehicleId || !d.vehicleName);
    else if (this.deviceFilter === 'never') rows = rows.filter(d => !d.lastSeenAt);

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

  vehicleLabel(d: GpsDevice): string {
    if (d.vehicleName) return d.vehicleName;
    if (d.vehicleId) return 'Invalid link';
    return 'Unlinked';
  }

  trackerModelLabel(d: GpsDevice): string {
    return d.model || d.vendor || '—';
  }

  ignitionLabel(d: GpsDevice): string {
    if (!d.lastSeenAt) return '—';
    if (d.lastIgnition == null) return 'Unknown';
    return d.lastIgnition ? 'On' : 'Off';
  }

  ignitionBadgeClass(d: GpsDevice): string {
    if (!d.lastSeenAt) return 'badge-gray';
    if (d.lastIgnition === true) return 'badge-green';
    if (d.lastIgnition === false) return 'badge-red';
    return 'badge-gray';
  }

  speedLabel(d: GpsDevice): string {
    if (d.lastSpeed == null || !d.lastSeenAt) return '—';
    return `${Math.round(d.lastSpeed)} km/h`;
  }

  signalLabel(d: GpsDevice): string {
    if (d.lastRssi == null) return '—';
    if (d.lastRssi >= -70) return 'Strong';
    if (d.lastRssi >= -85) return 'Good';
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
    if (mins < 1) return 'Just now';
    if (mins < 60) return `${mins}m ago`;
    const hrs = Math.floor(mins / 60);
    if (hrs < 24) return `${hrs}h ago`;
    return `${Math.floor(hrs / 24)}d ago`;
  }

  lastSeenTooltip(d: GpsDevice): string {
    if (!d.lastSeenAt) return 'No telemetry received yet';
    const when = new Date(d.lastSeenAt).toLocaleString();
    if (!this.traccarStatus?.connected) return `${when} (last known — cached)`;
    return when;
  }

  connectionLabel(d: GpsDevice): string {
    if (!this.traccarStatus?.connected && !d.lastSeenAt) return 'Unknown';
    if (!d.lastSeenAt) return 'Never seen';
    return d.isOnline === true ? 'Online' : 'Offline';
  }

  connectionBadgeClass(d: GpsDevice): string {
    const label = this.connectionLabel(d);
    if (label === 'Online') return 'badge-green';
    if (label === 'Offline') return 'badge-red';
    return 'badge-gray';
  }

  openCreate(): void {
    this.editDevice = null;
    this.form.reset({ supportsEngineCutoff: false });
    this.form.get('uniqueId')?.enable();
    this.showForm = true;
  }

  openEdit(d: GpsDevice): void {
    this.editDevice = d;
    this.form.patchValue({
      uniqueId: d.uniqueId,
      name: d.name,
      vehicleId: d.vehicleId ?? null,
      protocol: d.protocol ?? '',
      supportsEngineCutoff: d.supportsEngineCutoff
    });
    this.form.get('uniqueId')?.disable();
    this.showForm = true;
  }

  save(): void {
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const payload = {
      uniqueId: v.uniqueId!,
      name: v.name!,
      vehicleId: v.vehicleId ?? undefined,
      protocol: v.protocol || undefined,
      supportsEngineCutoff: !!v.supportsEngineCutoff
    };

    if (this.editDevice) {
      this.gps.updateDevice(this.editDevice.id, { ...payload, isActive: this.editDevice.isActive }).subscribe({
        next: () => { this.toast.success('Device updated'); this.showForm = false; this.load(); },
        error: err => this.toast.error(err?.error?.message ?? 'Update failed')
      });
    } else {
      this.gps.createDevice(payload).subscribe({
        next: () => { this.toast.success('Device registered'); this.showForm = false; this.load(); },
        error: err => this.toast.error(err?.error?.message ?? 'Create failed')
      });
    }
  }

  deleteDevice(d: GpsDevice): void {
    const label = d.vehicleName || d.name;
    if (!confirm(`Remove tracker for "${label}"?`)) return;
    this.gps.deleteDevice(d.id).subscribe({
      next: () => { this.toast.success('Device removed'); this.load(); },
      error: err => this.toast.error(err?.error?.message ?? 'Delete failed')
    });
  }
}
