import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/common.model';

export interface FatigueEvent {
  id: number;
  tenantId: number;
  tenantName?: string | null;
  driverId: number;
  driverName: string;
  driverPhone?: string | null;
  vehicleId?: number | null;
  vehicleName?: string | null;
  source: string;
  signalType: string;
  confidence?: number | null;
  earScore?: number | null;
  expressionHint?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  status: string;
  escalationStage: number;
  ackDeadlineUtc: string;
  acknowledgedAtUtc?: string | null;
  createdAtUtc: string;
  lastEscalationNote?: string | null;
  driverRiskScore?: number | null;
  compositeRiskScore?: number | null;
  supportingSignalsJson?: string | null;
}

export interface FatigueSettings {
  ackWindowSeconds: number;
  stageIntervalSeconds: number;
  autoStopEnabled: boolean;
}

@Injectable({ providedIn: 'root' })
export class FatigueDetectionService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/fatigue-detection`;

  listEvents(status?: string, limit = 50): Observable<FatigueEvent[]> {
    let params = new HttpParams().set('limit', limit);
    if (status) params = params.set('status', status);
    return this.http
      .get<ApiResponse<FatigueEvent[]>>(`${this.base}/events`, { params })
      .pipe(map((r) => r.data ?? []));
  }

  listPlatformEvents(tenantId?: number, status?: string, limit = 100): Observable<FatigueEvent[]> {
    let params = new HttpParams().set('limit', limit);
    if (tenantId != null) params = params.set('tenantId', tenantId);
    if (status) params = params.set('status', status);
    return this.http
      .get<ApiResponse<FatigueEvent[]>>(`${this.base}/platform/events`, { params })
      .pipe(map((r) => r.data ?? []));
  }

  resolve(id: number): Observable<boolean> {
    return this.http
      .post<ApiResponse<boolean>>(`${this.base}/events/${id}/resolve`, {})
      .pipe(map((r) => r.success));
  }

  getSettings(): Observable<FatigueSettings> {
    return this.http
      .get<ApiResponse<FatigueSettings>>(`${this.base}/settings`)
      .pipe(map((r) => r.data!));
  }

  saveSettings(body: FatigueSettings): Observable<FatigueSettings> {
    return this.http
      .put<ApiResponse<FatigueSettings>>(`${this.base}/settings`, body)
      .pipe(map((r) => r.data!));
  }
}
