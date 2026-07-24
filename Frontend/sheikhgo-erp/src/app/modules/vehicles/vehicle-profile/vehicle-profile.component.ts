import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ChartData } from 'chart.js';
import { forkJoin, of, Subscription } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { VehicleService } from '../../../core/services/vehicle.service';
import { BookingService } from '../../../core/services/booking.service';
import { DriverService } from '../../../core/services/driver.service';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { GpsRealtimeService } from '../../../core/services/gps-realtime.service';
import {
  Vehicle,
  VehicleStatus,
  VehicleStatusLabels,
  VehicleDocument,
  VehicleGps,
  VehicleFuelSummary,
  VehicleMaintenance,
  FuelTypeLabels,
  FuelType,
  MaintenanceStatusLabels,
  MaintenanceStatus
} from '../../../core/models/vehicle.model';
import { Booking } from '../../../core/models/booking.model';
import { Driver, DriverStatusLabels } from '../../../core/models/driver.model';
import { GpsTrip, PositionDto, TrackerDetail, TripAnalyticsSummary } from '../../../core/models/gps-tracking.model';
import { resolveFleetStatus } from '../../../core/utils/gps-status.util';
import { compareGpsTimestamps, parseGpsTimestamp } from '../../../core/utils/gps-timestamp.util';
import { formatRelativeTime } from '../../../core/utils/relative-time.util';
import {
  resolveUploadUrl,
  resolveVehicleImageUrl,
  resolveDriverPhotoUrl,
  isPdfUploadUrl
} from '../../../core/utils/upload-url.util';
import {
  VEHICLE_IMAGE_ANGLES,
  VehicleImageAngle,
  parseVehicleImageAngle,
  isPrimaryVehicleImage
} from '../vehicle-register-wizard/models/vehicle-wizard.model';

/** Matches Live Map / backend IsOnline window (30 minutes). */
const GPS_ONLINE_MS = 30 * 60 * 1000;

interface ComplianceItem {
  key: string;
  label: string;
  expiryDate?: string;
  status: string;
  statusClass: string;
  document?: VehicleDocument;
}

interface PhotoSlot {
  angle: VehicleImageAngle;
  label: string;
  document?: VehicleDocument;
  isPrimary: boolean;
}

interface TimelineEvent {
  title: string;
  detail?: string;
  timestamp?: string;
  icon: string;
  tone: 'success' | 'warning' | 'danger' | 'primary' | 'muted';
}

const COMPLIANCE_TYPES: { key: string; label: string; aliases: string[] }[] = [
  { key: 'Registration', label: 'Registration', aliases: ['Registration'] },
  { key: 'Insurance', label: 'Insurance', aliases: ['Insurance'] },
  { key: 'RoadTax', label: 'Road Tax', aliases: ['RoadTax', 'Road Tax'] },
  { key: 'Fitness', label: 'Fitness', aliases: ['Fitness'] },
  { key: 'Permit', label: 'Permit', aliases: ['Permit'] }
];

const DOC_LABELS: Record<string, string> = {
  Registration: 'Registration',
  Insurance: 'Insurance',
  RoadTax: 'Road Tax',
  Fitness: 'Fitness',
  Permit: 'Permit',
  VehicleImage: 'Vehicle Photo'
};

@Component({
  standalone: false,
  selector: 'app-vehicle-profile',
  templateUrl: './vehicle-profile.component.html',
  styleUrls: ['./vehicle-profile.component.scss']
})
export class VehicleProfileComponent implements OnInit, OnDestroy {
  vehicle: Vehicle | null = null;
  gps: VehicleGps | null = null;
  driver: Driver | null = null;
  tracker: TrackerDetail | null = null;
  /** True once trip analytics request finished (success or fail). */
  tripStatsLoaded = false;
  loading = true;
  /** Secondary panels (fuel/trips/etc.) fill in after first paint. */
  secondaryLoading = false;
  error: string | null = null;

  documents: VehicleDocument[] = [];
  recentBookings: Booking[] = [];
  fuelSummary: VehicleFuelSummary | null = null;
  recentMaintenance: VehicleMaintenance[] = [];
  recentTrips: GpsTrip[] = [];
  tripSummary: TripAnalyticsSummary | null = null;

  photoPreviewUrl: string | null = null;
  photoPreviewLabel = '';

  private sub?: Subscription;
  private secondarySubs: Subscription[] = [];
  private gpsPollSub?: Subscription;
  private realtimeSub?: Subscription;
  private connectionStateSub?: Subscription;
  private gpsPollTimer?: ReturnType<typeof setInterval>;
  private liveTelemetryVehicleId: number | null = null;
  private vehicleId = 0;
  private pendingSecondary = 0;

  readonly bookingColumns = ['bookingNumber', 'customerName', 'pickupTime', 'status'];
  readonly fuelColumns = ['fuelDate', 'fuelType', 'liters', 'totalCost'];
  readonly maintenanceColumns = ['maintenanceDate', 'description', 'status', 'cost'];
  readonly tripColumns = ['date', 'distance', 'duration', 'avgSpeed', 'fuel'];
  readonly docColumns = ['document', 'status', 'expiry', 'action'];

  fuelChartData: ChartData = { labels: [], datasets: [] };
  tripChartData: ChartData = { labels: [], datasets: [] };
  readonly chartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: {
      x: { grid: { display: false } },
      y: { beginAtZero: true, grid: { color: 'rgba(15, 23, 42, 0.06)' } }
    }
  };

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private vehicleService: VehicleService,
    private bookingService: BookingService,
    private driverService: DriverService,
    private gpsService: GpsTrackingService,
    private realtime: GpsRealtimeService
  ) {}

  ngOnInit(): void {
    const id = +this.route.snapshot.paramMap.get('id')!;
    this.vehicleId = id;
    this.loadProfile(id);
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
    this.secondarySubs.forEach(s => s.unsubscribe());
    this.secondarySubs = [];
    this.gpsPollSub?.unsubscribe();
    this.realtimeSub?.unsubscribe();
    this.connectionStateSub?.unsubscribe();
    if (this.gpsPollTimer) clearInterval(this.gpsPollTimer);
    this.liveTelemetryVehicleId = null;
    void this.realtime.subscribeVehicle(null);
  }

  /**
   * Fast first paint: vehicle + documents only.
   * GPS / fuel / trips / bookings load in the background so Traccar latency never blocks the page.
   */
  private loadProfile(id: number): void {
    this.loading = true;
    this.secondaryLoading = true;
    this.error = null;

    this.sub?.unsubscribe();
    this.sub = forkJoin({
      vehicle: this.vehicleService.getById(id),
      documents: this.vehicleService.getDocuments(id).pipe(catchError(() => of([] as VehicleDocument[])))
    }).subscribe({
      next: ({ vehicle, documents }) => {
        this.vehicle = vehicle;
        this.documents = documents;
        // Seed GPS card from enriched getById fields so UI is useful before /gps returns.
        this.seedGpsFromVehicle(vehicle);
        this.loading = false;
        this.startLiveTelemetry(id);
        this.loadSecondary(id);
      },
      error: () => {
        this.loading = false;
        this.secondaryLoading = false;
        this.error = 'Failed to load vehicle profile.';
      }
    });
  }

  private seedGpsFromVehicle(v: Vehicle): void {
    if (!v.gpsDeviceId && !v.hasGpsDevice) return;
    this.gps = {
      gpsDeviceId: v.gpsDeviceId ?? null,
      deviceName: v.trackerName ?? null,
      uniqueId: v.gpsImei ?? null,
      isActive: true,
      lastSeenAt: v.gpsLastSeenAt ?? null,
      lastIgnition: v.engineIgnition ?? null,
      latitude: v.locationLatitude ?? null,
      longitude: v.locationLongitude ?? null,
      speed: v.locationSpeed ?? null,
      lastUpdate: v.locationLastUpdate ?? v.gpsLastSeenAt ?? null,
      simNumber: v.gpsSim ?? null,
      modelName: v.trackerModel ?? null,
      brandName: v.trackerBrand ?? null,
      installationDate: v.trackerInstallationDate ?? null,
      totalDistanceKm: v.totalDistanceKm ?? null,
      batteryLevel: v.batteryLevel ?? null,
      gsmSignal: v.gsmSignal ?? null,
      address: v.address ?? null,
      gpsOnline: !!v.gpsOnline,
      heading: null,
      fuelLevel: null
    };
  }

  private loadSecondary(id: number): void {
    const from = new Date();
    from.setDate(from.getDate() - 30);
    const to = new Date();
    const driverId = this.vehicle?.driverId ?? null;
    const gpsDeviceId = this.vehicle?.gpsDeviceId ?? this.gps?.gpsDeviceId ?? null;

    this.secondarySubs.forEach(s => s.unsubscribe());
    this.secondarySubs = [];
    this.pendingSecondary = 4;
    this.secondaryLoading = true;
    this.tripStatsLoaded = false;

    // GPS alone — must not wait on trip reports.
    this.secondarySubs.push(
      this.vehicleService.getGps(id).pipe(catchError(() => of(null))).subscribe(gps => {
        if (
          gps
          && (
            !this.gps?.lastUpdate
            || !gps.lastUpdate
            || compareGpsTimestamps(gps.lastUpdate, this.gps.lastUpdate) >= 0
          )
        ) {
          this.gps = gps;
        }
        const deviceId = gps?.gpsDeviceId ?? gpsDeviceId;
        if (deviceId && !this.tracker) {
          this.secondarySubs.push(
            this.gpsService.getTracker(deviceId).pipe(catchError(() => of(null))).subscribe(t => {
              this.tracker = t;
            })
          );
        }
        this.markSecondaryDone();
      })
    );

    // Driver detail (photo, branch, rating, etc.).
    this.secondarySubs.push(
      (driverId
        ? this.driverService.getById(driverId).pipe(catchError(() => of(null)))
        : of(null)
      ).subscribe(d => {
        this.driver = d;
        this.markSecondaryDone();
      })
    );

    // Local ERP panels (fast SQL).
    this.secondarySubs.push(
      forkJoin({
        fuel: this.vehicleService.getFuel(id, 1, 20).pipe(catchError(() => of(null))),
        maintenance: this.vehicleService.getMaintenance(id, 1, 10).pipe(
          catchError(() => of({ items: [] as VehicleMaintenance[] }))
        ),
        bookings: this.bookingService.getAll(1, 50).pipe(catchError(() => of({ items: [] as Booking[] })))
      }).subscribe(({ fuel, maintenance, bookings }) => {
        this.fuelSummary = fuel;
        this.recentMaintenance = maintenance.items ?? [];
        this.recentBookings = (bookings.items ?? []).filter(b => b.vehicleId === id).slice(0, 8);
        this.buildCharts();
        this.markSecondaryDone();
      })
    );

    // Traccar trip reports — often the slowest; fill KPIs when ready.
    this.secondarySubs.push(
      forkJoin({
        trips: this.gpsService.getTrips(id, from, to, undefined, { page: 1, pageSize: 10 }).pipe(
          catchError(() => of({ items: [] as GpsTrip[] }))
        ),
        tripAnalytics: this.gpsService.getTripAnalytics(id, from, to).pipe(catchError(() => of(null)))
      }).subscribe(({ trips, tripAnalytics }) => {
        this.recentTrips = trips.items ?? [];
        this.tripSummary = tripAnalytics?.summary ?? null;
        this.tripStatsLoaded = true;
        this.buildCharts();
        this.markSecondaryDone();
      })
    );
  }

  private markSecondaryDone(): void {
    this.pendingSecondary = Math.max(0, this.pendingSecondary - 1);
    if (this.pendingSecondary === 0) this.secondaryLoading = false;
  }

  /** SignalR primary; HTTP poll only while hub is disconnected (5–10s). */
  private startLiveTelemetry(vehicleId: number): void {
    this.gpsPollSub?.unsubscribe();
    this.realtimeSub?.unsubscribe();
    this.connectionStateSub?.unsubscribe();
    if (this.gpsPollTimer) clearInterval(this.gpsPollTimer);
    this.liveTelemetryVehicleId = vehicleId;

    void this.realtime.connect().then(() => this.realtime.subscribeVehicle(vehicleId));
    this.realtimeSub = this.realtime.locationUpdates$.subscribe(update => {
      if (update.vehicleId !== vehicleId) return;
      this.applyRealtimePosition(update);
    });

    this.connectionStateSub = this.realtime.connectionState$.subscribe(state => {
      if (this.gpsPollTimer) {
        clearInterval(this.gpsPollTimer);
        this.gpsPollTimer = undefined;
      }
      if (state !== 'connected' && this.liveTelemetryVehicleId === vehicleId) {
        this.refreshGps(vehicleId);
        this.gpsPollTimer = setInterval(() => this.refreshGps(vehicleId), 10_000);
      }
    });
  }

  private refreshGps(vehicleId: number): void {
    this.gpsPollSub?.unsubscribe();
    this.gpsPollSub = this.vehicleService.getGps(vehicleId).pipe(catchError(() => of(null))).subscribe(gps => {
      if (!gps) return;
      // Do not overwrite a fresher SignalR fix with a slightly older poll.
      if (
        this.gps?.lastUpdate
        && gps.lastUpdate
        && compareGpsTimestamps(this.gps.lastUpdate, gps.lastUpdate) > 0
      ) {
        return;
      }
      this.gps = gps;
    });
  }

  private applyRealtimePosition(update: PositionDto): void {
    const prev = this.gps;
    this.gps = {
      gpsDeviceId: prev?.gpsDeviceId ?? update.gpsDeviceId ?? this.vehicle?.gpsDeviceId ?? null,
      deviceName: prev?.deviceName ?? null,
      uniqueId: prev?.uniqueId ?? this.vehicle?.gpsImei ?? null,
      isActive: true,
      lastSeenAt: update.timestamp,
      lastIgnition: update.ignition ?? prev?.lastIgnition ?? null,
      latitude: update.latitude,
      longitude: update.longitude,
      speed: update.speed,
      lastUpdate: update.timestamp,
      simNumber: prev?.simNumber ?? this.vehicle?.gpsSim ?? null,
      modelName: prev?.modelName ?? this.vehicle?.trackerModel ?? null,
      brandName: prev?.brandName ?? this.vehicle?.trackerBrand ?? null,
      installationDate: prev?.installationDate ?? this.vehicle?.trackerInstallationDate ?? null,
      totalDistanceKm: update.totalDistanceKm ?? prev?.totalDistanceKm ?? null,
      batteryLevel: update.batteryLevel ?? prev?.batteryLevel ?? null,
      gsmSignal: update.gsmSignal ?? prev?.gsmSignal ?? null,
      address: update.address ?? prev?.address ?? null,
      gpsOnline: true,
      heading: update.heading ?? prev?.heading ?? null,
      fuelLevel: update.fuelLevel ?? prev?.fuelLevel ?? null
    };
  }

  // ─── Derived display helpers ───────────────────────────────────────────

  get primaryImageUrl(): string | null {
    const fromApi = resolveVehicleImageUrl(this.vehicle?.imageUrl);
    if (fromApi) return fromApi;
    const images = this.imageDocuments;
    const primary = images.find(d => isPrimaryVehicleImage(d.notes));
    const fallback = primary ?? images[0];
    if (!fallback || isPdfUploadUrl(fallback.fileUrl)) return null;
    return resolveUploadUrl(fallback.fileUrl);
  }

  get imageDocuments(): VehicleDocument[] {
    return this.documents.filter(d => d.documentType === 'VehicleImage' && !!d.fileUrl?.trim());
  }

  get photoSlots(): PhotoSlot[] {
    const imageDocs = this.imageDocuments;
    const used = new Set<number>();
    return VEHICLE_IMAGE_ANGLES.map(({ angle, label }) => {
      let doc = imageDocs.find(d => !used.has(d.id) && parseVehicleImageAngle(d.notes) === angle);
      if (!doc && angle === 'Front') {
        doc = imageDocs.find(d => !used.has(d.id) && !parseVehicleImageAngle(d.notes));
      }
      if (doc) used.add(doc.id);
      return { angle, label, document: doc, isPrimary: doc ? isPrimaryVehicleImage(doc.notes) : false };
    });
  }

  get complianceDocuments(): VehicleDocument[] {
    return this.documents.filter(d => d.documentType !== 'VehicleImage');
  }

  get complianceItems(): ComplianceItem[] {
    return COMPLIANCE_TYPES.map(item => {
      const doc = this.documents.find(d =>
        !!d.fileUrl?.trim()
        && item.aliases.some(a => d.documentType.toLowerCase() === a.toLowerCase())
      );
      const expiry = doc?.expiryDate
        ?? (item.key === 'Insurance' ? this.vehicle?.insuranceExpiryDate ?? undefined : undefined);
      return {
        key: item.key,
        label: item.label,
        expiryDate: expiry ?? undefined,
        status: doc || expiry ? this.docStatus(expiry) : 'Missing',
        statusClass: doc || expiry ? this.docStatusClass(expiry) : 'status-missing',
        document: doc
      };
    });
  }

  get complianceValid(): number {
    return this.complianceItems.filter(i => i.status === 'Valid' || i.status === 'No Expiry').length;
  }

  get complianceExpiring(): number {
    return this.complianceItems.filter(i => i.status === 'Expiring Soon').length;
  }

  get complianceExpired(): number {
    return this.complianceItems.filter(i => i.status === 'Expired' || i.status === 'Missing').length;
  }

  get displayMileage(): number {
    const fromGps = this.gps?.totalDistanceKm ?? this.vehicle?.totalDistanceKm;
    if (fromGps != null && fromGps > 0) return Number(fromGps);
    return this.vehicle?.currentMileage ?? 0;
  }

  /** Motion / lifecycle state — same rule as Live Map (`resolveFleetStatus`). */
  get liveStatus(): string {
    const v = this.vehicle;
    if (!v) return 'Unknown';
    if (v.status === VehicleStatus.Maintenance) return 'Maintenance';
    if (!v.hasGpsDevice && !v.gpsDeviceId && !this.gps?.gpsDeviceId) return this.vehicleStatusLabel(v);
    return resolveFleetStatus({
      speed: Number(this.gps?.speed ?? v.locationSpeed ?? 0),
      ignition: this.gps?.lastIgnition ?? v.engineIgnition,
      lastUpdated: this.lastTelemetryAt,
      hasGps: !!(v.hasGpsDevice || v.gpsDeviceId || this.gps?.gpsDeviceId)
    });
  }

  get liveStatusLabel(): string {
    const map: Record<string, string> = {
      moving: 'Moving',
      idle: 'Idle',
      parked: 'Parked',
      offline: 'Offline',
      never_seen: 'Never Seen',
      sos: 'SOS',
      Maintenance: 'Maintenance',
      Available: 'Available',
      'On Trip': 'On Trip',
      Retired: 'Retired',
      Draft: 'Draft'
    };
    return map[this.liveStatus] ?? this.liveStatus;
  }

  /** Connection freshness — independent of Parked/Moving (matches Live Map "Connected"). */
  get isGpsOnline(): boolean {
    const at = this.lastTelemetryAt;
    if (at) {
      const age = Date.now() - parseGpsTimestamp(at);
      if (Number.isFinite(age) && age >= 0 && age <= GPS_ONLINE_MS) return true;
    }
    return !!(this.gps?.gpsOnline ?? this.vehicle?.gpsOnline);
  }

  get lastTelemetryAt(): string | null {
    return this.gps?.lastUpdate
      ?? this.vehicle?.locationLastUpdate
      ?? this.gps?.lastSeenAt
      ?? this.vehicle?.gpsLastSeenAt
      ?? null;
  }

  get motionLabel(): string | null {
    if (!this.isGpsOnline) return null;
    const s = this.liveStatus;
    if (s === 'moving' || s === 'idle' || s === 'parked' || s === 'sos') return this.liveStatusLabel;
    return null;
  }

  get liveStatusClass(): string {
    if (!this.hasTracker) return 'live-muted';
    if (!this.isGpsOnline) return 'live-off';
    const s = this.liveStatus;
    if (s === 'moving') return 'live-moving';
    if (s === 'idle') return 'live-idle';
    if (s === 'parked') return 'live-parked';
    if (s === 'sos') return 'live-sos';
    if (s === 'Maintenance') return 'live-maint';
    return 'live-online';
  }

  get hasTracker(): boolean {
    return !!(this.vehicle?.hasGpsDevice || this.vehicle?.gpsDeviceId || this.gps?.gpsDeviceId);
  }

  get trackerStatusLabel(): string {
    if (!this.hasTracker) return 'Unassigned';
    return this.isGpsOnline ? 'Connected' : 'Offline';
  }

  get displaySpeed(): number | null {
    const s = this.gps?.speed ?? this.vehicle?.locationSpeed;
    return s != null ? Number(s) : null;
  }

  get displayIgnition(): boolean | null {
    return this.gps?.lastIgnition ?? this.vehicle?.engineIgnition ?? null;
  }

  get lastUpdateRelative(): string {
    return formatRelativeTime(this.lastTelemetryAt);
  }

  get reportedOrDash(): string {
    return 'Not Reported';
  }

  get displayAddress(): string {
    return this.gps?.address || this.vehicle?.address || this.reportedOrDash;
  }

  get displayBattery(): string {
    const b = this.gps?.batteryLevel ?? this.vehicle?.batteryLevel ?? this.tracker?.lastBatteryLevel;
    if (b == null) return this.reportedOrDash;
    const n = Number(b);
    // Traccar batteryLevel is usually 0–100%; power/voltage is often > 100 or decimal volts.
    if (n > 0 && n <= 100) return `${n.toFixed(0)}%`;
    return `${n.toFixed(1)} V`;
  }

  get displayGsm(): string {
    const g = this.gps?.gsmSignal ?? this.vehicle?.gsmSignal ?? this.tracker?.lastRssi;
    return g != null ? String(g) : this.reportedOrDash;
  }

  get displayCoords(): string {
    const lat = this.gps?.latitude ?? this.vehicle?.locationLatitude;
    const lng = this.gps?.longitude ?? this.vehicle?.locationLongitude;
    if (lat == null || lng == null || (lat === 0 && lng === 0)) return this.reportedOrDash;
    return `${Number(lat).toFixed(5)}, ${Number(lng).toFixed(5)}`;
  }

  get displayHeading(): string {
    const h = this.gps?.heading;
    if (h == null || !Number.isFinite(Number(h))) return this.reportedOrDash;
    const deg = ((Math.round(Number(h)) % 360) + 360) % 360;
    const dirs = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];
    return `${deg}° ${dirs[Math.round(deg / 45) % 8]}`;
  }

  get displayFuelLevel(): string {
    const f = this.gps?.fuelLevel;
    return f != null ? `${Number(f).toFixed(0)}%` : this.reportedOrDash;
  }

  get displayMotion(): string {
    return this.motionLabel || (this.isGpsOnline ? 'Stationary' : 'Unknown');
  }

  get driverPhotoUrl(): string | null {
    return resolveDriverPhotoUrl(this.driver?.photoUrl);
  }

  get driverStatusLabel(): string {
    if (!this.driver) return 'Assigned';
    return DriverStatusLabels[this.driver.status] ?? this.driver.status?.toString() ?? 'Assigned';
  }

  get compliancePercent(): number {
    const total = this.complianceItems.length || 1;
    return Math.round((this.complianceValid / total) * 100);
  }

  get nextExpiringItem(): ComplianceItem | null {
    const withExpiry = this.complianceItems
      .filter(i => i.expiryDate && (i.status === 'Expiring Soon' || i.status === 'Valid'))
      .sort((a, b) => this.daysUntil(a.expiryDate!) - this.daysUntil(b.expiryDate!));
    return withExpiry[0] ?? null;
  }

  get lastFuelLog() {
    return this.fuelSummary?.items?.[0] ?? null;
  }

  get lastTripLabel(): string {
    if (!this.tripStatsLoaded) return 'Loading';
    const t = this.recentTrips[0];
    if (!t) return '—';
    const dist = t.distanceKm != null ? `${Number(t.distanceKm).toFixed(1)} km` : '';
    return [formatRelativeTime(t.endTime || t.startTime), dist].filter(Boolean).join(' · ');
  }

  get lifetimeCost(): number | null {
    const purchase = this.vehicle?.purchasePrice;
    const fuel = this.totalFuelCost;
    const maint = this.totalMaintenanceCost;
    if (purchase == null && fuel === 0 && maint === 0) return null;
    return (purchase ?? 0) + fuel + maint;
  }

  /** KPI display: distinguish "no data yet" from a real zero. */
  kpiValue(value: number | null | undefined, opts?: { suffix?: string; digits?: string; treatZeroAsMissing?: boolean }): string {
    if (value == null || !Number.isFinite(Number(value))) return '—';
    const n = Number(value);
    if (opts?.treatZeroAsMissing && n === 0) return '—';
    const formatted = opts?.digits
      ? n.toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 1 })
      : Math.round(n).toLocaleString();
    return opts?.suffix ? `${formatted}${opts.suffix}` : formatted;
  }

  get tripsKpi(): string {
    if (!this.tripStatsLoaded) return 'Loading';
    const n = this.tripSummary?.tripCount ?? this.recentTrips.length;
    return n > 0 ? String(n) : '—';
  }

  get distanceKpi(): string {
    if (!this.tripStatsLoaded) return 'Loading';
    const n = this.tripSummary?.distanceKm;
    if (n == null || n === 0) return '—';
    return `${Math.round(n).toLocaleString()} km`;
  }

  get engineHoursKpi(): string {
    if (!this.tripStatsLoaded) return 'Loading';
    const hours = this.tripSummary?.engineHours
      ?? (this.tripSummary?.drivingMinutes != null ? this.tripSummary.drivingMinutes / 60 : null);
    if (hours == null || hours === 0) return '—';
    return hours.toFixed(1);
  }

  get idleKpi(): string {
    if (!this.tripStatsLoaded) return 'Loading';
    const n = this.tripSummary?.idleMinutes;
    if (n == null || n === 0) return '—';
    return String(Math.round(n));
  }

  get avgSpeedKpi(): string {
    if (!this.tripStatsLoaded) return 'Loading';
    const n = this.tripSummary?.avgSpeedKmh;
    if (n == null || n === 0) return '—';
    return `${Math.round(n)}`;
  }

  get fuelUsedKpi(): string {
    if (this.fuelSummary == null) return 'Loading';
    if (!this.fuelSummary.totalCount) return '—';
    return `${Math.round(this.totalFuelLiters)} L`;
  }

  get fuelCostKpi(): string {
    if (this.fuelSummary == null) return 'Loading';
    if (!this.fuelSummary.totalCount) return '—';
    return `PKR ${Math.round(this.totalFuelCost).toLocaleString()}`;
  }

  get maintCostKpi(): string {
    if (this.secondaryLoading && !this.recentMaintenance.length) return 'Loading';
    if (!this.recentMaintenance.length) return '—';
    return `PKR ${Math.round(this.totalMaintenanceCost).toLocaleString()}`;
  }

  get mapsUrl(): string | null {
    const lat = this.gps?.latitude ?? this.vehicle?.locationLatitude;
    const lng = this.gps?.longitude ?? this.vehicle?.locationLongitude;
    if (lat == null || lng == null || (lat === 0 && lng === 0)) return null;
    return `https://www.google.com/maps?q=${lat},${lng}`;
  }

  get totalFuelLiters(): number {
    return this.fuelSummary?.totalLiters ?? 0;
  }

  get totalFuelCost(): number {
    return this.fuelSummary?.totalCost ?? 0;
  }

  get totalMaintenanceCost(): number {
    return this.recentMaintenance.reduce((s, m) => s + (m.cost || 0), 0);
  }

  get timelineEvents(): TimelineEvent[] {
    const events: TimelineEvent[] = [];
    const v = this.vehicle;
    if (!v) return [];

    if (v.createdAt) {
      events.push({
        title: 'Vehicle Registered',
        detail: v.name,
        timestamp: v.createdAt,
        icon: 'directions_bus',
        tone: 'primary'
      });
    }

    if (v.hasGpsDevice || v.gpsDeviceId || this.gps?.gpsDeviceId) {
      events.push({
        title: 'Tracker Installed',
        detail: this.gps?.deviceName ?? v.trackerName ?? v.gpsImei ?? 'GPS device linked',
        timestamp: this.gps?.installationDate ?? v.trackerInstallationDate ?? v.updatedAt ?? undefined,
        icon: 'gps_fixed',
        tone: 'success'
      });
    }

    if (v.driverName) {
      events.push({
        title: 'Driver Assigned',
        detail: v.driverName,
        icon: 'person',
        tone: 'success'
      });
    }

    for (const m of this.recentMaintenance) {
      events.push({
        title: m.status === MaintenanceStatus.Completed ? 'Maintenance Completed' : 'Maintenance Scheduled',
        detail: m.description,
        timestamp: m.maintenanceDate,
        icon: 'build',
        tone: m.status === MaintenanceStatus.Completed ? 'success' : 'warning'
      });
    }

    for (const log of this.fuelSummary?.items ?? []) {
      events.push({
        title: 'Fuel Added',
        detail: `${log.liters} L · PKR ${log.totalCost}`,
        timestamp: log.fuelDate,
        icon: 'local_gas_station',
        tone: 'primary'
      });
    }

    for (const trip of this.recentTrips.slice(0, 5)) {
      events.push({
        title: 'Trip Completed',
        detail: `${trip.distanceKm?.toFixed?.(1) ?? trip.distanceKm} km · ${trip.durationMinutes} min`,
        timestamp: trip.endTime || trip.startTime,
        icon: 'route',
        tone: 'success'
      });
    }

    for (const booking of this.recentBookings.slice(0, 5)) {
      events.push({
        title: 'Booking Assigned',
        detail: `${booking.bookingNumber || '#' + booking.id} · ${booking.customerName}`,
        timestamp: booking.pickupTime,
        icon: 'event_available',
        tone: 'primary'
      });
    }

    return events
      .sort((a, b) => {
        const ta = a.timestamp ? new Date(a.timestamp).getTime() : 0;
        const tb = b.timestamp ? new Date(b.timestamp).getTime() : 0;
        return tb - ta;
      })
      .slice(0, 12);
  }

  private buildCharts(): void {
    const fuelItems = [...(this.fuelSummary?.items ?? [])].reverse().slice(-8);
    this.fuelChartData = {
      labels: fuelItems.map(f => new Date(f.fuelDate).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })),
      datasets: [{
        data: fuelItems.map(f => f.liters),
        label: 'Liters',
        borderColor: '#1565c0',
        backgroundColor: 'rgba(21, 101, 192, 0.12)',
        fill: true,
        tension: 0.35
      }]
    };

    const trips = [...this.recentTrips].reverse().slice(-8);
    this.tripChartData = {
      labels: trips.map(t => new Date(t.startTime).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })),
      datasets: [{
        data: trips.map(t => t.distanceKm),
        label: 'Distance (km)',
        backgroundColor: '#0d9488',
        borderRadius: 4
      }]
    };
  }

  // ─── Labels / status helpers ───────────────────────────────────────────

  fuelTypeLabel(type: FuelType): string {
    return FuelTypeLabels[type] ?? 'Unknown';
  }

  vehicleStatusLabel(v: Vehicle): string {
    return VehicleStatusLabels[v.status as VehicleStatus] ?? 'Unknown';
  }

  maintenanceStatusLabel(status: MaintenanceStatus): string {
    return MaintenanceStatusLabels[status] ?? 'Unknown';
  }

  docLabel(type: string): string {
    if (type === 'VehicleImage') {
      return 'Vehicle Photo';
    }
    return DOC_LABELS[type] ?? type;
  }

  docStatus(expiryDate?: string | null): string {
    if (!expiryDate) return 'No Expiry';
    const days = this.daysUntil(expiryDate);
    if (days < 0) return 'Expired';
    if (days <= 30) return 'Expiring Soon';
    return 'Valid';
  }

  docStatusClass(expiryDate?: string | null): string {
    if (!expiryDate) return 'status-neutral';
    const days = this.daysUntil(expiryDate);
    if (days < 0) return 'status-expired';
    if (days <= 30) return 'status-expiring';
    return 'status-valid';
  }

  docFileUrl(doc: VehicleDocument): string | null {
    return resolveUploadUrl(doc.fileUrl);
  }

  photoUrl(doc?: VehicleDocument): string | null {
    if (!doc?.fileUrl?.trim() || isPdfUploadUrl(doc.fileUrl)) return null;
    return resolveUploadUrl(doc.fileUrl);
  }

  isPdf(doc: VehicleDocument): boolean {
    return isPdfUploadUrl(doc.fileUrl);
  }

  daysUntil(dateStr: string): number {
    const target = new Date(dateStr.includes('T') ? dateStr : `${dateStr}T00:00:00`);
    const now = new Date();
    now.setHours(0, 0, 0, 0);
    target.setHours(0, 0, 0, 0);
    return Math.round((target.getTime() - now.getTime()) / 86400000);
  }

  getStatusColor(status: string): string {
    const colors: Record<string, string> = {
      Pending: '#f57f17', Confirmed: '#1565c0', Started: '#00695c',
      InProgress: '#00695c', Completed: '#2e7d32', Cancelled: '#c62828'
    };
    return colors[status] ?? '#666';
  }

  getMaintenanceStatusColor(status: MaintenanceStatus): string {
    const colors: Record<number, string> = { 1: '#f57f17', 2: '#1565c0', 3: '#2e7d32' };
    return colors[status] ?? '#666';
  }

  driverInitials(): string {
    const name = this.driver?.fullName || this.vehicle?.driverName;
    if (!name) return '?';
    return name.split(/\s+/).map(n => n[0] ?? '').join('').slice(0, 2).toUpperCase();
  }

  openPhotoPreview(url: string, label: string): void {
    this.photoPreviewUrl = url;
    this.photoPreviewLabel = label;
  }

  closePhotoPreview(): void {
    this.photoPreviewUrl = null;
    this.photoPreviewLabel = '';
  }

  // ─── Navigation actions ────────────────────────────────────────────────

  editVehicle(): void {
    this.router.navigate(['/vehicles', this.vehicle?.id, 'edit']);
  }

  goLiveTracking(): void {
    this.router.navigate(['/gps-tracking/live'], { queryParams: { vehicleId: this.vehicle?.id } });
  }

  goTripHistory(): void {
    this.router.navigate(['/gps-tracking/trips'], { queryParams: { vehicleId: this.vehicle?.id } });
  }

  goReplay(): void {
    this.router.navigate(['/gps-tracking/history'], { queryParams: { vehicleId: this.vehicle?.id } });
  }

  createBooking(): void {
    this.router.navigate(['/bookings/new'], { queryParams: { vehicleId: this.vehicle?.id } });
  }

  addFuel(): void {
    this.router.navigate(['/fuel-logs/new'], { queryParams: { vehicleId: this.vehicle?.id } });
  }

  scheduleMaintenance(): void {
    this.router.navigate(['/fleet/maintenance/schedules'], { queryParams: { vehicleId: this.vehicle?.id } });
  }

  assignDriver(): void {
    this.router.navigate(['/fleet/assignments'], { queryParams: { vehicleId: this.vehicle?.id } });
  }

  installTracker(): void {
    this.router.navigate(['/gps-tracking/devices'], { queryParams: { vehicleId: this.vehicle?.id } });
  }

  viewTracker(): void {
    const id = this.gps?.gpsDeviceId ?? this.vehicle?.gpsDeviceId;
    if (id) {
      this.router.navigate(['/gps-tracking/devices'], { queryParams: { trackerId: id } });
    } else {
      this.installTracker();
    }
  }

  goCommands(): void {
    const id = this.vehicle?.id;
    this.router.navigate(['/gps-tracking/devices'], { queryParams: { vehicleId: id, tab: 'commands' } });
  }

  deleteVehicle(): void {
    if (!this.vehicle || !confirm(`Delete vehicle "${this.vehicle.name}"? This cannot be undone.`)) return;
    this.vehicleService.delete(this.vehicle.id).subscribe({
      next: () => this.router.navigate(['/vehicles']),
      error: () => alert('Failed to delete vehicle.')
    });
  }

  viewDriver(): void {
    if (this.vehicle?.driverId) {
      this.router.navigate(['/drivers', this.vehicle.driverId]);
    }
  }

  openDoc(doc: VehicleDocument): void {
    const url = this.docFileUrl(doc);
    if (url) window.open(url, '_blank', 'noopener');
  }

  renewCompliance(item: ComplianceItem): void {
    this.router.navigate(['/vehicles', this.vehicle?.id, 'edit']);
  }
}
