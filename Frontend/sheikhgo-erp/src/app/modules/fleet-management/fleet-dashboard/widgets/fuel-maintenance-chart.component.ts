import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ChartData } from 'chart.js';
import { UiChartComponent, UiChartOptions } from '../../../../shared/components/ui';
import { FuelMaintenanceChart } from '../fleet-dashboard.model';
import { FLEET_PRIMARY, FLEET_SECONDARY } from '../fleet-dashboard.mock';

@Component({
  selector: 'fleet-fuel-maintenance-chart',
  standalone: true,
  imports: [UiChartComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="flex h-full flex-col rounded-xl border border-fleet-border bg-white p-6">
      <div class="mb-6 flex items-center justify-between">
        <h3 class="text-lg font-bold text-fleet-text">Fuel Usage vs. Maintenance Cost</h3>
        <div class="flex gap-4">
          <div class="flex items-center gap-2">
            <span class="h-3 w-3 rounded-sm" [style.background]="primary"></span>
            <span class="text-[11px] text-fleet-text-muted">Fuel</span>
          </div>
          <div class="flex items-center gap-2">
            <span class="h-3 w-3 rounded-sm" [style.background]="secondary"></span>
            <span class="text-[11px] text-fleet-text-muted">Maint.</span>
          </div>
        </div>
      </div>

      <div class="flex-1">
        <ui-chart type="bar" [data]="chartData()" [options]="options" height="260px"></ui-chart>
      </div>
    </section>
  `,
  styles: [`:host { display: block; height: 100%; }`]
})
export class FuelMaintenanceChartComponent {
  readonly data = input.required<FuelMaintenanceChart>();

  protected readonly primary = FLEET_PRIMARY;
  protected readonly secondary = FLEET_SECONDARY;

  protected readonly chartData = computed<ChartData>(() => ({
    labels: this.data().labels,
    datasets: [
      { label: 'Fuel', data: this.data().fuel, backgroundColor: FLEET_PRIMARY, borderRadius: 4, maxBarThickness: 18 },
      { label: 'Maint.', data: this.data().maintenance, backgroundColor: FLEET_SECONDARY, borderRadius: 4, maxBarThickness: 18 }
    ]
  }));

  protected readonly options: UiChartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: {
      x: { grid: { display: false } },
      y: { beginAtZero: true, grid: { color: 'rgba(15, 23, 42, 0.06)' }, ticks: { callback: (v) => `$${v}k` } }
    }
  };
}
