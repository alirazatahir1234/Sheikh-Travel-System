import { Component, OnDestroy, OnInit } from '@angular/core';
import { ChartData } from 'chart.js';
import { Subscription } from 'rxjs';
import { skip } from 'rxjs/operators';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import {
  AccidentRiskDashboard,
  AccidentRiskFactor,
  DriverAccidentRisk,
  RouteAccidentRisk
} from '../../../core/models/gps-tracking.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';
import { toDateInputValue } from '../../../core/utils/date-input.util';
import {
  TripDatePreset,
  ANALYTICS_DATE_PRESETS,
  applyTripDatePreset,
  validateAnalyticsDateRange
} from '../utils/trip-date-preset.util';
import { GpsScopeFilterService } from '../gps-scope-filter.service';
import { UiChartOptions } from '../../../shared/components/ui/chart/ui-chart.component';

type RiskBandFilter = 'all' | 'high' | 'moderate' | 'low';
type TableTab = 'drivers' | 'routes';

interface FactorBucket {
  key: string;
  label: string;
  color: string;
  count: number;
  points: number;
}

@Component({
  standalone: false,
  selector: 'app-accident-risk-page',
  templateUrl: './accident-risk-page.component.html',
  styleUrls: ['./accident-risk-page.component.scss']
})
export class AccidentRiskPageComponent implements OnInit, OnDestroy {
  readonly datePresets = ANALYTICS_DATE_PRESETS;
  datePreset: TripDatePreset = 'last30Days';
  from = '';
  to = '';
  dateRangeError: string | null = null;

  loading = false;
  kpiLoading = false;
  loadError: string | null = null;
  dashboard: AccidentRiskDashboard | null = null;

  tab: TableTab = 'drivers';
  searchQuery = '';
  riskFilter: RiskBandFilter = 'all';

  expandedDriverId: number | null = null;
  expandedRouteId: number | null = null;
  selectedDriverId: number | null = null;

  factorBuckets: FactorBucket[] = [];
  factorsChartData: ChartData<'doughnut'> = { labels: [], datasets: [] };
  // Cast to UiChartOptions (union of chart types) — ChartOptions<'doughnut'> is not assignable to the union.
  readonly factorsChartOptions: UiChartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { display: false },
      tooltip: {
        callbacks: {
          label: (ctx: { label?: string; parsed: unknown }) => {
            const label = ctx.label ?? '';
            const value = typeof ctx.parsed === 'number' ? ctx.parsed : 0;
            return `${label}: ${value}`;
          }
        }
      }
    },
    cutout: '68%'
  } as UiChartOptions;

  /** No time-series endpoint — keep empty chart shell for layout only. */
  trendsChartData: ChartData<'line'> = { labels: [], datasets: [] };
  readonly trendsChartOptions: UiChartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: {
      x: { grid: { display: false }, ticks: { color: '#94a3b8', maxRotation: 0 } },
      y: {
        beginAtZero: true,
        max: 100,
        grid: { color: 'rgba(15, 23, 42, 0.06)' },
        ticks: { color: '#94a3b8' }
      }
    }
  };

  private scopeSub: Subscription | null = null;
  private loadSub: Subscription | null = null;

  constructor(
    private gps: GpsTrackingService,
    private scopeFilter: GpsScopeFilterService
  ) {}

  ngOnInit(): void {
    this.applyPreset(this.datePreset);
    this.load();
    this.scopeSub = this.scopeFilter.scope$.pipe(skip(1)).subscribe(() => this.load());
  }

  ngOnDestroy(): void {
    this.scopeSub?.unsubscribe();
    this.loadSub?.unsubscribe();
  }

  get needsCompanySelection(): boolean {
    return !this.scopeFilter.isFleetScopeReady;
  }

  get drivers(): DriverAccidentRisk[] {
    return this.dashboard?.drivers ?? [];
  }

  get routes(): RouteAccidentRisk[] {
    return this.dashboard?.routes ?? [];
  }

  get highRiskDrivers(): number | null {
    if (!this.dashboard) return null;
    return this.dashboard.highRiskDriverCount;
  }

  get highRiskRoutes(): number | null {
    if (!this.dashboard) return null;
    return this.dashboard.highRiskRouteCount;
  }

  /** Aggregate fleet index from scored drivers — null when no drivers (not inventing zero-risk). */
  get fleetRiskScore(): number | null {
    const list = this.drivers;
    if (!list.length) return null;
    return Math.round(list.reduce((s, d) => s + (d.riskScore ?? 0), 0) / list.length);
  }

  get factorsTotal(): number {
    return this.factorBuckets.reduce((s, b) => s + b.count, 0);
  }

  get hasTrendSeries(): boolean {
    return false;
  }

  get filteredDrivers(): DriverAccidentRisk[] {
    const q = this.searchQuery.trim().toLowerCase();
    return this.drivers
      .filter(d => {
        if (q) {
          const name = (d.driverName ?? '').toLowerCase();
          if (!name.includes(q) && !String(d.driverId).includes(q)) return false;
        }
        return this.matchesBand(d.riskBand);
      })
      .sort((a, b) => (b.riskScore ?? 0) - (a.riskScore ?? 0));
  }

  get filteredRoutes(): RouteAccidentRisk[] {
    const q = this.searchQuery.trim().toLowerCase();
    return this.routes
      .filter(r => {
        if (q) {
          const label = (r.routeLabel ?? '').toLowerCase();
          if (!label.includes(q) && !String(r.routeId).includes(q)) return false;
        }
        return this.matchesBand(r.riskBand);
      })
      .sort((a, b) => (b.riskScore ?? 0) - (a.riskScore ?? 0));
  }

  get tableCount(): number {
    return this.tab === 'drivers' ? this.filteredDrivers.length : this.filteredRoutes.length;
  }

  applyPreset(preset: TripDatePreset): void {
    this.datePreset = preset;
    const range = applyTripDatePreset(preset);
    this.from = toDateInputValue(range.from);
    this.to = toDateInputValue(range.to);
    this.dateRangeError = validateAnalyticsDateRange(this.from, this.to);
  }

  onCustomDatesChange(): void {
    this.datePreset = 'custom';
    this.dateRangeError = validateAnalyticsDateRange(this.from, this.to);
  }

  load(): void {
    if (this.dateRangeError) return;
    if (this.needsCompanySelection) {
      this.dashboard = null;
      this.rebuildDerived();
      return;
    }

    this.loadSub?.unsubscribe();
    this.loading = true;
    this.kpiLoading = !this.dashboard;
    this.loadError = null;

    this.loadSub = this.gps.getAccidentRiskDashboard(this.from, this.to).subscribe({
      next: data => {
        this.dashboard = data;
        this.rebuildDerived();
        this.loading = false;
        this.kpiLoading = false;
      },
      error: err => {
        this.loadError = apiErrorMessage(err);
        this.loading = false;
        this.kpiLoading = false;
      }
    });
  }

  setTab(tab: TableTab): void {
    this.tab = tab;
    this.searchQuery = '';
    this.expandedDriverId = null;
    this.expandedRouteId = null;
    this.selectedDriverId = null;
  }

  toggleDriver(id: number): void {
    this.expandedDriverId = this.expandedDriverId === id ? null : id;
    this.selectedDriverId = this.expandedDriverId;
  }

  toggleRoute(id: number): void {
    this.expandedRouteId = this.expandedRouteId === id ? null : id;
  }

  viewSelectedDetails(): void {
    if (this.tab === 'drivers' && this.selectedDriverId != null) {
      this.expandedDriverId = this.selectedDriverId;
      return;
    }
    const first = this.filteredDrivers[0];
    if (first) {
      this.selectedDriverId = first.driverId;
      this.expandedDriverId = first.driverId;
    }
  }

  bandClass(band: string): string {
    const b = (band ?? '').toLowerCase();
    if (b === 'high') return 'band-high';
    if (b === 'moderate' || b === 'medium') return 'band-mod';
    return 'band-low';
  }

  initials(name: string | null | undefined): string {
    const parts = (name ?? '').trim().split(/\s+/).filter(Boolean);
    if (!parts.length) return '?';
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }

  barWidth(score: number | undefined | null): string {
    const v = Math.max(0, Math.min(100, score ?? 0));
    return `${v}%`;
  }

  topFactors(factors: AccidentRiskFactor[] | undefined | null, limit = 2): AccidentRiskFactor[] {
    return [...(factors ?? [])]
      .filter(f => (f.points ?? 0) > 0 || (f.rawCount ?? 0) > 0)
      .sort((a, b) => (b.points ?? 0) - (a.points ?? 0))
      .slice(0, limit);
  }

  exportCsv(): void {
    if (this.tab === 'drivers') {
      const rows = this.filteredDrivers;
      if (!rows.length) return;
      const header = ['Rank', 'DriverId', 'DriverName', 'RiskScore', 'RiskBand', 'KeyFactors'];
      const lines = [
        header.join(','),
        ...rows.map((r, i) =>
          [
            i + 1,
            r.driverId,
            csvEscape(r.driverName),
            r.riskScore,
            csvEscape(r.riskBand),
            csvEscape(this.topFactors(r.factors, 5).map(f => f.label).join('; '))
          ].join(',')
        )
      ];
      this.downloadCsv(lines.join('\n'), `accident-risk-drivers-${this.from}_${this.to}.csv`);
      return;
    }

    const rows = this.filteredRoutes;
    if (!rows.length) return;
    const header = ['Rank', 'RouteId', 'Route', 'DistanceKm', 'Trips', 'RiskScore', 'RiskBand', 'KeyFactors'];
    const lines = [
      header.join(','),
      ...rows.map((r, i) =>
        [
          i + 1,
          r.routeId,
          csvEscape(r.routeLabel),
          r.distanceKm,
          r.tripCountInPeriod,
          r.riskScore,
          csvEscape(r.riskBand),
          csvEscape(this.topFactors(r.factors, 5).map(f => f.label).join('; '))
        ].join(',')
      )
    ];
    this.downloadCsv(lines.join('\n'), `accident-risk-routes-${this.from}_${this.to}.csv`);
  }

  private matchesBand(band: string): boolean {
    if (this.riskFilter === 'all') return true;
    const b = (band ?? '').toLowerCase();
    if (this.riskFilter === 'moderate') return b === 'moderate' || b === 'medium';
    return b === this.riskFilter;
  }

  private rebuildDerived(): void {
    this.factorBuckets = this.buildFactorBuckets();
    const labels = this.factorBuckets.map(b => b.label);
    const data = this.factorBuckets.map(b => b.count);
    const colors = this.factorBuckets.map(b => b.color);
    this.factorsChartData = {
      labels,
      datasets: [
        {
          data,
          backgroundColor: colors,
          borderWidth: 0,
          hoverOffset: 4
        }
      ]
    };
    // Intentionally empty — API has no risk time-series.
    this.trendsChartData = { labels: [], datasets: [] };
  }

  private buildFactorBuckets(): FactorBucket[] {
    const buckets: Record<string, FactorBucket> = {
      speed: { key: 'speed', label: 'Speed Violations', color: '#ef4444', count: 0, points: 0 },
      fatigue: { key: 'fatigue', label: 'Fatigue Events', color: '#f59e0b', count: 0, points: 0 },
      accidents: { key: 'accidents', label: 'Past Accidents', color: '#3b82f6', count: 0, points: 0 },
      route_difficulty: {
        key: 'route_difficulty',
        label: 'Route Difficulty',
        color: '#9333ea',
        count: 0,
        points: 0
      },
      other: { key: 'other', label: 'Other Factors', color: '#64748b', count: 0, points: 0 }
    };

    const ingest = (factors: AccidentRiskFactor[] | undefined) => {
      for (const f of factors ?? []) {
        const key = this.mapFactorKey(f.key);
        const bucket = buckets[key] ?? buckets['other'];
        bucket.count += Math.max(0, f.rawCount ?? 0);
        bucket.points += Math.max(0, f.points ?? 0);
      }
    };

    for (const d of this.drivers) ingest(d.factors);
    for (const r of this.routes) ingest(r.factors);

    // Prefer raw event counts; fall back to contribution points so zero-count
    // difficulty factors still appear when they contributed to scores.
    return Object.values(buckets).map(b => ({
      ...b,
      count: b.count > 0 ? b.count : b.points > 0 ? b.points : 0
    }));
  }

  private mapFactorKey(key: string): string {
    const k = (key ?? '').toLowerCase();
    if (k.includes('speed')) return 'speed';
    if (k.includes('fatigue')) return 'fatigue';
    if (k.includes('accident')) return 'accidents';
    if (k.includes('route') || k.includes('difficulty')) return 'route_difficulty';
    return 'other';
  }

  private downloadCsv(content: string, filename: string): void {
    const blob = new Blob([content], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }
}

function csvEscape(value: string | null | undefined): string {
  const s = value ?? '';
  if (/[",\n]/.test(s)) return `"${s.replace(/"/g, '""')}"`;
  return s;
}
