import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  BulkGeofenceActionPayload,
  BulkGeofenceResult,
  GeofenceActivityItem,
  GeofenceDetail,
  GeofenceGroup,
  GeofenceInsideVehicle,
  GeofenceListItem,
  GeofenceRecipientOptions,
  GeofenceSummary,
  GeofenceTypeOption,
  GeofenceVisitGroupBy,
  GeofenceVisitGroups,
  GeofenceVisitQuery,
  GeofenceVisitsPage,
  GeofenceVisitsSummary,
  ImportGeofencesResult,
  SaveGeofencePayload,
  TestGeofenceShapePayload,
  TestGeofenceShapeResult
} from './geofence.models';

/**
 * Calls for the geofences workspace (screens 1–3). The tenant is never sent: the API takes it from the signed-in
 * user. The older /gps/geofences routes stay in GpsTrackingService for the live map and the driver app.
 */
@Injectable({ providedIn: 'root' })
export class GeofenceWorkspaceService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/gps/geofences`;

  list(): Observable<GeofenceListItem[]> {
    return this.http.get<GeofenceListItem[]>(`${this.base}/workspace`);
  }

  detail(id: number): Observable<GeofenceDetail> {
    return this.http.get<GeofenceDetail>(`${this.base}/${id}`);
  }

  summary(): Observable<GeofenceSummary> {
    return this.http.get<GeofenceSummary>(`${this.base}/summary`);
  }

  inside(id: number): Observable<GeofenceInsideVehicle[]> {
    return this.http.get<GeofenceInsideVehicle[]>(`${this.base}/${id}/inside`);
  }

  /** Entries, exits and rule alerts on a company-local day (yyyy-MM-dd); today when omitted. */
  activity(id: number, day?: string | null): Observable<GeofenceActivityItem[]> {
    const params = day ? new HttpParams().set('day', day) : undefined;
    return this.http.get<GeofenceActivityItem[]>(`${this.base}/${id}/activity`, { params });
  }

  create(payload: SaveGeofencePayload): Observable<number> {
    return this.http.post<number>(`${this.base}/workspace`, payload);
  }

  update(id: number, payload: SaveGeofencePayload): Observable<number> {
    return this.http.put<number>(`${this.base}/workspace/${id}`, payload);
  }

  visits(query: GeofenceVisitQuery): Observable<GeofenceVisitsPage> {
    let params = this.visitParams(query, true);
    params = params.set('page', String(query.page ?? 1)).set('pageSize', String(query.pageSize ?? 25));
    return this.http.get<GeofenceVisitsPage>(`${this.base}/visits`, { params });
  }

  visitsSummary(query: GeofenceVisitQuery): Observable<GeofenceVisitsSummary> {
    return this.http.get<GeofenceVisitsSummary>(`${this.base}/visits/summary`, { params: this.visitParams(query, false) });
  }

  visitGroups(groupBy: Exclude<GeofenceVisitGroupBy, 'visit'>, query: GeofenceVisitQuery): Observable<GeofenceVisitGroups> {
    const params = this.visitParams(query, true).set('groupBy', groupBy);
    return this.http.get<GeofenceVisitGroups>(`${this.base}/visits/groups`, { params });
  }

  testShape(payload: TestGeofenceShapePayload): Observable<TestGeofenceShapeResult> {
    return this.http.post<TestGeofenceShapeResult>(`${this.base}/test-shape`, payload);
  }

  bulk(payload: BulkGeofenceActionPayload): Observable<BulkGeofenceResult> {
    return this.http.post<BulkGeofenceResult>(`${this.base}/bulk`, payload);
  }

  import(items: SaveGeofencePayload[]): Observable<ImportGeofencesResult> {
    return this.http.post<ImportGeofencesResult>(`${this.base}/import`, { items });
  }

  groups(): Observable<GeofenceGroup[]> {
    return this.http.get<GeofenceGroup[]>(`${this.base}/groups`);
  }

  createGroup(name: string, sortOrder = 0): Observable<number> {
    return this.http.post<number>(`${this.base}/groups`, { name, sortOrder });
  }

  renameGroup(id: number, name: string, sortOrder = 0): Observable<number> {
    return this.http.put<number>(`${this.base}/groups/${id}`, { name, sortOrder });
  }

  deleteGroup(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/groups/${id}`);
  }

  types(): Observable<GeofenceTypeOption[]> {
    return this.http.get<GeofenceTypeOption[]>(`${this.base}/types`);
  }

  createType(name: string, color: string, isRestricted: boolean): Observable<number> {
    return this.http.post<number>(`${this.base}/types`, { name, color, isRestricted });
  }

  recipientOptions(): Observable<GeofenceRecipientOptions> {
    return this.http.get<GeofenceRecipientOptions>(`${this.base}/recipient-options`);
  }

  private visitParams(query: GeofenceVisitQuery, withStatus: boolean): HttpParams {
    let params = new HttpParams().set('from', query.from).set('to', query.to);
    if (query.geofenceId) params = params.set('geofenceId', String(query.geofenceId));
    if (query.vehicleIds?.length) params = params.set('vehicleIds', query.vehicleIds.join(','));
    if (query.driverId) params = params.set('driverId', String(query.driverId));
    if (withStatus && query.status && query.status !== 'all') params = params.set('status', query.status);
    return params;
  }
}
