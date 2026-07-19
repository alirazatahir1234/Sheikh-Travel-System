import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { TripService } from '../../../core/services/trip.service';
import { TripDashboard } from '../../../core/models/trip.model';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
  standalone: false,
  selector: 'app-trip-dashboard',
  templateUrl: './trip-dashboard.component.html',
  styleUrls: ['./trip-dashboard.component.scss']
})
export class TripDashboardComponent implements OnInit {
  loading = true;
  stats: TripDashboard | null = null;

  readonly cards: Array<{ key: keyof TripDashboard; label: string; icon: string; filter?: string }> = [
    { key: 'totalTrips', label: 'Total Trips', icon: 'map' },
    { key: 'scheduledTrips', label: 'Scheduled', icon: 'event', filter: 'Scheduled' },
    { key: 'ongoingTrips', label: 'Ongoing', icon: 'directions_car', filter: 'Started' },
    { key: 'completedTrips', label: 'Completed', icon: 'check_circle', filter: 'Completed' },
    { key: 'cancelledTrips', label: 'Cancelled', icon: 'cancel', filter: 'Cancelled' },
    { key: 'delayedTrips', label: 'Delayed', icon: 'schedule', filter: 'Delayed' },
    { key: 'todaysTrips', label: "Today's Trips", icon: 'today' },
    { key: 'upcomingTrips', label: 'Upcoming', icon: 'upcoming' }
  ];

  constructor(
    private trips: TripService,
    private router: Router,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.trips.getDashboard().subscribe({
      next: stats => {
        this.stats = stats;
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load trip dashboard.'));
      }
    });
  }

  openCard(card: { filter?: string; key: keyof TripDashboard }): void {
    if (card.key === 'todaysTrips') {
      this.router.navigate(['/trips/list'], { queryParams: { todayOnly: true } });
      return;
    }
    if (card.key === 'upcomingTrips') {
      this.router.navigate(['/trips/list'], { queryParams: { upcoming: true } });
      return;
    }
    this.router.navigate(['/trips/list'], {
      queryParams: card.filter ? { status: card.filter } : {}
    });
  }
}
