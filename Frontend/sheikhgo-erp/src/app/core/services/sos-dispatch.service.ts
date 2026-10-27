import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/common.model';

export interface SosEventRow {
  id: number;
  createdAt: string;
  status: string;
  driverId: number;
  driverName: string;
  driverPhone?: string;
  vehicleId?: number;
  latitude?: number;
  longitude?: number;
  speedKmh?: number;
  lastRouteSummary?: string;
  regionCode?: string;
  dispatchMode?: string;
  assignedResponderDriverId?: number;
  assignedTeamName?: string;
  distanceKm?: number;
  etaMinutes?: number;
  confirmationMessage?: string;
  suggestedPartnerId?: number;
  suggestedPartnerName?: string;
  message?: string;
}

export interface SosRecoveryPartner {
  id: number;
  regionCode: string;
  partnerType: string;
  name: string;
  phone: string;
  email?: string;
  notes?: string;
  isActive: boolean;
}

export interface UpsertSosPartnerPayload {
  regionCode: string;
  partnerType: string;
  name: string;
  phone: string;
  email?: string;
  notes?: string;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class SosDispatchService {
  private readonly base = `${environment.apiUrl}/sos-dispatch`;

  constructor(private http: HttpClient) {}

  listEvents(status?: string, limit = 50): Observable<SosEventRow[]> {
    const params: Record<string, string> = { limit: String(limit) };
    if (status) params['status'] = status;
    return this.http
      .get<ApiResponse<SosEventRow[]>>(`${this.base}/events`, { params })
      .pipe(map(r => r.data ?? []));
  }

  resolveEvent(id: number): Observable<boolean> {
    return this.http
      .post<ApiResponse<boolean>>(`${this.base}/events/${id}/resolve`, {})
      .pipe(map(r => r.success));
  }

  listPartners(): Observable<SosRecoveryPartner[]> {
    return this.http
      .get<ApiResponse<SosRecoveryPartner[]>>(`${this.base}/partners`)
      .pipe(map(r => r.data ?? []));
  }

  savePartner(body: UpsertSosPartnerPayload, id?: number): Observable<number> {
    if (id) {
      return this.http
        .put<ApiResponse<number>>(`${this.base}/partners/${id}`, body)
        .pipe(map(r => r.data ?? id));
    }
    return this.http
      .post<ApiResponse<number>>(`${this.base}/partners`, body)
      .pipe(map(r => r.data ?? 0));
  }

  deletePartner(id: number): Observable<boolean> {
    return this.http
      .delete<ApiResponse<boolean>>(`${this.base}/partners/${id}`)
      .pipe(map(r => r.success));
  }
}
