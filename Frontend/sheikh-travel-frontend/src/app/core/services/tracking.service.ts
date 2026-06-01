import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, forkJoin, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  VehicleLocation,
  TrackingUpdate,
  TrackingDto,
  FleetTrackStatus
} from '../models/tracking.model';
import { VehicleService } from './vehicle.service';
import { DriverService } from './driver.service';
import { VehicleStatus } from '../models/vehicle.model';

const STALE_MS = 30 * 60 * 1000;
const DELAYED_MS = 15 * 60 * 1000;

@Injectable({ providedIn: 'root' })
export class TrackingService {
  private readonly base = `${environment.apiUrl}/tracking`;

  constructor(
    private http: HttpClient,
    private vehicleService: VehicleService,
    private driverService: DriverService
  ) {}

  /**
   * Live GPS rows enriched with vehicle + driver names and derived status.
   */
  getAllVehicleLocations(): Observable<VehicleLocation[]> {
    return forkJoin({
      tracking: this.http.get<TrackingDto[]>(`${this.base}/live`).pipe(catchError(() => of([]))),
      vehicles: this.vehicleService.getAll(1, 500).pipe(catchError(() => of({ items: [] }))),
      drivers: this.driverService.getAll(1, 500).pipe(catchError(() => of({ items: [] })))
    }).pipe(
      map(({ tracking, vehicles, drivers }) => {
        const vehicleMap = new Map(vehicles.items.map(v => [v.id, v]));
        const driverMap = new Map(drivers.items.map(d => [d.id, d.fullName]));
        const liveIds = new Set(tracking.map(t => t.vehicleId));

        const live: VehicleLocation[] = tracking.map(t => {
          const vehicle = vehicleMap.get(t.vehicleId);
          const status = this.deriveStatus(t.speed, t.timestamp);
          return {
            vehicleId: t.vehicleId,
            vehicleName: vehicle?.name ?? `Vehicle #${t.vehicleId}`,
            registrationNumber: vehicle?.registrationNumber ?? '',
            latitude: t.latitude,
            longitude: t.longitude,
            lastUpdated: t.timestamp,
            speed: Number(t.speed) || 0,
            status,
            driverName: t.driverId ? driverMap.get(t.driverId) : undefined,
            hasGps: true,
            routeHint: this.routeHint(status, Number(t.speed) || 0)
          };
        });

        const offline: VehicleLocation[] = vehicles.items
          .filter(v => !liveIds.has(v.id) && v.status !== VehicleStatus.Retired)
          .map(v => ({
            vehicleId: v.id,
            vehicleName: v.name,
            registrationNumber: v.registrationNumber,
            latitude: 0,
            longitude: 0,
            lastUpdated: '',
            speed: 0,
            status: (v.status === VehicleStatus.OnTrip ? 'scheduled' : 'offline') as FleetTrackStatus,
            hasGps: false,
            routeHint: v.status === VehicleStatus.OnTrip ? 'Scheduled trip' : 'No GPS signal'
          }));

        return [...live, ...offline];
      })
    );
  }

  private deriveStatus(speed: number, timestamp: string): FleetTrackStatus {
    const age = Date.now() - new Date(timestamp).getTime();
    if (age > STALE_MS) return 'offline';
    if (age > DELAYED_MS && speed <= 2) return 'delayed';
    if (speed > 5) return 'moving';
    if (speed > 0) return 'idle';
    return 'idle';
  }

  private routeHint(status: FleetTrackStatus, speed: number): string {
    if (status === 'moving') return `${Math.round(speed)} km/h`;
    if (status === 'idle') return 'Idle • awaiting movement';
    if (status === 'delayed') return 'Delayed • check route';
    return 'Last known position';
  }

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

  getHistory(vehicleId: number, from?: Date, to?: Date): Observable<TrackingDto[]> {
    const params: Record<string, string> = {};
    if (from) params['from'] = from.toISOString();
    if (to) params['to'] = to.toISOString();
    return this.http.get<TrackingDto[]>(`${this.base}/history/${vehicleId}`, { params });
  }
}
