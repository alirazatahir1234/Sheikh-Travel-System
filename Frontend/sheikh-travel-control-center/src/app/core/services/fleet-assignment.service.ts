import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/common.model';
import {
  CancelAssignmentRequest,
  CompleteAssignmentRequest,
  CreateAssignmentRequest,
  FleetAssignment,
  FleetAssignmentChangelog,
  FleetAssignmentFilters,
  FleetAssignmentStats,
  TransferAssignmentRequest
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
    if (filters.dateFrom)       params = params.set('dateFrom', filters.dateFrom);
    if (filters.dateTo)         params = params.set('dateTo', filters.dateTo);

    return this.http.get<AssignmentPage>(this.base, { params });
  }

  stats(): Observable<FleetAssignmentStats> {
    return this.http.get<FleetAssignmentStats>(`${this.base}/stats`);
  }

  changelog(id: number): Observable<FleetAssignmentChangelog[]> {
    return this.http.get<FleetAssignmentChangelog[]>(`${this.base}/${id}/changelog`);
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
}
