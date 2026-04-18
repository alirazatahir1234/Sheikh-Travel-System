import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Payment, CreatePaymentRequest } from '../models/payment.model';
import { ApiResponse, PagedResult } from '../models/common.model';

@Injectable({ providedIn: 'root' })
export class PaymentService {
  private readonly base = `${environment.apiUrl}/payments`;

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 10): Observable<PagedResult<Payment>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<Payment>>(this.base, { params });
  }

  getByBookingId(bookingId: number): Observable<Payment[]> {
    return this.http.get<Payment[]>(`${this.base}/booking/${bookingId}`);
  }

  create(request: CreatePaymentRequest): Observable<Payment> {
    return this.http.post<ApiResponse<Payment>>(this.base, request).pipe(map(res => res.data));
  }
}
