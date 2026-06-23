import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { WorkOrderListItem, WorkOrderStatusLabels } from '../../../../../core/models/maintenance.model';
import { woActualCost, woEstimatedCost } from '../utils/wo.util';

@Component({
  selector: 'wo-table',
  standalone: true,
  imports: [CurrencyPipe, DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="table-wrap">
      @if (loading()) {
        <div class="loading">Loading work orders…</div>
      } @else {
        <table>
          <thead>
            <tr>
              <th>Work Order No</th>
              <th>Vehicle</th>
              <th>Service Type</th>
              <th>Workshop</th>
              <th>Assigned Technician</th>
              <th>Estimated Cost</th>
              <th>Actual Cost</th>
              <th>Start Date</th>
              <th>Completion Date</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            @for (wo of orders(); track wo.id) {
              <tr class="clickable" (click)="rowClick.emit(wo.id)">
                <td class="wo-no">{{ wo.workOrderNumber }}</td>
                <td>
                  <div class="vehicle">
                    <span>{{ wo.vehicleName }}</span>
                    @if (wo.vehicleRegistration) {
                      <small>{{ wo.vehicleRegistration }}</small>
                    }
                  </div>
                </td>
                <td>{{ wo.serviceTypeName || '—' }}</td>
                <td>{{ wo.workshopName || '—' }}</td>
                <td>{{ wo.technicianName || '—' }}</td>
                <td>{{ estimated(wo) | currency }}</td>
                <td>{{ actual(wo) | currency }}</td>
                <td>{{ wo.startDate ? (wo.startDate | date:'MMM d, y') : '—' }}</td>
                <td>{{ wo.completedAt ? (wo.completedAt | date:'MMM d, y') : '—' }}</td>
                <td>
                  <span class="badge badge--{{ statusClass(wo.status) }}">{{ statusLabel(wo.status) }}</span>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="10" class="empty">No work orders match the selected filters.</td></tr>
            }
          </tbody>
        </table>
      }
    </div>
  `,
  styles: [`
    .table-wrap { overflow-x: auto; background: #fff; border: 1px solid #e2e8f0; border-radius: 14px; }
    .loading, .empty { text-align: center; padding: 2.5rem 1rem; color: #94a3b8; font-size: 0.875rem; }
    table { width: 100%; border-collapse: collapse; min-width: 1040px; }
    th, td { padding: 0.75rem 0.875rem; border-bottom: 1px solid #f1f5f9; font-size: 0.8125rem; text-align: left; white-space: nowrap; }
    th { background: #f8faf9; color: #64748b; font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.04em; }
    tbody tr:last-child td { border-bottom: none; }
    .clickable { cursor: pointer; }
    .clickable:hover { background: #f0faf6; }
    .wo-no { font-weight: 700; color: #0b6b50; font-family: monospace; }
    .vehicle { display: flex; flex-direction: column; gap: 0.1rem; }
    .vehicle small { color: #94a3b8; font-size: 0.6875rem; }
    .badge { display: inline-block; padding: 0.2rem 0.5rem; border-radius: 999px; font-size: 0.6875rem; font-weight: 700; text-transform: uppercase; }
    .badge--info { background: #dbeafe; color: #1d4ed8; }
    .badge--warning { background: #fef3c7; color: #b45309; }
    .badge--done { background: #dcfce7; color: #15803d; }
    .badge--danger { background: #fee2e2; color: #dc2626; }
    .badge--muted { background: #f1f5f9; color: #64748b; }
  `]
})
export class WoTableComponent {
  readonly orders = input<WorkOrderListItem[]>([]);
  readonly loading = input(false);
  readonly rowClick = output<number>();

  estimated = woEstimatedCost;
  actual = woActualCost;

  statusLabel(s: string): string {
    return (WorkOrderStatusLabels as Record<string, string>)[s] ?? s;
  }

  statusClass(s: string): string {
    if (s === 'InProgress' || s === 'WaitingParts') return 'warning';
    if (s === 'Open' || s === 'Assigned' || s === 'Draft') return 'info';
    if (s === 'Completed' || s === 'Closed') return 'done';
    if (s === 'Cancelled') return 'danger';
    return 'muted';
  }
}
