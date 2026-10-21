import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { PageEvent } from '@angular/material/paginator';
import { Subject, Subscription, debounceTime, distinctUntilChanged, forkJoin } from 'rxjs';
import { TripService } from '../../../core/services/trip.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { DriverService } from '../../../core/services/driver.service';
import {
  TripFilter,
  TripListItem,
  TripStatus,
  TripStatusGroup,
  TRIP_STATUSES,
  formatTripStatusLabel,
  tripStatusesForGroup
} from '../../../core/models/trip.model';
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
  statusGroup: TripStatusGroup | '' = '';
  selectedVehicleId: number | null = null;
  selectedDriverId: number | null = null;
  dateFrom: Date | null = null;
  dateTo: Date | null = null;
  todayOnly = false;
  tomorrowOnly = false;
  upcomingOnly = false;
  vehicleOptions: Array<{ id: number; name: string }> = [];
  driverOptions: Array<{ id: number; fullName: string }> = [];
  readonly statusOptions = TRIP_STATUSES;

  private readonly searchSubject = new Subject<string>();
  private searchSub?: Subscription;
  private querySub?: Subscription;
  private skipNextQueryLoad = false;

  constructor(
    private trips: TripService,
    private vehiclesApi: VehicleService,
    private driversApi: DriverService,
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

  get activeFilterChips(): string[] {
    const chips: string[] = [];
    if (this.statusGroup === 'scheduled') chips.push('Scheduled group');
    if (this.statusGroup === 'ongoing') chips.push('Ongoing group');
    if (this.todayOnly) chips.push('Today');
    if (this.tomorrowOnly) chips.push('Tomorrow');
    if (this.upcomingOnly) chips.push('Upcoming');
    return chips;
  }

  ngOnInit(): void {
    this.initDefaultDateRange();
    this.loadFilterOptions();

    this.searchSub = this.searchSubject.pipe(
      debounceTime(350),
      distinctUntilChanged()
    ).subscribe(term => {
      this.search = term;
      this.syncQueryParams({ resetPage: true });
    });

    this.querySub = this.route.queryParamMap.subscribe(params => {
      if (this.skipNextQueryLoad) {
        this.skipNextQueryLoad = false;
        return;
      }

      this.status = params.get('status') || '';
      const group = params.get('statusGroup');
      this.statusGroup = group === 'scheduled' || group === 'ongoing' ? group : '';
      this.todayOnly = params.get('todayOnly') === 'true';
      this.tomorrowOnly = params.get('tomorrowOnly') === 'true';
      this.upcomingOnly = params.get('upcoming') === 'true' || params.get('upcomingOnly') === 'true';
      this.search = params.get('search') || this.search;

      const vehicleId = Number(params.get('vehicleId'));
      this.selectedVehicleId = Number.isFinite(vehicleId) && vehicleId > 0 ? vehicleId : null;
      const driverId = Number(params.get('driverId'));
      this.selectedDriverId = Number.isFinite(driverId) && driverId > 0 ? driverId : null;

      const page = Number(params.get('page'));
      this.pageIndex = Number.isFinite(page) && page > 0 ? page - 1 : 0;

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

  onStatusChange(): void {
    this.statusGroup = '';
    this.syncQueryParams({ resetPage: true });
  }

  onVehicleChange(): void {
    this.syncQueryParams({ resetPage: true });
  }

  onDriverChange(): void {
    this.syncQueryParams({ resetPage: true });
  }

  load(resetPage = false): void {
    if (resetPage) this.pageIndex = 0;
    this.loading = true;
    this.error = null;

    const statuses = tripStatusesForGroup(this.statusGroup);
    const filter: TripFilter = {
      status: statuses?.length ? '' : ((this.status as TripFilter['status']) || ''),
      statuses: statuses ?? undefined,
      search: this.search.trim() || undefined,
      driverId: this.selectedDriverId,
      vehicleId: this.selectedVehicleId,
      todayOnly: this.todayOnly || undefined,
      tomorrowOnly: this.tomorrowOnly || undefined,
      upcomingOnly: this.upcomingOnly || undefined,
      dateFrom: this.dateFrom ? this.toDateInput(this.dateFrom) : undefined,
      dateTo: this.dateTo ? this.toDateInput(this.dateTo) : undefined
    };

    this.trips.getAll(this.pageIndex + 1, this.pageSize, filter).subscribe({
      next: res => {
        this.items = [...res.items].sort((a, b) => {
          const ta = new Date(a.plannedStart).getTime();
          const tb = new Date(b.plannedStart).getTime();
          if (tb !== ta) return tb - ta;
          return b.id - a.id;
        });
        this.totalCount = res.totalCount;
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.items = [];
        this.totalCount = 0;
        this.error = apiErrorMessage(err, 'Failed to load trips.');
        this.toast.error(this.error);
      }
    });
  }

  onPage(e: PageEvent): void {
    this.pageIndex = e.pageIndex;
    this.pageSize = e.pageSize;
    this.syncQueryParams();
  }

  clearFilters(): void {
    this.search = '';
    this.status = '';
    this.statusGroup = '';
    this.selectedVehicleId = null;
    this.selectedDriverId = null;
    this.todayOnly = false;
    this.tomorrowOnly = false;
    this.upcomingOnly = false;
    this.initDefaultDateRange();
    this.pageIndex = 0;
    this.skipNextQueryLoad = true;
    void this.router.navigate(['/trips/list']).then(() => this.load(true));
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

  /** Live pin when trip is active (incl. Delayed) AND GPS is online. */
  showViewLive(row: TripListItem): boolean {
    return this.isLiveEligible(row.status) && this.hasLiveTracking(row) && !!row.vehicleId;
  }

  hasLiveTracking(row: TripListItem): boolean {
    return !!row.gpsOnline;
  }

  isLiveEligible(status: TripStatus): boolean {
    return status === 'Started' || status === 'Enroute' || status === 'AtPickup' || status === 'Delayed';
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

  formatTripStatus(status: string | TripStatus | null | undefined): string {
    return formatTripStatusLabel(status);
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

  private loadFilterOptions(): void {
    forkJoin({
      vehicles: this.vehiclesApi.getAll(1, 500),
      drivers: this.driversApi.getAll(1, 500)
    }).subscribe({
      next: ({ vehicles, drivers }) => {
        this.vehicleOptions = vehicles.items
          .map(v => ({ id: v.id, name: v.name || v.registrationNumber || `Vehicle #${v.id}` }))
          .sort((a, b) => a.name.localeCompare(b.name));
        this.driverOptions = drivers.items
          .map(d => ({ id: d.id, fullName: d.fullName }))
          .sort((a, b) => a.fullName.localeCompare(b.fullName));
      },
      error: () => {
        /* keep empty dropdowns; list still loads */
      }
    });
  }

  private syncQueryParams(opts?: { resetPage?: boolean }): void {
    if (opts?.resetPage) this.pageIndex = 0;
    this.skipNextQueryLoad = true;
    void this.router.navigate(['/trips/list'], {
      queryParams: {
        status: this.statusGroup ? null : (this.status || null),
        statusGroup: this.statusGroup || null,
        vehicleId: this.selectedVehicleId || null,
        driverId: this.selectedDriverId || null,
        search: this.search.trim() || null,
        todayOnly: this.todayOnly ? true : null,
        tomorrowOnly: this.tomorrowOnly ? true : null,
        upcoming: this.upcomingOnly ? true : null,
        page: this.pageIndex > 0 ? this.pageIndex + 1 : null
      },
      queryParamsHandling: 'merge'
    }).then(() => this.load(opts?.resetPage));
  }

  private initDefaultDateRange(): void {
    this.dateFrom = null;
    this.dateTo = null;
  }
}
