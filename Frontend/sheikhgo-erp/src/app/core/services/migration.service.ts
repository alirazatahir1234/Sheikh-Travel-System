import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface SchemaMigrationStatus {
  name: string;
  order: number;
  isApplied: boolean;
  appliedAtUtc?: string | null;
  appliedBy?: string | null;
}

export interface SchemaMigrationApplyResult {
  appliedCount: number;
  skippedCount: number;
  appliedNames: string[];
  failedMigration?: string | null;
  errorMessage?: string | null;
}

@Injectable({ providedIn: 'root' })
export class MigrationService {
  private readonly base = `${environment.apiUrl}/migrations`;

  constructor(private http: HttpClient) {}

  getStatus(): Observable<SchemaMigrationStatus[]> {
    return this.http.get<SchemaMigrationStatus[]>(this.base);
  }

  applyPending(): Observable<SchemaMigrationApplyResult> {
    return this.http.post<SchemaMigrationApplyResult>(`${this.base}/apply-pending`, {});
  }
}
