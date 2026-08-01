import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
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
  GeofenceAssignment,
  GeofenceStats,
  UpsertGeofenceAssignments,
  GpsAlertRule,
  GpsAlertEvent,
  GpsTrip,
  TripDetailBundle,
  TripListQuery,
  TripAnalyticsBundle,
  TripAnalyticsSummary,
  TripFleetFilters,
  TripReplayBundle,
  HistoryReplayBundle,
  GpsFleetStatus,
  GpsFleetStatusLocal,
  GpsFleetStatusSnapshot,
  TripDeviceContext,
  GpsDeviceCommand,
  GpsDeviceCommandDetail,
  GpsCommandLibraryItem,
  GpsCommandLibraryParam,
  SupportedCommand,
  GpsEta,
  IngestPositionPayload,
  TraccarStatusDto,
  TraccarDeviceDto,
  TraccarSyncResultDto,
  TraccarSyncRunResult,
  TraccarSyncStatusDto,
  AnalyticsFilters,
  AnalyticsOverview,
  DistanceAnalytics,
  SpeedAnalytics,
  IdleAnalytics,
  StopAnalytics,
  DriverScore,
  FleetUtilization,
  FuelAnalytics,
  GeofenceAnalytics,
  AlertEventStats,
  GpsAlertStats,
  HeatmapPoint,
  GpsVehicleHealth,
  VehicleRanking,
  CostAnalytics,
  Trends,
  ComparativeAnalytics,
  AnalyticsReportSchedule,
  CreateAnalyticsReportSchedule,
  UpdateAnalyticsReportSchedule
} from '../models/gps-tracking.model';
import { PagedResult, normalizePagedResult } from '../models/common.model';
import { VehicleService } from './vehicle.service';
import { DriverService } from './driver.service';
import { VehicleStatus } from '../models/vehicle.model';
import { resolveFleetStatus } from '../utils/gps-status.util';
import { parseGpsTimestamp } from '../utils/gps-timestamp.util';

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
                temperature: live.temperature,
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
    const age = Date.now() - parseGpsTimestamp(timestamp);
    return Number.isFinite(age) && age >= 0 && age <= RECENT_MS;
  }

  ingestPosition(payload: IngestPositionPayload): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/positions`, payload);
  }

  reverseGeocode(lat: number, lng: number, forceRefresh = false): Observable<{
    formattedAddress: string;
    road?: string;
    city?: string;
    state?: string;
    country?: string;
    postalCode?: string;
    fromCache?: boolean;
    placeName?: string;
    placeType?: string;
  } | null> {
    const params: Record<string, string> = {
      lat: String(lat),
      lng: String(lng)
    };
    if (forceRefresh) params['forceRefresh'] = 'true';
    return this.http
      .get<{
        success?: boolean;
        data?: {
          formattedAddress: string;
          road?: string;
          city?: string;
          state?: string;
          country?: string;
          postalCode?: string;
          fromCache?: boolean;
          placeName?: string;
          placeType?: string;
        };
        formattedAddress?: string;
        road?: string;
        city?: string;
        state?: string;
        country?: string;
        postalCode?: string;
        fromCache?: boolean;
        placeName?: string;
        placeType?: string;
      }>(`${this.base}/location/reverse`, { params })
      .pipe(
        map(res => {
          const raw = (res as { data?: unknown }).data ?? res;
          if (!raw || typeof raw !== 'object') return null;
          const r = raw as {
            formattedAddress?: string;
            FormattedAddress?: string;
            road?: string;
            Road?: string;
            city?: string;
            City?: string;
            state?: string;
            State?: string;
            country?: string;
            Country?: string;
            postalCode?: string;
            PostalCode?: string;
            fromCache?: boolean;
            FromCache?: boolean;
            placeName?: string;
            PlaceName?: string;
            placeType?: string;
            PlaceType?: string;
          };
          const formattedAddress =
            r.formattedAddress?.trim() || r.FormattedAddress?.trim() || '';
          if (!formattedAddress) return null;
          return {
            formattedAddress,
            road: r.road ?? r.Road,
            city: r.city ?? r.City,
            state: r.state ?? r.State,
            country: r.country ?? r.Country,
            postalCode: r.postalCode ?? r.PostalCode,
            fromCache: r.fromCache ?? r.FromCache,
            placeName: r.placeName ?? r.PlaceName,
            placeType: r.placeType ?? r.PlaceType
          };
        }),
        catchError(() => of(null))
      );
  }

  getHistory(vehicleId: number, from?: Date, to?: Date): Observable<PositionDto[]> {
    const params: Record<string, string> = {};
    if (from) params['from'] = from.toISOString();
    if (to) params['to'] = to.toISOString();
    return this.http.get<PositionDto[]>(`${this.base}/history/${vehicleId}`, { params });
  }

  getHistoryReplay(vehicleId: number, from?: Date, to?: Date): Observable<HistoryReplayBundle> {
    const params: Record<string, string> = { vehicleId: String(vehicleId) };
    if (from) params['from'] = from.toISOString();
    if (to) params['to'] = to.toISOString();
    return this.http.get<HistoryReplayBundle>(`${this.base}/history/replay`, { params });
  }

  exportHistory(
    vehicleId: number,
    from: Date,
    to: Date,
    format: 'csv' | 'gpx' | 'geojson' | 'kml'
  ): Observable<Blob> {
    const params: Record<string, string> = {
      from: from.toISOString(),
      to: to.toISOString(),
      format
    };
    return this.http.get(`${this.base}/history/${vehicleId}/export`, {
      params,
      responseType: 'blob'
    });
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

  getTrips(
    vehicleId?: number | null,
    from?: Date,
    to?: Date,
    filters?: TripFleetFilters,
    query?: TripListQuery
  ): Observable<PagedResult<GpsTrip>> {
    const params: Record<string, string> = {};
    if (vehicleId) params['vehicleId'] = String(vehicleId);
    if (from) params['from'] = from.toISOString();
    if (to) params['to'] = to.toISOString();
    if (filters?.branchId) params['branchId'] = String(filters.branchId);
    if (filters?.departmentId) params['departmentId'] = String(filters.departmentId);
    if (filters?.driverId) params['driverId'] = String(filters.driverId);
    if (query?.page) params['page'] = String(query.page);
    if (query?.pageSize) params['pageSize'] = String(query.pageSize);
    if (query?.search) params['search'] = query.search;
    if (query?.sortBy) params['sortBy'] = query.sortBy;
    if (query?.sortDir) params['sortDir'] = query.sortDir;
    if (query?.minDistanceKm != null) params['minDistanceKm'] = String(query.minDistanceKm);
    if (query?.maxDistanceKm != null) params['maxDistanceKm'] = String(query.maxDistanceKm);
    if (query?.minAvgSpeedKmh != null) params['minAvgSpeedKmh'] = String(query.minAvgSpeedKmh);
    if (query?.maxAvgSpeedKmh != null) params['maxAvgSpeedKmh'] = String(query.maxAvgSpeedKmh);
    if (query?.status) params['status'] = query.status;
    return this.http.get<PagedResult<GpsTrip>>(`${this.base}/trips`, { params }).pipe(
      map(r => normalizePagedResult(r))
    );
  }

  private tripDetailCache = new Map<string, TripDetailBundle>();

  getTripDetail(tripKey: string, useCache = true): Observable<TripDetailBundle> {
    if (useCache && this.tripDetailCache.has(tripKey)) {
      return of(this.tripDetailCache.get(tripKey)!);
    }
    return this.http.get<TripDetailBundle>(`${this.base}/trips/${encodeURIComponent(tripKey)}`).pipe(
      map(bundle => {
        this.tripDetailCache.set(tripKey, bundle);
        return bundle;
      })
    );
  }

  clearTripDetailCache(tripKey?: string): void {
    if (tripKey) this.tripDetailCache.delete(tripKey);
    else this.tripDetailCache.clear();
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

  getGeofences(filters?: {
    search?: string;
    areaType?: string;
    isActive?: boolean;
    vehicleId?: number;
  }): Observable<Geofence[]> {
    const params: Record<string, string> = {};
    if (filters?.search) params['search'] = filters.search;
    if (filters?.areaType) params['areaType'] = filters.areaType;
    if (filters?.isActive != null) params['isActive'] = String(filters.isActive);
    if (filters?.vehicleId) params['vehicleId'] = String(filters.vehicleId);
    return this.http.get<Geofence[]>(`${this.base}/geofences`, { params });
  }

  getGeofenceStats(): Observable<GeofenceStats> {
    return this.http.get<GeofenceStats>(`${this.base}/geofences/stats`);
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

  duplicateGeofence(id: number): Observable<number> {
    return this.http.post<number>(`${this.base}/geofences/${id}/duplicate`, {});
  }

  getGeofenceAssignments(geofenceId: number): Observable<GeofenceAssignment[]> {
    return this.http.get<GeofenceAssignment[]>(`${this.base}/geofences/${geofenceId}/assignments`);
  }

  upsertGeofenceAssignments(geofenceId: number, body: UpsertGeofenceAssignments): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/geofences/${geofenceId}/assignments`, body);
  }

  deleteGeofenceAssignment(geofenceId: number, assignmentId: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/geofences/${geofenceId}/assignments/${assignmentId}`);
  }

  getGeofenceEvents(geofenceId: number, from?: string, to?: string): Observable<GpsAlertEvent[]> {
    const params: Record<string, string> = {};
    if (from) params['from'] = from;
    if (to) params['to'] = to;
    return this.http.get<GpsAlertEvent[]>(`${this.base}/geofences/${geofenceId}/events`, { params });
  }

  getAlertRules(): Observable<GpsAlertRule[]> {
    return this.http.get<GpsAlertRule[]>(`${this.base}/alerts/rules`);
  }

  createAlertRule(body: Partial<GpsAlertRule>): Observable<number> {
    return this.http.post<number>(`${this.base}/alerts/rules`, body);
  }

  getAlertEvents(
    vehicleId?: number,
    optionsOrUnacknowledgedOnly?: boolean | {
      severity?: string;
      status?: string;
      readState?: string;
      datePreset?: string;
      driverId?: number;
      eventType?: string;
      geofenceId?: number;
      limit?: number;
    },
    severity?: string,
    status?: string,
    limit?: number
  ): Observable<GpsAlertEvent[]> {
    const options = typeof optionsOrUnacknowledgedOnly === 'object'
      ? optionsOrUnacknowledgedOnly
      : {
          readState: optionsOrUnacknowledgedOnly ? 'unread' : undefined,
          severity,
          status,
          limit
        };
    const params: Record<string, string> = {};
    if (vehicleId) params['vehicleId'] = String(vehicleId);
    if (options?.severity) params['severity'] = options.severity;
    if (options?.status) params['status'] = options.status;
    if (options?.readState) params['readState'] = options.readState;
    if (options?.datePreset) params['datePreset'] = options.datePreset;
    if (options?.driverId) params['driverId'] = String(options.driverId);
    if (options?.eventType) params['eventType'] = options.eventType;
    if (options?.geofenceId) params['geofenceId'] = String(options.geofenceId);
    return this.http.get<GpsAlertEvent[]>(`${this.base}/alerts/events`, { params }).pipe(
      map(events => (options?.limit ? events.slice(0, options.limit) : events))
    );
  }

  getAlertStats(): Observable<GpsAlertStats> {
    return this.http.get<GpsAlertStats>(`${this.base}/alerts/stats`);
  }

  acknowledgeAlert(id: number): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/alerts/events/${id}/acknowledge`, {});
  }

  markRead(id: number): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/alerts/events/${id}/read`, {});
  }

  resolveAlert(id: number, resolutionNotes?: string): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/alerts/events/${id}/resolve`, { resolutionNotes });
  }

  archiveAlert(id: number, archiveReason?: string): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/alerts/events/${id}/archive`, { archiveReason });
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

  sendCommand(
    gpsDeviceId: number,
    commandType: string,
    reason?: string,
    attributes?: Record<string, unknown>
  ): Observable<number> {
    return this.http.post<number>(`${this.base}/commands/send`, { gpsDeviceId, commandType, reason, attributes });
  }

  getCommands(
    deviceId: number,
    filters?: { status?: string; commandType?: string; from?: Date; to?: Date; page?: number; pageSize?: number }
  ): Observable<GpsDeviceCommand[]> {
    const params: Record<string, string> = {};
    if (filters?.status) params['status'] = filters.status;
    if (filters?.commandType) params['commandType'] = filters.commandType;
    if (filters?.from) params['from'] = filters.from.toISOString();
    if (filters?.to) params['to'] = filters.to.toISOString();
    if (filters?.page) params['page'] = String(filters.page);
    if (filters?.pageSize) params['pageSize'] = String(filters.pageSize);
    return this.http.get<GpsDeviceCommand[]>(`${this.base}/commands/${deviceId}`, { params });
  }

  getCommandLibrary(): Observable<GpsCommandLibraryItem[]> {
    return this.http.get<GpsCommandLibraryItem[]>(`${this.base}/commands/library`);
  }

  getCommandLibraryParameters(commandKey?: string): Observable<GpsCommandLibraryParam[]> {
    const params: Record<string, string> = {};
    if (commandKey) params['commandKey'] = commandKey;
    return this.http.get<GpsCommandLibraryParam[]>(`${this.base}/commands/library/parameters`, { params });
  }

  getCommandById(id: number): Observable<GpsDeviceCommandDetail> {
    return this.http.get<GpsDeviceCommandDetail>(`${this.base}/commands/item/${id}`);
  }

  getVehicleCommands(vehicleId: number): Observable<GpsDeviceCommand[]> {
    return this.http.get<GpsDeviceCommand[]>(`${this.base}/commands/vehicle/${vehicleId}`);
  }

  getSupportedCommands(deviceId: number): Observable<SupportedCommand[]> {
    return this.http.get<SupportedCommand[]>(`${this.base}/commands/supported/${deviceId}`);
  }

  retryCommand(id: number): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/commands/${id}/retry`, {});
  }

  cancelCommand(id: number, reason?: string): Observable<boolean> {
    const params: Record<string, string> = {};
    if (reason) params['reason'] = reason;
    return this.http.post<boolean>(`${this.base}/commands/${id}/cancel`, {}, { params });
  }

  getEta(bookingId: number): Observable<GpsEta> {
    return this.http.get<GpsEta>(`${this.base}/eta`, { params: { bookingId: String(bookingId) } });
  }

  // ── Analytics ────────────────────────────────────────────────────────────────

  private analyticsParams(filters?: AnalyticsFilters): Record<string, string> {
    const params: Record<string, string> = {};
    if (filters?.from) params['from'] = filters.from;
    if (filters?.to) params['to'] = filters.to;
    if (filters?.branchId) params['branchId'] = String(filters.branchId);
    if (filters?.departmentId) params['departmentId'] = String(filters.departmentId);
    if (filters?.driverId) params['driverId'] = String(filters.driverId);
    return params;
  }

  getAnalyticsOverview(filters?: AnalyticsFilters): Observable<AnalyticsOverview> {
    return this.http.get<AnalyticsOverview>(`${this.base}/analytics/overview`, { params: this.analyticsParams(filters) });
  }

  getDistanceAnalytics(filters?: AnalyticsFilters): Observable<DistanceAnalytics> {
    return this.http.get<DistanceAnalytics>(`${this.base}/analytics/distance`, { params: this.analyticsParams(filters) });
  }

  getSpeedAnalytics(filters?: AnalyticsFilters): Observable<SpeedAnalytics> {
    return this.http.get<SpeedAnalytics>(`${this.base}/analytics/speed`, { params: this.analyticsParams(filters) });
  }

  getIdleAnalytics(filters?: AnalyticsFilters): Observable<IdleAnalytics> {
    return this.http.get<IdleAnalytics>(`${this.base}/analytics/idle`, { params: this.analyticsParams(filters) });
  }

  getStopAnalytics(filters?: AnalyticsFilters): Observable<StopAnalytics> {
    return this.http.get<StopAnalytics>(`${this.base}/analytics/stops`, { params: this.analyticsParams(filters) });
  }

  getDriverScoreRanking(filters?: AnalyticsFilters): Observable<DriverScore[]> {
    return this.http.get<DriverScore[]>(`${this.base}/analytics/drivers/ranking`, { params: this.analyticsParams(filters) });
  }

  getFleetUtilization(filters?: AnalyticsFilters): Observable<FleetUtilization> {
    return this.http.get<FleetUtilization>(`${this.base}/analytics/utilization`, { params: this.analyticsParams(filters) });
  }

  getFuelAnalytics(filters?: AnalyticsFilters): Observable<FuelAnalytics> {
    return this.http.get<FuelAnalytics>(`${this.base}/analytics/fuel`, { params: this.analyticsParams(filters) });
  }

  getGeofenceAnalytics(filters?: AnalyticsFilters): Observable<GeofenceAnalytics> {
    return this.http.get<GeofenceAnalytics>(`${this.base}/analytics/geofences`, { params: this.analyticsParams(filters) });
  }

  getAlertEventStats(filters?: AnalyticsFilters): Observable<AlertEventStats> {
    return this.http.get<AlertEventStats>(`${this.base}/analytics/events`, { params: this.analyticsParams(filters) });
  }

  getPositionHeatmap(vehicleIds: number[], filters?: AnalyticsFilters): Observable<HeatmapPoint[]> {
    let params = new HttpParams();
    vehicleIds.forEach(id => { params = params.append('vehicleIds', String(id)); });
    const f = this.analyticsParams(filters);
    Object.keys(f).forEach(key => { params = params.set(key, f[key]); });
    return this.http.get<HeatmapPoint[]>(`${this.base}/analytics/heatmap`, { params });
  }

  getVehicleHealth(branchId?: number, departmentId?: number): Observable<GpsVehicleHealth[]> {
    const params: Record<string, string> = {};
    if (branchId) params['branchId'] = String(branchId);
    if (departmentId) params['departmentId'] = String(departmentId);
    return this.http.get<GpsVehicleHealth[]>(`${this.base}/analytics/vehicle-health`, { params });
  }

  getVehicleRanking(filters?: AnalyticsFilters): Observable<VehicleRanking[]> {
    return this.http.get<VehicleRanking[]>(`${this.base}/analytics/vehicles/ranking`, { params: this.analyticsParams(filters) });
  }

  getCostAnalytics(filters?: AnalyticsFilters): Observable<CostAnalytics> {
    return this.http.get<CostAnalytics>(`${this.base}/analytics/cost`, { params: this.analyticsParams(filters) });
  }

  getAnalyticsTrends(filters?: AnalyticsFilters, granularity: 'daily' | 'weekly' | 'monthly' = 'daily'): Observable<Trends> {
    const params = { ...this.analyticsParams(filters), granularity };
    return this.http.get<Trends>(`${this.base}/analytics/trends`, { params });
  }

  getComparativeAnalytics(fromA: string, toA: string, fromB?: string, toB?: string, filters?: AnalyticsFilters): Observable<ComparativeAnalytics> {
    const params: Record<string, string> = { fromA, toA };
    if (fromB) params['fromB'] = fromB;
    if (toB) params['toB'] = toB;
    if (filters?.branchId) params['branchId'] = String(filters.branchId);
    if (filters?.departmentId) params['departmentId'] = String(filters.departmentId);
    return this.http.get<ComparativeAnalytics>(`${this.base}/analytics/comparative`, { params });
  }

  getAnalyticsReportSchedules(): Observable<AnalyticsReportSchedule[]> {
    return this.http.get<AnalyticsReportSchedule[]>(`${this.base}/analytics/reports/schedules`);
  }

  createAnalyticsReportSchedule(body: CreateAnalyticsReportSchedule): Observable<number> {
    return this.http.post<number>(`${this.base}/analytics/reports/schedules`, body);
  }

  updateAnalyticsReportSchedule(id: number, body: UpdateAnalyticsReportSchedule): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/analytics/reports/schedules/${id}`, body);
  }

  deleteAnalyticsReportSchedule(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/analytics/reports/schedules/${id}`);
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
