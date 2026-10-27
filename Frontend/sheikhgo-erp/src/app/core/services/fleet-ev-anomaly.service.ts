import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/common.model';

export interface EvFleetDashboard {
  electricVehicleCount: number;
  rangeAlertCount: number;
  averageBatteryPercent?: number | null;
  vehicles: EvFleetVehicleRow[];
  chargingStations: ChargingStation[];
}

export interface EvFleetVehicleRow {
  vehicleId: number;
  vehicleName: string;
  registrationNumber: string;
  batteryPercent?: number | null;
  electricRangeKm?: number | null;
  estimatedRangeKm?: number | null;
  latitude?: number | null;
  longitude?: number | null;
  rangeAlert: boolean;
}

export interface ChargingStation {
  id: number;
  name: string;
  latitude: number;
  longitude: number;
  address?: string | null;
  connectorType?: string | null;
  isActive: boolean;
}

export interface FleetAnomalyEvent {
  id: number;
  vehicleId: number;
  vehicleName?: string | null;
  driverId?: number | null;
  driverName?: string | null;
  anomalyType: string;
  severity: string;
  latitude?: number | null;
  longitude?: number | null;
  speedKmh?: number | null;
  details?: string | null;
  isReviewed: boolean;
  createdAtUtc: string;
  reviewNotes?: string | null;
}

export interface VehicleDocumentTrackerItem {
  complianceDocumentId?: number | null;
  vehicleId: number;
  vehicleName: string;
  documentType: string;
  expiryDate?: string | null;
  status: string;
  source: string;
}

@Injectable({ providedIn: 'root' })
export class FleetEvAnomalyService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/fleet`;

  getEvDashboard(): Observable<EvFleetDashboard> {
    return this.http
      .get<ApiResponse<EvFleetDashboard>>(`${this.base}/ev/dashboard`)
      .pipe(map((r) => r.data!));
  }

  listChargingStations(): Observable<ChargingStation[]> {
    return this.http
      .get<ApiResponse<ChargingStation[]>>(`${this.base}/ev/charging-stations`)
      .pipe(map((r) => r.data ?? []));
  }

  saveChargingStation(body: Partial<ChargingStation> & { name: string; latitude: number; longitude: number }): Observable<number> {
    return this.http
      .post<ApiResponse<number>>(`${this.base}/ev/charging-stations`, body)
      .pipe(map((r) => r.data!));
  }

  listAnomalies(unreviewedOnly = false, vehicleId?: number): Observable<FleetAnomalyEvent[]> {
    const params: Record<string, string> = {};
    if (unreviewedOnly) params['unreviewedOnly'] = 'true';
    if (vehicleId) params['vehicleId'] = String(vehicleId);
    return this.http
      .get<ApiResponse<FleetAnomalyEvent[]>>(`${this.base}/anomalies`, { params })
      .pipe(map((r) => r.data ?? []));
  }

  reviewAnomaly(id: number, notes?: string): Observable<boolean> {
    return this.http
      .post<ApiResponse<boolean>>(`${this.base}/anomalies/${id}/review`, { notes: notes ?? null })
      .pipe(map((r) => r.data ?? false));
  }

  getDocumentTracker(): Observable<VehicleDocumentTrackerItem[]> {
    return this.http
      .get<ApiResponse<VehicleDocumentTrackerItem[]>>(`${this.base}/documents/tracker`)
      .pipe(map((r) => r.data ?? []));
  }

  upsertDocument(body: {
    id?: number | null;
    vehicleId: number;
    documentType: string;
    documentNumber?: string | null;
    issuedDate?: string | null;
    expiryDate?: string | null;
    fileUrl?: string | null;
    notes?: string | null;
  }): Observable<number> {
    return this.http
      .post<ApiResponse<number>>(`${this.base}/documents/tracker`, body)
      .pipe(map((r) => r.data!));
  }
}
