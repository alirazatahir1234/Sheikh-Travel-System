import { Component, OnInit } from '@angular/core';
import { TripService } from '../../../core/services/trip.service';
import { TripAnalytics, TripListItem } from '../../../core/models/trip.model';
import { ExportService } from '../../../core/services/export.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
  standalone: false,
  selector: 'app-trip-reports',
  templateUrl: './trip-reports.component.html',
  styleUrls: ['./trip-reports.component.scss']
})
export class TripReportsComponent implements OnInit {
  loading = true;
  exporting = false;
  analytics: TripAnalytics | null = null;
  from = '';
  to = '';

  constructor(
    private trips: TripService,
    private exportService: ExportService,
    private toast: UiToastService
  ) {
    const end = new Date();
    const start = new Date();
    start.setDate(end.getDate() - 29);
    this.from = this.toDateInput(start);
    this.to = this.toDateInput(end);
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.trips.getAnalytics(this.from, this.to).subscribe({
      next: analytics => {
        this.analytics = analytics;
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load trip analytics.'));
      }
    });
  }

  exportSummary(): void {
    if (!this.analytics) return;
    const a = this.analytics;
    this.exportService.exportExcel(
      [
        { metric: 'Total Trips', value: a.totalTrips },
        { metric: 'Completed', value: a.completedTrips },
        { metric: 'Cancelled', value: a.cancelledTrips },
        { metric: 'Delayed', value: a.delayedTrips },
        { metric: 'Ongoing', value: a.ongoingTrips },
        { metric: 'Completion Rate %', value: a.completionRate },
        { metric: 'Planned Distance Km', value: a.totalPlannedDistanceKm ?? 0 },
        { metric: 'Actual Distance Km', value: a.totalActualDistanceKm ?? 0 },
        { metric: 'Total Expenses', value: a.totalExpenses }
      ],
      [
        { header: 'Metric', accessor: r => r.metric },
        { header: 'Value', accessor: r => r.value }
      ],
      {
        filename: `trip-summary-${this.from}-${this.to}`,
        sheetName: 'Summary',
        title: `Trip Summary ${this.from} → ${this.to}`
      }
    );
  }

  exportTripsCsv(): void {
    this.exporting = true;
    this.trips.getAll(1, 500, { dateFrom: this.from, dateTo: this.to }).subscribe({
      next: page => {
        this.exportService.exportCsv<TripListItem>(
          page.items,
          [
            { header: 'Trip #', accessor: r => r.tripNumber },
            { header: 'Customer', accessor: r => r.customerName ?? '' },
            { header: 'Driver', accessor: r => r.driverName ?? '' },
            { header: 'Vehicle', accessor: r => r.vehicleName ?? '' },
            { header: 'Date', accessor: r => (r.tripDate || '').slice(0, 10) },
            { header: 'Start', accessor: r => r.plannedStart },
            { header: 'Status', accessor: r => r.status },
            { header: 'Type', accessor: r => r.tripType },
            { header: 'Priority', accessor: r => r.priority }
          ],
          { filename: `trips-${this.from}-${this.to}`, sheetName: 'Trips', title: 'Trips Export' }
        );
        this.exporting = false;
      },
      error: err => {
        this.exporting = false;
        this.toast.error(apiErrorMessage(err, 'Failed to export trips.'));
      }
    });
  }

  barWidth(count: number, list: { count: number }[]): string {
    const max = Math.max(...list.map(x => x.count), 1);
    return `${Math.round((count / max) * 100)}%`;
  }

  private toDateInput(d: Date): string {
    const y = d.getFullYear();
    const m = `${d.getMonth() + 1}`.padStart(2, '0');
    const day = `${d.getDate()}`.padStart(2, '0');
    return `${y}-${m}-${day}`;
  }
}
