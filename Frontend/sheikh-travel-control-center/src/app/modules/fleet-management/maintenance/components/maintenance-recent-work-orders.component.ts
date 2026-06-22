import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { WorkOrderListItem, WorkOrderStatusLabels } from '../../../../core/models/maintenance.model';

@Component({
  selector: 'maintenance-recent-work-orders',
  standalone: true,
  imports: [DatePipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="card">
      <div class="card-head">
        <h3>Recent Work Orders</h3>
        <a routerLink="/fleet/maintenance/work-orders">VIEW ALL</a>
      </div>
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>ID</th><th>Vehicle</th><th>Service Type</th><th>Priority</th>
              <th>Workshop</th><th>Est. Completion</th><th>Status</th>
            </tr>
          </thead>
          <tbody>
            @for (wo of orders(); track wo.id) {
              <tr class="clickable" (click)="orderSelect.emit(wo.id)">
                <td>#{{ wo.workOrderNumber }}</td>
                <td>{{ wo.vehicleName }} @if (wo.vehicleRegistration) { #{{ wo.vehicleRegistration }} }</td>
                <td>{{ wo.serviceTypeName || '—' }}</td>
                <td><span class="prio prio--{{ prioClass(wo.priority) }}">{{ wo.priority || '—' }}</span></td>
                <td>{{ wo.workshopName || '—' }}</td>
                <td>{{ wo.estimatedCompletionDate ? (wo.estimatedCompletionDate | date:'MMM d, y') : '—' }}</td>
                <td><span class="badge badge--{{ statusClass(wo.status) }}">{{ statusLabel(wo.status) }}</span></td>
              </tr>
            } @empty {
              <tr><td colspan="7" class="empty">No work orders yet.</td></tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `,
  styles: [`
    .card { background: #fff; border: 1px solid #e2e8f0; border-radius: 14px; padding: 1.25rem; box-shadow: 0 1px 3px rgba(11,107,80,.06); }
    .card-head { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; }
    h3 { margin: 0; font-size: 1rem; font-weight: 700; color: #0b6b50; }
    a { font-size: 0.75rem; font-weight: 700; color: #0b6b50; text-decoration: none; }
    .table-wrap { overflow-x: auto; }
    table { width: 100%; border-collapse: collapse; font-size: 0.8125rem; min-width: 640px; }
    th { text-align: left; color: #64748b; font-weight: 600; padding: 0.5rem; border-bottom: 1px solid #e2e8f0; white-space: nowrap; }
    td { padding: 0.625rem 0.5rem; border-bottom: 1px solid #f1f5f9; }
    .clickable { cursor: pointer; }
    .clickable:hover { background: #e8f5f0; }
    .empty { text-align: center; color: #94a3b8; padding: 1.5rem !important; }
    .badge { padding: 0.125rem 0.5rem; border-radius: 999px; font-size: 0.6875rem; font-weight: 700; text-transform: uppercase; }
    .badge--progress { background: #fee2e2; color: #dc2626; }
    .badge--pending { background: #dbeafe; color: #1d4ed8; }
    .badge--done { background: #e8f5f0; color: #0b6b50; }
    .badge--muted { background: #f1f5f9; color: #64748b; }
    .prio { font-size: 0.6875rem; font-weight: 700; text-transform: uppercase; }
    .prio--critical { color: #dc2626; }
    .prio--high { color: #f59e0b; }
    .prio--medium { color: #0b6b50; }
    .prio--low { color: #64748b; }
  `]
})
export class MaintenanceRecentWorkOrdersComponent {
  readonly orders = input<WorkOrderListItem[]>([]);
  readonly orderSelect = output<number>();

  statusLabel(s: string): string {
    return (WorkOrderStatusLabels as Record<string, string>)[s] ?? s;
  }

  statusClass(s: string): string {
    if (s === 'InProgress' || s === 'WaitingParts') return 'progress';
    if (s === 'Open' || s === 'Assigned' || s === 'Draft') return 'pending';
    if (s === 'Completed') return 'done';
    return 'muted';
  }

  prioClass(p?: string | null): string {
    return (p ?? 'medium').toLowerCase();
  }
}
