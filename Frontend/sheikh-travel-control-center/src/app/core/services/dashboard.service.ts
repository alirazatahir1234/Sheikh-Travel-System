import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { DashboardSummary, BookingReportDto, RevenueReportDto, BookingReport } from '../models/common.model';

export type { RevenueReportDto } from '../models/common.model';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  constructor(private http: HttpClient) {}

  /** GET /api/dashboard/summary (envelope is unwrapped by ApiEnvelopeInterceptor). */
  getSummary(): Observable<DashboardSummary> {
    return this.http.get<DashboardSummary>(`${environment.apiUrl}/dashboard/summary`);
  }

  /**
   * GET /api/reports/revenue
   * Backend expects fromDate/toDate query params.
   */
  getRevenueReport(fromDate: string, toDate: string): Observable<RevenueReportDto> {
    return this.http.get<RevenueReportDto>(`${environment.apiUrl}/reports/revenue`, {
      params: { fromDate, toDate }
    });
  }

  /**
   * GET /api/reports/bookings
   * Backend returns BookingReportDto (single object with counts).
   * We convert to BookingReport[] for chart compatibility.
   */
  getBookingStatusReport(fromDate?: string, toDate?: string): Observable<BookingReport[]> {
    const params: Record<string, string> = {};
    if (fromDate) params['fromDate'] = fromDate;
    if (toDate) params['toDate'] = toDate;

    return this.http.get<BookingReportDto>(`${environment.apiUrl}/reports/bookings`, { params }).pipe(
      map(dto => {
        const total = dto.totalBookings || 1;
        return [
          { status: 'Completed', count: dto.completed, percentage: Math.round((dto.completed / total) * 100) },
          { status: 'Pending', count: dto.pending, percentage: Math.round((dto.pending / total) * 100) },
          { status: 'Active', count: dto.active, percentage: Math.round((dto.active / total) * 100) },
          { status: 'Cancelled', count: dto.cancelled, percentage: Math.round((dto.cancelled / total) * 100) }
        ];
      })
    );
  }

  /**
   * GET /api/reports/vehicle-profit
   */
  getVehicleProfitReport(fromDate?: string, toDate?: string, vehicleId?: number): Observable<VehicleProfitDto[]> {
    const params: Record<string, string> = {};
    if (fromDate) params['fromDate'] = fromDate;
    if (toDate) params['toDate'] = toDate;
    if (vehicleId) params['vehicleId'] = vehicleId.toString();
    return this.http.get<VehicleProfitDto[]>(`${environment.apiUrl}/reports/vehicle-profit`, { params });
  }

  /**
   * GET /api/reports/driver-performance
   */
  getDriverPerformanceReport(fromDate?: string, toDate?: string): Observable<DriverPerformanceDto[]> {
    const params: Record<string, string> = {};
    if (fromDate) params['fromDate'] = fromDate;
    if (toDate) params['toDate'] = toDate;
    return this.http.get<DriverPerformanceDto[]>(`${environment.apiUrl}/reports/driver-performance`, { params });
  }
}

export interface VehicleProfitDto {
  vehicleId: number;
  vehicleName: string;
  revenue: number;
  fuelCost: number;
  maintenanceCost: number;
  profit: number;
}

export interface DriverPerformanceDto {
  driverId: number;
  driverName: string;
  totalTrips: number;
  completedTrips: number;
  totalRevenue: number;
}
