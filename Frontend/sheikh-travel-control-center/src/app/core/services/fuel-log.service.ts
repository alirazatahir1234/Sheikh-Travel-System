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

  getById(id: number): Observable<FuelLog> {
    return this.http.get<FuelLog>(`${this.base}/${id}`);
  }

  create(request: CreateFuelLogRequest): Observable<number> {
    return this.http.post<number>(this.base, request);
  }

  update(id: number, request: CreateFuelLogRequest): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${id}`, request);
  }

  delete(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/${id}`);
  }
}
