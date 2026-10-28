import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ChartData } from 'chart.js';
import { UiChartComponent, UiChartOptions } from '../../../../shared/components/ui/chart/ui-chart.component';
import {
  AssistantDataTable,
  AssistantFleetHealthData,
  AssistantMetricTile,
  SalesAnalyticsData,
  TripBookingAnalyticsData,
  asSalesAnalytics,
  asTripBookingAnalytics,
  extractDataTable,
  extractFollowUps,
  fleetHealthMetrics,
  formatToolLabel,
  healthBand,
  parseAssistantAnswer,
  asFleetHealthData
} from '../assistant-answer.util';
import { polishAssistantEmojis } from '../ai-assistant-ui.constants';

const STATUS_COLORS = ['#0d9488', '#3b82f6', '#f59e0b', '#ef4444', '#94a3b8'];

@Component({
  selector: 'app-ai-assistant-result',
  standalone: true,
  imports: [CommonModule, RouterModule, MatIconModule, MatButtonModule, MatTooltipModule, UiChartComponent],
  templateUrl: './ai-assistant-result.component.html',
  styleUrls: ['./ai-assistant-result.component.scss']
})
export class AiAssistantResultComponent {
  @Input() answer = '';
  @Input() toolUsed?: string | null;
  @Input() data?: unknown;
  @Input() warnings: string[] = [];
  @Input() mode?: string;
  /** Emits when user clicks a follow-up action chip under rich analytics. */
  @Output() followUp = new EventEmitter<string>();

  get blocks() {
    return parseAssistantAnswer(this.answer);
  }

  get toolLabel(): string | null {
    return formatToolLabel(this.toolUsed);
  }

  get fleetHealth(): AssistantFleetHealthData | null {
    return asFleetHealthData(this.data);
  }

  get fleetMetrics(): AssistantMetricTile[] {
    const h = this.fleetHealth;
    return h ? fleetHealthMetrics(h) : [];
  }

  get fleetBand(): 'good' | 'fair' | 'poor' | null {
    const h = this.fleetHealth;
    return h ? healthBand(h.healthPercent) : null;
  }

  get salesAnalytics(): SalesAnalyticsData | null {
    if (this.fleetHealth) return null;
    return asSalesAnalytics(this.data, this.toolUsed);
  }

  get tripAnalytics(): TripBookingAnalyticsData | null {
    if (this.fleetHealth || this.salesAnalytics) return null;
    return asTripBookingAnalytics(this.data, this.toolUsed);
  }

  get followUpActions(): string[] {
    const fromTrip = this.tripAnalytics?.followUps;
    if (fromTrip?.length) return fromTrip;
    const fromSales = this.salesAnalytics?.followUps;
    if (fromSales?.length) return fromSales;
    return extractFollowUps(this.data);
  }

  get table(): AssistantDataTable | null {
    if (this.fleetHealth || this.tripAnalytics || this.salesAnalytics) return null;
    return extractDataTable(this.data, this.toolUsed);
  }

  get trendChartData(): ChartData | null {
    const sales = this.salesAnalytics;
    if (sales?.trends?.length) {
      const labels = sales.trends.map(t => {
        const d = new Date(t.date);
        return Number.isNaN(d.getTime())
          ? t.date
          : d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
      });
      return {
        labels,
        datasets: [
          {
            label: 'Revenue',
            data: sales.trends.map(t => t.revenue),
            borderColor: '#0d9488',
            backgroundColor: 'rgba(13, 148, 136, 0.12)',
            tension: 0.35,
            fill: true,
            pointRadius: 2
          },
          {
            label: 'Payments',
            data: sales.trends.map(t => t.paymentCount),
            borderColor: '#3b82f6',
            backgroundColor: 'transparent',
            tension: 0.35,
            pointRadius: 2
          }
        ]
      };
    }

    const a = this.tripAnalytics;
    if (!a?.trends?.length) return null;
    const labels = a.trends.map(t => {
      const d = new Date(t.date);
      return Number.isNaN(d.getTime())
        ? t.date
        : d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
    });
    return {
      labels,
      datasets: [
        {
          label: 'Trips',
          data: a.trends.map(t => t.trips),
          borderColor: '#0d9488',
          backgroundColor: 'rgba(13, 148, 136, 0.12)',
          tension: 0.35,
          fill: true,
          pointRadius: 2
        },
        {
          label: 'Completed',
          data: a.trends.map(t => t.completed),
          borderColor: '#3b82f6',
          backgroundColor: 'transparent',
          tension: 0.35,
          pointRadius: 2
        },
        {
          label: 'Bookings',
          data: a.trends.map(t => t.bookings),
          borderColor: '#f59e0b',
          backgroundColor: 'transparent',
          tension: 0.35,
          pointRadius: 2
        }
      ]
    };
  }

  get statusChartData(): ChartData | null {
    const dist =
      this.salesAnalytics?.statusDistribution ?? this.tripAnalytics?.statusDistribution;
    if (!dist?.length) return null;
    const slices = dist.filter(s => s.count > 0);
    const source = slices.length ? slices : dist;
    return {
      labels: source.map(s => s.label),
      datasets: [
        {
          data: source.map(s => s.count),
          backgroundColor: source.map((_, i) => STATUS_COLORS[i % STATUS_COLORS.length]),
          borderWidth: 0,
          hoverOffset: 4
        }
      ]
    };
  }

  get statusTotal(): number {
    const dist =
      this.salesAnalytics?.statusDistribution ?? this.tripAnalytics?.statusDistribution;
    return dist?.reduce((sum, s) => sum + s.count, 0) ?? 0;
  }

  /** Mock-style period chip on the trends card (e.g. "This Month"). */
  get periodBadge(): string | null {
    const salesLabel = this.salesAnalytics?.period?.label;
    if (salesLabel) {
      const map: Record<string, string> = {
        today: 'Today',
        last_7_days: 'This Week',
        this_month: 'This Month',
        last_30_days: 'Last 30 Days',
        week_over_week: 'WoW',
        month_over_month: 'MoM'
      };
      if (map[salesLabel]) return map[salesLabel];
    }
    const period = this.salesAnalytics?.period ?? this.tripAnalytics?.period;
    if (!period?.start || !period?.end) return null;
    const start = new Date(period.start);
    const end = new Date(period.end);
    if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) return 'Period';

    const now = new Date();
    const monthStart = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1));
    const nextMonth = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() + 1, 1));
    if (start.getTime() <= monthStart.getTime() + 86_400_000 && end.getTime() >= nextMonth.getTime() - 86_400_000) {
      return 'This Month';
    }

    const spanDays = Math.max(1, Math.round((end.getTime() - start.getTime()) / 86_400_000));
    if (spanDays <= 1) return 'Today';
    if (spanDays <= 8) return 'This Week';
    if (spanDays <= 35) return 'This Month';
    return start.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })
      + ' – '
      + end.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
  }

  insightIcon(index: number): string {
    const icons = ['bar_chart', 'lightbulb', 'trending_up', 'insights', 'show_chart'];
    return icons[index % icons.length];
  }

  readonly trendChartOptions: UiChartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'bottom',
        labels: { boxWidth: 10, font: { size: 11 } }
      }
    },
    scales: {
      x: { grid: { display: false }, ticks: { maxRotation: 0, autoSkip: true, maxTicksLimit: 8 } },
      y: { beginAtZero: true, ticks: { precision: 0 }, grid: { color: '#f1f5f9' } }
    }
  };

  readonly statusChartOptions: UiChartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { display: false }
    },
    // Chart.js doughnut cutout (typed loosely for mixed chart options)
    ...( { cutout: '68%' } as object )
  } as UiChartOptions;

  metricPercent(m: AssistantMetricTile): number {
    if (m.unit !== '%') return 0;
    return Math.max(0, Math.min(100, m.value));
  }

  polishedWarning(text: string): string {
    return polishAssistantEmojis(text);
  }

  kpiIcon(kpi: { icon?: string; key: string }): string {
    if (kpi.icon) return kpi.icon;
    switch (kpi.key) {
      case 'totalTrips':
        return 'route';
      case 'totalBookings':
        return 'event';
      case 'pendingBookings':
        return 'schedule';
      case 'tripCompletionRate':
        return 'donut_large';
      case 'totalRevenue':
      case 'revenueCurrent':
      case 'revenueToday':
        return 'payments';
      case 'topCustomerRevenue':
        return 'emoji_events';
      default:
        return 'insights';
    }
  }

  formatKpiValue(kpi: { value: number; unit: string }): string {
    if (kpi.unit === 'percent') {
      return `${kpi.value.toLocaleString(undefined, { maximumFractionDigits: 1 })}%`;
    }
    if (kpi.unit === 'currency') {
      return kpi.value.toLocaleString(undefined, {
        minimumFractionDigits: 0,
        maximumFractionDigits: 2
      });
    }
    return kpi.value.toLocaleString(undefined, { maximumFractionDigits: 0 });
  }

  deltaClass(delta?: number | null): string {
    if (delta == null || delta === 0) return 'flat';
    return delta > 0 ? 'up' : 'down';
  }

  formatDelta(delta?: number | null): string {
    if (delta == null) return '→ 0%';
    if (delta === 0) return '→ 0%';
    const arrow = delta > 0 ? '↑' : '↓';
    const abs = Math.abs(delta).toLocaleString(undefined, { maximumFractionDigits: 1 });
    return `${arrow} ${abs}%`;
  }

  /** Bottom progress fill for mock KPI cards (completion % or share). */
  kpiBarPercent(kpi: { key: string; value: number; unit: string; sharePercent?: number | null }): number | null {
    if (kpi.unit === 'percent' || kpi.key === 'tripCompletionRate') {
      return Math.max(0, Math.min(100, kpi.value));
    }
    if (kpi.sharePercent != null && Number.isFinite(kpi.sharePercent)) {
      return Math.max(0, Math.min(100, kpi.sharePercent));
    }
    // Total Trips / Bookings: soft bar from delta magnitude when present is skipped —
    // show a thin accent only for completion-like KPIs above.
    return null;
  }

  insightTone(tip: string, index: number): string {
    const t = tip.toLowerCase();
    if (t.includes('pending') || t.includes('still') || t.includes('attention') || t.includes('risk')) {
      return 'warn';
    }
    if (index === 0) return 'ok';
    return 'neutral';
  }
}
