/**
 * 'moving'/'idle'/'parked'/'offline'/'never_seen'/'sos' are derived from live Traccar telemetry
 * (see core/utils/gps-status.util.ts for the single source of truth on how). 'scheduled'/'delayed'
 * are a separate, booking-derived concern and are not produced by that function.
 */
export type FleetTrackStatus =
  | 'moving'
  | 'idle'
  | 'parked'
  | 'offline'
  | 'never_seen'
  | 'sos'
  | 'scheduled'
  | 'delayed';

export interface PositionDto {
  id: number;
  vehicleId: number;
  driverId?: number;
  bookingId?: number;
  gpsDeviceId?: number;
  latitude: number;
  longitude: number;
  speed: number;
  heading?: number;
  altitude?: number;
  ignition?: boolean;
  timestamp: string;
  fuelLevel?: number;
  batteryLevel?: number;
  gsmSignal?: number;
  totalDistanceKm?: number;
  address?: string;
  alarmType?: string;
}

export interface VehicleLocation {
  vehicleId: number;
  vehicleName: string;
  registrationNumber: string;
  latitude: number;
  longitude: number;
  lastUpdated: string;
  speed: number;
  status: FleetTrackStatus;
  driverName?: string;
  hasGps?: boolean;
  /** True when telemetry was received within the recent window (typically 30 min). */
  isLive?: boolean;
  routeHint?: string;
  ignition?: boolean;
  heading?: number;
  fuelLevel?: number;
  batteryLevel?: number;
  gsmSignal?: number;
  totalDistanceKm?: number;
  address?: string;
  alarmType?: string;
  vehicleType?: string | null;
  trackerName?: string;
  imei?: string;
  relayOutput?: string;
}

export interface SosAlertPayload {
  vehicleId: number;
  latitude: number;
  longitude: number;
  timestamp: string;
}

export interface GpsDevice {
  id: number;
  vehicleId?: number;
  vehicleName?: string;
  plateNumber?: string;
  driverId?: number;
  driverName?: string;
  uniqueId: string;
  name: string;
  protocol?: string;
  disabled?: boolean;
  supportsEngineCutoff: boolean;
  lastIgnition?: boolean;
  lastSeenAt?: string;
  isActive: boolean;
  isOnline?: boolean;
  lastSpeed?: number;
  lastBatteryLevel?: number;
  lastRssi?: number;
  traccarDeviceId?: number;
  isTraccarLinked?: boolean;
  isValidImei?: boolean;
  trackerBrandId?: number;
  trackerBrandName?: string;
  modelName?: string;
  model?: string;
  simNumber?: string;
  vendor?: string;
  serialNumber?: string;
  installationDate?: string;
  installedBy?: string;
  installationNotes?: string;
  relayOutput?: string;
  relayPurpose?: string;
  currentStatus?: string;
}

export interface TrackerDetail extends GpsDevice {
  driverId?: number;
  category?: string;
  phone?: string;
  contact?: string;
  disabled?: boolean;
  trackerModelKey?: string;
  trackerModelId?: number;
  trackerBrandId?: number;
  trackerBrandName?: string;
  modelName?: string;
  countryCode?: string;
  simProvider?: string;
  simPackage?: string;
  monthlySimCost?: number;
  warrantyStart?: string;
  warrantyEnd?: string;
  purchaseDate?: string;
  purchasePrice?: number;
  currentStatus?: string;
  lastSyncAt?: string;
}

export interface RegisterTrackerPayload {
  name: string;
  uniqueId: string;
  category: string;
  trackerModelId: number;
  trackerModelKey?: string;
  phone?: string;
  contact?: string;
  disabled?: boolean;
  vehicleId?: number;
  driverId?: number;
  supportsEngineCutoff?: boolean;
  relayOutput?: string;
  relayPurpose?: string;
  installationDate?: string;
  installedBy?: string;
  installationNotes?: string;
  serialNumber?: string;
  countryCode?: string;
  simProvider?: string;
  simPackage?: string;
  monthlySimCost?: number;
  warrantyStart?: string;
  warrantyEnd?: string;
  purchaseDate?: string;
  purchasePrice?: number;
  vendor?: string;
  currentStatus?: string;
}

export interface TrackerRegisteredResult {
  id: number;
  name: string;
  uniqueId: string;
  protocolLabel: string;
  vehicleName?: string;
  plateNumber?: string;
  traccarDeviceId?: number;
  statusMessage: string;
}

export interface InstallTrackerPayload {
  vehicleId: number;
  driverId?: number;
  installationDate?: string;
  installedBy?: string;
  installationNotes?: string;
  relayOutput?: string;
}

export interface TransferTrackerPayload extends InstallTrackerPayload {
  reason: string;
}

export interface UninstallTrackerPayload {
  removedBy?: string;
  reason?: string;
}

export interface TrackerAssignment {
  id: number;
  gpsDeviceId: number;
  vehicleId: number;
  vehicleName?: string | null;
  plateNumber?: string | null;
  driverId?: number | null;
  driverName?: string | null;
  installedDate: string;
  removedDate?: string | null;
  installedBy?: string | null;
  removedBy?: string | null;
  reason?: string | null;
  isActive: boolean;
}

export interface TrackerInstallVehicle {
  vehicleId: number;
  name: string;
  plateNumber?: string | null;
  vehicleCode?: string | null;
  isSelectable: boolean;
  assignedTrackerName?: string | null;
  blockedReason?: string | null;
}

export interface Geofence {
  id: number;
  name: string;
  areaType: string;
  centerLat: number;
  centerLng: number;
  radiusMeters: number;
  geoJson?: string;
  isActive: boolean;
}

export interface GpsAlertRule {
  id: number;
  vehicleId?: number;
  vehicleName?: string;
  speedLimitKmh?: number;
  geofenceId?: number;
  geofenceName?: string;
  alertOnEnter: boolean;
  alertOnExit: boolean;
  isActive: boolean;
}

export interface GpsAlertEvent {
  id: number;
  ruleId?: number;
  vehicleId: number;
  vehicleName?: string;
  eventType: string;
  latitude: number;
  longitude: number;
  speed: number;
  message: string;
  timestamp: string;
  isAcknowledged: boolean;
}

export interface TripFleetFilters {
  branchId?: number | null;
  departmentId?: number | null;
  driverId?: number | null;
}

export interface GpsTrip {
  vehicleId: number;
  vehicleName?: string;
  gpsDeviceId?: number;
  deviceName?: string;
  startTime: string;
  endTime: string;
  distanceKm: number;
  avgSpeedKmh: number;
  maxSpeedKmh: number;
  durationMinutes: number;
  startAddress?: string | null;
  endAddress?: string | null;
  driverName?: string | null;
  fuelLiters?: number | null;
  plateNumber?: string | null;
  status?: string | null;
}


export interface TripAnalyticsSummary {
  tripCount: number;
  distanceKm: number;
  drivingMinutes: number;
  idleMinutes: number;
  fuelLiters?: number | null;
  avgSpeedKmh: number;
  maxSpeedKmh: number;
  stopCount: number;
  overspeedCount: number;
  harshBrakeCount: number;
  harshAccelCount: number;
  engineHours?: number | null;
}

export interface TripEvent {
  time: string;
  type: string;
  latitude?: number | null;
  longitude?: number | null;
  address?: string | null;
  speedKmh?: number | null;
}

export interface TripStop {
  startTime: string;
  endTime: string;
  latitude: number;
  longitude: number;
  address?: string | null;
  durationMinutes: number;
}

export interface FleetTripStop {
  vehicleId: number;
  vehicleName?: string | null;
  plateNumber?: string | null;
  driverName?: string | null;
  startTime: string;
  endTime: string;
  latitude: number;
  longitude: number;
  address?: string | null;
  durationMinutes: number;
}

export interface FleetTripEvent {
  vehicleId: number;
  vehicleName?: string | null;
  plateNumber?: string | null;
  driverName?: string | null;
  time: string;
  type: string;
  latitude?: number | null;
  longitude?: number | null;
  address?: string | null;
  speedKmh?: number | null;
}

/**
 * Page over a capped-fan-out fleet report (Stops/Events) — totalCount/pagination only cover
 * vehiclesQueried, not the full vehiclesInScope. Surface the gap when vehiclesQueried < vehiclesInScope.
 */
export interface FleetReportPage<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  vehiclesInScope: number;
  vehiclesQueried: number;
}

export interface TripAnalyticsBundle {
  summary: TripAnalyticsSummary;
  events: TripEvent[];
  stops: TripStop[];
}

export interface TripReplayPosition {
  timestamp: string;
  latitude: number;
  longitude: number;
  speedKmh: number;
  heading?: number | null;
  ignition?: boolean | null;
  altitude?: number | null;
  address?: string | null;
  batteryLevel?: number | null;
  satellites?: number | null;
}

export interface TripReplaySummary {
  distanceKm: number;
  drivingMinutes: number;
  avgSpeedKmh: number;
  maxSpeedKmh: number;
  fuelLiters?: number | null;
  engineHours?: number | null;
}

/** Aggregated replay payload from Traccar route + stops + events reports. */
export interface TripReplayBundle {
  route: TripReplayPosition[];
  playback: TripReplayPosition[];
  stops: TripStop[];
  events: TripEvent[];
  summary?: TripReplaySummary | null;
}

export interface GpsFleetStatus {
  totalVehicles: number;
  online: number;
  offline: number;
  moving: number;
  idle: number;
  parked: number;
  neverSeen: number;
  avgSpeedKmh?: number | null;
  todayDistanceKm?: number | null;
}

export interface GpsDashboardTrends {
  online: number;
  moving: number;
  parked: number;
  idle: number;
  offline: number;
  neverSeen: number;
  totalFleet: number;
  alertsToday: number;
}

export interface GpsDashboardSparkline {
  moving: number[];
  parked: number[];
  idle: number[];
  offline: number[];
}

export interface GpsDashboardSummary {
  online: number;
  moving: number;
  parked: number;
  idle: number;
  offline: number;
  neverSeen: number;
  totalFleet: number;
  alertsToday: number;
  trends: GpsDashboardTrends;
  sparkline: GpsDashboardSparkline;
  lastSyncAt: string;
}

export interface TripDeviceContext {
  vehicleId: number;
  vehicleName?: string | null;
  plateNumber?: string | null;
  gpsDeviceId?: number | null;
  deviceName?: string | null;
  uniqueId?: string | null;
  hasTraccarLink: boolean;
  isOnline: boolean;
  lastPositionAt?: string | null;
  lastLatitude?: number | null;
  lastLongitude?: number | null;
  lastAddress?: string | null;
  lastSpeedKmh?: number | null;
  lastIgnition?: boolean | null;
}

export interface TripVehicleOption {
  vehicleId: number;
  vehicleName: string;
  plateNumber: string;
  deviceName: string;
  uniqueId: string;
  isOnline: boolean;
}

export interface GpsDeviceCommand {
  id: number;
  gpsDeviceId: number;
  deviceName?: string;
  commandType: string;
  status: string;
  reason?: string;
  requestedBy?: string;
  requestedAt: string;
  completedAt?: string;
}

export interface GpsEta {
  bookingId: number;
  vehicleId: number;
  vehicleName?: string;
  distanceKm: number;
  etaMinutes?: number;
  driverLatitude: number;
  driverLongitude: number;
  pickupLatitude: number;
  pickupLongitude: number;
}

export interface IngestPositionPayload {
  vehicleId: number;
  driverId?: number;
  bookingId?: number;
  gpsDeviceId?: number;
  latitude: number;
  longitude: number;
  speed: number;
  heading?: number;
  altitude?: number;
  ignition?: boolean;
}

/** @deprecated Use PositionDto — kept for live-map compatibility */
export type TrackingDto = PositionDto;

// ── Traccar admin types ───────────────────────────────────────────────────────

export interface TraccarStatusDto {
  connected: boolean;
  serverVersion?: string | null;
  deviceCount: number;
  lastError?: string | null;
}

export interface TraccarDeviceDto {
  id: number;
  name: string;
  uniqueId: string;
  status: string;
  category?: string;
  phone?: string;
  model?: string;
  disabled: boolean;
  lastUpdate?: string;
}

export interface TraccarSyncResultDto {
  imported: number;
  updated: number;
  skipped: number;
}

export interface TraccarSyncJobResult {
  job: string;
  processed: number;
  imported: number;
  updated: number;
  skipped: number;
  error?: string | null;
}

export interface TraccarSyncRunResult {
  completedAt: string;
  jobs: TraccarSyncJobResult[];
}

export interface TraccarSyncStatusDto {
  enabled: boolean;
  connected: boolean;
  isRunning: boolean;
  lastPositionSyncAt?: string | null;
  lastDeviceSyncAt?: string | null;
  lastEventSyncAt?: string | null;
  lastSyncCompletedAt?: string | null;
  lastError?: string | null;
  positionSyncIntervalSeconds?: number;
}
