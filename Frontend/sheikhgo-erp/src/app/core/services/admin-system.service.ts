import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface ResetDatabaseAvailability {
  available: boolean;
  environment?: string;
  reason?: string;
}

export interface ResetDatabaseResult {
  success: boolean;
  message: string;
  deletedCompanies: number;
  deletedUsers: number;
  deletedTrips: number;
  deletedVehicles: number;
  deletedBookings?: number;
  deletedDrivers?: number;
  deletedCustomers?: number;
  environment?: string;
  performedBy?: number;
  performedAt?: string;
}

@Injectable({ providedIn: 'root' })
export class AdminSystemService {
  private readonly base = `${environment.apiUrl}/admin/system`;

  constructor(private http: HttpClient) {}

  getResetAvailability(): Observable<ResetDatabaseAvailability> {
    return this.http.get<ResetDatabaseAvailability>(`${this.base}/reset-database/availability`);
  }

  resetDatabase(confirmation: string): Observable<ResetDatabaseResult> {
    return this.http.post<ResetDatabaseResult>(`${this.base}/reset-database`, { confirmation });
  }
}
