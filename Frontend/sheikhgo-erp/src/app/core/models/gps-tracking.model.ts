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
  driverPhone?: string;
  temperature?: number;
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
  /** Nearest shop / landmark from reverse geocode (client-enriched). */
  placeName?: string;
  placeType?: string;
  alarmType?: string;
  vehicleType?: string | null;
  trackerName?: string;
  imei?: string;
  relayOutput?: string;
  driverPhone?: string;
  temperature?: number;
  /** Active booking this vehicle is currently on, if any — drives the Trip/ETA panel section. */
  bookingId?: number;
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
  supportsRelay?: boolean;
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
  geoJson?: string | null;
  isActive: boolean;
  color?: string | null;
  category?: string | null;
  description?: string | null;
  assignedVehicleCount?: number;
}

export interface GeofenceAssignment {
  id: number;
  geofenceId: number;
  vehicleId?: number | null;
  vehicleName?: string | null;
  registrationNumber?: string | null;
  branchId?: number | null;
  branchName?: string | null;
  departmentId?: number | null;
  departmentName?: string | null;
}

export interface GeofenceStats {
  total: number;
  active: number;
  vehiclesInside: number;
  todayEntries: number;
  todayExits: number;
  unackedBreaches: number;
}

export interface UpsertGeofenceAssignments {
  vehicleIds?: number[];
  branchId?: number | null;
  departmentId?: number | null;
  replaceVehicles?: boolean;
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
  geofenceId?: number | null;
  geofenceName?: string | null;
  severity?: string;
  status?: string;
  driverId?: number | null;
  driverName?: string | null;
  readAt?: string | null;
  readBy?: string | null;
  acknowledgedAt?: string | null;
  acknowledgedBy?: string | null;
  resolvedAt?: string | null;
  resolvedBy?: string | null;
  resolutionNotes?: string | null;
  archivedAt?: string | null;
  archivedBy?: string | null;
  canAcknowledge?: boolean;
  canResolve?: boolean;
  canArchive?: boolean;
  canDelete?: boolean;
}

export interface GpsAlertStats {
  total: number;
  today: number;
  unread: number;
  active: number;
  critical: number;
  resolved: number;
  archived: number;
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
  tripKey?: string | null;
  status?: string | null;
}

export interface TripDetailBundle {
  trip: GpsTrip;
  summary?: TripReplaySummary | null;
  stops: TripStop[];
  events: TripEvent[];
  route: TripReplayPosition[];
  playback: TripReplayPosition[];
}

export interface TripListQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
  minDistanceKm?: number;
  maxDistanceKm?: number;
  minAvgSpeedKmh?: number;
  maxAvgSpeedKmh?: number;
  status?: string;
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
  geofenceId?: number | null;
  geofenceName?: string | null;
  label?: string | null;
}

export interface TripStop {
  startTime: string;
  endTime: string;
  latitude: number;
  longitude: number;
  address?: string | null;
  durationMinutes: number;
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
  totalDistanceKm?: number | null;
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

/** Unified history replay bundle (route, stats, vehicle context). */
export interface HistoryReplayBundle {
  route: TripReplayPosition[];
  playback: TripReplayPosition[];
  stops: TripStop[];
  events: TripEvent[];
  summary?: TripReplaySummary | null;
  statistics?: TripAnalyticsSummary | null;
  mileageKm?: number | null;
  vehicle?: TripDeviceContext | null;
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

/**
 * Local-data-sourced fleet snapshot (Vehicles + VehicleCurrentLocation) — what the Live Map KPI
 * strip actually uses. NOT the same source as GpsFleetStatus (which calls Traccar directly) — do
 * not conflate the two.
 */
export interface GpsFleetStatusLocal {
  totalVehicles: number;
  online: number;
  offline: number;
  moving: number;
  idle: number;
  parked: number;
  neverSeen: number;
  sos: number;
  alertsToday: number;
}

export interface GpsFleetStatusSnapshot {
  snapshotAt: string;
  totalVehicles: number;
  online: number;
  offline: number;
  moving: number;
  idle: number;
  parked: number;
  neverSeen: number;
  sos: number;
  alertsToday: number;
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

export type CommandType =
  | 'engineStop' | 'engineResume' | 'positionSingle' | 'restart'
  | 'relayOn' | 'relayOff' | 'buzzer' | 'customSms' | 'custom'
  | 'status' | 'version' | 'iccid' | 'imsi' | 'param' | 'battery' | 'signal'
  | 'apn' | 'server' | 'heartbeat' | 'timezone';

export type CommandStatus =
  | 'pending' | 'sent' | 'acknowledged' | 'failed' | 'timeout' | 'cancelled' | 'not_configured' | 'PendingApproval';

export interface GpsCommandLibraryItem {
  commandKey: string;
  displayName: string;
  category: string;
  description?: string | null;
  requiredCapabilityKey?: string | null;
  dangerLevel: string;
  requiresApproval: boolean;
  requiresReason: boolean;
  sortOrder: number;
  isActive: boolean;
}

export interface GpsCommandLibraryParam {
  id: number;
  commandKey: string;
  paramKey: string;
  displayName: string;
  dataType: string;
  isRequired: boolean;
  defaultValue?: string | null;
  minValue?: number | null;
  maxValue?: number | null;
  sortOrder: number;
}

export interface GpsDeviceCommand {
  id: number;
  gpsDeviceId: number;
  deviceName?: string;
  commandType: CommandType | string;
  status: CommandStatus | string;
  reason?: string;
  requestedBy?: string;
  requestedAt: string;
  completedAt?: string;
  retryCount: number;
  maxRetries: number;
  errorMessage?: string;
  nextRetryAt?: string;
}

export interface GpsCommandResponse {
  id: number;
  source: string;
  responseCode?: string;
  responseText?: string;
  receivedAt: string;
}

export interface GpsDeviceCommandDetail {
  command: GpsDeviceCommand;
  attributesJson?: string;
  responses: GpsCommandResponse[];
}

export interface SupportedCommand {
  type: string;
  label: string;
  available: boolean;
  reason?: string;
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
  /** False when Traccar:Enabled=false — HTTP may still work but positions are not polled. */
  syncEnabled?: boolean;
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
  adaptivePositionSync?: boolean;
  adaptiveIntervalReason?: string | null;
  effectivePositionSyncIntervalSeconds?: number;
}

export interface AnalyticsFilters {
  from?: string;
  to?: string;
  branchId?: number;
  departmentId?: number;
  driverId?: number;
}

export interface AnalyticsOverview {
  totalVehicles: number;
  online: number;
  offline: number;
  moving: number;
  idle: number;
  stopped: number;
  distanceKm: number;
  drivingMinutes: number;
  idleMinutes: number;
  avgSpeedKmh: number;
  maxSpeedKmh: number;
  fuelLiters?: number;
  engineHours?: number;
  tripsToday: number;
  stopsToday: number;
  geofenceEntriesToday: number;
  overspeedEventsToday: number;
  utilizationPercent?: number;
}

export interface DailyMetric {
  date: string;
  distanceKm: number;
  tripCount: number;
  avgSpeedKmh: number;
}

export interface DistanceAnalytics {
  daily: DailyMetric[];
  totalDistanceKm: number;
  totalTrips: number;
}

export interface SpeedHistogramBucket {
  label: string;
  count: number;
}

export interface SpeedAnalytics {
  avgSpeedKmh: number;
  maxSpeedKmh: number;
  histogram: SpeedHistogramBucket[];
  dailyAvgSpeed: DailyMetric[];
}

export interface VehicleIdle {
  vehicleId: number;
  vehicleName?: string;
  idleMinutes: number;
}

export interface IdleAnalytics {
  totalIdleMinutes: number;
  longestIdleMinutes: number;
  topIdleVehicles: VehicleIdle[];
  isPartial: boolean;
}

export interface StopAnalytics {
  totalStops: number;
  avgStopDurationMinutes: number;
  longestStopMinutes: number;
  isPartial: boolean;
}

export interface DriverScoreFactors {
  tripCount: number;
  distanceKm: number;
  overspeedCount: number;
  idleMinutes: number;
  harshEventCount: number;
  nightDrivingPercent: number;
  fuelLiters?: number;
}

export interface DriverScore {
  driverId: number;
  driverName: string;
  score: number;
  rating: string;
  factors: DriverScoreFactors;
  isPartial: boolean;
}

export interface DailyUtilization {
  date: string;
  utilizationPercent: number;
}

export interface FleetUtilization {
  runningHours: number;
  idleHours: number;
  parkingHours: number;
  offlineHours: number;
  utilizationPercent: number;
  utilizationLabel: string;
  daily: DailyUtilization[];
}

export interface DailyFuel {
  date: string;
  liters: number;
  cost: number;
}

export interface VehicleFuel {
  vehicleId: number;
  vehicleName?: string;
  liters: number;
  cost: number;
  distanceKm?: number;
  litersPer100Km?: number;
}

export interface FuelAnalytics {
  totalLiters: number;
  totalCost: number;
  fleetLitersPer100Km?: number;
  daily: DailyFuel[];
  byVehicle: VehicleFuel[];
}

export interface GeofenceVisit {
  geofenceId: number;
  geofenceName: string;
  entryCount: number;
  exitCount: number;
  avgDwellMinutes?: number;
}

export interface GeofenceAnalytics {
  mostVisited: GeofenceVisit[];
  leastVisited: GeofenceVisit[];
  totalEntries: number;
  totalExits: number;
}

export interface EventTypeCount {
  key: string;
  count: number;
}

export interface DailyEventCount {
  date: string;
  count: number;
}

export interface AlertEventStats {
  byType: EventTypeCount[];
  bySeverity: EventTypeCount[];
  daily: DailyEventCount[];
  total: number;
}

export interface HeatmapPoint {
  latitude: number;
  longitude: number;
  count: number;
}

export interface GpsVehicleHealth {
  vehicleId: number;
  vehicleName: string;
  batteryLevel?: number;
  gpsSignal?: number;
  maintenanceStatus: string;
  insuranceExpiryDate?: string;
  insuranceStatus: string;
  trackerWarrantyEnd?: string;
  trackerWarrantyStatus: string;
}

export interface VehicleRanking {
  vehicleId: number;
  vehicleName: string;
  distanceKm: number;
  tripCount: number;
  avgSpeedKmh: number;
  fuelCost: number;
  maintenanceCost: number;
  costPerKm?: number;
}

export interface CostAnalytics {
  totalFuelCost: number;
  totalMaintenanceCost: number;
  totalCost: number;
  totalDistanceKm: number;
  costPerKm?: number;
  costBasisNote: string;
}

export interface TrendPoint {
  period: string;
  distanceKm: number;
  tripCount: number;
  overspeedCount: number;
}

export interface Trends {
  points: TrendPoint[];
}

export interface ComparativePeriod {
  label: string;
  distanceKm: number;
  tripCount: number;
  avgSpeedKmh: number;
  overspeedCount: number;
}

export interface ComparativeAnalytics {
  periodA: ComparativePeriod;
  periodB?: ComparativePeriod;
}

export interface AnalyticsReportFilters {
  branchId?: number | null;
  departmentId?: number | null;
  from?: string | null;
  to?: string | null;
}

export interface AnalyticsReportSchedule {
  id: number;
  reportType: string;
  filters: AnalyticsReportFilters;
  frequency: string;
  recipients: string;
  nextRunAt?: string | null;
  lastRunAt?: string | null;
  lastRunStatus?: string | null;
  isActive: boolean;
}

export interface CreateAnalyticsReportSchedule {
  reportType: string;
  filters: AnalyticsReportFilters;
  frequency: string;
  recipients: string;
}

export interface UpdateAnalyticsReportSchedule {
  frequency?: string;
  recipients?: string;
  isActive?: boolean;
  filters?: AnalyticsReportFilters;
}
