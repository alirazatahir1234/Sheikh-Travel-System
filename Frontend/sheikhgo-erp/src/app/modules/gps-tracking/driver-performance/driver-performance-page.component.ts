import { Component, OnDestroy, OnInit } from '@angular/core';
import { Subscription, TimeoutError, of } from 'rxjs';
import { catchError, finalize, skip, timeout } from 'rxjs/operators';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { AuthService } from '../../../core/services/auth.service';
import {
  DriverPerformanceDetail,
  DriverScore,
  DriverScoreWeights
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

type PerformanceBand = 'all' | 'excellent' | 'good' | 'fair' | 'poor' | 'training' | 'reward';
type SortKey = 'overall' | 'safety' | 'fuel' | 'onTime' | 'trips' | 'name';

const DEFAULT_WEIGHTS: DriverScoreWeights = { safetyWeight: 40, fuelWeight: 30, onTimeWeight: 30 };
/** SQL ranking path (no live Traccar) — still fail closed so KPIs never hang. */
const RANKING_TIMEOUT_MS = 45_000;

@Component({
  standalone: false,
  selector: 'app-driver-performance-page',
  templateUrl: './driver-performance-page.component.html',
  styleUrls: ['./driver-performance-page.component.scss']
})
export class DriverPerformancePageComponent implements OnInit, OnDestroy {
  readonly datePresets = ANALYTICS_DATE_PRESETS;
  datePreset: TripDatePreset = 'last30Days';
  from = '';
  to = '';
  dateRangeError: string | null = null;
  /** Full ranking/table load. KPIs keep prior values while refreshing (stale-while-revalidate). */
  loading = false;
  /** True only when KPI cards have no rows yet (first paint / empty scope). */
  kpiLoading = false;
  loadError: string | null = null;
  rows: DriverScore[] = [];

  searchQuery = '';
  performanceFilter: PerformanceBand = 'all';
  sortKey: SortKey = 'overall';

  selectedDriverId: number | null = null;
  detailLoading = false;
  detailError: string | null = null;
  detail: DriverPerformanceDetail | null = null;

  weights: DriverScoreWeights = { ...DEFAULT_WEIGHTS };
  weightsDraft: DriverScoreWeights = { ...DEFAULT_WEIGHTS };
  weightsError: string | null = null;
  weightsSaving = false;
  weightsMessage: string | null = null;

  private scopeSub: Subscription | null = null;
  private loadSub: Subscription | null = null;
  private detailSub: Subscription | null = null;

  constructor(
    private gps: GpsTrackingService,
    private auth: AuthService,
    private scopeFilter: GpsScopeFilterService
  ) {}

  get canManageWeights(): boolean {
    return this.auth.hasPermission('Driver.ManagePerformanceWeights');
  }

  get needsCompanySelection(): boolean {
    return !this.scopeFilter.isFleetScopeReady;
  }

  get tenantScopeId(): number | null | undefined {
    return this.scopeFilter.tenantScopeId;
  }

  get vehicleFilterId(): number | null {
    return this.scopeFilter.vehicleFilterId;
  }

  get filteredRows(): DriverScore[] {
    const q = this.searchQuery.trim().toLowerCase();
    let list = this.rows.filter(r => {
      if (q) {
        const idLabel = this.driverCode(r.driverId).toLowerCase();
        const name = (r.driverName ?? '').toLowerCase();
        if (!name.includes(q) && !idLabel.includes(q) && !String(r.driverId).includes(q)) {
          return false;
        }
      }
      switch (this.performanceFilter) {
        case 'excellent':
          return (r.score ?? 0) >= 90;
        case 'good':
          return (r.score ?? 0) >= 75 && (r.score ?? 0) < 90;
        case 'fair':
          return (r.score ?? 0) >= 60 && (r.score ?? 0) < 75;
        case 'poor':
          return (r.score ?? 0) < 60;
        case 'training':
          return !!r.needsTraining;
        case 'reward':
          return !!r.rewardEligible;
        default:
          return true;
      }
    });

    const dir = 1;
    list = [...list].sort((a, b) => {
      switch (this.sortKey) {
        case 'safety':
          return ((b.safetyScore ?? 0) - (a.safetyScore ?? 0)) * dir;
        case 'fuel':
          return ((b.fuelEfficiencyScore ?? 0) - (a.fuelEfficiencyScore ?? 0)) * dir;
        case 'onTime':
          return ((b.onTimeScore ?? 0) - (a.onTimeScore ?? 0)) * dir;
        case 'trips':
          return ((b.factors?.tripCount ?? 0) - (a.factors?.tripCount ?? 0)) * dir;
        case 'name':
          return (a.driverName ?? '').localeCompare(b.driverName ?? '');
        case 'overall':
        default:
          return ((b.score ?? 0) - (a.score ?? 0)) * dir || ((a.rank ?? 0) - (b.rank ?? 0));
      }
    });
    return list;
  }

  get kpiAvgScore(): number | null {
    if (!this.rows.length) return null;
    return Math.round(this.rows.reduce((s, r) => s + (r.score ?? 0), 0) / this.rows.length);
  }

  get kpiDrivers(): number {
    return this.rows.length;
  }

  get kpiSafety(): number | null {
    return this.avgOf(r => r.safetyScore);
  }

  get kpiFuel(): number | null {
    return this.avgOf(r => r.fuelEfficiencyScore);
  }

  get kpiOnTime(): number | null {
    return this.avgOf(r => r.onTimeScore);
  }

  get avgScoreCaption(): string {
    const v = this.kpiAvgScore;
    if (v == null) return 'No scored drivers yet';
    if (v >= 90) return 'Excellent performance';
    if (v >= 75) return 'Good performance';
    if (v >= 60) return 'Fair performance';
    return 'Needs coaching focus';
  }

  ngOnInit(): void {
    this.applyPreset(this.datePreset);
    this.scopeSub = this.scopeFilter.scope$.pipe(skip(1)).subscribe(() => {
      this.clearDetail();
      this.loadWeights();
      this.load();
    });
    this.loadWeights();
    this.load();
  }

  ngOnDestroy(): void {
    this.scopeSub?.unsubscribe();
    this.loadSub?.unsubscribe();
    this.detailSub?.unsubscribe();
  }

  applyPreset(preset: TripDatePreset): void {
    this.datePreset = preset;
    if (preset === 'custom') {
      this.dateRangeError = validateAnalyticsDateRange(this.from, this.to);
      return;
    }
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
    this.loadSub?.unsubscribe();
    if (this.needsCompanySelection) {
      this.rows = [];
      this.loading = false;
      this.kpiLoading = false;
      this.loadError = null;
      return;
    }
    if (this.dateRangeError) return;

    this.loading = true;
    // Keep previous KPI numbers on reload; skeleton only when nothing to show yet.
    this.kpiLoading = this.rows.length === 0;
    this.loadError = null;
    const filters = this.analyticsFilters();

    this.loadSub = this.gps
      .getDriverScoreRanking(filters, this.tenantScopeId)
      .pipe(
        timeout(RANKING_TIMEOUT_MS),
        catchError(err => {
          if (err instanceof TimeoutError) {
            this.loadError =
              'Ranking timed out. Try a shorter period or retry.';
          } else {
            this.loadError = apiErrorMessage(err);
          }
          return of([] as DriverScore[]);
        }),
        finalize(() => {
          this.loading = false;
          this.kpiLoading = false;
        })
      )
      .subscribe(rows => {
        this.rows = rows ?? [];
        this.kpiLoading = false;
        if (this.selectedDriverId != null) {
          const stillThere = this.rows.some(r => r.driverId === this.selectedDriverId);
          if (stillThere) this.openDetail(this.selectedDriverId);
          else this.clearDetail();
        }
      });
  }

  loadWeights(): void {
    if (this.needsCompanySelection) return;
    this.gps.getDriverScoreWeights(this.tenantScopeId).subscribe({
      next: w => {
        this.weights = w;
        this.weightsDraft = { ...w };
      },
      error: () => {
        /* ranking still works with server defaults */
      }
    });
  }

  resetWeights(): void {
    if (!this.canManageWeights) return;
    this.weightsDraft = { ...DEFAULT_WEIGHTS };
    this.weightsMessage = null;
    this.weightsError = null;
  }

  nudgeWeight(key: 'safetyWeight' | 'fuelWeight' | 'onTimeWeight', delta: number): void {
    if (!this.canManageWeights) return;
    const next = Math.max(0, Math.min(1000, (this.weightsDraft[key] ?? 0) + delta));
    this.weightsDraft = { ...this.weightsDraft, [key]: next };
    this.weightsMessage = null;
    this.weightsError = null;
  }

  saveWeights(): void {
    if (!this.canManageWeights || this.needsCompanySelection) return;
    this.weightsSaving = true;
    this.weightsError = null;
    this.weightsMessage = null;
    this.gps.upsertDriverScoreWeights(this.weightsDraft, this.tenantScopeId).subscribe({
      next: w => {
        this.weights = w;
        this.weightsDraft = { ...w };
        this.weightsSaving = false;
        this.weightsMessage = 'Weights saved. Ranking recomputed with new Overall.';
        this.load();
      },
      error: err => {
        this.weightsSaving = false;
        this.weightsError = apiErrorMessage(err);
      }
    });
  }

  openDetail(driverId: number): void {
    this.selectedDriverId = driverId;
    this.detailLoading = true;
    this.detailError = null;
    this.detailSub?.unsubscribe();
    this.detailSub = this.gps
      .getDriverPerformanceDetail(driverId, this.analyticsFilters(), this.tenantScopeId)
      .pipe(
        timeout(RANKING_TIMEOUT_MS),
        finalize(() => {
          this.detailLoading = false;
        })
      )
      .subscribe({
        next: detail => {
          this.detail = detail;
        },
        error: err => {
          this.detail = null;
          this.detailError =
            err instanceof TimeoutError
              ? 'Detail timed out. Retry or shorten the period.'
              : apiErrorMessage(err);
        }
      });
  }

  clearDetail(): void {
    this.detailSub?.unsubscribe();
    this.selectedDriverId = null;
    this.detail = null;
    this.detailError = null;
    this.detailLoading = false;
  }

  exportRankings(): void {
    const rows = this.filteredRows;
    if (!rows.length) return;
    const header = [
      'Rank',
      'DriverId',
      'DriverName',
      'Overall',
      'Safety',
      'Fuel',
      'OnTime',
      'Trips',
      'Rating',
      'RewardEligible',
      'NeedsTraining',
      'Partial'
    ];
    const lines = [
      header.join(','),
      ...rows.map(r =>
        [
          r.rank ?? '',
          r.driverId,
          csvEscape(r.driverName),
          r.score,
          r.safetyScore ?? '',
          r.fuelEfficiencyScore ?? '',
          r.onTimeScore ?? '',
          r.factors?.tripCount ?? '',
          csvEscape(r.rating),
          r.rewardEligible ? '1' : '0',
          r.needsTraining ? '1' : '0',
          r.isPartial ? '1' : '0'
        ].join(',')
      )
    ];
    const blob = new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `driver-performance-${this.from}_${this.to}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  driverCode(driverId: number): string {
    return `DRV-${String(driverId).padStart(3, '0')}`;
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

  scoreTone(score: number | undefined | null): 'good' | 'warn' | 'bad' | 'muted' {
    if (score == null) return 'muted';
    if (score >= 75) return 'good';
    if (score >= 60) return 'warn';
    return 'bad';
  }

  rankMedal(rank: number | undefined | null): 'gold' | 'silver' | 'bronze' | null {
    if (rank === 1) return 'gold';
    if (rank === 2) return 'silver';
    if (rank === 3) return 'bronze';
    return null;
  }

  /** Best available trend: latest history delta when detail matches; else null (no invented %). */
  rowTrend(row: DriverScore): number | null {
    if (this.detail?.score.driverId === row.driverId && this.detail.history.length) {
      return this.detail.history[0].delta ?? null;
    }
    return null;
  }

  flags(row: DriverScore): string {
    const parts: string[] = [];
    if (row.rewardEligible) parts.push('Reward');
    if (row.needsTraining) parts.push('Training');
    if (row.isPartial) parts.push('Partial data');
    if (row.fuelUsedPeerBand) parts.push('Peer fuel');
    return parts.join(' · ') || '—';
  }

  private analyticsFilters() {
    return {
      from: this.from,
      to: this.to,
      vehicleId: this.vehicleFilterId ?? undefined
    };
  }

  private avgOf(pick: (r: DriverScore) => number | undefined | null): number | null {
    const vals = this.rows.map(pick).filter((v): v is number => v != null && !Number.isNaN(v));
    if (!vals.length) return null;
    return Math.round(vals.reduce((a, b) => a + b, 0) / vals.length);
  }
}

function csvEscape(value: string | null | undefined): string {
  const s = value ?? '';
  if (/[",\n]/.test(s)) return `"${s.replace(/"/g, '""')}"`;
  return s;
}
