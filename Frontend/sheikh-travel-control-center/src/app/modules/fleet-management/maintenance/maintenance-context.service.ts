import { Injectable, signal } from '@angular/core';
import { Subject } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class MaintenanceContextService {
  readonly period = signal('Month');
  readonly searchTerm = signal('');
  readonly exportRequested$ = new Subject<void>();

  requestExport(): void {
    this.exportRequested$.next();
  }
}
