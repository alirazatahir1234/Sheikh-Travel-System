import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { FuelMaintenanceSummary } from '../../../../core/models/maintenance.model';
import { UiChartComponent } from '../../../../shared/components/ui/chart/ui-chart.component';

@Component({
  selector: 'maintenance-fuel-summary',
  standalone: true,
  imports: [CurrencyPipe, UiChartComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (summary(); as s) {
      <div class="card">
        <h3>Fuel vs Maintenance Cost</h3>
        <ui-chart type="bar" [data]="chartData()" height="220px"></ui-chart>
        @if (s.highCostVehicles.length) {
          <div class="high-cost">
            <p class="sub">Highest combined cost vehicles</p>
            @for (v of s.highCostVehicles.slice(0, 3); track v.vehicleId) {
              <div class="row">
                <span>{{ v.vehicleName }}</span>
                <span>{{ (v.fuelCost + v.maintenanceCost) | currency }}</span>
              </div>
            }
          </div>
        }
      </div>
    }
  `,
  styles: [`
    .card { background: #fff; border: 1px solid #e2e8f0; border-radius: 14px; padding: 1.25rem; min-width: 0; }
    h3 { margin: 0 0 1rem; font-size: 1rem; font-weight: 700; color: #0b6b50; }
    .sub { margin: 1rem 0 0.5rem; font-size: 0.75rem; font-weight: 600; color: #64748b; text-transform: uppercase; }
    .row { display: flex; justify-content: space-between; font-size: 0.8125rem; padding: 0.375rem 0; border-bottom: 1px solid #f1f5f9; gap: 0.75rem; }
    @media (max-width: 640px) {
      .card { padding: 1rem; }
    }
  `]
})
export class MaintenanceFuelSummaryComponent {
  readonly summary = input<FuelMaintenanceSummary | null | undefined>(null);

  chartData = computed(() => {
    const s = this.summary();
    if (!s) return { labels: [], datasets: [] };
    return {
      labels: s.labels,
      datasets: [
        { label: 'Fuel', data: s.fuelCosts, backgroundColor: '#0b6b50' },
        { label: 'Maintenance', data: s.maintenanceCosts, backgroundColor: '#f59e0b' }
      ]
    };
  });
}
