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
  TrackerDetail,
  RegisterTrackerPayload,
  TrackerRegisteredResult,
  Geofence,
  GpsAlertRule,
  GpsAlertEvent,
  GpsTrip,
  GpsDeviceCommand,
  GpsEta,
  IngestPositionPayload,
  TraccarStatusDto,
  TraccarDeviceDto,
  TraccarSyncResultDto,
  TraccarSyncRunResult,
  TraccarSyncStatusDto
} from '../models/gps-tracking.model';
import { VehicleService } from './vehicle.service';
import { DriverService } from './driver.service';
import { VehicleStatus } from '../models/vehicle.model';

const STALE_MS = 30 * 60 * 1000;
const DELAYED_MS = 15 * 60 * 1000;
const RECENT_MS = STALE_MS;

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
        const liveByVehicle = new Map(tracking.map(t => [t.vehicleId, t]));

        const result: VehicleLocation[] = vehicles.items
          .filter(v => v.status !== VehicleStatus.Retired)
          .map(v => {
            const live = liveByVehicle.get(v.id);
            if (live && this.hasValidCoords(live.latitude, live.longitude)) {
              const status = this.deriveStatus(live.speed, live.timestamp);
              return {
                vehicleId: v.id,
                vehicleName: v.name ?? `Vehicle #${v.id}`,
                registrationNumber: v.registrationNumber ?? '',
                latitude: this.toCoord(live.latitude)!,
                longitude: this.toCoord(live.longitude)!,
                lastUpdated: live.timestamp,
                speed: Number(live.speed) || 0,
                status,
                driverName: live.driverId ? driverMap.get(live.driverId) : v.driverName ?? undefined,
                hasGps: true,
                isLive: this.isRecentTelemetry(live.timestamp),
                routeHint: this.routeHint(status, Number(live.speed) || 0),
                ignition: live.ignition
              };
            }

            const lat = this.toCoord(v.locationLatitude);
            const lng = this.toCoord(v.locationLongitude);
            if (this.hasValidCoords(lat, lng)) {
              const lastUpdated = v.locationLastUpdate ?? '';
              const status = lastUpdated
                ? this.deriveStatus(0, lastUpdated)
                : ('offline' as FleetTrackStatus);
              return {
                vehicleId: v.id,
                vehicleName: v.name ?? `Vehicle #${v.id}`,
                registrationNumber: v.registrationNumber ?? '',
                latitude: lat!,
                longitude: lng!,
                lastUpdated,
                speed: 0,
                status,
                driverName: v.driverName ?? undefined,
                hasGps: true,
                isLive: this.isRecentTelemetry(lastUpdated),
                routeHint: status === 'offline' ? 'Last known position' : this.routeHint(status, 0),
                ignition: v.engineIgnition ?? undefined
              };
            }

            return {
              vehicleId: v.id,
              vehicleName: v.name ?? `Vehicle #${v.id}`,
              registrationNumber: v.registrationNumber ?? '',
              latitude: 0,
              longitude: 0,
              lastUpdated: '',
              speed: 0,
              status: (v.status === VehicleStatus.OnTrip ? 'scheduled' : 'offline') as FleetTrackStatus,
              driverName: v.driverName ?? undefined,
              hasGps: false,
              routeHint: v.hasGpsDevice ? 'Awaiting GPS signal' : 'No GPS signal'
            };
          });

        return result;
      })
    );
  }

  private hasValidCoords(lat?: number | null, lng?: number | null): boolean {
    if (lat == null || lng == null) return false;
    if (!Number.isFinite(lat) || !Number.isFinite(lng)) return false;
    return !(lat === 0 && lng === 0);
  }

  private toCoord(value: unknown): number | null {
    const n = Number(value);
    return Number.isFinite(n) ? n : null;
  }

  private isRecentTelemetry(timestamp: string): boolean {
    if (!timestamp) return false;
    const age = Date.now() - new Date(timestamp).getTime();
    return Number.isFinite(age) && age >= 0 && age <= RECENT_MS;
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
    return this.http.get<TrackerDetail[]>(`${this.base}/trackers`).pipe(
      map(list => list as GpsDevice[])
    );
  }

  getTracker(id: number): Observable<TrackerDetail> {
    return this.http.get<TrackerDetail>(`${this.base}/trackers/${id}`);
  }

  registerTracker(body: RegisterTrackerPayload): Observable<TrackerRegisteredResult> {
    return this.http.post<TrackerRegisteredResult>(`${this.base}/trackers/register`, body);
  }

  updateTracker(id: number, body: Partial<RegisterTrackerPayload> & { isActive?: boolean }): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/trackers/${id}`, body);
  }

  deleteTracker(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/trackers/${id}`);
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

  sendCommand(gpsDeviceId: number, commandType: string, reason?: string): Observable<number> {
    return this.http.post<number>(`${this.base}/commands/send`, { gpsDeviceId, commandType, reason });
  }

  getCommands(deviceId: number): Observable<GpsDeviceCommand[]> {
    return this.http.get<GpsDeviceCommand[]>(`${this.base}/commands/${deviceId}`);
  }

  getEta(bookingId: number): Observable<GpsEta> {
    return this.http.get<GpsEta>(`${this.base}/eta`, { params: { bookingId: String(bookingId) } });
  }

  // ── Traccar admin ───────────────────────────────────────────────────────────

  getTraccarStatus(): Observable<TraccarStatusDto> {
    return this.http.get<TraccarStatusDto>(`${this.base}/traccar/status`);
  }

  getTraccarDevices(): Observable<TraccarDeviceDto[]> {
    return this.http.get<TraccarDeviceDto[]>(`${this.base}/traccar/devices`);
  }

  syncTraccarDevices(): Observable<TraccarSyncResultDto> {
    return this.http.post<TraccarSyncResultDto>(`${this.base}/traccar/sync-devices`, {});
  }

  runTraccarSync(): Observable<TraccarSyncRunResult> {
    return this.http.post<TraccarSyncRunResult>(`${this.base}/traccar/sync`, {});
  }

  getTraccarSyncStatus(): Observable<TraccarSyncStatusDto> {
    return this.http.get<TraccarSyncStatusDto>(`${this.base}/traccar/sync-status`);
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
