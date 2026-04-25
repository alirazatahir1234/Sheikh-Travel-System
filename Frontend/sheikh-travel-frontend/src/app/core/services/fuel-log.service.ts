import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/common.model';
import { FuelLog, CreateFuelLogRequest } from '../models/fuel-log.model';

@Injectable({ providedIn: 'root' })
export class FuelLogService {
  private readonly base = `${environment.apiUrl}/fuellogs`;

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 20): Observable<PagedResult<FuelLog>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<FuelLog>>(this.base, { params });
  }

  create(request: CreateFuelLogRequest): Observable<number> {
    return this.http.post<number>(this.base, request);
  }
}
