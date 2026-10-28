import { Component, OnInit } from '@angular/core';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { AuthService } from '../../../core/services/auth.service';
import {
  DriverPerformanceDetail,
  DriverScore,
  DriverScoreWeights
} from '../../../core/models/gps-tracking.model';
import { UiTableColumn } from '../../../shared/components/ui';
import { apiErrorMessage } from '../../../core/utils/api-error.util';
import { toDateInputValue } from '../../../core/utils/date-input.util';
import {
  TripDatePreset,
  ANALYTICS_DATE_PRESETS,
  applyTripDatePreset,
  validateAnalyticsDateRange
} from '../utils/trip-date-preset.util';

@Component({
  standalone: false,
  selector: 'app-driver-performance-page',
  templateUrl: './driver-performance-page.component.html',
  styleUrls: ['./driver-performance-page.component.scss']
})
export class DriverPerformancePageComponent implements OnInit {
  readonly datePresets = ANALYTICS_DATE_PRESETS;
  datePreset: TripDatePreset = 'last30Days';
  from = '';
  to = '';
  dateRangeError: string | null = null;
  loading = false;
  loadError: string | null = null;
  rows: DriverScore[] = [];

  selectedDriverId: number | null = null;
  detailLoading = false;
  detailError: string | null = null;
  detail: DriverPerformanceDetail | null = null;

  weights: DriverScoreWeights = { safetyWeight: 40, fuelWeight: 30, onTimeWeight: 30 };
  weightsDraft: DriverScoreWeights = { ...this.weights };
  weightsError: string | null = null;
  weightsSaving = false;
  weightsMessage: string | null = null;

  readonly columns: UiTableColumn[] = [
    { key: 'rank', label: '#', align: 'right', sortable: true },
    { key: 'driverName', label: 'Driver', sortable: true },
    { key: 'score', label: 'Overall', align: 'right', sortable: true },
    { key: 'safetyScore', label: 'Safety', align: 'right' },
    { key: 'fuelEfficiencyScore', label: 'Fuel', align: 'right' },
    { key: 'onTimeScore', label: 'On-time', align: 'right' },
    { key: 'rating', label: 'Rating' },
    { key: 'flags', label: 'Flags' }
  ];

  constructor(
    private gps: GpsTrackingService,
    private auth: AuthService
  ) {}

  get canManageWeights(): boolean {
    return this.auth.hasPermission('Driver.ManagePerformanceWeights');
  }

  ngOnInit(): void {
    this.applyPreset(this.datePreset);
    this.loadWeights();
    this.load();
  }

  applyPreset(preset: TripDatePreset): void {
    this.datePreset = preset;
    const range = applyTripDatePreset(preset);
    this.from = toDateInputValue(range.from);
    this.to = toDateInputValue(range.to);
    this.dateRangeError = validateAnalyticsDateRange(this.from, this.to);
  }

  load(): void {
    if (this.dateRangeError) return;
    this.loading = true;
    this.loadError = null;
    this.gps.getDriverScoreRanking({ from: this.from, to: this.to }).subscribe({
      next: rows => {
        this.rows = rows ?? [];
        this.loading = false;
        if (this.selectedDriverId != null) {
          this.openDetail(this.selectedDriverId);
        }
      },
      error: err => {
        this.loadError = apiErrorMessage(err);
        this.loading = false;
      }
    });
  }

  loadWeights(): void {
    this.gps.getDriverScoreWeights().subscribe({
      next: w => {
        this.weights = w;
        this.weightsDraft = { ...w };
      },
      error: () => {
        /* ranking still works with server defaults */
      }
    });
  }

  saveWeights(): void {
    if (!this.canManageWeights) return;
    this.weightsSaving = true;
    this.weightsError = null;
    this.weightsMessage = null;
    this.gps.upsertDriverScoreWeights(this.weightsDraft).subscribe({
      next: w => {
        this.weights = w;
        this.weightsDraft = { ...w };
        this.weightsSaving = false;
        this.weightsMessage = 'Weights saved. Refresh ranking to recompute Overall.';
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
    this.gps.getDriverPerformanceDetail(driverId, { from: this.from, to: this.to }).subscribe({
      next: detail => {
        this.detail = detail;
        this.detailLoading = false;
      },
      error: err => {
        this.detailError = apiErrorMessage(err);
        this.detailLoading = false;
      }
    });
  }

  clearDetail(): void {
    this.selectedDriverId = null;
    this.detail = null;
    this.detailError = null;
  }

  flags(row: DriverScore): string {
    const parts: string[] = [];
    if (row.rewardEligible) parts.push('Reward');
    if (row.needsTraining) parts.push('Training');
    if (row.isPartial) parts.push('Partial data');
    if (row.fuelUsedPeerBand) parts.push('Peer fuel');
    return parts.join(' · ') || '—';
  }

  barWidth(score: number | undefined | null): string {
    const v = Math.max(0, Math.min(100, score ?? 0));
    return `${v}%`;
  }
}
