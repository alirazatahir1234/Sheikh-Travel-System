import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  Customer,
  CustomerFilter,
  CustomerListStats,
  CreateCustomerRequest,
  UpdateCustomerRequest
} from '../models/customer.model';
import { PagedResult } from '../models/common.model';

@Injectable({ providedIn: 'root' })
export class CustomerService {
  private readonly base = `${environment.apiUrl}/customers`;

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 10, filter?: CustomerFilter): Observable<PagedResult<Customer>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (filter?.search)   params = params.set('search', filter.search);
    if (filter?.isActive != null) params = params.set('isActive', filter.isActive);
    if (filter?.recency)  params = params.set('recency', filter.recency);
    return this.http.get<PagedResult<Customer>>(this.base, { params });
  }

  getStats(filter?: Omit<CustomerFilter, 'recency'>): Observable<CustomerListStats> {
    let params = new HttpParams();
    if (filter?.search)   params = params.set('search', filter.search);
    if (filter?.isActive != null) params = params.set('isActive', filter.isActive);
    return this.http.get<{ total: number; new: number; returning: number }>(`${this.base}/stats`, { params }).pipe(
      map(s => ({ total: s.total, newCount: s.new, returning: s.returning }))
    );
  }

  getById(id: number): Observable<Customer> {
    return this.http.get<Customer>(`${this.base}/${id}`);
  }

  create(request: CreateCustomerRequest): Observable<number> {
    return this.http.post<number>(this.base, request);
  }

  update(request: UpdateCustomerRequest): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${request.id}`, request);
  }

  delete(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/${id}`);
  }
}
