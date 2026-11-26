import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { PagedResult, normalizePagedResult } from '../../../core/models/common.model';
import {
  CancelTravelService, SaveTravelFlight, SaveTravelHotelBooking, TravelBooking, TravelBookingListItem, TravelBookingStatus, TravelBookingSummary,
  TravelBookingTransitionAction, TravelFlightTransitionAction, TravelHotelTransitionAction, TravelRefundStatus
} from '../models/travel.models';

export interface TravelBookingQuery {
  page: number; pageSize: number; search?: string; status?: TravelBookingStatus | ''; ownerType?: 'Customer' | 'Group' | '';
  travelFrom?: string; travelTo?: string; ticketingDue?: boolean;
}

/** Booking files API (`api/travel/bookings`). */
@Injectable({ providedIn: 'root' })
export class TravelBookingService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/travel/bookings`;

  getBookings(q: TravelBookingQuery): Observable<PagedResult<TravelBookingListItem>> {
    let params = new HttpParams().set('page', q.page).set('pageSize', q.pageSize);
    if (q.search?.trim()) params = params.set('search', q.search.trim());
    if (q.status) params = params.set('status', q.status);
    if (q.ownerType) params = params.set('ownerType', q.ownerType);
    if (q.travelFrom) params = params.set('travelFrom', q.travelFrom);
    if (q.travelTo) params = params.set('travelTo', q.travelTo);
    if (q.ticketingDue) params = params.set('ticketingDue', true);
    return this.http.get<PagedResult<TravelBookingListItem>>(this.base, { params }).pipe(map(r => normalizePagedResult(r)));
  }
  getSummary(): Observable<TravelBookingSummary> { return this.http.get<TravelBookingSummary>(`${this.base}/summary`); }
  getByCustomer(customerId: number): Observable<TravelBookingListItem[]> { return this.http.get<TravelBookingListItem[]>(`${this.base}/by-customer/${customerId}`); }
  getByGroup(groupId: number): Observable<TravelBookingListItem[]> { return this.http.get<TravelBookingListItem[]>(`${this.base}/by-group/${groupId}`); }
  getBooking(id: number): Observable<TravelBooking> { return this.http.get<TravelBooking>(`${this.base}/${id}`); }

  create(dto: { customerId: number | null; groupId: number | null; title: string; enquiryId: number | null; visaCaseId: number | null;
    assignedAgentUserId: number | null; branchId: number | null; notes: string | null }): Observable<number> {
    return this.http.post<number>(this.base, dto);
  }
  update(id: number, dto: { title: string; enquiryId: number | null; visaCaseId: number | null; assignedAgentUserId: number | null; notes: string | null }): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${id}`, dto);
  }
  transition(id: number, action: TravelBookingTransitionAction, note: string | null): Observable<string> {
    return this.http.post<string>(`${this.base}/${id}/transition`, { action, note });
  }

  saveFlight(id: number, flightId: number | null, dto: SaveTravelFlight): Observable<number> {
    return flightId ? this.http.put<number>(`${this.base}/${id}/flights/${flightId}`, dto) : this.http.post<number>(`${this.base}/${id}/flights`, dto);
  }
  transitionFlight(id: number, flightId: number, action: TravelFlightTransitionAction, pnr: string | null, ticketingTimeLimitUtc: string | null,
    note: string | null): Observable<string> {
    return this.http.post<string>(`${this.base}/${id}/flights/${flightId}/transition`, { action, pnr, ticketingTimeLimitUtc, note });
  }
  setTickets(id: number, flightId: number, tickets: { passengerId: number; ticketNumber: string }[]): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${id}/flights/${flightId}/tickets`, { tickets });
  }
  cancelFlight(id: number, flightId: number, dto: CancelTravelService): Observable<number> {
    return this.http.post<number>(`${this.base}/${id}/flights/${flightId}/cancel`, dto);
  }
  cancelPassenger(id: number, flightId: number, passengerId: number, dto: CancelTravelService): Observable<number> {
    return this.http.post<number>(`${this.base}/${id}/flights/${flightId}/passengers/${passengerId}/cancel`, dto);
  }
  deleteFlight(id: number, flightId: number): Observable<boolean> { return this.http.delete<boolean>(`${this.base}/${id}/flights/${flightId}`); }

  saveHotel(id: number, hotelBookingId: number | null, dto: SaveTravelHotelBooking): Observable<number> {
    return hotelBookingId ? this.http.put<number>(`${this.base}/${id}/hotels/${hotelBookingId}`, dto) : this.http.post<number>(`${this.base}/${id}/hotels`, dto);
  }
  transitionHotel(id: number, hotelBookingId: number, action: TravelHotelTransitionAction, confirmationNumber: string | null, note: string | null): Observable<string> {
    return this.http.post<string>(`${this.base}/${id}/hotels/${hotelBookingId}/transition`, { action, confirmationNumber, note });
  }
  cancelHotel(id: number, hotelBookingId: number, dto: CancelTravelService): Observable<number> {
    return this.http.post<number>(`${this.base}/${id}/hotels/${hotelBookingId}/cancel`, { ...dto, ticketOutcome: null });
  }
  deleteHotel(id: number, hotelBookingId: number): Observable<boolean> { return this.http.delete<boolean>(`${this.base}/${id}/hotels/${hotelBookingId}`); }

  updateRefundStatus(id: number, cancellationId: number, refundStatus: TravelRefundStatus, note: string | null): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${id}/cancellations/${cancellationId}/refund-status`, { refundStatus, note });
  }
}
