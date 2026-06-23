import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { catchError, of } from 'rxjs';
import { FleetAssignmentService } from '../../../../core/services/fleet-assignment.service';
import { AssignmentCalendarItem } from '../../../../core/models/fleet-assignment.model';

@Component({
  selector: 'assignment-calendar',
  standalone: true,
  imports: [DatePipe, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="calendar-page">
      <h2 class="title"><mat-icon>calendar_month</mat-icon> Assignment Calendar</h2>
      @if (loading()) {
        <p class="muted">Loading…</p>
      } @else if (!items().length) {
        <p class="muted">No assignments in this range.</p>
      } @else {
        <ul class="calendar-list">
          @for (item of items(); track item.id) {
            <li class="calendar-item">
              <span class="date">{{ item.startAt | date:'dd MMM yyyy' }}</span>
              <div>
                <strong>{{ item.assignmentNo }}</strong> — {{ item.vehicleName }}
                @if (item.driverName) { · {{ item.driverName }} }
                <span class="status">{{ item.status }}</span>
              </div>
            </li>
          }
        </ul>
      }
    </div>
  `,
  styles: [`
    .calendar-page { padding: 1rem; }
    .title { display: flex; align-items: center; gap: 0.5rem; font-size: 1.125rem; margin: 0 0 1rem; }
    .calendar-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.625rem; }
    .calendar-item { display: grid; grid-template-columns: 110px 1fr; gap: 0.75rem; padding: 0.75rem; border: 1px solid #e2e8f0; border-radius: 8px; background: #fff; font-size: 0.8125rem; }
    .date { font-weight: 700; color: #0f766e; }
    .status { margin-left: 0.5rem; font-size: 0.6875rem; font-weight: 700; color: #64748b; text-transform: uppercase; }
    .muted { color: #94a3b8; }
  `]
})
export class AssignmentCalendarComponent implements OnInit {
  private readonly assignmentService = inject(FleetAssignmentService);

  readonly loading = signal(true);
  readonly items = signal<AssignmentCalendarItem[]>([]);

  ngOnInit(): void {
    const from = new Date();
    const to = new Date();
    to.setDate(to.getDate() + 30);
    this.assignmentService.calendar(from.toISOString(), to.toISOString()).pipe(
      catchError(() => of([]))
    ).subscribe(rows => {
      this.items.set(rows);
      this.loading.set(false);
    });
  }
}
