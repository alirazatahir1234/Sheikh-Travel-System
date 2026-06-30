import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ChartOptions } from 'chart.js';
import { VehicleHealthSummary, UpcomingService } from '../../../../core/models/maintenance.model';
import { UiChartComponent } from '../../../../shared/components/ui';

const HEALTH_LEGEND = [
  { key: 'healthy', label: 'Healthy', color: '#047857' },
  { key: 'serviceDueSoon', label: 'Due Soon', color: '#d97706' },
  { key: 'overdue', label: 'Overdue', color: '#b91c1c' },
  { key: 'inWorkshop', label: 'In Workshop', color: '#1d4ed8' }
] as const;

@Component({
  selector: 'maintenance-vehicle-health',
  standalone: true,
  imports: [UiChartComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
  <div class="card">
    <h3>Vehicle Health Status</h3>
    <ui-chart
      type="doughnut"
      [data]="chartData()"
      [options]="chartOptions"
      height="200px" />
    <ul class="legend" aria-label="Vehicle health legend">
      @for (item of legendItems(); track item.label) {
        <li class="legend__item">
          <span class="legend__swatch" [style.background-color]="item.color"></span>
          <span class="legend__label">{{ item.label }}</span>
        </li>
      }
    </ul>
  </div>
  `,
  styles: [`
    .card { background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 1.25rem; min-width: 0; }
    h3 { margin: 0 0 0.75rem; font-size: 1rem; font-weight: 700; }
    .legend {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 0.625rem 1rem;
      margin: 0.875rem 0 0;
      padding: 0;
      list-style: none;
    }
    .legend__item {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      min-width: 0;
    }
    .legend__swatch {
      width: 0.75rem;
      height: 0.75rem;
      border-radius: 2px;
      flex-shrink: 0;
    }
    .legend__label {
      font-size: 0.8125rem;
      line-height: 1.25rem;
      color: #334155;
      white-space: nowrap;
    }
    @media (max-width: 480px) {
      .legend { grid-template-columns: 1fr; }
    }
  `]
})
export class MaintenanceVehicleHealthComponent {
  readonly health = input<VehicleHealthSummary | null>(null);

  readonly chartOptions: ChartOptions<'doughnut'> = {
    responsive: true,
    maintainAspectRatio: false,
    cutout: '65%',
    plugins: { legend: { display: false } }
  };

  readonly legendItems = computed(() => {
    const h = this.health();
    return HEALTH_LEGEND.map(item => ({
      label: item.label,
      color: item.color,
      value: h?.[item.key] ?? 0
    }));
  });

  chartData() {
    const h = this.health();
    return {
      labels: HEALTH_LEGEND.map(item => item.label),
      datasets: [{
        data: HEALTH_LEGEND.map(item => h?.[item.key] ?? 0),
        backgroundColor: HEALTH_LEGEND.map(item => item.color)
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
