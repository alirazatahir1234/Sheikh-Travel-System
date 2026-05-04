import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { VehicleProfitDto, DriverPerformanceDto, PaymentReportDto } from '../models/common.model';

@Injectable({ providedIn: 'root' })
export class ReportService {
  private readonly reportsBase = `${environment.apiUrl}/reports`;
  private readonly paymentsBase = `${environment.apiUrl}/payments`;

  constructor(private http: HttpClient) {}

  getVehicleProfit(fromDate?: string, toDate?: string, vehicleId?: number): Observable<VehicleProfitDto[]> {
    let params = new HttpParams();
    if (fromDate) params = params.set('fromDate', fromDate);
    if (toDate) params = params.set('toDate', toDate);
    if (vehicleId != null) params = params.set('vehicleId', vehicleId);
    return this.http.get<VehicleProfitDto[]>(`${this.reportsBase}/vehicle-profit`, { params });
  }

  getDriverPerformance(fromDate?: string, toDate?: string): Observable<DriverPerformanceDto[]> {
    let params = new HttpParams();
    if (fromDate) params = params.set('fromDate', fromDate);
    if (toDate) params = params.set('toDate', toDate);
    return this.http.get<DriverPerformanceDto[]>(`${this.reportsBase}/driver-performance`, { params });
  }

  getPaymentReport(fromDate?: string, toDate?: string): Observable<PaymentReportDto> {
    let params = new HttpParams();
    if (fromDate) params = params.set('fromDate', fromDate);
    if (toDate) params = params.set('toDate', toDate);
    return this.http.get<PaymentReportDto>(`${this.paymentsBase}/report`, { params });
  }
}
