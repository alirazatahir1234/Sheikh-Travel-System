import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, input, model, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { catchError, of } from 'rxjs';
import { VehicleService } from '../../../core/services/vehicle.service';
import { DriverService } from '../../../core/services/driver.service';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { PlatformService } from '../../../core/services/platform.service';
import { BookingService } from '../../../core/services/booking.service';
import {
  Vehicle,
  VehicleListItem,
  VehicleStatus,
  VehicleStatusLabels,
  FuelTypeLabels,
  MaintenanceStatusLabels,
  MaintenanceStatus,
  VehicleDocument,
  VehicleMaintenance,
  VehicleFuelSummary,
  VehicleGps,
  normalizeVehicleStatus,
  normalizeFuelType
} from '../../../core/models/vehicle.model';
import { Booking } from '../../../core/models/booking.model';
import { UiDrawerComponent } from '../../../shared/components/ui/drawer/ui-drawer.component';
import { UiButtonComponent } from '../../../shared/components/ui/button/ui-button.component';
import { UiInputComponent } from '../../../shared/components/ui/input/ui-input.component';
import { UiSelectComponent } from '../../../shared/components/ui/select/ui-select.component';
import { UiModalComponent } from '../../../shared/components/ui/modal/ui-modal.component';
import { UiEmptyStateComponent } from '../../../shared/components/ui/empty-state/ui-empty-state.component';
import { UiTab, UiSelectOption } from '../../../shared/components/ui/types/ui.types';
import { dateInputToIso } from '../../../core/utils/date-input.util';
import { resolveUploadUrl, resolveVehicleImageUrl, isPdfUploadUrl } from '../../../core/utils/upload-url.util';
import { deriveOperationalStatus } from '../utils/vehicle-status.util';
import { formatRelativeTime } from '../../../core/utils/relative-time.util';

interface VehicleAlert {
  id: string;
  type: string;
  title: string;
  detail: string;
  priority: 'high' | 'medium' | 'low';
  date?: string;
}

interface ComplianceItem {
  key: string;
  label: string;
  expiryDate?: string;
  status: string;
  statusClass: string;
  document?: VehicleDocument;
}

interface HistoryEvent {
  title: string;
  detail?: string;
  timestamp?: string;
  icon: string;
  tone: 'success' | 'warning' | 'danger' | 'primary' | 'muted';
}

const COMPLIANCE_TYPES: { key: string; label: string; aliases: string[] }[] = [
  { key: 'Registration', label: 'Registration Card', aliases: ['Registration'] },
  { key: 'Insurance', label: 'Insurance Policy', aliases: ['Insurance'] },
  { key: 'RoadTax', label: 'Road Tax', aliases: ['RoadTax', 'Road Tax'] },
  { key: 'Fitness', label: 'Fitness Certificate', aliases: ['Fitness'] },
  { key: 'Permit', label: 'Permit', aliases: ['Permit'] }
];

const DOCUMENT_CATALOG: { type: string; label: string }[] = [
  { type: 'Registration', label: 'Registration Card' },
  { type: 'Insurance', label: 'Insurance Policy' },
  { type: 'RoadTax', label: 'Road Tax' },
  { type: 'Fitness', label: 'Fitness Certificate' },
  { type: 'Permit', label: 'Permit' },
  { type: 'VehicleImage', label: 'Vehicle Photos' }
];

@Component({
  selector: 'app-vehicle-details-drawer',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    DatePipe,
    DecimalPipe,
    MatIconModule,
    MatMenuModule,
    UiDrawerComponent,
    UiButtonComponent,
    UiInputComponent,
    UiSelectComponent,
    UiModalComponent,
    UiEmptyStateComponent
  ],
  templateUrl: './vehicle-details-drawer.component.html',
  styleUrls: ['./vehicle-details-drawer.component.scss']
})
export class VehicleDetailsDrawerComponent {
  private readonly vehicleService = inject(VehicleService);
  private readonly driverService = inject(DriverService);
  private readonly gpsService = inject(GpsTrackingService);
  private readonly platformService = inject(PlatformService);
  private readonly bookingService = inject(BookingService);
  private readonly snackBar = inject(MatSnackBar);
  readonly router = inject(Router);

  readonly vehicleId = input<number | null>(null);
  readonly listItem = input<VehicleListItem | null>(null);
  readonly open = model(false);
  readonly statusChanged = output<void>();
  readonly closed = output<void>();

  activeTab = 'general';
  historyFilter: 'today' | 'week' | 'month' | 'all' = 'month';

  readonly loading = signal(false);
  readonly loadError = signal(false);
  readonly maintenanceLoading = signal(false);
  readonly fuelLoading = signal(false);
  readonly gpsLoading = signal(false);
  readonly tripsLoading = signal(false);
  readonly documentsLoading = signal(false);
  readonly vehicle = signal<Vehicle | null>(null);
  readonly documents = signal<VehicleDocument[]>([]);
  readonly displayDocuments = computed(() =>
    this.documents().filter(doc =>
      doc.documentType !== 'VehicleImage' && !!doc.fileUrl?.trim()
    )
  );
  readonly maintenance = signal<VehicleMaintenance[]>([]);
  readonly nextService = signal<VehicleMaintenance | null>(null);
  readonly fuelSummary = signal<VehicleFuelSummary | null>(null);
  readonly gps = signal<VehicleGps | null>(null);
  readonly trips = signal<Booking[]>([]);
  readonly branchMap = signal<Map<number, string>>(new Map());
  readonly departmentMap = signal<Map<number, string>>(new Map());

  protected readonly Math = Math;
  protected readonly documentCatalog = DOCUMENT_CATALOG;
  readonly docTypeOptions: UiSelectOption[] = DOCUMENT_CATALOG
    .filter(d => d.type !== 'VehicleImage')
    .map(d => ({ value: d.type, label: d.label }));

  readonly tabs: UiTab[] = [
    { id: 'general', label: 'General', icon: 'info' },
    { id: 'documents', label: 'Docs', icon: 'description' },
    { id: 'maintenance', label: 'Maint', icon: 'build' },
    { id: 'fuel', label: 'Fuel', icon: 'local_gas_station' },
    { id: 'gps', label: 'GPS', icon: 'gps_fixed' },
    { id: 'trips', label: 'Trips', icon: 'route' },
    { id: 'alerts', label: 'Alerts', icon: 'notifications' },
    { id: 'compliance', label: 'Comply', icon: 'verified_user' },
    { id: 'assignments', label: 'Assign', icon: 'assignment_ind' },
    { id: 'history', label: 'History', icon: 'history' }
  ];

  statusModalOpen = false;
  assignDriverModalOpen = false;
  assignGpsModalOpen = false;
  docModalOpen = false;

  selectedStatus = String(VehicleStatus.Available);
  statusReason = '';
  selectedDriverId: string | null = null;
  selectedGpsDeviceId: string | null = null;
  bookingId: number | null = null;

  newDocType = '';
  newDocFile: File | null = null;
  newDocExpiry = '';
  newDocNotes = '';
  docUploading = false;

  driverOptions: UiSelectOption[] = [];
  gpsOptions: UiSelectOption[] = [];
  statusOptions: UiSelectOption[] = Object.values(VehicleStatus)
    .filter((v): v is VehicleStatus => typeof v === 'number')
    .map(s => ({ value: String(s), label: VehicleStatusLabels[s] }));

  private loadedTabs = new Set<string>();

  readonly resolvedImageUrl = computed(() => resolveVehicleImageUrl(this.listItem()?.imageUrl));
  readonly effectiveVehicleId = computed(() => this.vehicleId() ?? this.listItem()?.id ?? null);
  readonly hasPreview = computed(() => !!(this.vehicle() || this.listItem()));
  readonly displayName = computed(() => this.vehicle()?.name ?? this.listItem()?.name ?? 'Vehicle');
  readonly displayCode = computed(() =>
    this.vehicle()?.vehicleCode ?? this.listItem()?.vehicleCode ?? this.vehicle()?.registrationNumber ?? this.listItem()?.registrationNumber ?? '—'
  );
  readonly displayRegistration = computed(() =>
    this.vehicle()?.registrationNumber ?? this.listItem()?.registrationNumber ?? '—'
  );
  readonly displayStatus = computed(() =>
    normalizeVehicleStatus(this.vehicle()?.status ?? this.listItem()?.status ?? VehicleStatus.Available)
  );
  readonly displayMileage = computed(() =>
    this.vehicle()?.currentMileage ?? this.listItem()?.currentMileage ?? 0
  );
  readonly displayFuelAverage = computed(() =>
    this.vehicle()?.fuelAverage ?? this.listItem()?.fuelAverage ?? 0
  );
  readonly displayFuelType = computed(() =>
    normalizeFuelType(this.vehicle()?.fuelType ?? this.listItem()?.fuelType ?? 1)
  );
  readonly displayMake = computed(() => this.vehicle()?.make ?? this.listItem()?.make ?? null);
  readonly displayModel = computed(() => this.vehicle()?.model ?? this.listItem()?.model ?? null);
  readonly displayYear = computed(() => this.vehicle()?.year ?? this.listItem()?.year ?? null);
  readonly displayVin = computed(() => this.vehicle()?.vin ?? this.listItem()?.vin ?? null);
  readonly displayVehicleType = computed(() =>
    this.vehicle()?.vehicleType ?? this.listItem()?.vehicleType ?? null
  );
  readonly displaySeating = computed(() =>
    this.vehicle()?.seatingCapacity ?? this.listItem()?.seatingCapacity ?? 0
  );
  readonly displayColor = computed(() => this.vehicle()?.color ?? null);
  readonly displayEngineNo = computed(() => this.vehicle()?.engineNo ?? null);
  readonly displayChassisNo = computed(() => this.vehicle()?.chassisNo ?? null);
  readonly displayPurchaseDate = computed(() => this.vehicle()?.purchaseDate ?? null);
  readonly displayInsuranceExpiry = computed(() =>
    this.vehicle()?.insuranceExpiryDate ?? this.listItem()?.insuranceExpiryDate ?? null
  );
  readonly displayLocation = computed(() => {
    const g = this.gps();
    if (g?.latitude != null && g?.longitude != null) {
      return {
        latitude: g.latitude,
        longitude: g.longitude,
        lastUpdate: g.lastUpdate ?? null
      };
    }
    const row = this.listItem();
    if (row?.locationLatitude != null && row?.locationLongitude != null) {
      return {
        latitude: row.locationLatitude,
        longitude: row.locationLongitude,
        lastUpdate: row.locationLastUpdate ?? row.gpsLastSeenAt ?? null
      };
    }
    return null;
  });
  readonly displayImei = computed(() =>
    this.gps()?.uniqueId ?? this.listItem()?.gpsImei ?? null
  );
  readonly displaySim = computed(() => this.listItem()?.gpsSim ?? null);
  readonly displayLastPing = computed(() =>
    this.gps()?.lastUpdate ?? this.listItem()?.gpsLastSeenAt ?? this.listItem()?.locationLastUpdate ?? null
  );
  readonly displayIgnition = computed(() => {
    const g = this.gps();
    if (g?.lastIgnition != null) return g.lastIgnition;
    const row = this.listItem();
    return row?.engineIgnition ?? null;
  });
  readonly driverName = computed(() => this.listItem()?.driverName ?? null);
  readonly operationalStatus = computed(() => {
    const row = this.listItem();
    if (row) return deriveOperationalStatus(row);
    const v = this.vehicle();
    if (!v) return { label: 'Unknown', variant: 'inactive' as const };
    return { label: this.statusLabel(v.status), variant: 'info' as const };
  });
  readonly gpsOnline = computed(() => this.listItem()?.gpsOnline ?? this.gps()?.isActive ?? false);
  readonly branchName = computed(() => {
    const branchId = this.vehicle()?.branchId ?? this.listItem()?.branchId;
    if (!branchId) return '—';
    return this.branchMap().get(branchId) ?? `Branch #${branchId}`;
  });
  readonly departmentName = computed(() => {
    const deptId = this.vehicle()?.departmentId;
    if (!deptId) return '—';
    return this.departmentMap().get(deptId) ?? `Dept #${deptId}`;
  });
  readonly fleetLabel = computed(() => `${this.branchName()} Fleet`);
  readonly totalMaintenanceCost = computed(() =>
    this.maintenance().reduce((sum, m) => sum + m.cost, 0)
  );
  readonly openWorkOrders = computed(() =>
    this.maintenance().filter(m => m.status !== MaintenanceStatus.Completed).length
  );
  readonly completedTripsCount = computed(() =>
    this.trips().filter(t => t.status === 'Completed').length
  );
  readonly inProgressTripsCount = computed(() =>
    this.trips().filter(t => t.status !== 'Completed' && t.status !== 'Cancelled').length
  );
  readonly highPriorityAlertsCount = computed(() =>
    this.vehicleAlerts().filter(a => a.priority === 'high').length
  );
  readonly mediumPriorityAlertsCount = computed(() =>
    this.vehicleAlerts().filter(a => a.priority === 'medium').length
  );
  readonly lastServiceDate = computed(() => this.maintenance()[0]?.maintenanceDate ?? null);
  readonly displayNextServiceDue = computed(() =>
    this.listItem()?.nextServiceDue ?? this.nextService()?.nextDueDate ?? null
  );
  readonly displayNextDueMileage = computed(() => this.listItem()?.nextDueMileage ?? null);
  readonly displayServiceAlert = computed(() =>
    this.listItem()?.serviceAlert ?? this.nextService()?.description ?? null
  );
  readonly hasGpsTracker = computed(() => {
    const row = this.listItem();
    const v = this.vehicle();
    const g = this.gps();
    return !!(v?.gpsDeviceId ?? g?.gpsDeviceId ?? row?.hasGpsDevice ?? row?.gpsImei ?? g?.uniqueId);
  });
  readonly displayTrackerAssigned = computed(() => this.hasGpsTracker() ? 'Assigned' : 'Unassigned');
  readonly vehicleAlerts = computed((): VehicleAlert[] => {
    const alerts: VehicleAlert[] = [];
    const v = this.vehicle();
    const row = this.listItem();

    const insuranceExpiry = v?.insuranceExpiryDate ?? row?.insuranceExpiryDate ?? null;
    if (insuranceExpiry) {
      const days = this.daysUntil(insuranceExpiry);
      if (days <= 30) {
        alerts.push({
          id: 'insurance',
          type: 'Insurance Expiry',
          title: 'Insurance Expiry',
          detail: `Policy expires ${new Date(insuranceExpiry).toLocaleDateString()}`,
          priority: days < 0 ? 'high' : 'medium',
          date: insuranceExpiry
        });
      }
    }

    const nextServiceDue = this.displayNextServiceDue();
    const serviceAlert = this.displayServiceAlert();
    if (nextServiceDue) {
      const days = this.daysUntil(nextServiceDue);
      if (days <= 14) {
        alerts.push({
          id: 'maintenance',
          type: 'Maintenance Due',
          title: 'Maintenance Due',
          detail: serviceAlert ?? 'Scheduled service is due',
          priority: days < 0 ? 'high' : 'medium',
          date: nextServiceDue
        });
      }
    } else if (this.nextService()?.nextDueDate) {
      const ns = this.nextService()!;
      const days = this.daysUntil(ns.nextDueDate!);
      if (days <= 14) {
        alerts.push({
          id: 'maintenance-record',
          type: 'Maintenance Due',
          title: 'Maintenance Due',
          detail: ns.description,
          priority: days < 0 ? 'high' : 'medium',
          date: ns.nextDueDate!
        });
      }
    }

    if (row?.status === VehicleStatus.Maintenance) {
      alerts.push({
        id: 'vehicle-maintenance',
        type: 'Vehicle Status',
        title: 'In Maintenance',
        detail: `${row.name} is currently under maintenance.`,
        priority: 'medium'
      });
    }

    if (row && (row.hasGpsDevice || row.gpsImei) && !row.gpsOnline) {
      alerts.push({
        id: 'tracker-offline',
        type: 'Tracker Offline',
        title: 'Tracker Offline',
        detail: `Last ping ${formatRelativeTime(row.gpsLastSeenAt ?? row.locationLastUpdate)}`,
        priority: 'high'
      });
    }

    if (!row?.hasGpsDevice && !row?.gpsImei) {
      alerts.push({
        id: 'no-tracker',
        type: 'Tracker Offline',
        title: 'Tracker Not Assigned',
        detail: 'Assign a GPS device to enable live tracking.',
        priority: 'medium'
      });
    }

    for (const doc of this.documents()) {
      if (!doc.expiryDate) continue;
      const days = this.daysUntil(doc.expiryDate);
      if (days <= 30) {
        alerts.push({
          id: `doc-${doc.id}`,
          type: 'Document Expiry',
          title: `${doc.documentType} Expiring`,
          detail: `Expires ${new Date(doc.expiryDate).toLocaleDateString()}`,
          priority: days < 0 ? 'high' : 'medium',
          date: doc.expiryDate
        });
      }
    }

    return alerts.sort((a, b) => {
      const order = { high: 0, medium: 1, low: 2 };
      return order[a.priority] - order[b.priority];
    });
  });
  readonly complianceItems = computed((): ComplianceItem[] =>
    COMPLIANCE_TYPES.map(item => {
      const doc = this.documents().find(d =>
        !!d.fileUrl?.trim()
        && item.aliases.some(a => d.documentType.toLowerCase() === a.toLowerCase())
      );
      const expiry = doc?.expiryDate ?? (item.key === 'Insurance' ? this.displayInsuranceExpiry() ?? undefined : undefined);
      return {
        key: item.key,
        label: item.label,
        expiryDate: expiry,
        status: doc ? this.getDocStatus(expiry) : (expiry ? this.getDocStatus(expiry) : 'Missing'),
        statusClass: doc || expiry ? this.getDocStatusClass(expiry) : 'vd-status-neutral',
        document: doc
      };
    })
  );
  readonly complianceValidCount = computed(() =>
    this.complianceItems().filter(i => i.status === 'Valid' || i.status === 'No Expiry').length
  );
  readonly complianceExpiringCount = computed(() =>
    this.complianceItems().filter(i => i.status === 'Expiring Soon').length
  );
  readonly complianceIssueCount = computed(() =>
    this.complianceItems().filter(i => i.status === 'Expired' || i.status === 'Missing').length
  );
  readonly historyEvents = computed((): HistoryEvent[] => {
    const events: HistoryEvent[] = [];
    const v = this.vehicle();
    const row = this.listItem();

    const createdAt = v?.createdAt ?? row?.createdAt;
    if (createdAt) {
      events.push({
        title: 'Vehicle Created',
        detail: v?.name ?? row?.name ?? 'Vehicle',
        timestamp: createdAt,
        icon: 'directions_bus',
        tone: 'primary'
      });
    }

    if (this.hasGpsTracker()) {
      events.push({
        title: 'Tracker Assigned',
        detail: this.gps()?.deviceName ?? this.displayImei() ?? 'GPS device linked',
        timestamp: this.gps()?.lastUpdate ?? v?.updatedAt ?? createdAt,
        icon: 'gps_fixed',
        tone: 'success'
      });
    }

    if (this.driverName()) {
      events.push({
        title: 'Driver Assigned',
        detail: this.driverName()!,
        icon: 'person',
        tone: 'success'
      });
    }

    if (row?.serviceAlert) {
      events.push({
        title: 'Service Alert',
        detail: row.serviceAlert,
        timestamp: row.nextServiceDue ?? undefined,
        icon: 'build',
        tone: 'warning'
      });
    }

    for (const m of this.maintenance()) {
      events.push({
        title: m.status === MaintenanceStatus.Completed ? 'Maintenance Completed' : 'Maintenance Scheduled',
        detail: m.description,
        timestamp: m.maintenanceDate,
        icon: 'build',
        tone: m.status === MaintenanceStatus.Completed ? 'success' : 'warning'
      });
    }

    for (const log of this.fuelSummary()?.items ?? []) {
      events.push({
        title: 'Fuel Added',
        detail: `${log.liters}L at ${log.station || 'station'}`,
        timestamp: log.fuelDate,
        icon: 'local_gas_station',
        tone: 'primary'
      });
    }

    for (const doc of this.documents()) {
      events.push({
        title: 'Document Uploaded',
        detail: doc.documentType,
        icon: 'description',
        tone: 'muted'
      });
    }

    for (const trip of this.trips()) {
      events.push({
        title: trip.status === 'Completed' ? 'Trip Completed' : 'Trip Started',
        detail: `${trip.routeName} · ${trip.driverName || 'No driver'}`,
        timestamp: trip.pickupTime,
        icon: 'route',
        tone: trip.status === 'Completed' ? 'success' : 'primary'
      });
    }

    return this.filterHistory(events).sort((a, b) => {
      const ta = a.timestamp ? new Date(a.timestamp).getTime() : 0;
      const tb = b.timestamp ? new Date(b.timestamp).getTime() : 0;
      return tb - ta;
    });
  });

  constructor() {
    this.platformService.getBranches().pipe(catchError(() => of([]))).subscribe(branches => {
      this.branchMap.set(new Map(branches.map(b => [b.id, b.name])));
    });
    this.platformService.getDepartments().pipe(catchError(() => of([]))).subscribe(depts => {
      this.departmentMap.set(new Map(depts.map(d => [d.id, d.name])));
    });

    effect(() => {
      const id = this.effectiveVehicleId();
      const isOpen = this.open();

      if (!isOpen) {
        this.vehicle.set(null);
        this.loading.set(false);
        this.loadError.set(false);
        this.maintenanceLoading.set(false);
        this.fuelLoading.set(false);
        this.gpsLoading.set(false);
        this.tripsLoading.set(false);
        this.documentsLoading.set(false);
        this.maintenance.set([]);
        this.fuelSummary.set(null);
        this.trips.set([]);
        return;
      }

      if (!id) return;

      this.loadedTabs.clear();
      this.activeTab = 'general';
      this.historyFilter = 'month';
      this.vehicle.set(null);
      this.loadError.set(false);
      this.maintenance.set([]);
      this.fuelSummary.set(null);
      this.trips.set([]);
      this.seedGpsFromListItem();
      this.prefetchTabDataEarly(id);
      this.loadVehicle(id);
    });
  }

  retryLoad(): void {
    const id = this.effectiveVehicleId();
    if (id) this.loadVehicle(id);
  }

  onTabChange(tabId: string): void {
    this.activeTab = tabId;
    const id = this.effectiveVehicleId();
    if (id && this.open() && !this.loadedTabs.has(tabId)) {
      this.loadTab(tabId, id);
    }
  }

  onDrawerClosed(): void { this.closed.emit(); }

  closeDrawer(): void {
    this.open.set(false);
    this.closed.emit();
  }

  statusLabel(status: VehicleStatus): string {
    return VehicleStatusLabels[status] ?? 'Unknown';
  }

  fuelTypeLabel(ft: number): string {
    return FuelTypeLabels[ft as keyof typeof FuelTypeLabels] ?? '—';
  }

  maintenanceStatusLabel(status: number): string {
    return MaintenanceStatusLabels[status as keyof typeof MaintenanceStatusLabels] ?? '—';
  }

  driverInitials(): string {
    const name = this.driverName();
    if (!name) return '';
    return name.split(' ').map((n: string) => n[0] ?? '').join('').slice(0, 2).toUpperCase();
  }

  docLabel(type: string): string {
    return DOCUMENT_CATALOG.find(d => d.type === type)?.label ?? type;
  }

  docFileUrl(doc: VehicleDocument): string | null {
    return resolveUploadUrl(doc.fileUrl);
  }

  isPdfDoc(doc: VehicleDocument): boolean {
    return isPdfUploadUrl(doc.fileUrl);
  }

  getDocStatus(expiryDate?: string): string {
    if (!expiryDate) return 'No Expiry';
    const days = this.daysUntil(expiryDate);
    if (days < 0) return 'Expired';
    if (days <= 30) return 'Expiring Soon';
    return 'Valid';
  }

  getDocStatusClass(expiryDate?: string): string {
    if (!expiryDate) return 'vd-status-neutral';
    const days = this.daysUntil(expiryDate);
    if (days < 0) return 'vd-status-error';
    if (days <= 30) return 'vd-status-warning';
    return 'vd-status-success';
  }

  getMaintenanceStatusClass(status: number): string {
    const map: Record<number, string> = {
      1: 'vd-status-neutral',
      2: 'vd-status-warning',
      3: 'vd-status-success'
    };
    return map[status] ?? 'vd-status-neutral';
  }

  alertPriorityClass(priority: string): string {
    if (priority === 'high') return 'vd-status-error';
    if (priority === 'medium') return 'vd-status-warning';
    return 'vd-status-neutral';
  }

  mapsUrl(gps: VehicleGps): string | null {
    if (gps.latitude == null || gps.longitude == null) return null;
    return this.mapsUrlFromCoords(gps.latitude, gps.longitude);
  }

  mapsUrlFromCoords(latitude: number, longitude: number): string {
    return `https://www.google.com/maps?q=${latitude},${longitude}`;
  }

  totalTripDistance(): number {
    return this.trips().length * 42;
  }

  editVehicle(): void {
    const id = this.effectiveVehicleId();
    if (id) {
      this.closeDrawer();
      void this.router.navigate(['/vehicles', id, 'edit']);
    }
  }

  viewFullProfile(): void {
    const id = this.effectiveVehicleId();
    if (id) void this.router.navigate(['/vehicles', id]);
  }

  openStatusModal(): void {
    const v = this.vehicle();
    if (v) this.selectedStatus = String(v.status);
    this.statusReason = '';
    this.statusModalOpen = true;
  }

  openAssignDriverModal(): void {
    this.driverService.getAll(1, 500).subscribe(result => {
      this.driverOptions = result.items.map(d => ({
        value: String(d.id),
        label: `${d.fullName} (${d.phone})`
      }));
      this.selectedDriverId = null;
      this.bookingId = null;
      this.assignDriverModalOpen = true;
    });
  }

  openAssignGpsModal(): void {
    this.gpsService.getDevices().subscribe(devices => {
      const v = this.vehicle();
      this.gpsOptions = devices.map(d => ({
        value: String(d.id),
        label: `${d.name} (${d.uniqueId})`
      }));
      this.selectedGpsDeviceId = v?.gpsDeviceId != null ? String(v.gpsDeviceId) : null;
      this.assignGpsModalOpen = true;
    });
  }

  submitStatusChange(): void {
    const id = this.vehicleId();
    if (!id) return;
    this.vehicleService.changeStatus(id, {
      status: Number(this.selectedStatus) as VehicleStatus,
      reason: this.statusReason || null
    }).subscribe({
      next: () => {
        this.snackBar.open('Status updated', 'Close', { duration: 2000 });
        this.statusModalOpen = false;
        this.loadVehicle(id);
        this.statusChanged.emit();
      },
      error: () => this.snackBar.open('Status update failed', 'Close', { duration: 3000 })
    });
  }

  submitAssignDriver(): void {
    const id = this.vehicleId();
    if (!id || !this.selectedDriverId) return;
    this.vehicleService.assignDriver(id, {
      driverId: Number(this.selectedDriverId),
      bookingId: this.bookingId || null
    }).subscribe({
      next: () => {
        this.snackBar.open('Driver assigned', 'Close', { duration: 2000 });
        this.assignDriverModalOpen = false;
        this.loadVehicle(id);
        this.statusChanged.emit();
      },
      error: () => this.snackBar.open('Assign driver failed', 'Close', { duration: 3000 })
    });
  }

  submitAssignGps(): void {
    const id = this.vehicleId();
    if (!id || !this.selectedGpsDeviceId) return;
    this.vehicleService.assignGps(id, { gpsDeviceId: Number(this.selectedGpsDeviceId) }).subscribe({
      next: () => {
        this.snackBar.open('GPS device assigned', 'Close', { duration: 2000 });
        this.assignGpsModalOpen = false;
        this.loadedTabs.delete('gps');
        this.loadTab('gps', id);
        this.loadVehicle(id);
        this.statusChanged.emit();
      },
      error: () => this.snackBar.open('Assign GPS failed', 'Close', { duration: 3000 })
    });
  }

  submitDocument(): void {
    const id = this.vehicleId();
    if (!id || !this.newDocType.trim() || !this.newDocFile) return;

    this.docUploading = true;
    this.vehicleService.uploadDocument(
      id,
      this.newDocFile,
      this.newDocType.trim(),
      dateInputToIso(this.newDocExpiry) ?? undefined,
      this.newDocNotes || undefined
    ).subscribe({
      next: () => {
        this.snackBar.open('Document uploaded', 'Close', { duration: 2000 });
        this.docModalOpen = false;
        this.resetDocForm();
        this.loadedTabs.delete('documents');
        this.loadedTabs.delete('compliance');
        this.loadTab('documents', id);
      },
      error: () => {
        this.docUploading = false;
        this.snackBar.open('Upload failed', 'Close', { duration: 3000 });
      }
    });
  }

  onDocFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.newDocFile = input.files?.[0] ?? null;
  }

  private resetDocForm(): void {
    this.docUploading = false;
    this.newDocType = '';
    this.newDocFile = null;
    this.newDocExpiry = '';
    this.newDocNotes = '';
  }

  private daysUntil(dateStr: string): number {
    return Math.floor((new Date(dateStr).getTime() - Date.now()) / 86_400_000);
  }

  private filterHistory(events: HistoryEvent[]): HistoryEvent[] {
    if (this.historyFilter === 'all') return events;
    const now = Date.now();
    const limits: Record<string, number> = {
      today: 1,
      week: 7,
      month: 30
    };
    const days = limits[this.historyFilter] ?? 30;
    return events.filter(e => {
      if (!e.timestamp) return true;
      const diff = (now - new Date(e.timestamp).getTime()) / 86_400_000;
      return diff <= days;
    });
  }

  private seedGpsFromListItem(): void {
    const row = this.listItem();
    if (!row) return;

    if (row.locationLatitude != null && row.locationLongitude != null) {
      this.gps.set({
        latitude: row.locationLatitude,
        longitude: row.locationLongitude,
        lastUpdate: row.locationLastUpdate ?? row.gpsLastSeenAt ?? undefined,
        uniqueId: row.gpsImei ?? undefined,
        isActive: row.gpsOnline,
        lastIgnition: row.engineIgnition ?? undefined
      } as VehicleGps);
      return;
    }

    if (row.gpsImei || row.hasGpsDevice) {
      this.gps.set({
        uniqueId: row.gpsImei ?? undefined,
        isActive: row.gpsOnline,
        lastIgnition: row.engineIgnition ?? undefined,
        lastUpdate: row.gpsLastSeenAt ?? undefined
      } as VehicleGps);
    }
  }

  private prefetchDocumentsEarly(id: number): void {
    this.documentsLoading.set(true);
    this.vehicleService.getDocuments(id).pipe(catchError(() => of([]))).subscribe(docs => {
      this.documents.set(docs);
      this.documentsLoading.set(false);
      this.loadedTabs.add('documents');
      this.loadedTabs.add('compliance');
    });
  }

  private prefetchTabDataEarly(id: number): void {
    this.prefetchDocumentsEarly(id);
    this.loadMaintenanceData(id);
    this.loadFuelData(id);
    this.loadGpsData(id);
    this.loadTripsData(id);
  }

  private loadMaintenanceData(id: number): void {
    this.maintenanceLoading.set(true);
    this.vehicleService.getMaintenance(id, 1, 20).pipe(
      catchError(() => of({ items: [], totalCount: 0, page: 1, pageSize: 20 }))
    ).subscribe(result => {
      this.maintenance.set(result.items);
      this.nextService.set(result.items.find(m => m.nextDueDate) ?? result.items[0] ?? null);
      this.maintenanceLoading.set(false);
      this.loadedTabs.add('maintenance');
    });
  }

  private loadFuelData(id: number): void {
    this.fuelLoading.set(true);
    this.vehicleService.getFuel(id, 1, 20).pipe(
      catchError(() => of({ items: [], totalLiters: 0, totalCost: 0, totalCount: 0 }))
    ).subscribe(summary => {
      this.fuelSummary.set(summary);
      this.fuelLoading.set(false);
      this.loadedTabs.add('fuel');
    });
  }

  private loadGpsData(id: number): void {
    this.gpsLoading.set(true);
    this.vehicleService.getGps(id).pipe(catchError(() => of({} as VehicleGps))).subscribe(g => {
      this.mergeGpsData(g);
      this.gpsLoading.set(false);
      this.loadedTabs.add('gps');
    });
  }

  private loadTripsData(id: number): void {
    this.tripsLoading.set(true);
    this.bookingService.getAll(1, 200).pipe(
      catchError(() => of({ items: [], totalCount: 0, page: 1, pageSize: 200 }))
    ).subscribe(result => {
      this.trips.set(result.items.filter(b => b.vehicleId === id));
      this.tripsLoading.set(false);
      this.loadedTabs.add('trips');
      this.loadedTabs.add('assignments');
      this.loadedTabs.add('history');
      this.loadedTabs.add('alerts');
    });
  }

  private mergeGpsData(apiGps: VehicleGps): void {
    const current = this.gps();
    const row = this.listItem();
    this.gps.set({
      gpsDeviceId: apiGps.gpsDeviceId ?? current?.gpsDeviceId ?? (row?.hasGpsDevice ? -1 : null),
      deviceName: apiGps.deviceName ?? current?.deviceName ?? null,
      uniqueId: apiGps.uniqueId ?? current?.uniqueId ?? row?.gpsImei ?? null,
      isActive: apiGps.isActive ?? current?.isActive ?? row?.gpsOnline ?? null,
      lastIgnition: apiGps.lastIgnition ?? current?.lastIgnition ?? row?.engineIgnition ?? null,
      latitude: apiGps.latitude ?? current?.latitude ?? row?.locationLatitude ?? null,
      longitude: apiGps.longitude ?? current?.longitude ?? row?.locationLongitude ?? null,
      speed: apiGps.speed ?? current?.speed ?? null,
      lastUpdate: apiGps.lastUpdate ?? apiGps.lastSeenAt ?? current?.lastUpdate ?? row?.locationLastUpdate ?? row?.gpsLastSeenAt ?? null,
      lastSeenAt: apiGps.lastSeenAt ?? current?.lastSeenAt ?? row?.gpsLastSeenAt ?? null
    });
  }

  private prefetchGeneralTab(id: number): void {
    if (!this.loadedTabs.has('maintenance')) {
      this.loadMaintenanceData(id);
    } else {
      this.vehicleService.getMaintenance(id, 1, 1).pipe(
        catchError(() => of({ items: [], totalCount: 0, page: 1, pageSize: 1 }))
      ).subscribe(r => this.nextService.set(r.items[0] ?? null));
    }

    if (!this.loadedTabs.has('gps')) {
      this.loadGpsData(id);
    }

    if (!this.loadedTabs.has('documents')) {
      this.prefetchDocumentsEarly(id);
    }
  }

  private loadVehicle(id: number): void {
    this.loading.set(true);
    this.loadError.set(false);
    this.vehicleService.getById(id).subscribe({
      next: v => {
        this.vehicle.set(v);
        this.loading.set(false);
        this.loadError.set(false);
        this.loadedTabs.add('general');
        this.prefetchGeneralTab(id);
      },
      error: () => {
        this.loading.set(false);
        this.loadError.set(true);
      }
    });
  }

  private loadTab(tab: string, id: number): void {
    switch (tab) {
      case 'documents':
      case 'compliance':
        if (!this.loadedTabs.has('documents')) {
          this.prefetchDocumentsEarly(id);
        } else {
          this.loadedTabs.add(tab);
        }
        break;
      case 'maintenance':
        if (!this.loadedTabs.has('maintenance')) {
          this.loadMaintenanceData(id);
        }
        break;
      case 'fuel':
        if (!this.loadedTabs.has('fuel')) {
          this.loadFuelData(id);
        }
        break;
      case 'gps':
        if (!this.loadedTabs.has('gps')) {
          this.loadGpsData(id);
        }
        break;
      case 'trips':
        if (!this.loadedTabs.has('trips')) {
          this.loadTripsData(id);
        }
        break;
      case 'alerts':
        if (!this.loadedTabs.has('documents')) {
          this.vehicleService.getDocuments(id).pipe(catchError(() => of([]))).subscribe(docs => {
            this.documents.set(docs);
            this.loadedTabs.add('documents');
          });
        }
        if (!this.loadedTabs.has('maintenance')) {
          this.loadMaintenanceData(id);
        }
        this.loadedTabs.add(tab);
        break;
      case 'assignments':
      case 'history':
        if (!this.loadedTabs.has('trips')) {
          this.loadTripsData(id);
        } else {
          this.loadedTabs.add(tab);
        }
        break;
      default:
        this.loadedTabs.add(tab);
    }
  }
}
