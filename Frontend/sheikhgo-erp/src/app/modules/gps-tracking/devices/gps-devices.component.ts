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
  syncing = false;
  showForm = false;
  editDevice: GpsDevice | null = null;
  traccarStatus: TraccarStatusDto | null = null;
  traccarSyncStatus: TraccarSyncStatusDto | null = null;

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
    'name', 'uniqueId', 'vehicle', 'ignition', 'lastSeen', 'connection', 'enabled', 'actions'
  ];

  dataSource = new MatTableDataSource<GpsDevice>([]);
  readonly Math = Math;

  form!: ReturnType<FormBuilder['group']>;

  private readonly imeiPattern = /^\d{14,20}$/;
  private pollTimer?: ReturnType<typeof setInterval>;
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
      name: ['', [Validators.required, Validators.minLength(3)]],
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
    this.pollTimer = setInterval(() => {
      this.load(true);
      this.loadSyncStatus();
    }, 30_000);
  }

  ngOnDestroy(): void {
    if (this.pollTimer) clearInterval(this.pollTimer);
  }

  ngAfterViewInit(): void {
    this.attachPaginator();
    this.paginatorReady = true;
    if (this.devices.length > 0) {
      this.applyFilters();
    }
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

  private attachPaginator(): void {
    if (this.paginator) {
      this.dataSource.paginator = this.paginator;
    }
  }

  get linkableVehicles(): Vehicle[] {
    return this.vehicles.filter(
      v => v.status !== VehicleStatus.Draft && v.name.trim().toLowerCase() !== 'draft vehicle'
    );
  }

  get filteredCount(): number {
    return this.dataSource.filteredData.length;
  }

  get totalCount(): number {
    return this.devices.length;
  }

  get canRefreshNow(): boolean {
    return !!this.traccarStatus?.connected && !this.syncing && !this.traccarSyncStatus?.isRunning;
  }

  get autoSyncLabel(): string {
    const sync = this.traccarSyncStatus;
    if (!sync?.enabled) return 'Auto-sync disabled in configuration';
    if (!sync.connected) return 'Traccar unreachable — background sync paused';
    if (sync.isRunning) return 'Manual sync in progress…';
    const last = sync.lastPositionSyncAt;
    if (!last) return 'Auto-sync active · awaiting first position sync';
    const secs = Math.max(0, Math.floor((Date.now() - new Date(last).getTime()) / 1000));
    if (secs < 60) return `Auto-sync active · last position ${secs}s ago`;
    const mins = Math.floor(secs / 60);
    return `Auto-sync active · last position ${mins}m ago`;
  }

  get traccarChipLabel(): string {
    if (!this.traccarStatus?.connected) return 'Traccar Offline';
    const onTraccar = this.traccarStatus.deviceCount;
    const registered = this.totalCount;
    if (registered > onTraccar) {
      return `Traccar · ${onTraccar} on server · ${registered} registered`;
    }
    return `Traccar · ${onTraccar} devices`;
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

  loadTraccarStatus(): void {
    this.gps.getTraccarStatus().subscribe({
      next: s => { this.traccarStatus = s; },
      error: () => { this.traccarStatus = { connected: false, deviceCount: 0 }; }
    });
  }

  loadSyncStatus(): void {
    this.gps.getTraccarSyncStatus().subscribe({
      next: s => { this.traccarSyncStatus = s; },
      error: () => {}
    });
  }

  refreshNow(): void {
    if (!this.canRefreshNow) return;
    this.syncing = true;
    this.gps.runTraccarSync().subscribe({
      next: r => {
        this.syncing = false;
        const devices = r.jobs.find(j => j.job === 'devices');
        const positions = r.jobs.find(j => j.job === 'positions');
        const events = r.jobs.find(j => j.job === 'events');
        const parts: string[] = [];
        if (devices) {
          parts.push(`devices ${devices.imported} new, ${devices.updated} updated`);
        }
        if (positions) {
          parts.push(`positions ${positions.imported} ingested`);
        }
        if (events) {
          parts.push(`events ${events.imported} new`);
        }
        this.toast.success(parts.length ? `Refresh complete: ${parts.join(' · ')}` : 'Refresh complete');
        this.load();
        this.loadTraccarStatus();
        this.loadSyncStatus();
      },
      error: err => {
        this.syncing = false;
        this.toast.error(err?.error?.message ?? 'Refresh failed');
        this.loadTraccarStatus();
        this.loadSyncStatus();
      }
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

    if (this.deviceFilter === 'online') {
      rows = rows.filter(d => !!d.isOnline);
    } else if (this.deviceFilter === 'offline') {
      rows = rows.filter(d => !!d.lastSeenAt && !d.isOnline);
    } else if (this.deviceFilter === 'unlinked') {
      rows = rows.filter(d => !d.vehicleId);
    } else if (this.deviceFilter === 'never') {
      rows = rows.filter(d => !d.lastSeenAt);
    }

    if (q) {
      rows = rows.filter(d =>
        d.name.toLowerCase().includes(q) ||
        d.uniqueId.toLowerCase().includes(q) ||
        (d.vehicleName?.toLowerCase().includes(q) ?? false) ||
        this.vehicleName(d.vehicleId).toLowerCase().includes(q)
      );
    }

    this.dataSource.data = rows;
    if (this.paginatorReady) {
      this.paginator?.firstPage();
    }
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
    if (!confirm(`Delete device "${d.name}"?`)) return;
    this.gps.deleteDevice(d.id).subscribe({
      next: () => { this.toast.success('Device removed'); this.load(); },
      error: err => this.toast.error(err?.error?.message ?? 'Delete failed')
    });
  }

  vehicleName(vehicleId?: number): string {
    if (!vehicleId) return '—';
    const fromDevice = this.devices.find(d => d.vehicleId === vehicleId)?.vehicleName;
    if (fromDevice) return fromDevice;
    return this.vehicles.find(v => v.id === vehicleId)?.name ?? `#${vehicleId}`;
  }

  ignitionLabel(d: GpsDevice): string {
    if (d.lastIgnition == null) return 'Unknown';
    return d.lastIgnition ? 'On' : 'Off';
  }

  lastSeenLabel(d: GpsDevice): string {
    if (!d.lastSeenAt) {
      return this.traccarStatus?.connected ? 'Awaiting first ping' : 'Never';
    }
    const diff = Date.now() - new Date(d.lastSeenAt).getTime();
    const mins = Math.floor(diff / 60000);
    if (mins < 1) return 'Just now';
    if (mins < 60) return `${mins}m ago`;
    const hrs = Math.floor(mins / 60);
    if (hrs < 24) return `${hrs}h ago`;
    return `${Math.floor(hrs / 24)}d ago`;
  }

  lastSeenTooltip(d: GpsDevice): string {
    if (!d.lastSeenAt) return 'No telemetry received yet';
    return new Date(d.lastSeenAt).toLocaleString();
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
}
