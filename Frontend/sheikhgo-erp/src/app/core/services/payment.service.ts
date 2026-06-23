import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Payment, PaymentDetail, CreatePaymentRequest, UpdatePaymentStatusRequest, PaymentFilter } from '../models/payment.model';
import { PagedResult } from '../models/common.model';

@Injectable({ providedIn: 'root' })
export class PaymentService {
  private readonly base = `${environment.apiUrl}/payments`;

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 10, filter?: PaymentFilter): Observable<PagedResult<Payment>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (filter?.bookingId)  params = params.set('bookingId', filter.bookingId);
    if (filter?.status)     params = params.set('status',    filter.status);
    if (filter?.dateFrom)   params = params.set('dateFrom',  filter.dateFrom);
    if (filter?.dateTo)     params = params.set('dateTo',    filter.dateTo);
    return this.http.get<PagedResult<Payment>>(this.base, { params });
  }

  getById(id: number): Observable<PaymentDetail> {
    return this.http.get<PaymentDetail>(`${this.base}/${id}`);
  }

  getByBookingId(bookingId: number): Observable<Payment[]> {
    return this.http.get<Payment[]>(`${this.base}/booking/${bookingId}`);
  }

  create(request: CreatePaymentRequest): Observable<number> {
    return this.http.post<number>(this.base, { payment: request });
  }

  updateStatus(id: number, request: UpdatePaymentStatusRequest): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${id}/status`, request);
  }

  exportCsv(filter?: PaymentFilter): Observable<PagedResult<Payment>> {
    return this.getAll(1, 10000, filter);
  }
}

