import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { ChartData } from 'chart.js';
import { MatIconModule } from '@angular/material/icon';
import { UiChartComponent, UiChartOptions } from '../../../../../shared/components/ui';
import { SharedModule } from '../../../../../shared/shared.module';
import { LiveMapEvent } from '../../state/live-map-events.util';

@Component({
  selector: 'app-fleet-analytics-row',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SharedModule, MatIconModule, UiChartComponent],
  templateUrl: './fleet-analytics-row.component.html'
})
export class FleetAnalyticsRowComponent {
  @Input() lineChartData: ChartData = { labels: [], datasets: [] };
  @Input() doughnutChartData: ChartData = { labels: [], datasets: [] };
  @Input() chartOptions: UiChartOptions = {};
  @Input() events: LiveMapEvent[] = [];
  @Input() loading = false;
}
