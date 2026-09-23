import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { PageEvent } from '@angular/material/paginator';
import { Subject, Subscription, debounceTime, distinctUntilChanged } from 'rxjs';
import { TripService } from '../../../core/services/trip.service';
import { TripFilter, TripListItem, TripStatus, TRIP_STATUSES } from '../../../core/models/trip.model';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
  standalone: false,
  selector: 'app-trip-list',
  templateUrl: './trip-list.component.html',
  styleUrls: ['./trip-list.component.scss']
})
export class TripListComponent implements OnInit, OnDestroy {
  displayedColumns = [
    'index', 'tripNumber', 'bookingNumber', 'routeName', 'driverName', 'vehicleName',
    'plannedStart', 'status', 'liveLocation', 'actions'
  ];

  items: TripListItem[] = [];
  activeTrip: TripListItem | null = null;
  loading = true;
  error: string | null = null;
  totalCount = 0;
  pageIndex = 0;
  pageSize = 10;
  search = '';
  status = '';
  selectedVehicle = '';
  selectedDriver = '';
  dateFrom: Date | null = null;
  dateTo: Date | null = null;
  todayOnly = false;
  tomorrowOnly = false;
  upcomingOnly = false;
  vehicleOptions: string[] = [];
  driverOptions: string[] = [];
  readonly statusOptions = TRIP_STATUSES;

  private readonly searchSubject = new Subject<string>();
  private searchSub?: Subscription;
  private querySub?: Subscription;

  constructor(
    private trips: TripService,
    private route: ActivatedRoute,
    private router: Router,
    private toast: UiToastService
  ) {}

  get showingLabel(): string {
    if (!this.totalCount) return 'Showing 0 of 0 trips';
    const start = this.pageIndex * this.pageSize + 1;
    const end = Math.min((this.pageIndex + 1) * this.pageSize, this.totalCount);
    return `Showing ${start}–${end} of ${this.totalCount} trips`;
  }

  ngOnInit(): void {
    this.initDefaultDateRange();
    this.searchSub = this.searchSubject.pipe(
      debounceTime(350),
      distinctUntilChanged()
    ).subscribe(term => {
      this.search = term;
      this.load(true);
    });

    this.querySub = this.route.queryParamMap.subscribe(params => {
      this.status = params.get('status') || '';
      this.todayOnly = params.get('todayOnly') === 'true';
      this.tomorrowOnly = params.get('tomorrowOnly') === 'true';
      this.upcomingOnly = params.get('upcoming') === 'true' || params.get('upcomingOnly') === 'true';
      this.pageIndex = 0;
      this.load();
    });
  }

  ngOnDestroy(): void {
    this.searchSub?.unsubscribe();
    this.querySub?.unsubscribe();
  }

  onSearchChange(term: string): void {
    this.searchSubject.next(term);
  }

  load(resetPage = false): void {
    if (resetPage) this.pageIndex = 0;
    this.loading = true;
    this.error = null;

    const searchParts = [
      this.search.trim(),
      this.selectedVehicle.trim(),
      this.selectedDriver.trim()
    ].filter(Boolean);

    const filter: TripFilter = {
      status: (this.status as TripFilter['status']) || '',
      search: searchParts.length ? searchParts.join(' ') : undefined,
      todayOnly: this.todayOnly || undefined,
      tomorrowOnly: this.tomorrowOnly || undefined,
      upcomingOnly: this.upcomingOnly || undefined,
      dateFrom: this.dateFrom ? this.toDateInput(this.dateFrom) : undefined,
      dateTo: this.dateTo ? this.toDateInput(this.dateTo) : undefined
    };

    this.trips.getAll(this.pageIndex + 1, this.pageSize, filter).subscribe({
      next: res => {
        // Newest start time first (matches API ORDER BY PlannedStart DESC, Id DESC)
        this.items = [...res.items].sort((a, b) => {
          const ta = new Date(a.plannedStart).getTime();
          const tb = new Date(b.plannedStart).getTime();
          if (tb !== ta) return tb - ta;
          return b.id - a.id;
        });
        this.totalCount = res.totalCount;
        this.mergeOptions(res.items);
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.error = apiErrorMessage(err, 'Failed to load trips.');
        this.toast.error(this.error);
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
    this.selectedVehicle = '';
    this.selectedDriver = '';
    this.todayOnly = false;
    this.tomorrowOnly = false;
    this.upcomingOnly = false;
    this.initDefaultDateRange();
    this.router.navigate(['/trips/list']);
    this.load(true);
  }

  view(id: number): void {
    this.router.navigate(['/trips', id]);
  }

  edit(id: number): void {
    this.router.navigate(['/trips', id, 'edit']);
  }

  openLive(row: TripListItem): void {
    this.router.navigate(['/gps-tracking/live'], {
      queryParams: { tripId: row.id, vehicleId: row.vehicleId || undefined }
    });
  }

  openRoute(row: TripListItem): void {
    this.router.navigate(['/trips', row.id]);
  }

  /** Live pin only when trip is in progress AND GPS live tracking is available. */
  showViewLive(row: TripListItem): boolean {
    return this.isInProgress(row.status) && this.hasLiveTracking(row);
  }

  hasLiveTracking(row: TripListItem): boolean {
    return !!row.gpsOnline;
  }

  isInProgress(status: TripStatus): boolean {
    return status === 'Started' || status === 'Enroute' || status === 'AtPickup';
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

  statusClass(status: TripStatus): string {
    const key = String(status || '').toLowerCase().replace(/\s+/g, '');
    if (this.isInProgress(status)) {
      return 'ops-status--inprogress';
    }
    return `ops-status--${key}`;
  }

  /** CamelCase → readable label, e.g. VehicleAssigned → "Vehicle Assigned". */
  formatTripStatus(status: string | TripStatus | null | undefined): string {
    if (!status) return '';
    if (this.isInProgress(status as TripStatus)) return 'In Progress';
    return String(status).replace(/([A-Z])/g, ' $1').trim();
  }

  formatRoute(row: TripListItem): string {
    if (row.routeName) {
      return row.routeName.includes('->')
        ? row.routeName.replace(/->/g, '→')
        : row.routeName.includes(' - ')
          ? row.routeName.replace(' - ', ' → ')
          : row.routeName;
    }
    const pickup = row.pickupAddress?.trim();
    const drop = row.destinationAddress?.trim();
    if (pickup && drop) return `${pickup} → ${drop}`;
    return pickup || drop || '—';
  }

  toDateInput(value: Date | null): string {
    if (!value) return '';
    const y = value.getFullYear();
    const m = String(value.getMonth() + 1).padStart(2, '0');
    const d = String(value.getDate()).padStart(2, '0');
    return `${y}-${m}-${d}`;
  }

  onDateFromChange(value: string): void {
    this.dateFrom = value ? new Date(`${value}T00:00:00`) : null;
    this.load(true);
  }

  onDateToChange(value: string): void {
    this.dateTo = value ? new Date(`${value}T00:00:00`) : null;
    this.load(true);
  }

  private initDefaultDateRange(): void {
    this.dateFrom = null;
    this.dateTo = null;
  }

  private mergeOptions(items: TripListItem[]): void {
    const vehicles = new Set(this.vehicleOptions);
    const drivers = new Set(this.driverOptions);
    for (const row of items) {
      if (row.vehicleName?.trim()) vehicles.add(row.vehicleName.trim());
      if (row.driverName?.trim()) drivers.add(row.driverName.trim());
    }
    this.vehicleOptions = [...vehicles].sort((a, b) => a.localeCompare(b));
    this.driverOptions = [...drivers].sort((a, b) => a.localeCompare(b));
  }
}
