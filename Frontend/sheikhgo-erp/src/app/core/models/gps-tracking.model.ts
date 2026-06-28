export type FleetTrackStatus = 'moving' | 'idle' | 'offline' | 'scheduled' | 'delayed';

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
}

export interface GpsDevice {
  id: number;
  vehicleId?: number;
  vehicleName?: string;
  plateNumber?: string;
  driverName?: string;
  uniqueId: string;
  name: string;
  protocol?: string;
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
  model?: string;
  simNumber?: string;
  vendor?: string;
  serialNumber?: string;
  installationDate?: string;
  installedBy?: string;
  installationNotes?: string;
  relayOutput?: string;
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

export interface GpsTrip {
  vehicleId: number;
  vehicleName?: string;
  gpsDeviceId?: number;
  startTime: string;
  endTime: string;
  distanceKm: number;
  avgSpeedKmh: number;
  maxSpeedKmh: number;
  durationMinutes: number;
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
