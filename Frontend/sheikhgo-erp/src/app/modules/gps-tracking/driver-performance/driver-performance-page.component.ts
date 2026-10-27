import { Component, OnInit } from '@angular/core';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { DriverScore } from '../../../core/models/gps-tracking.model';
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

  constructor(private gps: GpsTrackingService) {}

  ngOnInit(): void {
    this.applyPreset(this.datePreset);
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
      },
      error: err => {
        this.loadError = apiErrorMessage(err);
        this.loading = false;
      }
    });
  }

  flags(row: DriverScore): string {
    const parts: string[] = [];
    if (row.rewardEligible) parts.push('Reward');
    if (row.needsTraining) parts.push('Training');
    if (row.isPartial) parts.push('Partial data');
    return parts.join(' · ') || '—';
  }
}
