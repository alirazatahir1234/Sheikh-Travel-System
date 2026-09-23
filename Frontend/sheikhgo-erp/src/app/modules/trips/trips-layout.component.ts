import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { TripService } from '../../core/services/trip.service';
import { TripDashboard } from '../../core/models/trip.model';

interface TripNavItem {
  label: string;
  route: string;
  exact: boolean;
}

@Component({
  standalone: false,
  selector: 'app-trips-layout',
  templateUrl: './trips-layout.component.html',
  styleUrls: ['./trips-layout.component.scss']
})
export class TripsLayoutComponent implements OnInit {
  stats: TripDashboard | null = null;

  readonly navItems: TripNavItem[] = [
    { label: 'Trip List', route: 'list', exact: true },
    { label: 'Live Dashboard', route: 'live', exact: true },
    { label: 'Calendar', route: 'calendar', exact: true },
    { label: 'Map View', route: 'dashboard', exact: true },
    { label: 'Reports', route: 'reports', exact: true }
  ];

  constructor(
    private trips: TripService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.trips.getDashboard().subscribe({
      next: stats => (this.stats = stats),
      error: () => (this.stats = null)
    });
  }

  openList(status?: string): void {
    this.router.navigate(['/trips/list'], {
      queryParams: status ? { status } : {}
    });
  }
}
