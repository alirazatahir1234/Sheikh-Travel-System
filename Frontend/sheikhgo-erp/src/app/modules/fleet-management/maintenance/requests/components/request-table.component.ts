import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MaintenanceRequest } from '../../../../../core/models/maintenance.model';

@Component({
  selector: 'request-table',
  standalone: true,
  imports: [DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Request #</th><th>Vehicle</th><th>Driver</th><th>Category</th>
            <th>Type</th><th>Priority</th><th>Status</th><th>Date</th>
          </tr>
        </thead>
        <tbody>
          @for (r of requests(); track r.id) {
            <tr class="clickable" (click)="rowSelect.emit(r.id)">
              <td>{{ r.requestNumber }}</td>
              <td>{{ r.vehicleName }} @if (r.vehicleRegistration) { ({{ r.vehicleRegistration }}) }</td>
              <td>{{ r.driverName || '—' }}</td>
              <td><span class="cat">{{ r.issueCategory }}</span></td>
              <td><span class="type">{{ r.requestType }}</span></td>
              <td><span class="prio prio--{{ (r.priority || 'medium').toLowerCase() }}">{{ r.priority }}</span></td>
              <td><span class="status status--{{ r.status.toLowerCase() }}">{{ r.status }}</span></td>
              <td>{{ r.requestDate | date:'MMM d, y' }}</td>
            </tr>
          } @empty {
            <tr><td colspan="8" class="empty">No requests found.</td></tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: [`
    .table-wrap { overflow-x: auto; background: #fff; border: 1px solid #e2e8f0; border-radius: 14px; }
    table { width: 100%; border-collapse: collapse; font-size: 0.8125rem; min-width: 860px; }
    th { text-align: left; padding: 0.75rem; background: #f8faf9; color: #64748b; font-weight: 600; border-bottom: 1px solid #e2e8f0; }
    td { padding: 0.75rem; border-bottom: 1px solid #f1f5f9; }
    .clickable { cursor: pointer; }
    .clickable:hover { background: #e8f5f0; }
    .empty { text-align: center; color: #94a3b8; padding: 2rem !important; }
    .cat, .type { background: #f1f5f9; padding: 0.125rem 0.5rem; border-radius: 4px; font-size: 0.75rem; white-space: nowrap; }
    .prio { font-weight: 700; font-size: 0.6875rem; text-transform: uppercase; }
    .prio--critical { color: #dc2626; }
    .prio--high { color: #f59e0b; }
    .prio--medium { color: #0b6b50; }
    .prio--low { color: #64748b; }
    .status { font-size: 0.6875rem; font-weight: 700; text-transform: uppercase; padding: 0.125rem 0.5rem; border-radius: 999px; }
    .status--open { background: #fef3c7; color: #f59e0b; }
    .status--approved { background: #e8f5f0; color: #0b6b50; }
    .status--rejected { background: #fee2e2; color: #dc2626; }
    .status--inprogress { background: #dbeafe; color: #1d4ed8; }
  `]
})
export class RequestTableComponent {
  readonly requests = input<MaintenanceRequest[]>([]);
  readonly rowSelect = output<number>();
}
