import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { catchError, of } from 'rxjs';
import { FleetAssignmentService } from '../../../../core/services/fleet-assignment.service';
import { FleetAssignmentChangelog } from '../../../../core/models/fleet-assignment.model';

@Component({
  selector: 'assignment-history-panel',
  standalone: true,
  imports: [DatePipe, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="history-panel">
      <h4 class="history-title"><mat-icon>history</mat-icon> Assignment History</h4>
      @if (loading()) {
        <p class="history-muted">Loading history…</p>
      } @else if (!entries().length) {
        <p class="history-muted">No history recorded.</p>
      } @else {
        <ul class="history-list">
          @for (e of entries(); track e.id) {
            <li class="history-item">
              <div class="history-dot"></div>
              <div class="history-body">
                <div class="history-header">
                  <strong>{{ e.actionType }}</strong>
                  <span class="history-date">{{ e.createdAt | date:'dd MMM yyyy HH:mm' }}</span>
                </div>
                @if (e.oldVehicleName || e.newVehicleName) {
                  <p class="history-line">Vehicle: {{ e.oldVehicleName || '—' }} → {{ e.newVehicleName || '—' }}</p>
                }
                @if (e.oldDriverName || e.newDriverName) {
                  <p class="history-line">Driver: {{ e.oldDriverName || '—' }} → {{ e.newDriverName || '—' }}</p>
                }
                @if (e.reason) { <p class="history-reason">{{ e.reason }}</p> }
                <p class="history-by">By {{ e.createdBy || 'system' }}</p>
              </div>
            </li>
          }
        </ul>
      }
    </div>
  `,
  styles: [`
    .history-panel { margin-top: 1rem; }
    .history-title { display: flex; align-items: center; gap: 0.375rem; font-size: 0.875rem; font-weight: 700; margin: 0 0 0.75rem; }
    .history-title mat-icon { font-size: 16px; width: 16px; height: 16px; }
    .history-muted { font-size: 0.8125rem; color: #94a3b8; }
    .history-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.75rem; }
    .history-item { display: flex; gap: 0.625rem; }
    .history-dot { width: 10px; height: 10px; border-radius: 50%; background: #0f766e; margin-top: 0.35rem; flex-shrink: 0; }
    .history-body { flex: 1; min-width: 0; }
    .history-header { display: flex; justify-content: space-between; gap: 0.5rem; font-size: 0.8125rem; }
    .history-date { color: #64748b; font-size: 0.75rem; }
    .history-line, .history-reason, .history-by { margin: 0.2rem 0 0; font-size: 0.75rem; color: #475569; }
    .history-by { color: #94a3b8; }
  `]
})
export class AssignmentHistoryPanelComponent {
  private readonly assignmentService = inject(FleetAssignmentService);

  readonly assignmentId = input<number | null>(null);
  readonly loading = signal(false);
  readonly entries = signal<FleetAssignmentChangelog[]>([]);

  constructor() {
    effect(() => {
      const id = this.assignmentId();
      if (!id) {
        this.entries.set([]);
        return;
      }
      this.loading.set(true);
      this.assignmentService.changelog(id).pipe(catchError(() => of([]))).subscribe(rows => {
        this.entries.set(rows);
        this.loading.set(false);
      });
    });
  }
}
