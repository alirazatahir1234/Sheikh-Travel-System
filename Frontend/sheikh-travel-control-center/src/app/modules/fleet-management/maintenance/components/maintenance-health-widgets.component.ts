import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { VehicleHealthSummary, UpcomingService } from '../../../../core/models/maintenance.model';
import { UiChartComponent } from '../../../../shared/components/ui';

@Component({
  selector: 'maintenance-vehicle-health',
  standalone: true,
  imports: [UiChartComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
  <div class="card">
    <h3>Vehicle Health Status</h3>
    <ui-chart type="doughnut" [data]="chartData()" [options]="{ plugins: { legend: { position: 'bottom' } } }" height="200px"></ui-chart>
  </div>
  `,
  styles: [`
    .card { background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 1.25rem; min-width: 0; }
    h3 { margin: 0 0 0.75rem; font-size: 1rem; font-weight: 700; }
  `]
})
export class MaintenanceVehicleHealthComponent {
  readonly health = input<VehicleHealthSummary | null>(null);

  chartData() {
    const h = this.health();
    return {
      labels: ['Healthy', 'Due Soon', 'Overdue', 'In Workshop'],
      datasets: [{
        data: [h?.healthy ?? 0, h?.serviceDueSoon ?? 0, h?.overdue ?? 0, h?.inWorkshop ?? 0],
        backgroundColor: ['#047857', '#d97706', '#b91c1c', '#1d4ed8']
      }]
    };
  }
}

@Component({
  selector: 'maintenance-upcoming-services',
  standalone: true,
  imports: [DatePipe, DecimalPipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="card">
      <div class="card__head">
        <h3>Upcoming Services</h3>
        <a routerLink="/fleet/maintenance/schedules" class="link">View scheduler →</a>
      </div>
      <div class="table-wrap">
      <table>
        <thead><tr><th>Vehicle</th><th>Service</th><th>Due</th><th>Priority</th></tr></thead>
        <tbody>
          @for (s of services(); track $index) {
            <tr>
              <td>{{ s.vehicleName }}</td>
              <td>{{ s.serviceType }}</td>
              <td>
                @if (s.dueDate) {
                  {{ s.dueDate | date:'MMM d, y' }}
                } @else if (s.dueMileage != null) {
                  {{ s.dueMileage | number:'1.0-0' }} km
                } @else {
                  —
                }
              </td>
              <td>{{ s.priority }}</td>
            </tr>
          } @empty {
            <tr><td colspan="4" class="empty">No upcoming services scheduled.</td></tr>
          }
        </tbody>
      </table>
      </div>
    </div>
  `,
  styles: [`
    .card { background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 1.25rem; min-width: 0; }
    .card__head { display: flex; align-items: center; justify-content: space-between; gap: 0.5rem; margin-bottom: 0.75rem; flex-wrap: wrap; }
    h3 { margin: 0; font-size: 1rem; font-weight: 700; }
    .link { font-size: 0.8125rem; font-weight: 600; color: #0B6B50; text-decoration: none; }
    .link:hover { text-decoration: underline; }
    .table-wrap { overflow-x: auto; -webkit-overflow-scrolling: touch; }
    table { width: 100%; border-collapse: collapse; font-size: 0.8125rem; min-width: 480px; }
    th { text-align: left; color: #64748b; padding: 0.375rem 0.5rem; border-bottom: 1px solid #e2e8f0; white-space: nowrap; }
    td { padding: 0.5rem; border-bottom: 1px solid #f1f5f9; }
    .empty { text-align: center; color: #94a3b8; }
    @media (max-width: 640px) {
      .card { padding: 1rem; }
    }
  `]
})
export class MaintenanceUpcomingServicesComponent {
  readonly services = input<UpcomingService[]>([]);
}
