import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, forkJoin, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  VehicleLocation,
  PositionDto,
  FleetTrackStatus,
  GpsDevice,
  Geofence,
  GpsAlertRule,
  GpsAlertEvent,
  GpsTrip,
  GpsDeviceCommand,
  GpsEta,
  IngestPositionPayload
} from '../models/gps-tracking.model';
import { VehicleService } from './vehicle.service';
import { DriverService } from './driver.service';
import { VehicleStatus } from '../models/vehicle.model';

const STALE_MS = 30 * 60 * 1000;
const DELAYED_MS = 15 * 60 * 1000;

@Injectable({ providedIn: 'root' })
export class GpsTrackingService {
  private readonly base = `${environment.apiUrl}/gps`;

  constructor(
    private http: HttpClient,
    private vehicleService: VehicleService,
    private driverService: DriverService
  ) {}

  getAllVehicleLocations(): Observable<VehicleLocation[]> {
    return forkJoin({
      tracking: this.http.get<PositionDto[]>(`${this.base}/live`).pipe(catchError(() => of([]))),
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
            routeHint: this.routeHint(status, Number(t.speed) || 0),
            ignition: t.ignition
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

  ingestPosition(payload: IngestPositionPayload): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/positions`, payload);
  }

  getHistory(vehicleId: number, from?: Date, to?: Date): Observable<PositionDto[]> {
    const params: Record<string, string> = {};
    if (from) params['from'] = from.toISOString();
    if (to) params['to'] = to.toISOString();
    return this.http.get<PositionDto[]>(`${this.base}/history/${vehicleId}`, { params });
  }

  getTrips(vehicleId?: number, from?: Date, to?: Date): Observable<GpsTrip[]> {
    const params: Record<string, string> = {};
    if (vehicleId) params['vehicleId'] = String(vehicleId);
    if (from) params['from'] = from.toISOString();
    if (to) params['to'] = to.toISOString();
    return this.http.get<GpsTrip[]>(`${this.base}/trips`, { params });
  }

  getGeofences(): Observable<Geofence[]> {
    return this.http.get<Geofence[]>(`${this.base}/geofences`);
  }

  createGeofence(body: Partial<Geofence>): Observable<number> {
    return this.http.post<number>(`${this.base}/geofences`, body);
  }

  updateGeofence(id: number, body: Partial<Geofence>): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/geofences/${id}`, body);
  }

  deleteGeofence(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/geofences/${id}`);
  }

  getAlertRules(): Observable<GpsAlertRule[]> {
    return this.http.get<GpsAlertRule[]>(`${this.base}/alerts/rules`);
  }

  createAlertRule(body: Partial<GpsAlertRule>): Observable<number> {
    return this.http.post<number>(`${this.base}/alerts/rules`, body);
  }

  getAlertEvents(vehicleId?: number, unacknowledgedOnly?: boolean): Observable<GpsAlertEvent[]> {
    const params: Record<string, string> = {};
    if (vehicleId) params['vehicleId'] = String(vehicleId);
    if (unacknowledgedOnly) params['unacknowledgedOnly'] = 'true';
    return this.http.get<GpsAlertEvent[]>(`${this.base}/alerts/events`, { params });
  }

  acknowledgeAlert(id: number): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/alerts/events/${id}/acknowledge`, {});
  }

  getGeofenceBreachCount(): Observable<number> {
    return this.http.get<number>(`${this.base}/alerts/geofence-breaches/count`);
  }

  getDevices(): Observable<GpsDevice[]> {
    return this.http.get<GpsDevice[]>(`${this.base}/devices`);
  }

  createDevice(body: Partial<GpsDevice>): Observable<number> {
    return this.http.post<number>(`${this.base}/devices`, body);
  }

  updateDevice(id: number, body: Partial<GpsDevice>): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/devices/${id}`, body);
  }

  deleteDevice(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/devices/${id}`);
  }

  sendCommand(gpsDeviceId: number, commandType: string): Observable<number> {
    return this.http.post<number>(`${this.base}/commands/send`, { gpsDeviceId, commandType });
  }

  getCommands(deviceId: number): Observable<GpsDeviceCommand[]> {
    return this.http.get<GpsDeviceCommand[]>(`${this.base}/commands/${deviceId}`);
  }

  getEta(bookingId: number): Observable<GpsEta> {
    return this.http.get<GpsEta>(`${this.base}/eta`, { params: { bookingId: String(bookingId) } });
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
}
