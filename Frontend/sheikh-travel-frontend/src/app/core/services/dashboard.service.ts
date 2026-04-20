import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { DashboardSummary, RevenueReport, BookingReport } from '../models/common.model';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  constructor(private http: HttpClient) {}

  /** GET /api/dashboard/summary (envelope is unwrapped by ApiEnvelopeInterceptor). */
  getSummary(): Observable<DashboardSummary> {
    return this.http.get<DashboardSummary>(`${environment.apiUrl}/dashboard/summary`);
  }

  getRevenueReport(from: string, to: string): Observable<RevenueReport[]> {
    return this.http.get<RevenueReport[]>(`${environment.apiUrl}/reports/revenue`, {
      params: { from, to }
    });
  }

  getBookingStatusReport(): Observable<BookingReport[]> {
    return this.http.get<BookingReport[]>(`${environment.apiUrl}/reports/bookings-by-status`);
  }
}
