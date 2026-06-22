import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/common.model';
import {
  AssignmentCalendarItem,
  AssignmentUtilizationReport,
  AssignmentValidationResult,
  BulkAssignmentIdsRequest,
  BulkAssignmentResult,
  CancelAssignmentRequest,
  CompleteAssignmentRequest,
  CreateAssignmentRequest,
  FleetAssignment,
  FleetAssignmentChangelog,
  FleetAssignmentFilters,
  FleetAssignmentStats,
  TransferAssignmentRequest,
  ValidateAssignmentRequest
} from '../models/fleet-assignment.model';

export type AssignmentPage = PagedResult<FleetAssignment>;

@Injectable({ providedIn: 'root' })
export class FleetAssignmentService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/assignments`;

  list(filters: FleetAssignmentFilters, page = 1, pageSize = 20): Observable<AssignmentPage> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);

    if (filters.search)         params = params.set('search', filters.search);
    if (filters.status)         params = params.set('status', filters.status);
    if (filters.assignmentType) params = params.set('assignmentType', filters.assignmentType);
    if (filters.vehicleId)      params = params.set('vehicleId', filters.vehicleId);
    if (filters.driverId)       params = params.set('driverId', filters.driverId);
    if (filters.branchId)       params = params.set('branchId', filters.branchId);
    if (filters.departmentId)   params = params.set('departmentId', filters.departmentId);
    if (filters.dateFrom)       params = params.set('dateFrom', filters.dateFrom);
    if (filters.dateTo)         params = params.set('dateTo', filters.dateTo);

    return this.http.get<AssignmentPage>(this.base, { params });
  }

  stats(): Observable<FleetAssignmentStats> {
    return this.http.get<FleetAssignmentStats>(`${this.base}/stats`);
  }

  validate(body: ValidateAssignmentRequest): Observable<AssignmentValidationResult> {
    return this.http.post<AssignmentValidationResult>(`${this.base}/validate`, body);
  }

  changelog(id: number): Observable<FleetAssignmentChangelog[]> {
    return this.http.get<FleetAssignmentChangelog[]>(`${this.base}/${id}/changelog`);
  }

  calendar(from: string, to: string, view = 'vehicles', branchId?: number): Observable<AssignmentCalendarItem[]> {
    let params = new HttpParams().set('from', from).set('to', to).set('view', view);
    if (branchId) params = params.set('branchId', branchId);
    return this.http.get<AssignmentCalendarItem[]>(`${this.base}/calendar`, { params });
  }

  utilizationReport(): Observable<AssignmentUtilizationReport> {
    return this.http.get<AssignmentUtilizationReport>(`${this.base}/utilization-report`);
  }

  create(body: CreateAssignmentRequest): Observable<number> {
    return this.http.post<number>(this.base, body);
  }

  transfer(id: number, body: TransferAssignmentRequest): Observable<number> {
    return this.http.post<number>(`${this.base}/${id}/transfer`, body);
  }

  complete(id: number, body: CompleteAssignmentRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/complete`, body);
  }

  cancel(id: number, body: CancelAssignmentRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/cancel`, body);
  }

  approve(id: number, notes?: string | null): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/approve`, { notes: notes ?? null });
  }

  reject(id: number, reason: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/reject`, { reason });
  }

  bulkComplete(body: BulkAssignmentIdsRequest): Observable<BulkAssignmentResult> {
    return this.http.post<BulkAssignmentResult>(`${this.base}/bulk-complete`, body);
  }

  bulkCancel(body: BulkAssignmentIdsRequest): Observable<BulkAssignmentResult> {
    return this.http.post<BulkAssignmentResult>(`${this.base}/bulk-cancel`, body);
  }
}
