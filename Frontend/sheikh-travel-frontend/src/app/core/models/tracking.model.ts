export interface TrackingUpdate {
  vehicleId: number;
  latitude: number;
  longitude: number;
  speed?: number;
  notes?: string;
}

export interface TrackingRecord {
  id: number;
  vehicleId: number;
  vehicleName: string;
  latitude: number;
  longitude: number;
  speed?: number;
  recordedAt: string;
}

export interface VehicleLocation {
  vehicleId: number;
  vehicleName: string;
  registrationNumber: string;
  latitude: number;
  longitude: number;
  lastUpdated: string;
}

/** DTO returned by backend /tracking/live endpoint */
export interface TrackingDto {
  id: number;
  vehicleId: number;
  driverId?: number;
  bookingId?: number;
  latitude: number;
  longitude: number;
  speed: number;
  timestamp: string;
}
