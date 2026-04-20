import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, DashboardSummary, RevenueReport, BookingReport } from '../models/common.model';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  constructor(private http: HttpClient) {}

  /**
   * Fetches the dashboard KPI summary.
   * Backend endpoint: GET /api/dashboard/summary — returns ApiResponse<DashboardSummaryDto>.
   */
  getSummary(): Observable<DashboardSummary> {
    return this.http
      .get<ApiResponse<DashboardSummary>>(`${environment.apiUrl}/dashboard/summary`)
      .pipe(map(res => res.data));
  }

  getRevenueReport(from: string, to: string): Observable<RevenueReport[]> {
    return this.http
      .get<ApiResponse<RevenueReport[]> | RevenueReport[]>(`${environment.apiUrl}/reports/revenue`, {
        params: { from, to }
      })
      .pipe(map(unwrap<RevenueReport[]>));
  }

  getBookingStatusReport(): Observable<BookingReport[]> {
    return this.http
      .get<ApiResponse<BookingReport[]> | BookingReport[]>(`${environment.apiUrl}/reports/bookings-by-status`)
      .pipe(map(unwrap<BookingReport[]>));
  }
}

function unwrap<T>(res: ApiResponse<T> | T): T {
  return (res as ApiResponse<T>)?.data !== undefined ? (res as ApiResponse<T>).data : (res as T);
}
