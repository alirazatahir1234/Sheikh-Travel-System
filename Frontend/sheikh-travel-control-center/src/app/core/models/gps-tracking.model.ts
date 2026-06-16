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
  routeHint?: string;
  ignition?: boolean;
}

export interface GpsDevice {
  id: number;
  vehicleId?: number;
  vehicleName?: string;
  uniqueId: string;
  name: string;
  protocol?: string;
  supportsEngineCutoff: boolean;
  lastIgnition?: boolean;
  lastSeenAt?: string;
  isActive: boolean;
  model?: string;
  simNumber?: string;
  vendor?: string;
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
