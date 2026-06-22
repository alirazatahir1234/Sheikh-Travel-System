import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { UiChartComponent } from '../../../../shared/components/ui';
import { MaintenanceCostTrendPoint } from '../../../../core/models/maintenance.model';

@Component({
  selector: 'maintenance-cost-trend',
  standalone: true,
  imports: [MatIconModule, UiChartComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="card">
      <div class="card-head">
        <h3>Maintenance Cost Trend</h3>
        <div class="controls">
          <div class="periods">
            @for (p of periods; track p) {
              <button type="button" [class.active]="period() === p" (click)="periodChange.emit(p)">{{ p }}</button>
            }
          </div>
          <button type="button" class="toggle" (click)="chartType.set(chartType() === 'line' ? 'bar' : 'line')">
            <mat-icon>{{ chartType() === 'line' ? 'bar_chart' : 'show_chart' }}</mat-icon>
          </button>
        </div>
      </div>
      <div class="legend">
        <span><i class="dot dot--green"></i> Preventive</span>
        <span><i class="dot dot--purple"></i> Corrective</span>
        <span><i class="dot dot--red"></i> Breakdown</span>
      </div>
      <ui-chart [type]="chartType()" [data]="chartData()" [options]="options" height="280px"></ui-chart>
    </div>
  `,
  styles: [`
    .card { background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 1.25rem; }
    .card-head { display: flex; justify-content: space-between; align-items: center; gap: 1rem; margin-bottom: 0.75rem; flex-wrap: wrap; }
    h3 { margin: 0; font-size: 1rem; font-weight: 700; color: #0f172a; }
    .controls { display: flex; gap: 0.5rem; align-items: center; }
    .periods { display: flex; gap: 0.25rem; }
    .periods button { border: 1px solid #e2e8f0; background: #fff; border-radius: 6px; padding: 0.25rem 0.5rem; font-size: 0.75rem; cursor: pointer; }
    .periods button.active { background: #064e3b; color: #fff; border-color: #064e3b; }
    .toggle { border: 1px solid #e2e8f0; background: #fff; border-radius: 6px; width: 32px; height: 32px; display: grid; place-items: center; cursor: pointer; }
    .legend { display: flex; gap: 1rem; margin-bottom: 0.75rem; font-size: 0.75rem; color: #64748b; }
    .dot { display: inline-block; width: 8px; height: 8px; border-radius: 50%; margin-right: 0.25rem; }
    .dot--green { background: #047857; }
    .dot--purple { background: #7c3aed; }
    .dot--red { background: #b91c1c; }
  `]
})
export class MaintenanceCostTrendComponent {
  readonly data = input<MaintenanceCostTrendPoint[]>([]);
  readonly period = input('Month');
  readonly periodChange = output<string>();

  readonly periods = ['Today', 'Week', 'Month', 'Quarter', 'Year'];
  readonly chartType = signal<'line' | 'bar'>('line');

  readonly chartData = computed(() => {
    const d = this.data();
    return {
      labels: d.map(x => x.label),
      datasets: [
        { label: 'Preventive', data: d.map(x => x.preventiveCost), borderColor: '#047857', backgroundColor: '#04785733', tension: 0.3 },
        { label: 'Corrective', data: d.map(x => x.correctiveCost), borderColor: '#7c3aed', backgroundColor: '#7c3aed33', tension: 0.3 },
        { label: 'Breakdown', data: d.map(x => x.breakdownCost), borderColor: '#b91c1c', backgroundColor: '#b91c1c33', tension: 0.3 }
      ]
    };
  });

  readonly options = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: { y: { beginAtZero: true } }
  };
}
