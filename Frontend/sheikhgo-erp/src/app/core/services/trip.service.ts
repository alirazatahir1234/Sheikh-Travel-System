import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/common.model';
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

  getAll(page = 1, pageSize = 20, filter?: TripFilter): Observable<PagedResult<TripListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (filter?.status) params = params.set('status', filter.status);
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
    return this.http.get<PagedResult<TripListItem>>(this.base, { params });
  }

  getById(id: number): Observable<TripDetail> {
    return this.http.get<TripDetail>(`${this.base}/${id}`);
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
}
