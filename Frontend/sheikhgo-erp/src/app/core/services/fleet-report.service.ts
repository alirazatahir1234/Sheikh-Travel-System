import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { FleetReport, FleetReportFilters } from '../models/fleet-report.model';

@Injectable({ providedIn: 'root' })
export class FleetReportService {
  private readonly base = `${environment.apiUrl}/fleet-reports`;

  constructor(private http: HttpClient) {}

  getReport(reportType: string, filters?: FleetReportFilters): Observable<FleetReport> {
    let params = new HttpParams().set('reportType', reportType);
    if (filters?.from) params = params.set('from', filters.from);
    if (filters?.to) params = params.set('to', filters.to);
    if (filters?.vehicleId) params = params.set('vehicleId', filters.vehicleId);
    if (filters?.driverId) params = params.set('driverId', filters.driverId);
    if (filters?.branchId) params = params.set('branchId', filters.branchId);
    if (filters?.departmentId) params = params.set('departmentId', filters.departmentId);
    if (filters?.status) params = params.set('status', filters.status);
    if (filters?.maintenanceReportType) params = params.set('maintenanceReportType', filters.maintenanceReportType);
    return this.http.get<FleetReport>(this.base, { params });
  }
}
