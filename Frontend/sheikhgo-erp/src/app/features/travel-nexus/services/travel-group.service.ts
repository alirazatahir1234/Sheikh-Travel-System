import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { apiMessageOf } from '../../../core/interceptors/api-envelope.interceptor';
import { PagedResult, normalizePagedResult } from '../../../core/models/common.model';
import {
  TravelGroup, TravelGroupListItem, TravelGroupManifest, TravelGroupStatus, TravelGroupSummary, TravelGroupTransitionAction,
  TravelPriceCategory, TravelRoomType, TravelRoomingRoom, TravelTransportLegType
} from '../models/travel.models';

export interface TravelGroupQuery { page: number; pageSize: number; search?: string; status?: TravelGroupStatus | ''; departureFrom?: string; departureTo?: string; }

/** Umrah groups API (`api/travel/groups`). */
@Injectable({ providedIn: 'root' })
export class TravelGroupService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/travel/groups`;

  getGroups(q: TravelGroupQuery): Observable<PagedResult<TravelGroupListItem>> {
    let params = new HttpParams().set('page', q.page).set('pageSize', q.pageSize);
    if (q.search?.trim()) params = params.set('search', q.search.trim());
    if (q.status) params = params.set('status', q.status);
    if (q.departureFrom) params = params.set('departureFrom', q.departureFrom);
    if (q.departureTo) params = params.set('departureTo', q.departureTo);
    return this.http.get<PagedResult<TravelGroupListItem>>(this.base, { params }).pipe(map(r => normalizePagedResult(r)));
  }
  getSummary(): Observable<TravelGroupSummary> { return this.http.get<TravelGroupSummary>(`${this.base}/summary`); }
  getByCustomer(customerId: number): Observable<TravelGroupListItem[]> { return this.http.get<TravelGroupListItem[]>(`${this.base}/by-customer/${customerId}`); }
  getGroup(id: number): Observable<TravelGroup> { return this.http.get<TravelGroup>(`${this.base}/${id}`); }

  create(dto: { packageId: number; name: string; departureDate: string; capacity: number; branchId: number | null; notes: string | null }): Observable<number> {
    return this.http.post<number>(this.base, dto);
  }
  update(id: number, dto: { name: string; departureDate: string; returnDate: string; capacity: number; groupLeaderTravelerId: number | null;
    leaderName: string | null; leaderPhone: string | null; notes: string | null }): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${id}`, dto);
  }
  transition(id: number, action: TravelGroupTransitionAction, note: string | null): Observable<string> {
    return this.http.post<string>(`${this.base}/${id}/transition`, { action, note });
  }

  register(id: number, customerId: number, members: { travelerId: number; roomPreference: TravelRoomType; priceCategory: TravelPriceCategory }[]): Observable<number> {
    return this.http.post<number>(`${this.base}/${id}/members`, { customerId, members });
  }
  confirmMember(id: number, memberId: number): Observable<boolean> { return this.http.post<boolean>(`${this.base}/${id}/members/${memberId}/confirm`, {}); }
  updateMember(id: number, memberId: number, dto: { roomPreference: TravelRoomType; priceCategory: TravelPriceCategory; quotedPrice: number | null }): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${id}/members/${memberId}`, dto);
  }
  /** The server's message says what else happened (visa case withdrawal, a ticket still held) — surface it. */
  cancelMember(id: number, memberId: number, reason: string): Observable<{ ok: boolean; message: string | null }> {
    return this.http.post<boolean>(`${this.base}/${id}/members/${memberId}/cancel`, { reason }, { observe: 'response' })
      .pipe(map(res => ({ ok: res.body ?? true, message: apiMessageOf(res) })));
  }

  saveStay(id: number, stayId: number | null, dto: { hotelId: number; checkIn: string; checkOut: string; confirmationNumber: string | null }): Observable<number> {
    return stayId ? this.http.put<number>(`${this.base}/${id}/stays/${stayId}`, dto) : this.http.post<number>(`${this.base}/${id}/stays`, dto);
  }
  deleteStay(id: number, stayId: number): Observable<boolean> { return this.http.delete<boolean>(`${this.base}/${id}/stays/${stayId}`); }
  suggestRooming(id: number, stayId: number): Observable<TravelRoomingRoom[]> {
    return this.http.get<TravelRoomingRoom[]>(`${this.base}/${id}/stays/${stayId}/rooming/suggest`);
  }
  saveRooming(id: number, stayId: number, rooms: TravelRoomingRoom[]): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${id}/stays/${stayId}/rooming`, { rooms });
  }

  saveTransport(id: number, legId: number | null, dto: { legType: TravelTransportLegType; departAt: string; fromPlace: string; toPlace: string;
    providerName: string | null; vehicleType: string | null; seats: number | null; fleetBookingId: number | null; notes: string | null }): Observable<number> {
    return legId ? this.http.put<number>(`${this.base}/${id}/transport/${legId}`, dto) : this.http.post<number>(`${this.base}/${id}/transport`, dto);
  }
  deleteTransport(id: number, legId: number): Observable<boolean> { return this.http.delete<boolean>(`${this.base}/${id}/transport/${legId}`); }

  /** Audited server-side; passport numbers come masked unless the user holds Travel.Traveler.ViewPII. */
  getManifest(id: number): Observable<TravelGroupManifest> { return this.http.get<TravelGroupManifest>(`${this.base}/${id}/manifest`); }
}
