import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, forkJoin, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { VehicleLocation, TrackingUpdate, TrackingDto } from '../models/tracking.model';
import { VehicleService } from './vehicle.service';

@Injectable({ providedIn: 'root' })
export class TrackingService {
  private readonly base = `${environment.apiUrl}/tracking`;

  constructor(
    private http: HttpClient,
    private vehicleService: VehicleService
  ) {}

  /**
   * Gets live tracking data and enriches with vehicle names.
   * Backend returns TrackingDto[], we convert to VehicleLocation[].
   */
  getAllVehicleLocations(): Observable<VehicleLocation[]> {
    return forkJoin({
      tracking: this.http.get<TrackingDto[]>(`${this.base}/live`),
      vehicles: this.vehicleService.getAll(1, 500).pipe(catchError(() => of({ items: [] })))
    }).pipe(
      map(({ tracking, vehicles }) => {
        const vehicleMap = new Map(vehicles.items.map(v => [v.id, v]));
        return tracking.map(t => {
          const vehicle = vehicleMap.get(t.vehicleId);
          return {
            vehicleId: t.vehicleId,
            vehicleName: vehicle?.name ?? `Vehicle #${t.vehicleId}`,
            registrationNumber: vehicle?.registrationNumber ?? '',
            latitude: t.latitude,
            longitude: t.longitude,
            lastUpdated: t.timestamp
          };
        });
      })
    );
  }

  /**
   * Updates location for a vehicle.
   * Backend expects { location: UpdateLocationDto }.
   */
  updateLocation(update: TrackingUpdate): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/location`, {
      location: {
        vehicleId: update.vehicleId,
        driverId: null,
        bookingId: null,
        latitude: update.latitude,
        longitude: update.longitude,
        speed: update.speed ?? 0
      }
    });
  }

  /**
   * Gets tracking history for a specific vehicle.
   */
  getHistory(vehicleId: number, from?: Date, to?: Date): Observable<TrackingDto[]> {
    const params: Record<string, string> = {};
    if (from) params['from'] = from.toISOString();
    if (to) params['to'] = to.toISOString();
    return this.http.get<TrackingDto[]>(`${this.base}/history/${vehicleId}`, { params });
  }
}
