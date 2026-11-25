import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { PagedResult, normalizePagedResult } from '../../../core/models/common.model';
import {
  ConvertTravelEnquiry, LogTravelEnquiryActivity, SaveTravelEnquiry, TravelEnquiry, TravelEnquiryAction,
  TravelEnquiryConversionResult, TravelEnquiryListItem, TravelEnquiryQuery, TravelEnquiryStatus, TravelEnquirySummary
} from '../models/travel.models';

/** Enquiries API (`api/travel/enquiries`). */
@Injectable({ providedIn: 'root' })
export class TravelEnquiryService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/travel/enquiries`;

  getEnquiries(q: TravelEnquiryQuery): Observable<PagedResult<TravelEnquiryListItem>> {
    let params = new HttpParams().set('page', q.page).set('pageSize', q.pageSize);
    if (q.search?.trim()) params = params.set('search', q.search.trim());
    if (q.status) params = params.set('status', q.status);
    if (q.serviceType) params = params.set('serviceType', q.serviceType);
    if (q.agentUserId) params = params.set('agentUserId', q.agentUserId);
    if (q.createdFrom) params = params.set('createdFrom', q.createdFrom);
    if (q.createdTo) params = params.set('createdTo', q.createdTo);
    if (q.followUpDue) params = params.set('followUpDue', true);
    return this.http.get<PagedResult<TravelEnquiryListItem>>(this.base, { params }).pipe(map(r => normalizePagedResult(r)));
  }

  getSummary(): Observable<TravelEnquirySummary> {
    return this.http.get<TravelEnquirySummary>(`${this.base}/summary`);
  }

  getByCustomer(customerId: number): Observable<TravelEnquiryListItem[]> {
    return this.http.get<TravelEnquiryListItem[]>(`${this.base}/by-customer/${customerId}`);
  }

  getEnquiry(id: number): Observable<TravelEnquiry> {
    return this.http.get<TravelEnquiry>(`${this.base}/${id}`);
  }

  create(dto: SaveTravelEnquiry): Observable<number> {
    return this.http.post<number>(this.base, dto);
  }

  update(id: number, dto: SaveTravelEnquiry): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${id}`, dto);
  }

  changeStatus(id: number, action: Extract<TravelEnquiryAction, 'MarkContacted' | 'MarkQualified' | 'MarkQuoted' | 'Reopen'>, note?: string): Observable<TravelEnquiryStatus> {
    return this.http.post<TravelEnquiryStatus>(`${this.base}/${id}/status`, { action, note: note ?? null });
  }

  markLost(id: number, lostReasonId: number, lostNotes: string | null): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/${id}/lost`, { lostReasonId, lostNotes });
  }

  logActivity(id: number, dto: LogTravelEnquiryActivity): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/${id}/activities`, dto);
  }

  convert(id: number, dto: ConvertTravelEnquiry): Observable<TravelEnquiryConversionResult> {
    return this.http.post<TravelEnquiryConversionResult>(`${this.base}/${id}/convert`, dto);
  }
}
