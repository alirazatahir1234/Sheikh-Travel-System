import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MaintenanceScheduleListItem } from '../../../../../core/models/maintenance.model';

@Component({
  selector: 'schedule-action-menu',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="actions">
      <button type="button" class="actions__btn" title="Reschedule" (click)="reschedule.emit(schedule())">
        <mat-icon>event_repeat</mat-icon>
      </button>
      <button type="button" class="actions__btn" title="Create Work Order" (click)="createWorkOrder.emit(schedule())">
        <mat-icon>build</mat-icon>
      </button>
    </div>
  `,
  styles: [`
    .actions { display: flex; gap: 0.25rem; }
    .actions__btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 2rem;
      height: 2rem;
      border: 1px solid #e2e8f0;
      border-radius: 6px;
      background: #fff;
      color: #475569;
      cursor: pointer;
    }
    .actions__btn:hover { border-color: #0B6B50; color: #0B6B50; }
    .actions__btn mat-icon { font-size: 1rem; width: 1rem; height: 1rem; }
  `]
})
export class ScheduleActionMenuComponent {
  readonly schedule = input.required<MaintenanceScheduleListItem>();
  readonly reschedule = output<MaintenanceScheduleListItem>();
  readonly createWorkOrder = output<MaintenanceScheduleListItem>();
}
