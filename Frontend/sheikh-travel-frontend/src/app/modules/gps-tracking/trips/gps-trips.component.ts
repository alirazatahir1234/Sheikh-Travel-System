import { Component, OnInit } from '@angular/core';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { ExportService } from '../../../core/services/export.service';
import { Vehicle } from '../../../core/models/vehicle.model';
import { GpsTrip } from '../../../core/models/gps-tracking.model';

@Component({
  selector: 'app-gps-trips',
  templateUrl: './gps-trips.component.html',
  styleUrls: ['./gps-trips.component.scss']
})
export class GpsTripsComponent implements OnInit {
  vehicles: Vehicle[] = [];
  vehicleId: number | null = null;
  from = '';
  to = '';
  trips: GpsTrip[] = [];
  loading = false;
  error = '';

  constructor(
    private gps: GpsTrackingService,
    private vehicleService: VehicleService,
    private exportService: ExportService
  ) {}

  ngOnInit(): void {
    const now = new Date();
    const from = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
    this.from = this.toInput(from);
    this.to = this.toInput(now);
    this.vehicleService.getAll(1, 500).subscribe({ next: r => { this.vehicles = r.items; } });
    this.load();
  }

  load(): void {
    this.loading = true;
    this.error = '';
    const fromDate = this.from ? new Date(this.from) : undefined;
    const toDate = this.to ? new Date(this.to) : undefined;
    this.gps.getTrips(this.vehicleId ?? undefined, fromDate, toDate).subscribe({
      next: trips => {
        this.trips = trips;
        this.loading = false;
      },
      error: err => {
        this.error = err?.error?.message ?? 'Failed to load trips.';
        this.loading = false;
      }
    });
  }

  exportCsv(): void {
    this.exportService.exportExcel(
      this.trips,
      [
        { header: 'Vehicle', accessor: t => t.vehicleName ?? String(t.vehicleId) },
        { header: 'Start', accessor: t => t.startTime },
        { header: 'End', accessor: t => t.endTime },
        { header: 'Distance (km)', accessor: t => t.distanceKm },
        { header: 'Avg speed', accessor: t => t.avgSpeedKmh },
        { header: 'Max speed', accessor: t => t.maxSpeedKmh },
        { header: 'Duration (min)', accessor: t => t.durationMinutes }
      ],
      { filename: 'gps-trips', sheetName: 'Trips' }
    );
  }

  private toInput(d: Date): string {
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }
}
