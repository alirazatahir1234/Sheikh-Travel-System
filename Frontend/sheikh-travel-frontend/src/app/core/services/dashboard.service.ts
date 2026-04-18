import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { DashboardStats, RevenueReport, BookingReport } from '../models/common.model';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  constructor(private http: HttpClient) {}

  getStats(): Observable<DashboardStats> {
    return this.http.get<DashboardStats>(`${environment.apiUrl}/dashboard/stats`);
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
