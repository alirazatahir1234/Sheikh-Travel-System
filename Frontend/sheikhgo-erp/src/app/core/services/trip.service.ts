import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult } from '../models/common.model';
import {
  BulkTripImportResult,
  TripAssignmentConflict,
  TripCostEstimate,
  TripCostEstimateRequest,
  TripInsights,
  TripTemplate,
  UpsertTripTemplate
} from '../models/trip-enhancements.model';
import {
  CreateTripDto,
  TripAnalytics,
  TripCalendarItem,
  TripDashboard,
  TripDetail,
  TripFilter,
  TripListItem,
  TripRouteSummary,
  TripStatus,
  UpdateTripDto
} from '../models/trip.model';

@Injectable({ providedIn: 'root' })
export class TripService {
  private readonly base = `${environment.apiUrl}/trips`;

  constructor(private http: HttpClient) {}

  /** SG-06: X-Tenant-Id is interceptor-only; this helper is a no-op. */
  private tenantScopedHttpOptions(_tenantScopeId?: number | null): { headers?: HttpHeaders } {
    return {};
  }

  getDashboard(): Observable<TripDashboard> {
    return this.http.get<TripDashboard>(`${this.base}/dashboard`);
  }

  getCalendar(from: string, to: string): Observable<TripCalendarItem[]> {
    const params = new HttpParams().set('from', from).set('to', to);
    return this.http.get<TripCalendarItem[]>(`${this.base}/calendar`, { params });
  }

  getLive(todayOnly = true): Observable<TripListItem[]> {
    const params = new HttpParams().set('todayOnly', todayOnly);
    return this.http.get<TripListItem[]>(`${this.base}/live`, { params });
  }

  getAnalytics(from?: string, to?: string): Observable<TripAnalytics> {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get<TripAnalytics>(`${this.base}/analytics`, { params });
  }

  getRouteSummary(id: number): Observable<TripRouteSummary> {
    return this.http.get<TripRouteSummary>(`${this.base}/${id}/route`);
  }

  optimizeRoute(id: number): Observable<TripRouteSummary> {
    return this.http.post<TripRouteSummary>(`${this.base}/${id}/optimize-route`, {});
  }

  getAll(
    page = 1,
    pageSize = 20,
    filter?: TripFilter,
    tenantScopeId?: number | null
  ): Observable<PagedResult<TripListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (filter?.statuses?.length) {
      for (const s of filter.statuses) {
        params = params.append('statuses', s);
      }
    } else if (filter?.status) {
      params = params.set('status', filter.status);
    }
    if (filter?.driverId != null) params = params.set('driverId', filter.driverId);
    if (filter?.vehicleId != null) params = params.set('vehicleId', filter.vehicleId);
    if (filter?.routeId != null) params = params.set('routeId', filter.routeId);
    if (filter?.customerId != null) params = params.set('customerId', filter.customerId);
    if (filter?.dateFrom) params = params.set('dateFrom', filter.dateFrom);
    if (filter?.dateTo) params = params.set('dateTo', filter.dateTo);
    if (filter?.search) params = params.set('search', filter.search);
    if (filter?.todayOnly) params = params.set('todayOnly', true);
    if (filter?.tomorrowOnly) params = params.set('tomorrowOnly', true);
    if (filter?.upcomingOnly) params = params.set('upcomingOnly', true);
    return this.http.get<PagedResult<TripListItem>>(this.base, {
      params,
      ...this.tenantScopedHttpOptions(tenantScopeId)
    });
  }

  getById(id: number, tenantScopeId?: number | null): Observable<TripDetail> {
    return this.http.get<TripDetail>(`${this.base}/${id}`, this.tenantScopedHttpOptions(tenantScopeId));
  }

  create(trip: CreateTripDto): Observable<number> {
    return this.http.post<number>(this.base, { trip });
  }

  update(id: number, trip: UpdateTripDto): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${id}`, { id, trip });
  }

  updateStatus(id: number, status: TripStatus, note?: string, cancellationReason?: string): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${id}/status`, { id, status, note, cancellationReason });
  }

  assignDriver(id: number, driverId: number, assistantDriverId?: number | null, driverNotes?: string): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${id}/assign-driver`, {
      driverId,
      assistantDriverId,
      driverNotes
    });
  }

  assignVehicle(id: number, vehicleId: number): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${id}/assign-vehicle`, { vehicleId });
  }

  delete(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/${id}`);
  }

  createFromBooking(bookingId: number): Observable<number> {
    return this.http.post<number>(`${this.base}/from-booking/${bookingId}`, {});
  }

  addExpense(tripId: number, expense: { expenseType: string; amount: number; description?: string; expenseDate?: string }): Observable<number> {
    return this.http.post<number>(`${this.base}/${tripId}/expenses`, expense);
  }

  deleteExpense(tripId: number, expenseId: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/${tripId}/expenses/${expenseId}`);
  }

  addPassenger(tripId: number, passenger: { fullName: string; phone?: string; notes?: string }): Observable<number> {
    return this.http.post<number>(`${this.base}/${tripId}/passengers`, passenger);
  }

  updatePassenger(
    tripId: number,
    passengerId: number,
    passenger: { fullName: string; phone?: string; boardingStatus: string; dropStatus: string; notes?: string }
  ): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${tripId}/passengers/${passengerId}`, passenger);
  }

  deletePassenger(tripId: number, passengerId: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/${tripId}/passengers/${passengerId}`);
  }

  uploadDocument(tripId: number, file: File, documentType: string): Observable<unknown> {
    const form = new FormData();
    form.append('file', file);
    form.append('documentType', documentType);
    return this.http.post(`${this.base}/${tripId}/documents/upload`, form);
  }

  deleteDocument(tripId: number, documentId: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/${tripId}/documents/${documentId}`);
  }

  clone(id: number): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.base}/${id}/clone`, {}).pipe(map(unwrap));
  }

  bulkImportCsv(csvContent: string): Observable<BulkTripImportResult> {
    return this.http
      .post<ApiResponse<BulkTripImportResult>>(`${this.base}/import`, { csvContent })
      .pipe(map(unwrap));
  }

  getInsights(id: number): Observable<TripInsights> {
    return this.http.get<ApiResponse<TripInsights>>(`${this.base}/${id}/insights`).pipe(map(unwrap));
  }

  estimateCost(body: TripCostEstimateRequest): Observable<TripCostEstimate> {
    return this.http.post<ApiResponse<TripCostEstimate>>(`${this.base}/estimate-cost`, body).pipe(map(unwrap));
  }

  getAssignmentConflict(tripId: number, driverId?: number | null, vehicleId?: number | null): Observable<TripAssignmentConflict> {
    let params = new HttpParams();
    if (driverId != null) params = params.set('driverId', driverId);
    if (vehicleId != null) params = params.set('vehicleId', vehicleId);
    return this.http
      .get<ApiResponse<TripAssignmentConflict>>(`${this.base}/${tripId}/assignment-conflict`, { params })
      .pipe(map(unwrap));
  }

  sendDelayedEta(tripId: number, channel: 'email' | 'whatsapp', customMessage?: string): Observable<boolean> {
    return this.http
      .post<ApiResponse<boolean>>(`${this.base}/${tripId}/delayed-eta`, { channel, customMessage })
      .pipe(map(unwrap));
  }

  moveDispatchBoard(tripId: number, targetColumnKey: string): Observable<boolean> {
    return this.http
      .post<ApiResponse<boolean>>(`${this.base}/dispatch-board/move`, { tripId, targetColumnKey })
      .pipe(map(unwrap));
  }

  listTemplates(): Observable<TripTemplate[]> {
    return this.http.get<ApiResponse<TripTemplate[]>>(`${this.base}/templates`).pipe(map(r => unwrap(r) ?? []));
  }

  saveTemplate(body: UpsertTripTemplate): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.base}/templates`, body).pipe(map(unwrap));
  }

  deleteTemplate(id: number): Observable<boolean> {
    return this.http.delete<ApiResponse<boolean>>(`${this.base}/templates/${id}`).pipe(map(unwrap));
  }

  spawnFromTemplate(templateId: number, date?: string): Observable<number> {
    let params = new HttpParams();
    if (date) params = params.set('date', date);
    return this.http
      .post<ApiResponse<number>>(`${this.base}/templates/${templateId}/spawn`, {}, { params })
      .pipe(map(unwrap));
  }

  spawnDueRecurring(): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.base}/templates/spawn-due`, {}).pipe(map(unwrap));
  }
}

function unwrap<T>(r: ApiResponse<T>): T {
  if (!r?.success) throw new Error(r?.message || 'Request failed');
  return r.data as T;
}
