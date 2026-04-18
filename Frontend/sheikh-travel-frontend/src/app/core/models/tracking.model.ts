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
