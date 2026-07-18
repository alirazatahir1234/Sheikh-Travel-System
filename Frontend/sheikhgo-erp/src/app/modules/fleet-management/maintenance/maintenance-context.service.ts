import { Injectable, signal } from '@angular/core';
import { Subject } from 'rxjs';

/**
 * Shared toolbar state for Fleet Maintenance (search, period, export).
 * Provided by {@link MaintenanceShellComponent} so shell + active child share one instance.
 */
@Injectable()
export class MaintenanceContextService {
  readonly period = signal('Month');
  readonly searchTerm = signal('');
  readonly exportRequested$ = new Subject<void>();

  requestExport(): void {
    this.exportRequested$.next();
  }
}
