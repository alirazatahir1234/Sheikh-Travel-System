import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ChartData } from 'chart.js';
import { ExportService } from '../../../core/services/export.service';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import {
  GpsTrip,
  TripDetailBundle,
  TripEvent,
  TripReplayPosition,
  TripStop
} from '../../../core/models/gps-tracking.model';
import { formatTripEventType } from '../utils/trip-event.util';

@Component({
  standalone: false,
  selector: 'app-trip-detail-page',
  templateUrl: './trip-detail-page.component.html',
  styleUrls: ['./trip-detail-page.component.scss']
})
export class TripDetailPageComponent implements OnInit {
  tripKey = '';
  loading = true;
  error = '';
  bundle: TripDetailBundle | null = null;
  driverName = '';

  speedChartData: ChartData<'line'> = { labels: [], datasets: [] };
  altitudeChartData: ChartData<'line'> = { labels: [], datasets: [] };

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private gps: GpsTrackingService,
    private exportService: ExportService
  ) {}

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const key = params.get('tripKey');
      if (!key) {
        void this.router.navigate(['/gps-tracking/trips']);
        return;
      }
      this.tripKey = key;
      this.load();
    });
  }

  get trip(): GpsTrip | null {
    return this.bundle?.trip ?? null;
  }

  get replayRoute(): TripReplayPosition[] {
    return this.bundle?.route ?? [];
  }

  get replayPlayback(): TripReplayPosition[] {
    const b = this.bundle;
    if (!b) return [];
    return b.playback?.length ? b.playback : b.route;
  }

  get stops(): TripStop[] {
    return this.bundle?.stops ?? [];
  }

  get events(): TripEvent[] {
    return this.bundle?.events ?? [];
  }

  load(): void {
    this.loading = true;
    this.error = '';
    this.gps.getTripDetail(this.tripKey, false).subscribe({
      next: bundle => {
        this.bundle = bundle;
        this.driverName = bundle.trip.driverName ?? '';
        this.buildCharts(bundle.playback?.length ? bundle.playback : bundle.route);
        this.loading = false;
      },
      error: err => {
        this.error = err?.error?.message ?? 'Failed to load trip details.';
        this.loading = false;
      }
    });
  }

  back(): void {
    void this.router.navigate(['/gps-tracking/trips']);
  }

  printPage(): void {
    window.print();
  }

  exportDetail(kind: 'excel' | 'pdf' | 'csv'): void {
    if (!this.trip) return;
    const t = this.trip;
    const summaryRows = [{
      vehicle: t.vehicleName ?? `Vehicle #${t.vehicleId}`,
      driver: t.driverName ?? '—',
      start: t.startTime,
      end: t.endTime,
      distance: t.distanceKm,
      avgSpeed: t.avgSpeedKmh,
      maxSpeed: t.maxSpeedKmh,
      duration: t.durationMinutes,
      fuel: t.fuelLiters ?? '—'
    }];
    const meta = {
      filename: `trip-${t.vehicleId}-${new Date(t.startTime).toISOString().slice(0, 10)}`,
      sheetName: 'Trip Summary',
      title: `Trip — ${t.vehicleName ?? t.vehicleId}`
    };
    const cols = [
      { header: 'Vehicle', accessor: (r: typeof summaryRows[0]) => r.vehicle },
      { header: 'Driver', accessor: (r: typeof summaryRows[0]) => r.driver },
      { header: 'Start', accessor: (r: typeof summaryRows[0]) => r.start },
      { header: 'End', accessor: (r: typeof summaryRows[0]) => r.end },
      { header: 'Distance (km)', accessor: (r: typeof summaryRows[0]) => r.distance },
      { header: 'Avg speed', accessor: (r: typeof summaryRows[0]) => r.avgSpeed },
      { header: 'Max speed', accessor: (r: typeof summaryRows[0]) => r.maxSpeed },
      { header: 'Duration (min)', accessor: (r: typeof summaryRows[0]) => r.duration },
      { header: 'Fuel (L)', accessor: (r: typeof summaryRows[0]) => r.fuel }
    ];
    if (kind === 'pdf') this.exportService.exportPdf(summaryRows, cols, meta);
    else if (kind === 'csv') this.exportService.exportCsv(summaryRows, cols, meta);
    else this.exportService.exportExcel(summaryRows, cols, meta);
  }

  formatEventType(type: string): string {
    return formatTripEventType(type);
  }

  formatDuration(minutes: number): string {
    if (minutes < 60) return `${minutes} min`;
    const h = Math.floor(minutes / 60);
    const m = minutes % 60;
    return m ? `${h}h ${m}m` : `${h}h`;
  }

  private buildCharts(positions: TripReplayPosition[]): void {
    const labels = positions.map(p =>
      new Date(p.timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }));
    this.speedChartData = {
      labels,
      datasets: [{
        label: 'Speed (km/h)',
        data: positions.map(p => Number(p.speedKmh) || 0),
        borderColor: '#0f766e',
        backgroundColor: 'rgba(15, 118, 110, 0.12)',
        fill: true,
        tension: 0.25,
        pointRadius: 0
      }]
    };
    const hasAltitude = positions.some(p => p.altitude != null);
    this.altitudeChartData = hasAltitude ? {
      labels,
      datasets: [{
        label: 'Altitude (m)',
        data: positions.map(p => p.altitude ?? 0),
        borderColor: '#2563eb',
        backgroundColor: 'rgba(37, 99, 235, 0.1)',
        fill: true,
        tension: 0.25,
        pointRadius: 0
      }]
    } : { labels: [], datasets: [] };
  }
}
