import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface PermissionCoverageEndpoint {
  module: string;
  controller: string;
  action: string;
  httpMethod: string;
  route: string;
  requiredPermission: string | null;
  coverageStatus: string;
  notes: string | null;
}

export interface PermissionCoverageReport {
  totalEndpoints: number;
  protectedCount: number;
  partiallyProtectedCount: number;
  publicCount: number;
  internalCount: number;
  endpoints: PermissionCoverageEndpoint[];
}

@Injectable({ providedIn: 'root' })
export class PermissionCoverageService {
  private readonly base = `${environment.apiUrl}/platform/permission-coverage`;

  constructor(private http: HttpClient) {}

  getCoverage(): Observable<PermissionCoverageReport> {
    return this.http.get<PermissionCoverageReport>(this.base);
  }
}
