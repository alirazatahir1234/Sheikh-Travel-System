import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ChartData } from 'chart.js';
import { UiChartComponent, UiChartOptions } from '../../../../shared/components/ui/chart/ui-chart.component';
import {
  AssistantAcein,
  AssistantDataTable,
  AssistantFleetHealthData,
  AssistantMetricTile,
  FleetStatusAnalyticsData,
  GenericAnalyticsData,
  SalesAnalyticsData,
  TripBookingAnalyticsData,
  asFleetStatusAnalytics,
  asGenericAnalytics,
  asSalesAnalytics,
  asTripBookingAnalytics,
  extractAcein,
  extractDataTable,
  extractFollowUps,
  extractRecommendedActions,
  fleetHealthMetrics,
  formatToolLabel,
  hasStructuredError,
  healthBand,
  parseAssistantAnswer,
  asFleetHealthData
} from '../assistant-answer.util';
import { polishAssistantEmojis } from '../ai-assistant-ui.constants';

const STATUS_COLORS = ['#10b981', '#ef4444', '#8b5cf6', '#3b82f6', '#f59e0b', '#ec4899', '#94a3b8', '#cbd5e1'];

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
  /** Two-panel dashboard / mock response workspace styling. */
  @Input() workspace = false;
  /** Emits when user clicks a follow-up action chip under rich analytics. */
  @Output() followUp = new EventEmitter<string>();

  private readonly statusColorByKey: Record<string, string> = {
    online: '#10b981',
    moving: '#3b82f6',
    idle: '#f59e0b',
    parked: '#8b5cf6',
    sos: '#ec4899',
    offline: '#ef4444',
    neverseen: '#94a3b8',
    never_seen: '#94a3b8',
    unknown: '#cbd5e1'
  };

  get blocks() {
    return parseAssistantAnswer(this.answer);
  }

  get toolLabel(): string | null {
    return formatToolLabel(this.toolUsed);
  }

  get structuredError(): string | null {
    return hasStructuredError(this.data);
  }

  get fleetHealth(): AssistantFleetHealthData | null {
    if (this.fleetStatus) return null;
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

  get fleetStatus(): FleetStatusAnalyticsData | null {
    return asFleetStatusAnalytics(this.data, this.toolUsed);
  }

  /** Mock hero KPIs: Online / Offline / Parked / Idle from live summary (real counts only). */
  get fleetHeroKpis(): {
    key: string;
    label: string;
    value: number;
    sharePercent: number;
    tone: string;
    icon: string;
    hint: string;
  }[] {
    const fs = this.fleetStatus;
    if (!fs) return [];
    const total = Math.max(0, fs.summary.total);
    const pct = (n: number) => (total > 0 ? Math.round((n * 1000) / total) / 10 : 0);
    const vehicleWord = (n: number) => (n === 1 ? 'vehicle' : 'vehicles');
    return [
      {
        key: 'online',
        label: 'Online',
        value: fs.summary.online,
        sharePercent: pct(fs.summary.online),
        tone: 'green',
        icon: 'wifi',
        hint: fs.summary.online === 1
          ? 'Vehicle is sending live location.'
          : `${fs.summary.online} ${vehicleWord(fs.summary.online)} sending live location.`
      },
      {
        key: 'offline',
        label: 'Offline',
        value: fs.summary.offline,
        sharePercent: pct(fs.summary.offline),
        tone: 'red',
        icon: 'wifi_off',
        hint: fs.summary.offline === 0
          ? 'No vehicles are offline.'
          : `${fs.summary.offline} ${vehicleWord(fs.summary.offline)} are not sending location.`
      },
      {
        key: 'parked',
        label: 'Parked',
        value: fs.summary.parked,
        sharePercent: pct(fs.summary.parked),
        tone: 'purple',
        icon: 'local_parking',
        hint: fs.summary.parked === 0
          ? 'No vehicles are parked.'
          : `${fs.summary.parked} ${vehicleWord(fs.summary.parked)} parked at a location.`
      },
      {
        key: 'idle',
        label: 'Idle',
        value: fs.summary.idle,
        sharePercent: pct(fs.summary.idle),
        tone: 'amber',
        icon: 'pause_circle',
        hint: fs.summary.idle === 0
          ? 'No vehicles are currently idle.'
          : `${fs.summary.idle} ${vehicleWord(fs.summary.idle)} idle (ignition on).`
      }
    ];
  }

  get fleetSummaryText(): string {
    const fs = this.fleetStatus;
    if (!fs) return '';
    if (fs.acein?.answer) return fs.acein.answer;
    const offline = fs.summary.offline;
    const total = fs.summary.total;
    const pct = total > 0 ? Math.round((offline * 1000) / total) / 10 : 0;
    if (total === 0) return 'No vehicles were returned for this tenant’s live fleet status.';
    if (offline > 0) {
      return `${offline} vehicle${offline === 1 ? ' is' : 's are'} offline, which is ${pct}% of your total fleet (${total}).`;
    }
    return `Fleet status: ${fs.summary.online} of ${total} vehicles online.`;
  }

  get fleetAsOfLabel(): string {
    const now = new Date();
    return `Data as of ${now.toLocaleString(undefined, {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: 'numeric',
      minute: '2-digit'
    })}`;
  }

  get fleetActionButtons(): { label: string; icon: string; tone: string; prompt: string }[] {
    const fs = this.fleetStatus;
    if (!fs) return [];
    const buttons = [
      {
        label: 'View Offline Vehicles',
        icon: 'wifi_off',
        tone: 'red',
        prompt: 'Show all offline vehicles'
      },
      {
        label: 'Show on Live Map',
        icon: 'map',
        tone: 'teal',
        prompt: 'Open live map'
      },
      {
        label: 'Generate Status Report',
        icon: 'description',
        tone: 'blue',
        prompt: 'Generate a fleet status report'
      },
      {
        label: 'Refresh Data',
        icon: 'refresh',
        tone: 'dark',
        prompt: 'How many vehicles are offline in my fleet?'
      }
    ];
    if (fs.summary.offline === 0) {
      buttons[0] = {
        label: 'View Online Vehicles',
        icon: 'wifi',
        tone: 'green',
        prompt: 'Show online vehicles'
      };
    }
    return buttons;
  }

  get salesAnalytics(): SalesAnalyticsData | null {
    if (this.fleetHealth || this.fleetStatus) return null;
    return asSalesAnalytics(this.data, this.toolUsed);
  }

  get tripAnalytics(): TripBookingAnalyticsData | null {
    if (this.fleetHealth || this.fleetStatus || this.salesAnalytics) return null;
    return asTripBookingAnalytics(this.data, this.toolUsed);
  }

  get genericAnalytics(): GenericAnalyticsData | null {
    if (this.fleetHealth || this.fleetStatus || this.salesAnalytics || this.tripAnalytics) return null;
    return asGenericAnalytics(this.data, this.toolUsed);
  }

  get acein(): AssistantAcein | null {
    return this.fleetStatus?.acein
      ?? this.genericAnalytics?.acein
      ?? extractAcein(this.data);
  }

  get followUpActions(): string[] {
    const fromFleet = this.fleetStatus?.followUps;
    if (fromFleet?.length) return fromFleet;
    const fromTrip = this.tripAnalytics?.followUps;
    if (fromTrip?.length) return fromTrip;
    const fromSales = this.salesAnalytics?.followUps;
    if (fromSales?.length) return fromSales;
    const fromGeneric = this.genericAnalytics?.followUps;
    if (fromGeneric?.length) return fromGeneric;
    return extractFollowUps(this.data);
  }

  get recommendedActions(): string[] {
    const fromFleet = this.fleetStatus?.recommendedActions;
    if (fromFleet?.length) return fromFleet;
    const fromGeneric = this.genericAnalytics?.recommendedActions;
    if (fromGeneric?.length) return fromGeneric;
    return extractRecommendedActions(this.data);
  }

  get table(): AssistantDataTable | null {
    if (this.fleetHealth || this.fleetStatus || this.tripAnalytics || this.salesAnalytics || this.genericAnalytics) {
      return null;
    }
    return extractDataTable(this.data, this.toolUsed);
  }

  get showEmptyState(): boolean {
    if (this.structuredError) return false;
    if (this.fleetStatus?.empty) return true;
    if (!this.answer?.trim() && !this.fleetStatus && !this.tripAnalytics && !this.salesAnalytics
      && !this.fleetHealth && !this.genericAnalytics && !this.table) {
      return true;
    }
    return false;
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
      this.fleetStatus?.statusDistribution
      ?? this.salesAnalytics?.statusDistribution
      ?? this.tripAnalytics?.statusDistribution
      ?? this.genericAnalytics?.statusDistribution;
    if (!dist?.length) return null;
    const slices = dist.filter(s => s.count > 0);
    const source = slices.length ? slices : dist;
    return {
      labels: source.map(s => s.label),
      datasets: [
        {
          data: source.map(s => s.count),
          backgroundColor: source.map((s, i) => this.colorForStatus(s.key, i)),
          borderWidth: 0,
          hoverOffset: 4
        }
      ]
    };
  }

  get statusTotal(): number {
    const dist =
      this.fleetStatus?.statusDistribution
      ?? this.salesAnalytics?.statusDistribution
      ?? this.tripAnalytics?.statusDistribution
      ?? this.genericAnalytics?.statusDistribution;
    return dist?.reduce((sum, s) => sum + s.count, 0) ?? 0;
  }

  get statusLegend(): { label: string; count: number; color: string }[] {
    const dist =
      this.fleetStatus?.statusDistribution
      ?? this.salesAnalytics?.statusDistribution
      ?? this.tripAnalytics?.statusDistribution
      ?? this.genericAnalytics?.statusDistribution
      ?? [];
    return dist.map((s, i) => ({
      label: s.label,
      count: s.count,
      color: this.colorForStatus(s.key, i)
    }));
  }

  private colorForStatus(key: string | undefined, index: number): string {
    const k = (key ?? '').toLowerCase().replace(/\s+/g, '');
    return this.statusColorByKey[k] ?? STATUS_COLORS[index % STATUS_COLORS.length];
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
    const icons = ['warning', 'local_parking', 'check_circle', 'lightbulb', 'insights'];
    return icons[index % icons.length];
  }

  vehicleCountLabel(n: number): string {
    return `${n} vehicle${n === 1 ? '' : 's'}`;
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
      case 'totalVehicles':
        return 'directions_car';
      case 'online':
        return 'wifi';
      case 'offline':
        return 'wifi_off';
      case 'moving':
        return 'near_me';
      case 'parked':
        return 'local_parking';
      case 'idle':
        return 'pause_circle';
      case 'sos':
        return 'sos';
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
    return null;
  }

  insightTone(tip: string, index: number): string {
    const t = tip.toLowerCase();
    if (t.includes('pending') || t.includes('still') || t.includes('attention') || t.includes('risk')
      || t.includes('offline') || t.includes('sos')) {
      return 'warn';
    }
    if (index === 0) return 'ok';
    return 'neutral';
  }

  emitFollowUp(text: string): void {
    this.followUp.emit(text);
  }

  /** Relative “2 min ago” style for vehicle last-update column. */
  relativeTime(iso?: string | null): string {
    if (!iso) return '—';
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return '—';
    const diffMs = Date.now() - d.getTime();
    if (diffMs < 0) return 'just now';
    const mins = Math.floor(diffMs / 60_000);
    if (mins < 1) return 'just now';
    if (mins < 60) return `${mins} min ago`;
    const hours = Math.floor(mins / 60);
    if (hours < 48) return `${hours} hr ago`;
    const days = Math.floor(hours / 24);
    return `${days} day${days === 1 ? '' : 's'} ago`;
  }

  /** Location is not always present on get_fleet_status sample — never invent. */
  locationLabel(_v: unknown): string {
    return 'Location unavailable';
  }
}
