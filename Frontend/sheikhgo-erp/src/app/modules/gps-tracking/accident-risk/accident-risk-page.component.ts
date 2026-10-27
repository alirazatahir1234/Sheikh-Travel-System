import { Component, OnInit } from '@angular/core';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import {
  AccidentRiskDashboard,
  DriverAccidentRisk,
  RouteAccidentRisk
} from '../../../core/models/gps-tracking.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';
import {
  TripDatePreset,
  ANALYTICS_DATE_PRESETS,
  applyTripDatePreset,
  validateAnalyticsDateRange
} from '../utils/trip-date-preset.util';

@Component({
  standalone: false,
  selector: 'app-accident-risk-page',
  templateUrl: './accident-risk-page.component.html',
  styleUrls: ['./accident-risk-page.component.scss']
})
export class AccidentRiskPageComponent implements OnInit {
  readonly datePresets = ANALYTICS_DATE_PRESETS;
  datePreset: TripDatePreset = 'last30Days';
  from = '';
  to = '';
  dateRangeError: string | null = null;
  loading = false;
  loadError: string | null = null;
  dashboard: AccidentRiskDashboard | null = null;
  tab: 'drivers' | 'routes' = 'drivers';
  expandedDriverId: number | null = null;
  expandedRouteId: number | null = null;

  constructor(private gps: GpsTrackingService) {}

  ngOnInit(): void {
    this.applyPreset(this.datePreset);
    this.load();
  }

  get drivers(): DriverAccidentRisk[] {
    return this.dashboard?.drivers ?? [];
  }

  get routes(): RouteAccidentRisk[] {
    return this.dashboard?.routes ?? [];
  }

  applyPreset(preset: TripDatePreset): void {
    this.datePreset = preset;
    const range = applyTripDatePreset(preset);
    this.from = range.from;
    this.to = range.to;
    this.dateRangeError = validateAnalyticsDateRange(this.from, this.to);
  }

  load(): void {
    if (this.dateRangeError) return;
    this.loading = true;
    this.loadError = null;
    this.gps.getAccidentRiskDashboard(this.from, this.to).subscribe({
      next: data => {
        this.dashboard = data;
        this.loading = false;
      },
      error: err => {
        this.loadError = apiErrorMessage(err);
        this.loading = false;
      }
    });
  }

  toggleDriver(id: number): void {
    this.expandedDriverId = this.expandedDriverId === id ? null : id;
  }

  toggleRoute(id: number): void {
    this.expandedRouteId = this.expandedRouteId === id ? null : id;
  }

  bandClass(band: string): string {
    const b = band.toLowerCase();
    if (b === 'high') return 'band-high';
    if (b === 'moderate') return 'band-mod';
    return 'band-low';
  }
}
