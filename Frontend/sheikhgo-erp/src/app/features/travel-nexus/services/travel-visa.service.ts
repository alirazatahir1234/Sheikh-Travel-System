import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { PagedResult, normalizePagedResult } from '../../../core/models/common.model';
import {
  CreateTravelVisaCase, RecordTravelVisaDecision, SubmitTravelVisaCase, TravelVisaCase, TravelVisaCaseListItem,
  TravelVisaCaseQuery, TravelVisaCaseStatus, TravelVisaCaseSummary, TravelVisaDecisionResult, TravelVisaTransitionAction,
  UpdateTravelVisaCase
} from '../models/travel.models';

/** Visa cases API (`api/travel/visa-cases`). */
@Injectable({ providedIn: 'root' })
export class TravelVisaService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/travel/visa-cases`;

  getCases(q: TravelVisaCaseQuery): Observable<PagedResult<TravelVisaCaseListItem>> {
    let params = new HttpParams().set('page', q.page).set('pageSize', q.pageSize);
    if (q.search?.trim()) params = params.set('search', q.search.trim());
    if (q.status) params = params.set('status', q.status);
    if (q.countryCode) params = params.set('countryCode', q.countryCode);
    if (q.createdFrom) params = params.set('createdFrom', q.createdFrom);
    if (q.createdTo) params = params.set('createdTo', q.createdTo);
    return this.http.get<PagedResult<TravelVisaCaseListItem>>(this.base, { params }).pipe(map(r => normalizePagedResult(r)));
  }

  getSummary(): Observable<TravelVisaCaseSummary> {
    return this.http.get<TravelVisaCaseSummary>(`${this.base}/summary`);
  }

  getByCustomer(customerId: number): Observable<TravelVisaCaseListItem[]> {
    return this.http.get<TravelVisaCaseListItem[]>(`${this.base}/by-customer/${customerId}`);
  }

  getCase(id: number): Observable<TravelVisaCase> {
    return this.http.get<TravelVisaCase>(`${this.base}/${id}`);
  }

  create(dto: CreateTravelVisaCase): Observable<number> {
    return this.http.post<number>(this.base, dto);
  }

  update(id: number, dto: UpdateTravelVisaCase): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${id}`, dto);
  }

  transition(id: number, action: TravelVisaTransitionAction, note: string | null): Observable<TravelVisaCaseStatus> {
    return this.http.post<TravelVisaCaseStatus>(`${this.base}/${id}/transition`, { action, note });
  }

  submit(id: number, dto: SubmitTravelVisaCase): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/${id}/submit`, dto);
  }

  addApplicants(id: number, travelerIds: number[]): Observable<number> {
    return this.http.post<number>(`${this.base}/${id}/applicants`, { travelerIds });
  }

  removeApplicant(id: number, applicantId: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/${id}/applicants/${applicantId}`);
  }

  recordDecision(id: number, applicantId: number, dto: RecordTravelVisaDecision): Observable<TravelVisaDecisionResult> {
    return this.http.post<TravelVisaDecisionResult>(`${this.base}/${id}/applicants/${applicantId}/decision`, dto);
  }
}
