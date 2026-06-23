import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Route, RouteFilter, RouteListStats, CreateRouteRequest, UpdateRouteRequest } from '../models/route.model';
import { PagedResult } from '../models/common.model';

@Injectable({ providedIn: 'root' })
export class RouteService {
  private readonly base = `${environment.apiUrl}/routes`;

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 10, filter?: RouteFilter): Observable<PagedResult<Route>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (filter?.search)       params = params.set('search', filter.search);
    if (filter?.isActive != null) params = params.set('isActive', filter.isActive);
    if (filter?.distanceBand) params = params.set('distanceBand', filter.distanceBand);
    if (filter?.priceBand)    params = params.set('priceBand', filter.priceBand);
    return this.http.get<PagedResult<Route>>(this.base, { params });
  }

  getStats(filter?: Omit<RouteFilter, 'distanceBand'>): Observable<RouteListStats> {
    let params = new HttpParams();
    if (filter?.search)       params = params.set('search', filter.search);
    if (filter?.isActive != null) params = params.set('isActive', filter.isActive);
    if (filter?.priceBand)    params = params.set('priceBand', filter.priceBand);
    return this.http.get<RouteListStats>(`${this.base}/stats`, { params });
  }

  getById(id: number): Observable<Route> {
    return this.http.get<Route>(`${this.base}/${id}`);
  }

  create(request: CreateRouteRequest): Observable<number> {
    return this.http.post<number>(this.base, request);
  }

  update(request: UpdateRouteRequest): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${request.id}`, request);
  }

  delete(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/${id}`);
  }
}
