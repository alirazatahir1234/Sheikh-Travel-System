import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Invoice, InvoiceDetail, InvoiceFilter } from '../models/invoice.model';
import { PagedResult } from '../models/common.model';

@Injectable({ providedIn: 'root' })
export class InvoiceService {
  private readonly base = `${environment.apiUrl}/invoices`;

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 20, filter?: InvoiceFilter): Observable<PagedResult<Invoice>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (filter?.status) params = params.set('status', filter.status);
    if (filter?.bookingId) params = params.set('bookingId', filter.bookingId);
    if (filter?.customerId) params = params.set('customerId', filter.customerId);
    return this.http.get<PagedResult<Invoice>>(this.base, { params });
  }

  getById(id: number): Observable<InvoiceDetail> {
    return this.http.get<InvoiceDetail>(`${this.base}/${id}`);
  }

  issueFromBooking(bookingId: number): Observable<number> {
    return this.http.post<number>(`${this.base}/from-booking/${bookingId}`, {});
  }

  void(id: number): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/${id}/void`, {});
  }

  markPaid(id: number): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/${id}/mark-paid`, {});
  }
}
