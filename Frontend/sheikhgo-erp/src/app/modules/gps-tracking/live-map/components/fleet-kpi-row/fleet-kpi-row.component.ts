import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { GpsDashboardSummary } from '../../../../../core/models/gps-tracking.model';
import { SharedModule } from '../../../../../shared/shared.module';

export interface FleetCounts {
  total: number;
  online: number;
  offline: number;
  moving: number;
  idle: number;
  parked: number;
  neverSeen: number;
  sos: number;
}

interface KpiDef {
  key: string;
  label: string;
  icon: string;
  tone: string;
  count: (fc: FleetCounts, ds: GpsDashboardSummary | null) => number;
  trend?: (ds: GpsDashboardSummary | null) => number | null;
  sparkline?: (ds: GpsDashboardSummary | null) => number[];
}

@Component({
  selector: 'app-fleet-kpi-row',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SharedModule, MatIconModule],
  templateUrl: './fleet-kpi-row.component.html'
})
export class FleetKpiRowComponent {
  @Input() dashboardSummary: GpsDashboardSummary | null = null;
  @Input() fleetCounts: FleetCounts = {
    total: 0,
    online: 0,
    offline: 0,
    moving: 0,
    idle: 0,
    parked: 0,
    neverSeen: 0,
    sos: 0
  };
  @Input() loading = false;
  @Input() kpiHint?: (key: string) => string | undefined;

  readonly kpis: KpiDef[] = [
    {
      key: 'online',
      label: 'Online',
      icon: 'wifi_tethering',
      tone: 'online',
      count: fc => fc.online,
      trend: ds => ds?.trends.online ?? null,
      sparkline: ds => ds?.sparkline.moving ?? []
    },
    {
      key: 'moving',
      label: 'Moving',
      icon: 'directions_bus',
      tone: 'moving',
      count: fc => fc.moving,
      trend: ds => ds?.trends.moving ?? null,
      sparkline: ds => ds?.sparkline.moving ?? []
    },
    {
      key: 'parked',
      label: 'Parked',
      icon: 'local_parking',
      tone: 'parked',
      count: fc => fc.parked,
      trend: ds => ds?.trends.parked ?? null,
      sparkline: ds => ds?.sparkline.parked ?? []
    },
    {
      key: 'idle',
      label: 'Idle',
      icon: 'local_shipping',
      tone: 'idle',
      count: fc => fc.idle,
      trend: ds => ds?.trends.idle ?? null,
      sparkline: ds => ds?.sparkline.idle ?? []
    },
    {
      key: 'offline',
      label: 'Offline',
      icon: 'signal_wifi_off',
      tone: 'offline',
      count: fc => fc.offline,
      trend: ds => ds?.trends.offline ?? null,
      sparkline: ds => ds?.sparkline.offline ?? []
    },
    {
      key: 'neverSeen',
      label: 'Never Seen',
      icon: 'help_outline',
      tone: 'never',
      count: fc => fc.neverSeen,
      trend: ds => ds?.trends.neverSeen ?? null
    },
    {
      key: 'totalFleet',
      label: 'Total Fleet',
      icon: 'groups',
      tone: 'total',
      count: (fc, ds) => ds?.totalFleet ?? fc.total,
      trend: ds => ds?.trends.totalFleet ?? null
    },
    {
      key: 'alertsToday',
      label: 'Alerts Today',
      icon: 'notifications_active',
      tone: 'alerts',
      count: (_, ds) => ds?.alertsToday ?? 0,
      trend: ds => ds?.trends.alertsToday ?? null
    }
  ];

  countFor(kpi: KpiDef): number {
    return kpi.count(this.fleetCounts, this.dashboardSummary);
  }

  trendFor(kpi: KpiDef): number | null {
    return kpi.trend?.(this.dashboardSummary) ?? null;
  }

  sparklineFor(kpi: KpiDef): number[] {
    return kpi.sparkline?.(this.dashboardSummary) ?? [];
  }

  sparkHeight(value: number, series: number[]): number {
    const max = Math.max(...series, 1);
    return Math.max(12, Math.round((value / max) * 100));
  }

  hintFor(key: string): string | undefined {
    return this.kpiHint?.(key);
  }
}
