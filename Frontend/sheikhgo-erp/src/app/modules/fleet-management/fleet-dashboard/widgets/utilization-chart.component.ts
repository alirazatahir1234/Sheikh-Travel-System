import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ChartData } from 'chart.js';
import { UiChartComponent, UiChartOptions } from '../../../../shared/components/ui';
import { UtilizationChart } from '../fleet-dashboard.model';

@Component({
  selector: 'fleet-utilization-chart',
  standalone: true,
  imports: [UiChartComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="flex h-full flex-col rounded-xl border border-fleet-border bg-white p-6">
      <div class="mb-6 flex items-center justify-between">
        <div>
          <h3 class="text-lg font-bold text-fleet-text">Fleet Utilization</h3>
          <p class="text-[11px] text-fleet-text-muted">Average active capacity over the last 30 days</p>
        </div>
        <div class="flex items-center gap-4">
          @for (series of data().series; track series.label) {
            <div class="flex items-center gap-2">
              <span class="h-3 w-3 rounded-full" [style.background]="series.color"></span>
              <span class="text-[11px] text-fleet-text-muted">{{ series.label }}</span>
            </div>
          }
        </div>
      </div>

      <div class="flex-1">
        <ui-chart type="line" [data]="chartData()" [options]="options" height="260px"></ui-chart>
      </div>
    </section>
  `,
  styles: [`:host { display: block; height: 100%; }`]
})
export class UtilizationChartComponent {
  readonly data = input.required<UtilizationChart>();

  protected readonly chartData = computed<ChartData>(() => ({
    labels: this.data().labels,
    datasets: this.data().series.map((series) => ({
      label: series.label,
      data: series.values,
      borderColor: series.color,
      backgroundColor: series.color,
      borderWidth: series.label === '2024' ? 3 : 2,
      tension: 0.4,
      pointRadius: 0,
      pointHoverRadius: 4,
      fill: false
    }))
  }));

  protected readonly options: UiChartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: {
      x: { grid: { display: false } },
      y: { beginAtZero: true, max: 100, grid: { color: 'rgba(15, 23, 42, 0.06)' }, ticks: { callback: (v) => `${v}%` } }
    }
  };
}
