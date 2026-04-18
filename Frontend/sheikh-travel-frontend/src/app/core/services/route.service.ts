import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Route, CreateRouteRequest, UpdateRouteRequest } from '../models/route.model';
import { ApiResponse, PagedResult } from '../models/common.model';

@Injectable({ providedIn: 'root' })
export class RouteService {
  private readonly base = `${environment.apiUrl}/routes`;

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 10): Observable<PagedResult<Route>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<Route>>(this.base, { params });
  }

  getById(id: number): Observable<Route> {
    return this.http.get<Route>(`${this.base}/${id}`);
  }

  create(request: CreateRouteRequest): Observable<Route> {
    return this.http.post<ApiResponse<Route>>(this.base, request).pipe(map(res => res.data));
  }

  update(request: UpdateRouteRequest): Observable<Route> {
    return this.http.put<ApiResponse<Route>>(`${this.base}/${request.id}`, request).pipe(map(res => res.data));
  }

  delete(id: number): Observable<boolean> {
    return this.http.delete<ApiResponse<boolean>>(`${this.base}/${id}`).pipe(map(res => res.data));
  }
}
