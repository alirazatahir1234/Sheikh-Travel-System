import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/common.model';

export interface FuelFraudEvent {
  id: number;
  tenantId: number;
  tenantName?: string | null;
  vehicleId: number;
  vehicleName: string;
  vehicleRegistration?: string | null;
  driverId: number;
  driverName: string;
  orderedLiters: number;
  actualIncreaseLiters?: number | null;
  shortfallLiters?: number | null;
  fuelLevelAtOrderPercent?: number | null;
  fuelLevelAfterPercent?: number | null;
  stationName?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  status: string;
  gpsAlertEventId?: number | null;
  createdAtUtc: string;
  verifiedAtUtc?: string | null;
  resolvedAtUtc?: string | null;
  resolvedBy?: string | null;
}

@Injectable({ providedIn: 'root' })
export class FuelFraudService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/fuel-fraud`;

  listEvents(status?: string, limit = 50): Observable<FuelFraudEvent[]> {
    let params = new HttpParams().set('limit', limit);
    if (status) params = params.set('status', status);
    return this.http
      .get<ApiResponse<FuelFraudEvent[]>>(`${this.base}/events`, { params })
      .pipe(map((r) => r.data ?? []));
  }

  listPlatformEvents(tenantId?: number, status?: string, limit = 100): Observable<FuelFraudEvent[]> {
    let params = new HttpParams().set('limit', limit);
    if (tenantId != null) params = params.set('tenantId', tenantId);
    if (status) params = params.set('status', status);
    return this.http
      .get<ApiResponse<FuelFraudEvent[]>>(`${this.base}/platform/events`, { params })
      .pipe(map((r) => r.data ?? []));
  }

  resolve(id: number): Observable<boolean> {
    return this.http
      .post<ApiResponse<boolean>>(`${this.base}/events/${id}/resolve`, {})
      .pipe(map((r) => r.success));
  }
}
