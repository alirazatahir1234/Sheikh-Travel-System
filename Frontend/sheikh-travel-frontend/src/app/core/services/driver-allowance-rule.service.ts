import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/common.model';
import {
  DriverAllowanceRule,
  CreateDriverAllowanceRuleRequest,
  UpdateDriverAllowanceRuleRequest,
  CalculateDriverAllowanceRequest,
  CalculateDriverAllowanceResponse
} from '../models/driver-allowance-rule.model';

/**
 * Front door to the /api/DriverAllowanceRules endpoints.
 *
 * - CRUD is used by the admin console.
 * - `calculate()` is called by the booking wizard after route+vehicle are
 *   picked so the allowance field can be auto-filled while still being
 *   overridable by the dispatcher.
 */
@Injectable({ providedIn: 'root' })
export class DriverAllowanceRuleService {
  private readonly base = `${environment.apiUrl}/driverallowancerules`;

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 50, activeOnly = false): Observable<PagedResult<DriverAllowanceRule>> {
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize)
      .set('activeOnly', activeOnly);
    return this.http.get<PagedResult<DriverAllowanceRule>>(this.base, { params });
  }

  getById(id: number): Observable<DriverAllowanceRule> {
    return this.http.get<DriverAllowanceRule>(`${this.base}/${id}`);
  }

  create(request: CreateDriverAllowanceRuleRequest): Observable<number> {
    return this.http.post<number>(this.base, request);
  }

  update(request: UpdateDriverAllowanceRuleRequest): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${request.id}`, request);
  }

  delete(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/${id}`);
  }

  calculate(request: CalculateDriverAllowanceRequest): Observable<CalculateDriverAllowanceResponse> {
    return this.http.post<CalculateDriverAllowanceResponse>(`${this.base}/calculate`, { request });
  }
}
