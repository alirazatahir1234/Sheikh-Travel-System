import { Component, OnInit, OnDestroy, ViewChild, AfterViewInit, ChangeDetectorRef } from '@angular/core';
import { AbstractControl, FormBuilder, ValidationErrors, Validators } from '@angular/forms';
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
import { UiSelectOption } from '../../../shared/components/ui/types/ui.types';

type DeviceFilter = 'all' | 'online' | 'offline' | 'unlinked' | 'never';

interface TrackerModel {
  key: string;
  label: string;
  vendor: string;
  protocol: string;
  supportsEngineCutoff: boolean;
}

interface RegistrationSuccess {
  name: string;
  uniqueId: string;
  protocolLabel: string;
  vehicleLabel: string;
}

const TRACKER_CATALOG: TrackerModel[] = [
  { key: 'teltonika_fmb920', label: 'Teltonika FMB920', vendor: 'Teltonika', protocol: 'teltonika', supportsEngineCutoff: true },
  { key: 'teltonika_fmb140', label: 'Teltonika FMB140', vendor: 'Teltonika', protocol: 'teltonika', supportsEngineCutoff: true },
  { key: 'teltonika_fmb001', label: 'Teltonika FMB001', vendor: 'Teltonika', protocol: 'teltonika', supportsEngineCutoff: false },
  { key: 'teltonika_fmc001', label: 'Teltonika FMC001', vendor: 'Teltonika', protocol: 'teltonika', supportsEngineCutoff: false },
  { key: 'concox_gt06n',     label: 'Concox GT06N',     vendor: 'Concox',    protocol: 'gt06',      supportsEngineCutoff: true },
  { key: 'queclink_gv75',    label: 'Queclink GV75',    vendor: 'Queclink',  protocol: 'gl200',     supportsEngineCutoff: true },
];

const RELAY_OUTPUTS: UiSelectOption[] = [
  { value: 'output1', label: 'Output 1' },
  { value: 'output2', label: 'Output 2' },
  { value: 'output3', label: 'Output 3' },
];

const IMEI_PATTERN = /^\d{15}$/;
const SIM_PATTERN = /^(\+?\d{10,15})$/;

function optionalSimValidator(control: AbstractControl): ValidationErrors | null {
  const value = (control.value as string | null)?.trim();
  if (!value) return null;
  return SIM_PATTERN.test(value) ? null : { simFormat: true };
}

function todayIsoDate(): string {
  const d = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

@Component({
  selector: 'app-gps-devices',
  templateUrl: './gps-devices.component.html',
  styleUrls: ['./gps-devices.component.scss']
})
export class GpsDevicesComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild(MatPaginator) paginator?: MatPaginator;

  readonly trackerCatalog = TRACKER_CATALOG;
  readonly relayOutputs = RELAY_OUTPUTS;

  devices: GpsDevice[] = [];
  vehicles: Vehicle[] = [];
  loading = false;
  saving = false;
  showForm = false;
  editDevice: GpsDevice | null = null;
  registrationSuccess: RegistrationSuccess | null = null;
  traccarStatus: TraccarStatusDto | null = null;
  traccarSyncStatus: TraccarSyncStatusDto | null = null;
  clockNow = Date.now();

  searchQuery = '';
  deviceFilter: DeviceFilter = 'all';

  readonly filterOptions: { id: DeviceFilter; label: string }[] = [
    { id: 'all',     label: 'All' },
    { id: 'online',  label: 'Online' },
    { id: 'offline', label: 'Offline' },
    { id: 'unlinked', label: 'Unlinked' },
    { id: 'never',   label: 'Never seen' }
  ];

  readonly displayedColumns = [
    'vehicle', 'plate', 'driver', 'trackerModel', 'imei',
    'status', 'ignition', 'speed', 'lastSeen', 'signal', 'battery', 'actions'
  ];

  dataSource = new MatTableDataSource<GpsDevice>([]);
  form!: ReturnType<FormBuilder['group']>;

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
      trackerModelKey:      ['teltonika_fmb920', Validators.required],
      uniqueId:             ['', [Validators.required, Validators.pattern(IMEI_PATTERN)]],
      name:                 ['', [Validators.required, Validators.minLength(3), Validators.pattern(/[A-Za-z]/)]],
      vehicleId:            [''],
      simNumber:            ['', optionalSimValidator],
      supportsEngineCutoff: [false],
      relayOutput:          ['output1'],
      serialNumber:         [''],
      installationDate:     [todayIsoDate()],
      installedBy:          [''],
      installationNotes:    [''],
    });
  }

  ngOnInit(): void {
    this.vehicleService.getAll(1, 500).subscribe({
      next: r => { this.vehicles = r.items; }
    });
    this.loadInitial();
    this.devicePollTimer = setInterval(() => this.load(true), 30_000);
    this.syncPollTimer   = setInterval(() => this.loadSyncStatus(), 5_000);
    this.clockTimer      = setInterval(() => {
      this.clockNow = Date.now();
      this.cdr.markForCheck();
    }, 1_000);

    this.form.get('uniqueId')?.valueChanges.subscribe(raw => {
      const digits = String(raw ?? '').replace(/\D/g, '').slice(0, 15);
      if (digits !== raw) {
        this.form.get('uniqueId')?.setValue(digits, { emitEvent: false });
      }
    });

    this.form.get('trackerModelKey')?.valueChanges.subscribe(() => this.onTrackerModelChange());
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

  get selectedModel(): TrackerModel | null {
    const key = this.form.get('trackerModelKey')?.value as string;
    if (!key) return null;
    return TRACKER_CATALOG.find(m => m.key === key) ?? null;
  }

  get trackerModelOptions(): UiSelectOption[] {
    return TRACKER_CATALOG.map(m => ({ value: m.key, label: m.label }));
  }

  get vehicleOptions(): UiSelectOption[] {
    return this.linkableVehicles.map(v => ({
      value: String(v.id),
      label: this.vehicleOptionLabel(v),
    }));
  }

  get protocolDisplayLabel(): string {
    const model = this.selectedModel;
    if (!model?.vendor) return '—';
    return `${model.vendor} (Read Only)`;
  }

  get imeiValue(): string {
    return String(this.form.get('uniqueId')?.value ?? '');
  }

  get imeiIsValid(): boolean {
    return IMEI_PATTERN.test(this.imeiValue);
  }

  get imeiIsDuplicate(): boolean {
    if (this.editDevice || !this.imeiIsValid) return false;
    return this.devices.some(d => d.uniqueId === this.imeiValue);
  }

  get imeiFeedback(): string | null {
    const control = this.form.get('uniqueId');
    if (!control?.touched && !control?.dirty && !this.imeiValue) return null;
    if (!this.imeiValue) return null;
    if (!/^\d+$/.test(this.imeiValue)) return 'IMEI must contain numbers only';
    if (this.imeiValue.length < 15) return `${this.imeiValue.length}/15 digits`;
    if (this.imeiIsDuplicate) return 'IMEI already registered';
    if (this.imeiIsValid) return 'Valid IMEI';
    return null;
  }

  get imeiFeedbackOk(): boolean {
    return this.imeiFeedback === 'Valid IMEI';
  }

  get canSubmit(): boolean {
    if (this.saving || this.registrationSuccess) return false;
    if (!this.form.get('trackerModelKey')?.valid) return false;
    if (!this.imeiIsValid || this.imeiIsDuplicate) return false;
    if (!this.form.get('name')?.valid) return false;
    if (this.form.get('simNumber')?.invalid) return false;
    return true;
  }

  get showRelayOutput(): boolean {
    return !!this.form.get('supportsEngineCutoff')?.value;
  }

  onTrackerModelChange(): void {
    const model = this.selectedModel;
    if (!model) return;
    this.form.patchValue({
      supportsEngineCutoff: model.supportsEngineCutoff,
      relayOutput: model.supportsEngineCutoff ? 'output1' : 'output1',
    });
  }

  trackerModelError(): string {
    return this.form.get('trackerModelKey')?.touched && this.form.get('trackerModelKey')?.invalid
      ? 'Select a tracker model' : '';
  }

  nameError(): string {
    const c = this.form.get('name');
    if (!c?.touched && !c?.dirty) return '';
    if (c?.hasError('required')) return 'Tracker name is required';
    if (c?.hasError('minlength')) return 'At least 3 characters';
    if (c?.hasError('pattern')) return 'Must contain at least one letter';
    return '';
  }

  simError(): string {
    const c = this.form.get('simNumber');
    if (!c?.touched && !c?.dirty) return '';
    if (c?.hasError('simFormat')) return 'Use format +923001234567 or 03001234567';
    return '';
  }

  vehicleOptionLabel(v: Vehicle): string {
    const code = v.vehicleCode ? ` · ${v.vehicleCode}` : '';
    return `${v.name} - ${v.registrationNumber}${code}`;
  }

  closeForm(): void {
    this.showForm = false;
    this.registrationSuccess = null;
    this.saving = false;
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

  get linkableVehicles(): Vehicle[] {
    return this.vehicles.filter(
      v => v.status !== VehicleStatus.Draft && v.name.trim().toLowerCase() !== 'draft vehicle'
    );
  }

  private resolveVehicleLabel(vehicleId: string | null | undefined): string {
    if (!vehicleId) return 'Unassigned';
    const v = this.vehicles.find(x => String(x.id) === vehicleId);
    return v ? this.vehicleOptionLabel(v) : 'Unassigned';
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

    if (this.deviceFilter === 'online')   rows = rows.filter(d => !!d.isOnline);
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

  connectionLabel(d: GpsDevice): string {
    if (!this.traccarStatus?.connected && !d.lastSeenAt) return 'Unknown';
    if (!d.lastSeenAt) return 'Never seen';
    return d.isOnline === true ? 'Online' : 'Offline';
  }

  connectionBadgeClass(d: GpsDevice): string {
    const label = this.connectionLabel(d);
    if (label === 'Online')  return 'badge-green';
    if (label === 'Offline') return 'badge-red';
    return 'badge-gray';
  }

  // ── Form actions ─────────────────────────────────────────────────────────────

  openCreate(): void {
    this.editDevice = null;
    this.registrationSuccess = null;
    this.form.reset({
      trackerModelKey: 'teltonika_fmb920',
      supportsEngineCutoff: true,
      relayOutput: 'output1',
      installationDate: todayIsoDate(),
    });
    this.form.get('uniqueId')?.enable();
    this.showForm = true;
  }

  openEdit(d: GpsDevice): void {
    this.editDevice = d;
    this.registrationSuccess = null;
    const catalogModel = TRACKER_CATALOG.find(m => m.label === d.model);
    const modelKey = catalogModel?.key ?? TRACKER_CATALOG[0].key;
    this.form.patchValue({
      trackerModelKey:      modelKey,
      uniqueId:             d.uniqueId,
      name:                 d.name,
      vehicleId:            d.vehicleId ? String(d.vehicleId) : '',
      simNumber:            d.simNumber ?? '',
      supportsEngineCutoff: d.supportsEngineCutoff,
      relayOutput:          d.relayOutput ?? 'output1',
      serialNumber:         d.serialNumber ?? '',
      installationDate:     d.installationDate?.slice(0, 10) ?? todayIsoDate(),
      installedBy:          d.installedBy ?? '',
      installationNotes:    d.installationNotes ?? '',
    });
    this.form.get('uniqueId')?.disable();
    this.showForm = true;
  }

  private buildPayload() {
    const v = this.form.getRawValue();
    const model = this.selectedModel!;
    const vehicleId = (v.vehicleId as string)?.trim();

    return {
      uniqueId:             v.uniqueId as string,
      name:                 v.name as string,
      vehicleId:            vehicleId ? Number(vehicleId) : undefined,
      protocol:             model.protocol,
      supportsEngineCutoff: !!(v.supportsEngineCutoff),
      relayOutput:          v.supportsEngineCutoff ? (v.relayOutput as string) : undefined,
      model:                model.label,
      vendor:               model.vendor,
      simNumber:            (v.simNumber as string)?.trim() || undefined,
      serialNumber:         (v.serialNumber as string)?.trim() || undefined,
      installationDate:     (v.installationDate as string) || undefined,
      installedBy:          (v.installedBy as string)?.trim() || undefined,
      installationNotes:    (v.installationNotes as string)?.trim() || undefined,
    };
  }

  save(): void {
    if (!this.canSubmit) return;
    this.saving = true;
    const payload = this.buildPayload();

    if (this.editDevice) {
      this.gps.updateDevice(this.editDevice.id, { ...payload, isActive: this.editDevice.isActive }).subscribe({
        next: () => {
          this.saving = false;
          this.toast.success('Tracker updated');
          this.closeForm();
          this.load();
        },
        error: err => {
          this.saving = false;
          this.toast.error(err?.error?.message ?? 'Update failed');
        }
      });
      return;
    }

    this.gps.createDevice(payload).subscribe({
      next: () => {
        this.saving = false;
        this.registrationSuccess = {
          name: payload.name,
          uniqueId: payload.uniqueId,
          protocolLabel: this.protocolDisplayLabel.replace(' (Read Only)', ''),
          vehicleLabel: this.resolveVehicleLabel(this.form.get('vehicleId')?.value as string),
        };
        this.load();
      },
      error: err => {
        this.saving = false;
        const msg = err?.error?.message ?? 'Registration failed';
        if (msg.toLowerCase().includes('imei') || msg.toLowerCase().includes('duplicate')) {
          this.form.get('uniqueId')?.setErrors({ duplicate: true });
        }
        this.toast.error(msg);
      }
    });
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
