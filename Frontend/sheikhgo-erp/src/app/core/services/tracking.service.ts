import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { GpsTrackingService } from './gps-tracking.service';
import { TrackingUpdate } from '../models/tracking.model';

/** @deprecated Use GpsTrackingService — kept for backward compatibility */
@Injectable({ providedIn: 'root' })
export class TrackingService {
  constructor(private gps: GpsTrackingService) {}

  getAllVehicleLocations() {
    return this.gps.getAllVehicleLocations();
  }

  updateLocation(update: TrackingUpdate): Observable<boolean> {
    return this.gps.ingestPosition({
      vehicleId: update.vehicleId,
      latitude: update.latitude,
      longitude: update.longitude,
      speed: update.speed ?? 0
    });
  }

  getHistory(vehicleId: number, from?: Date, to?: Date) {
    return this.gps.getHistory(vehicleId, from, to);
  }
}
