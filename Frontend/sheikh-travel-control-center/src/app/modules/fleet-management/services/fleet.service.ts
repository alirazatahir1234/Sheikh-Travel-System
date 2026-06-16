import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import {
  AssignmentRow,
  ComplianceDocument,
  FleetDashboardSummary,
  InspectionRow
} from '../models/fleet.model';

interface ApiEnvelope<T> {
  success?: boolean;
  data?: T;
}

/**
 * Backend access for the fleet hub. Each call gracefully falls back to a
 * representative empty/demo dataset so screens render while the matching
 * API endpoints are wired up incrementally (mirrors SettingsService).
 */
@Injectable({ providedIn: 'root' })
export class FleetService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/fleet`;

  getDashboard(): Observable<FleetDashboardSummary> {
    return this.http.get<ApiEnvelope<FleetDashboardSummary> | FleetDashboardSummary>(`${this.base}/dashboard`).pipe(
      map(unwrap),
      catchError(() => of(DEMO_DASHBOARD))
    );
  }

  getComplianceDocuments(): Observable<ComplianceDocument[]> {
    return this.http.get<ApiEnvelope<ComplianceDocument[]> | ComplianceDocument[]>(`${this.base}/compliance`).pipe(
      map(unwrap),
      catchError(() => of(DEMO_COMPLIANCE))
    );
  }

  getInspections(): Observable<InspectionRow[]> {
    return this.http.get<ApiEnvelope<InspectionRow[]> | InspectionRow[]>(`${this.base}/inspections`).pipe(
      map(unwrap),
      catchError(() => of(DEMO_INSPECTIONS))
    );
  }

  getAssignments(): Observable<AssignmentRow[]> {
    return this.http.get<ApiEnvelope<AssignmentRow[]> | AssignmentRow[]>(`${this.base}/assignments`).pipe(
      map(unwrap),
      catchError(() => of(DEMO_ASSIGNMENTS))
    );
  }
}

function unwrap<T>(res: ApiEnvelope<T> | T): T {
  const env = res as ApiEnvelope<T>;
  return (env && typeof env === 'object' && 'data' in env && env.data !== undefined) ? env.data as T : (res as T);
}

const DEMO_DASHBOARD: FleetDashboardSummary = {
  totalVehicles: 0,
  activeVehicles: 0,
  driversOnDuty: 0,
  maintenanceDue: 0,
  monthlyFuelCost: 0,
  complianceAlerts: 0
};

const DEMO_COMPLIANCE: ComplianceDocument[] = [];
const DEMO_INSPECTIONS: InspectionRow[] = [];
const DEMO_ASSIGNMENTS: AssignmentRow[] = [];
