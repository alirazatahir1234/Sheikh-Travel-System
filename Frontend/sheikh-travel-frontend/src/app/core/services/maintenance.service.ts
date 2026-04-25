import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/common.model';
import {
  Maintenance,
  CreateMaintenanceRequest,
  UpdateMaintenanceStatusRequest,
  MaintenanceStatus
} from '../models/maintenance.model';

@Injectable({ providedIn: 'root' })
export class MaintenanceService {
  private readonly base = `${environment.apiUrl}/maintenance`;

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 20): Observable<PagedResult<Maintenance>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<Maintenance>>(this.base, { params });
  }

  create(request: CreateMaintenanceRequest): Observable<number> {
    return this.http.post<number>(this.base, request);
  }

  updateStatus(request: UpdateMaintenanceStatusRequest): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${request.id}/status`, { status: request.status });
  }
}
