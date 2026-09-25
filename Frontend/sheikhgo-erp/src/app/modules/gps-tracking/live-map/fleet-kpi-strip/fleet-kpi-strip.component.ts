import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output
} from '@angular/core';

export type LiveKpiKey =
  | 'total'
  | 'online'
  | 'moving'
  | 'parked'
  | 'idle'
  | 'offline'
  | 'never_seen'
  | 'alerts';

export interface LiveKpiTile {
  key: LiveKpiKey;
  label: string;
  value: number;
  icon: string;
  color: 'teal' | 'sky' | 'green' | 'blue' | 'amber' | 'rose' | 'purple' | 'orange';
  hint?: string;
  trend?: string;
  trendUp?: boolean;
  sparkline?: number[];
  selected?: boolean;
}

@Component({
  standalone: false,
  selector: 'app-fleet-kpi-strip',
  templateUrl: './fleet-kpi-strip.component.html',
  styleUrls: ['./fleet-kpi-strip.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FleetKpiStripComponent {
  @Input() tiles: LiveKpiTile[] = [];
  @Input() sosCount = 0;
  @Input() sosSelected = false;

  @Output() tileClick = new EventEmitter<LiveKpiKey>();
  @Output() sosClick = new EventEmitter<void>();

  onTile(key: LiveKpiKey): void {
    this.tileClick.emit(key);
  }

  trackByKey = (_: number, t: LiveKpiTile): string => t.key;

  sparkPoints(values: number[]): string {
    if (!values.length) return '';
    const max = Math.max(...values, 1);
    const n = values.length;
    return values
      .map((v, i) => {
        const x = n === 1 ? 24 : (i / (n - 1)) * 48;
        const y = 14 - (v / max) * 12;
        return `${x.toFixed(1)},${y.toFixed(1)}`;
      })
      .join(' ');
  }
}
