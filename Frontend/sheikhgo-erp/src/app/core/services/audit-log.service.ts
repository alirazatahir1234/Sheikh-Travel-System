import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/common.model';
import { AuditLog } from '../models/audit-log.model';

export interface AuditLogFilter {
  page?: number;
  pageSize?: number;
  action?: string;
  entityName?: string;
  userId?: number;
  fromDate?: string;
  toDate?: string;
}

@Injectable({ providedIn: 'root' })
export class AuditLogService {
  private readonly base = `${environment.apiUrl}/auditlogs`;

  constructor(private http: HttpClient) {}

  getAll(filter: AuditLogFilter = {}): Observable<PagedResult<AuditLog>> {
    let params = new HttpParams()
      .set('page', filter.page ?? 1)
      .set('pageSize', filter.pageSize ?? 20);

    if (filter.action) params = params.set('action', filter.action);
    if (filter.entityName) params = params.set('entityName', filter.entityName);
    if (filter.userId) params = params.set('userId', filter.userId);
    if (filter.fromDate) params = params.set('fromDate', filter.fromDate);
    if (filter.toDate) params = params.set('toDate', filter.toDate);

    return this.http.get<PagedResult<AuditLog>>(this.base, { params });
  }
}
