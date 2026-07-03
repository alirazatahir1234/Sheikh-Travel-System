import { Component, OnInit } from '@angular/core';
import { of, TimeoutError } from 'rxjs';
import { catchError, timeout } from 'rxjs/operators';
import { HttpErrorResponse } from '@angular/common/http';
import { SharedModule } from '../../../shared/shared.module';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { PlatformService } from '../../../core/services/platform.service';
import { DriverService } from '../../../core/services/driver.service';
import { FleetTripEvent } from '../../../core/models/gps-tracking.model';
import { Branch, Department } from '../../../core/models/platform.model';
import {
  TripDatePreset,
  TRIP_DATE_PRESETS,
  applyTripDatePreset,
  formatPeriodLabel,
  toDatetimeLocalInput
} from '../utils/trip-date-preset.util';
import { mapTripEvent } from '../utils/trip-event.util';

const FETCH_TIMEOUT_MS = 60_000;

interface FleetDriverOption {
  id: number;
  fullName: string;
}

type SortableEventKey = 'time' | 'vehicleName' | 'speedKmh';

/**
 * Scaled-down sibling of gps-trips.component.ts — a dedicated Traccar-fan-out-only report (no
 * local fallback tier at all, unlike Stops), kept on its own route/tab so it only fires its own
 * capped Traccar calls when actually visited (see Phase 2 plan Risk 1).
 */
@Component({
  selector: 'app-gps-events',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './gps-events.component.html',
  styleUrls: ['./gps-events.component.scss']
})
export class GpsEventsComponent implements OnInit {
  readonly datePresets = TRIP_DATE_PRESETS;
  readonly pageSizeOptions = [10, 25, 50, 100];
  readonly eventTypeOptions: { value: string | null; label: string }[] = [
    { value: null, label: 'All Events' },
    { value: 'overspeed', label: 'Overspeed' },
    { value: 'braking', label: 'Harsh Braking' },
    { value: 'acceleration', label: 'Harsh Acceleration' },
    { value: 'geofence', label: 'Geofence' },
    { value: 'alarm', label: 'Alarm' }
  ];

  datePreset: TripDatePreset = 'thisWeek';
  from = '';
  to = '';

  branches: Branch[] = [];
  departments: Department[] = [];
  fleetDrivers: FleetDriverOption[] = [];
  branchId: number | null = null;
  departmentId: number | null = null;
  driverId: number | null = null;
  eventType: string | null = null;

  page = 1;
  pageSize = 25;
  totalCount = 0;
  vehiclesInScope = 0;
  vehiclesQueried = 0;

  sortBy: SortableEventKey | null = null;
  sortDirection: 'asc' | 'desc' = 'desc';

  events: FleetTripEvent[] = [];
  loading = false;
  error = '';

  constructor(
    private gps: GpsTrackingService,
    private platform: PlatformService,
    private driverService: DriverService
  ) {}

  get periodLabel(): string {
    return formatPeriodLabel(this.from, this.to);
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  get showCappedScopeBanner(): boolean {
    return this.vehiclesQueried > 0 && this.vehiclesQueried < this.vehiclesInScope;
  }

  get sortedEvents(): FleetTripEvent[] {
    if (!this.sortBy) return this.events;
    const key = this.sortBy;
    const dir = this.sortDirection === 'asc' ? 1 : -1;
    return [...this.events].sort((a, b) => {
      const av = a[key];
      const bv = b[key];
      if (av == null && bv == null) return 0;
      if (av == null) return 1;
      if (bv == null) return -1;
      if (typeof av === 'string' && typeof bv === 'string') {
        const an = Date.parse(av);
        const bn = Date.parse(bv);
        if (!Number.isNaN(an) && !Number.isNaN(bn)) return (an - bn) * dir;
        return av.localeCompare(bv) * dir;
      }
      if (typeof av === 'number' && typeof bv === 'number') return (av - bv) * dir;
      return 0;
    });
  }

  ngOnInit(): void {
    this.applyPreset('thisWeek');
    this.platform.getBranches().subscribe({ next: b => { this.branches = b; }, error: () => {} });
    this.platform.getDepartments().subscribe({ next: d => { this.departments = d; }, error: () => {} });
    this.driverService.getAll(1, 500).subscribe({
      next: r => { this.fleetDrivers = r.items.map(d => ({ id: d.id, fullName: d.fullName })); },
      error: () => {}
    });
    this.load();
  }

  applyPreset(preset: TripDatePreset): void {
    this.datePreset = preset;
    if (preset === 'custom') return;
    const range = applyTripDatePreset(preset);
    this.from = toDatetimeLocalInput(range.from);
    this.to = toDatetimeLocalInput(range.to);
  }

  onPresetChange(preset: TripDatePreset): void {
    this.applyPreset(preset);
    if (preset !== 'custom') { this.page = 1; this.load(); }
  }

  onFilterChange(): void {
    this.page = 1;
    this.load();
  }

  load(): void {
    const fromDate = this.from ? new Date(this.from) : undefined;
    const toDate = this.to ? new Date(this.to) : undefined;
    if (!fromDate || !toDate || fromDate > toDate) {
      this.error = 'End Date cannot be earlier than Start Date.';
      return;
    }

    this.loading = true;
    this.error = '';
    const filters = { branchId: this.branchId, departmentId: this.departmentId, driverId: this.driverId };

    this.gps.getFleetEvents(fromDate, toDate, filters, this.eventType, this.page, this.pageSize).pipe(
      timeout(FETCH_TIMEOUT_MS),
      catchError((err: HttpErrorResponse | TimeoutError) => {
        this.error = err instanceof TimeoutError
          ? 'Events request timed out. Try a shorter date range.'
          : (err as HttpErrorResponse).error?.message ?? 'Failed to load events.';
        return of(null);
      })
    ).subscribe(result => {
      this.events = result?.items ?? [];
      this.totalCount = result?.totalCount ?? 0;
      this.vehiclesInScope = result?.vehiclesInScope ?? 0;
      this.vehiclesQueried = result?.vehiclesQueried ?? 0;
      this.loading = false;
    });
  }

  onSortChange(column: SortableEventKey): void {
    if (this.sortBy === column) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortBy = column;
      this.sortDirection = 'asc';
    }
  }

  sortIcon(column: SortableEventKey): string {
    if (this.sortBy !== column) return 'unfold_more';
    return this.sortDirection === 'asc' ? 'arrow_upward' : 'arrow_downward';
  }

  onPageSizeChange(): void {
    this.page = 1;
    this.load();
  }

  prevPage(): void {
    if (this.page > 1) { this.page--; this.load(); }
  }

  nextPage(): void {
    if (this.page < this.totalPages) { this.page++; this.load(); }
  }

  vehicleLabel(row: FleetTripEvent): string {
    const name = row.vehicleName ?? `Vehicle #${row.vehicleId}`;
    return row.plateNumber ? `${name} (${row.plateNumber})` : name;
  }

  eventView(row: FleetTripEvent) {
    return mapTripEvent(row);
  }
}
