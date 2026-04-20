import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Driver, CreateDriverRequest, UpdateDriverRequest } from '../models/driver.model';
import { PagedResult } from '../models/common.model';

@Injectable({ providedIn: 'root' })
export class DriverService {
  private readonly base = `${environment.apiUrl}/drivers`;

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 10): Observable<PagedResult<Driver>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<Driver>>(this.base, { params });
  }

  getById(id: number): Observable<Driver> {
    return this.http.get<Driver>(`${this.base}/${id}`);
  }

  create(request: CreateDriverRequest): Observable<Driver> {
    return this.http.post<Driver>(this.base, request);
  }

  update(request: UpdateDriverRequest): Observable<Driver> {
    return this.http.put<Driver>(`${this.base}/${request.id}`, request);
  }

  delete(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/${id}`);
  }
}
