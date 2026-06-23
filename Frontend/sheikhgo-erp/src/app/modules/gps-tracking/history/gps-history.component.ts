import { Component, OnInit } from '@angular/core';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { Vehicle } from '../../../core/models/vehicle.model';
import { PositionDto } from '../../../core/models/gps-tracking.model';

@Component({
  selector: 'app-gps-history',
  templateUrl: './gps-history.component.html',
  styleUrls: ['./gps-history.component.scss']
})
export class GpsHistoryComponent implements OnInit {
  vehicles: Vehicle[] = [];
  vehicleId: number | null = null;
  from = '';
  to = '';
  rows: PositionDto[] = [];
  loading = false;
  error = '';

  constructor(
    private gps: GpsTrackingService,
    private vehicleService: VehicleService
  ) {}

  ngOnInit(): void {
    const now = new Date();
    const from = new Date(now.getTime() - 24 * 60 * 60 * 1000);
    this.from = this.toInput(from);
    this.to = this.toInput(now);

    this.vehicleService.getAll(1, 500).subscribe({
      next: r => {
        this.vehicles = r.items;
        if (r.items.length) {
          this.vehicleId = r.items[0].id;
        }
      }
    });
  }

  load(): void {
    if (!this.vehicleId) return;
    const fromDate = new Date(this.from);
    const toDate = new Date(this.to);
    if (fromDate > toDate) {
      this.error = 'Start date must be before end date.';
      return;
    }
    if (toDate.getTime() - fromDate.getTime() > 30 * 24 * 60 * 60 * 1000) {
      this.error = 'Date range cannot exceed 30 days.';
      return;
    }

    this.loading = true;
    this.error = '';
    this.gps.getHistory(this.vehicleId, fromDate, toDate).subscribe({
      next: rows => {
        this.rows = rows;
        this.loading = false;
      },
      error: err => {
        this.error = err?.error?.message ?? 'Failed to load history.';
        this.loading = false;
      }
    });
  }

  private toInput(d: Date): string {
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }
}
