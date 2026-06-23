import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ScheduleStatus, ScheduleStatusLabels } from '../../../../../core/models/maintenance.model';

@Component({
  selector: 'schedule-status-badge',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="badge badge--{{ status() }}">{{ label() }}</span>`,
  styles: [`
    .badge {
      display: inline-block;
      padding: 0.2rem 0.5rem;
      border-radius: 999px;
      font-size: 0.6875rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.02em;
    }
    .badge--Upcoming { background: #d1fae5; color: #0B6B50; }
    .badge--DueSoon { background: #fef3c7; color: #b45309; }
    .badge--Overdue { background: #fee2e2; color: #DC2626; }
  `]
})
export class ScheduleStatusBadgeComponent {
  readonly status = input.required<ScheduleStatus>();

  label(): string {
    return ScheduleStatusLabels[this.status()] ?? this.status();
  }
}
