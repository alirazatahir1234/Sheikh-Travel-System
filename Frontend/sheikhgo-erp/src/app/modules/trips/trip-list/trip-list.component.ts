import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { PageEvent } from '@angular/material/paginator';
import { TripService } from '../../../core/services/trip.service';
import { TripFilter, TripListItem, TRIP_STATUSES } from '../../../core/models/trip.model';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
  standalone: false,
  selector: 'app-trip-list',
  templateUrl: './trip-list.component.html',
  styleUrls: ['./trip-list.component.scss']
})
export class TripListComponent implements OnInit {
  displayedColumns = [
    'tripNumber', 'bookingNumber', 'customerName', 'driverName', 'vehicleName',
    'routeName', 'pickupAddress', 'destinationAddress', 'tripDate', 'plannedStart',
    'status', 'gpsOnline', 'actions'
  ];

  items: TripListItem[] = [];
  loading = true;
  totalCount = 0;
  pageIndex = 0;
  pageSize = 20;
  search = '';
  status = '';
  todayOnly = false;
  tomorrowOnly = false;
  upcomingOnly = false;
  readonly statusOptions = TRIP_STATUSES;

  constructor(
    private trips: TripService,
    private route: ActivatedRoute,
    private router: Router,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.route.queryParamMap.subscribe(params => {
      this.status = params.get('status') || '';
      this.todayOnly = params.get('todayOnly') === 'true';
      this.tomorrowOnly = params.get('tomorrowOnly') === 'true';
      this.upcomingOnly = params.get('upcoming') === 'true' || params.get('upcomingOnly') === 'true';
      this.pageIndex = 0;
      this.load();
    });
  }

  load(): void {
    this.loading = true;
    const filter: TripFilter = {
      status: (this.status as TripFilter['status']) || '',
      search: this.search || undefined,
      todayOnly: this.todayOnly || undefined,
      tomorrowOnly: this.tomorrowOnly || undefined,
      upcomingOnly: this.upcomingOnly || undefined
    };
    this.trips.getAll(this.pageIndex + 1, this.pageSize, filter).subscribe({
      next: res => {
        this.items = res.items;
        this.totalCount = res.totalCount;
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load trips.'));
      }
    });
  }

  onPage(e: PageEvent): void {
    this.pageIndex = e.pageIndex;
    this.pageSize = e.pageSize;
    this.load();
  }

  clearFilters(): void {
    this.search = '';
    this.status = '';
    this.todayOnly = false;
    this.tomorrowOnly = false;
    this.upcomingOnly = false;
    this.router.navigate(['/trips/list']);
    this.load();
  }

  view(id: number): void {
    this.router.navigate(['/trips', id]);
  }

  edit(id: number): void {
    this.router.navigate(['/trips', id, 'edit']);
  }

  remove(row: TripListItem): void {
    if (!confirm(`Delete trip ${row.tripNumber}?`)) return;
    this.trips.delete(row.id).subscribe({
      next: () => {
        this.toast.success('Trip deleted.');
        this.load();
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Delete failed.'))
    });
  }
}
