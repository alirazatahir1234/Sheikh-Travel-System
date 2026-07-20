import { Component, OnDestroy, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { TripService } from '../../../core/services/trip.service';
import { TripListItem, TripStatus } from '../../../core/models/trip.model';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

interface BoardColumn {
  key: string;
  label: string;
  statuses: TripStatus[];
}

@Component({
  standalone: false,
  selector: 'app-trip-live-board',
  templateUrl: './trip-live-board.component.html',
  styleUrls: ['./trip-live-board.component.scss']
})
export class TripLiveBoardComponent implements OnInit, OnDestroy {
  loading = true;
  todayOnly = true;
  items: TripListItem[] = [];
  private refreshTimer: ReturnType<typeof setInterval> | null = null;

  readonly columns: BoardColumn[] = [
    { key: 'queued', label: 'Queued', statuses: ['Draft', 'Scheduled', 'DriverAssigned', 'VehicleAssigned'] },
    { key: 'started', label: 'Started', statuses: ['Started', 'AtPickup'] },
    { key: 'enroute', label: 'En Route', statuses: ['Enroute'] },
    { key: 'delayed', label: 'Delayed', statuses: ['Delayed'] }
  ];

  constructor(
    private trips: TripService,
    private router: Router,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.load();
    this.refreshTimer = setInterval(() => this.load(true), 30000);
  }

  ngOnDestroy(): void {
    if (this.refreshTimer) clearInterval(this.refreshTimer);
  }

  get gpsOnlineCount(): number {
    return this.items.filter(t => t.gpsOnline).length;
  }

  tripsFor(col: BoardColumn): TripListItem[] {
    return this.items.filter(t => col.statuses.includes(t.status));
  }

  load(silent = false): void {
    if (!silent) this.loading = true;
    this.trips.getLive(this.todayOnly).subscribe({
      next: items => {
        this.items = items;
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        if (!silent) this.toast.error(apiErrorMessage(err, 'Failed to load dispatch board.'));
      }
    });
  }

  toggleScope(): void {
    this.todayOnly = !this.todayOnly;
    this.load();
  }

  openTrip(id: number): void {
    this.router.navigate(['/trips', id]);
  }

  openLiveGps(trip: TripListItem, event: Event): void {
    event.stopPropagation();
    if (!trip.vehicleId) {
      this.toast.warning('Assign a vehicle before opening live GPS.');
      return;
    }
    this.router.navigate(['/gps-tracking/live'], { queryParams: { vehicleId: trip.vehicleId } });
  }
}
