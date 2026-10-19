import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  GenerateDraftSettlementRequest,
  Settlement,
  SettlementDetail,
  SettlementFilter
} from '../models/settlement.model';
import { PagedResult } from '../models/common.model';

@Injectable({ providedIn: 'root' })
export class SettlementService {
  private readonly base = `${environment.apiUrl}/settlements`;

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 20, filter?: SettlementFilter): Observable<PagedResult<Settlement>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (filter?.driverId) params = params.set('driverId', filter.driverId);
    if (filter?.status) params = params.set('status', filter.status);
    return this.http.get<PagedResult<Settlement>>(this.base, { params });
  }

  getById(id: number): Observable<SettlementDetail> {
    return this.http.get<SettlementDetail>(`${this.base}/${id}`);
  }

  generateDraft(request: GenerateDraftSettlementRequest): Observable<number> {
    return this.http.post<number>(`${this.base}/draft`, request);
  }

  approve(id: number): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/${id}/approve`, {});
  }

  markPaid(id: number, reference?: string): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/${id}/mark-paid`, { reference: reference ?? null });
  }

  adjust(id: number, adjustments: number, notes?: string): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/${id}/adjust`, { adjustments, notes: notes ?? null });
  }
}
