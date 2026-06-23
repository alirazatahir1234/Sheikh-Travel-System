import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { MaintenanceScheduleListItem } from '../../../../../core/models/maintenance.model';
import { ScheduleStatusBadgeComponent } from './schedule-status-badge.component';
import { ScheduleActionMenuComponent } from './schedule-action-menu.component';

@Component({
  selector: 'schedule-list-view',
  standalone: true,
  imports: [DatePipe, DecimalPipe, ScheduleStatusBadgeComponent, ScheduleActionMenuComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Vehicle</th>
            <th>Current Mileage</th>
            <th>Next Service Mileage</th>
            <th>Due Date</th>
            <th>Service Type</th>
            <th>Status</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          @for (s of schedules(); track s.id) {
            <tr>
              <td>
                <strong>{{ s.vehicleName }}</strong>
                @if (s.vehicleRegistration) {
                  <span class="muted">{{ s.vehicleRegistration }}</span>
                }
              </td>
              <td>{{ s.currentMileage | number:'1.0-0' }} km</td>
              <td>
                @if (s.nextServiceMileage != null) {
                  {{ s.nextServiceMileage | number:'1.0-0' }} km
                } @else {
                  —
                }
              </td>
              <td>{{ s.dueDate ? (s.dueDate | date:'mediumDate') : '—' }}</td>
              <td>{{ s.serviceTypeName }}</td>
              <td><schedule-status-badge [status]="s.status" /></td>
              <td>
                <schedule-action-menu
                  [schedule]="s"
                  (reschedule)="reschedule.emit($event)"
                  (createWorkOrder)="createWorkOrder.emit($event)" />
              </td>
            </tr>
          } @empty {
            <tr><td colspan="7" class="empty">No schedules match your filters.</td></tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: [`
    .table-wrap { overflow-x: auto; background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; }
    table { width: 100%; border-collapse: collapse; font-size: 0.8125rem; }
    th { text-align: left; color: #64748b; padding: 0.625rem 0.75rem; border-bottom: 1px solid #e2e8f0; font-weight: 600; }
    td { padding: 0.625rem 0.75rem; border-bottom: 1px solid #f1f5f9; vertical-align: middle; }
    .muted { display: block; font-size: 0.75rem; color: #94a3b8; font-weight: 400; }
    .empty { text-align: center; color: #94a3b8; padding: 2rem !important; }
  `]
})
export class ScheduleListViewComponent {
  readonly schedules = input.required<MaintenanceScheduleListItem[]>();
  readonly reschedule = output<MaintenanceScheduleListItem>();
  readonly createWorkOrder = output<MaintenanceScheduleListItem>();
}
