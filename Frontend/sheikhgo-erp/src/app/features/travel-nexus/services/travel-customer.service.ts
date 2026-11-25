import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { PagedResult, normalizePagedResult } from '../../../core/models/common.model';
import {
  CreateTravelCustomer, SaveTravelCustomer, SaveTravelTraveler, TravelCustomer,
  TravelCustomerListItem, TravelCustomerQuery, TravelTraveler
} from '../models/travel.models';

/** Travel customers + travelers API (`api/travel/customers`, `api/travel/travelers`). */
@Injectable({ providedIn: 'root' })
export class TravelCustomerService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/travel/customers`;
  private readonly travelersBase = `${environment.apiUrl}/travel/travelers`;

  getCustomers(q: TravelCustomerQuery): Observable<PagedResult<TravelCustomerListItem>> {
    let params = new HttpParams().set('page', q.page).set('pageSize', q.pageSize);
    if (q.search?.trim()) params = params.set('search', q.search.trim());
    if (q.customerKind) params = params.set('customerKind', q.customerKind);
    return this.http
      .get<PagedResult<TravelCustomerListItem>>(this.base, { params })
      .pipe(map(r => normalizePagedResult(r)));
  }

  getCustomer(customerId: number): Observable<TravelCustomer> {
    return this.http.get<TravelCustomer>(`${this.base}/${customerId}`);
  }

  createCustomer(dto: CreateTravelCustomer): Observable<number> {
    return this.http.post<number>(this.base, dto);
  }

  updateCustomer(customerId: number, dto: SaveTravelCustomer): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${customerId}`, dto);
  }

  archiveCustomer(customerId: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/${customerId}`);
  }

  getTravelers(customerId: number): Observable<TravelTraveler[]> {
    return this.http.get<TravelTraveler[]>(`${this.base}/${customerId}/travelers`);
  }

  createTraveler(customerId: number, dto: SaveTravelTraveler): Observable<number> {
    return this.http.post<number>(`${this.base}/${customerId}/travelers`, dto);
  }

  updateTraveler(id: number, dto: SaveTravelTraveler): Observable<boolean> {
    return this.http.put<boolean>(`${this.travelersBase}/${id}`, dto);
  }

  deleteTraveler(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.travelersBase}/${id}`);
  }
}
