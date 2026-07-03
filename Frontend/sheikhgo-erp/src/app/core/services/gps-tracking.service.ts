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
  InstallTrackerPayload,
  TransferTrackerPayload,
  UninstallTrackerPayload,
  TrackerAssignment,
  TrackerInstallVehicle,
  TrackerRegisteredResult,
  Geofence,
  GpsAlertRule,
  GpsAlertEvent,
  GpsTrip,
  TripAnalyticsBundle,
  TripAnalyticsSummary,
  TripFleetFilters,
  TripReplayBundle,
  GpsFleetStatus,
  GpsFleetStatusLocal,
  GpsFleetStatusSnapshot,
  TripDeviceContext,
  GpsDeviceCommand,
  GpsEta,
  IngestPositionPayload,
  TraccarStatusDto,
  TraccarDeviceDto,
  TraccarSyncResultDto,
  TraccarSyncRunResult,
  TraccarSyncStatusDto
} from '../models/gps-tracking.model';
import { PagedResult } from '../models/common.model';
import { VehicleService } from './vehicle.service';
import { DriverService } from './driver.service';
import { VehicleStatus } from '../models/vehicle.model';
import { resolveFleetStatus } from '../utils/gps-status.util';

const STALE_MS = 30 * 60 * 1000;
const RECENT_MS = STALE_MS;
// Single generously-sized page rather than a paged UI: the live map re-fetches on every refresh
// tick, and the vehicle list/marker layer already virtual-scrolls/clusters past this point.
// Raise if the fleet genuinely exceeds this — the backend endpoints are unbounded past here.
const LIVE_FLEET_PAGE_SIZE = 2000;

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
      tracking: this.http
        .get<PagedResult<PositionDto>>(`${this.base}/live`, { params: { pageSize: LIVE_FLEET_PAGE_SIZE } })
        .pipe(map(r => r.items), catchError(() => of([] as PositionDto[]))),
      vehicles: this.vehicleService.getAll(1, LIVE_FLEET_PAGE_SIZE).pipe(catchError(() => of({ items: [] }))),
      drivers: this.driverService.getAll(1, LIVE_FLEET_PAGE_SIZE).pipe(catchError(() => of({ items: [] }))),
      devices: this.getDevices().pipe(catchError(() => of([])))
    }).pipe(
      map(({ tracking, vehicles, drivers, devices }) => {
        const vehicleMap = new Map(vehicles.items.map(v => [v.id, v]));
        const driverMap = new Map(drivers.items.map(d => [d.id, d.fullName]));
        const liveByVehicle = new Map(tracking.map(t => [t.vehicleId, t]));
        const deviceByVehicle = new Map(
          devices.filter(d => d.vehicleId != null).map(d => [d.vehicleId as number, d])
        );

        const result: VehicleLocation[] = vehicles.items
          .filter(v => v.status !== VehicleStatus.Retired)
          .map(v => {
            const tracker = deviceByVehicle.get(v.id);
            const trackerFields = {
              imei: tracker?.uniqueId,
              trackerName: tracker?.name,
              relayOutput: tracker?.relayOutput
            };

            const live = liveByVehicle.get(v.id);
            if (live && this.hasValidCoords(live.latitude, live.longitude)) {
              const status = resolveFleetStatus({
                speed: Number(live.speed) || 0,
                ignition: live.ignition,
                lastUpdated: live.timestamp,
                hasGps: true,
                alarmType: live.alarmType
              });
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
                ignition: live.ignition,
                heading: live.heading,
                fuelLevel: live.fuelLevel,
                batteryLevel: live.batteryLevel,
                gsmSignal: live.gsmSignal,
                totalDistanceKm: live.totalDistanceKm,
                address: live.address,
                alarmType: live.alarmType,
                vehicleType: v.vehicleType,
                driverPhone: live.driverPhone,
                bookingId: live.bookingId,
                ...trackerFields
              };
            }

            const lat = this.toCoord(v.locationLatitude);
            const lng = this.toCoord(v.locationLongitude);
            if (this.hasValidCoords(lat, lng)) {
              const lastUpdated = v.locationLastUpdate ?? '';
              // A snapshot position exists, so this vehicle has a marker — never fall through to
              // resolveFleetStatus's "never_seen" (that's reserved for vehicles with no fix at all).
              const status: FleetTrackStatus = lastUpdated
                ? resolveFleetStatus({ speed: 0, ignition: v.engineIgnition, lastUpdated, hasGps: true })
                : 'offline';
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
                ignition: v.engineIgnition ?? undefined,
                vehicleType: v.vehicleType,
                ...trackerFields
              };
            }

            // No live cache row and no last-known snapshot: a tracker that's assigned but has
            // never reported a fix is "Never Seen" (appears in the grid, no map marker); a
            // vehicle with no tracker at all keeps the previous scheduled/offline hint.
            const status: FleetTrackStatus = v.hasGpsDevice
              ? 'never_seen'
              : ((v.status === VehicleStatus.OnTrip ? 'scheduled' : 'offline') as FleetTrackStatus);

            return {
              vehicleId: v.id,
              vehicleName: v.name ?? `Vehicle #${v.id}`,
              registrationNumber: v.registrationNumber ?? '',
              latitude: 0,
              longitude: 0,
              lastUpdated: '',
              speed: 0,
              status,
              driverName: v.driverName ?? undefined,
              hasGps: false,
              routeHint: v.hasGpsDevice ? 'Awaiting GPS signal' : 'No GPS signal',
              vehicleType: v.vehicleType,
              ...trackerFields
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


  getTripAnalytics(vehicleId: number, from?: Date, to?: Date): Observable<TripAnalyticsBundle> {
    const params: Record<string, string> = { vehicleId: String(vehicleId) };
    if (from) params['from'] = from.toISOString();
    if (to) params['to'] = to.toISOString();
    return this.http.get<TripAnalyticsBundle>(`${this.base}/trips/analytics`, { params });
  }

  getTripReplay(vehicleId: number, from?: Date, to?: Date): Observable<TripReplayBundle> {
    const params: Record<string, string> = { vehicleId: String(vehicleId) };
    if (from) params['from'] = from.toISOString();
    if (to) params['to'] = to.toISOString();
    return this.http.get<TripReplayBundle>(`${this.base}/trips/replay`, { params });
  }

  getFleetStatus(): Observable<GpsFleetStatus> {
    return this.http.get<GpsFleetStatus>(`${this.base}/dashboard/fleet-status`);
  }

  getFleetStatusLocal(): Observable<GpsFleetStatusLocal> {
    return this.http.get<GpsFleetStatusLocal>(`${this.base}/dashboard/fleet-status-local`);
  }

  getFleetStatusHistory(from?: Date, to?: Date): Observable<GpsFleetStatusSnapshot[]> {
    const params: Record<string, string> = {};
    if (from) params['from'] = from.toISOString();
    if (to) params['to'] = to.toISOString();
    return this.http.get<GpsFleetStatusSnapshot[]>(`${this.base}/dashboard/fleet-status-history`, { params });
  }

  getTripContext(vehicleId: number): Observable<TripDeviceContext> {
    return this.http.get<TripDeviceContext>(`${this.base}/trips/context`, {
      params: { vehicleId: String(vehicleId) }
    });
  }

  getTrips(vehicleId?: number | null, from?: Date, to?: Date, filters?: TripFleetFilters): Observable<GpsTrip[]> {
    const params: Record<string, string> = {};
    if (vehicleId) params['vehicleId'] = String(vehicleId);
    if (from) params['from'] = from.toISOString();
    if (to) params['to'] = to.toISOString();
    if (filters?.branchId) params['branchId'] = String(filters.branchId);
    if (filters?.departmentId) params['departmentId'] = String(filters.departmentId);
    if (filters?.driverId) params['driverId'] = String(filters.driverId);
    return this.http.get<GpsTrip[]>(`${this.base}/trips`, { params });
  }

  getFleetTripSummary(from?: Date, to?: Date, filters?: TripFleetFilters): Observable<TripAnalyticsSummary> {
    const params: Record<string, string> = {};
    if (from) params['from'] = from.toISOString();
    if (to) params['to'] = to.toISOString();
    if (filters?.branchId) params['branchId'] = String(filters.branchId);
    if (filters?.departmentId) params['departmentId'] = String(filters.departmentId);
    if (filters?.driverId) params['driverId'] = String(filters.driverId);
    return this.http.get<TripAnalyticsSummary>(`${this.base}/trips/fleet-summary`, { params });
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

  installTracker(id: number, body: InstallTrackerPayload): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/trackers/${id}/install`, body);
  }

  transferTracker(id: number, body: TransferTrackerPayload): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/trackers/${id}/transfer`, body);
  }

  uninstallTracker(id: number, body?: UninstallTrackerPayload): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/trackers/${id}/uninstall`, body ?? {});
  }

  getTrackerAssignments(id: number): Observable<TrackerAssignment[]> {
    return this.http.get<TrackerAssignment[]>(`${this.base}/trackers/${id}/assignments`);
  }

  getTrackerInstallVehicles(trackerId?: number): Observable<TrackerInstallVehicle[]> {
    const url = trackerId
      ? `${this.base}/trackers/install-vehicles?trackerId=${trackerId}`
      : `${this.base}/trackers/install-vehicles`;
    return this.http.get<TrackerInstallVehicle[]>(url);
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

  private routeHint(status: FleetTrackStatus, speed: number): string {
    if (status === 'moving') return `${Math.round(speed)} km/h`;
    if (status === 'idle') return 'Idle • awaiting movement';
    if (status === 'parked') return 'Parked • ignition off';
    if (status === 'sos') return 'SOS / panic alarm';
    if (status === 'never_seen') return 'Never seen • no fix yet';
    if (status === 'delayed') return 'Delayed • check route';
    return 'Last known position';
  }
}
