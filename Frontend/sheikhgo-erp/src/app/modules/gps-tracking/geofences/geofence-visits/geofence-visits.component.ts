import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { Observable, Subscription, from, of } from 'rxjs';
import { catchError, concatMap, reduce } from 'rxjs/operators';
import { GeofenceWorkspaceService } from '../geofence-workspace.service';
import {
  GeofenceListItem,
  GeofenceVisitGroupBy,
  GeofenceVisitGroups,
  GeofenceVisitQuery,
  GeofenceVisitRow,
  GeofenceVisitStatus,
  GeofenceVisitsSummary
} from '../geofence.models';
import {
  Badge,
  VISIT_STATUS_OPTIONS,
  formatDayTime,
  formatDuration,
  plural,
  visitStatusBadge
} from '../geofence-format.util';
import { VehicleService } from '../../../../core/services/vehicle.service';
import { DriverService } from '../../../../core/services/driver.service';
import { TenantTimeService } from '../../../../core/services/tenant-time.service';
import { ExportColumn, ExportService } from '../../../../core/services/export.service';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { toDate, toZonedDateInput, toZonedDateTimeLocal, zonedAddDays, zonedParts } from '../../../../core/utils/zoned-time.util';
import { VehicleListItem } from '../../../../core/models/vehicle.model';

export interface VisitKpi {
  label: string;
  value: string;
  note: string;
  tone: 'default' | 'amber' | 'red';
}

export interface OccupancyBar {
  hour: number;
  count: number;
  breach: boolean;
  heightPx: number;
  label: string;
}

export const PAGE_SIZE = 25;
const EXPORT_PAGE_SIZE = 100;
export const MAX_EXPORT_ROWS = 5_000;
const MAX_RANGE_DAYS = 366;

/** The five KPI cards of the visits report. */
export function visitKpis(s: GeofenceVisitsSummary | null): VisitKpi[] {
  if (!s) return [];
  const parts = [
    [s.overstays, 'overstay'],
    [s.overspeeds, 'overspeed'],
    [s.afterHoursExits, 'after hours'],
    [s.idleBreaches, 'idle']
  ].filter(([n]) => (n as number) > 0).map(([n, l]) => `${n} ${l}`);
  const avg = s.avgMinutes;
  const target = s.dwellTargetMinutes;
  return [
    { label: 'Visits', value: s.visits.toLocaleString('en-US'), note: plural(s.vehicles, 'vehicle'), tone: 'default' },
    { label: 'Total time on site', value: formatDuration(s.totalMinutes), note: 'open visits count up to now', tone: 'default' },
    {
      label: 'Avg time on site',
      value: avg == null ? '—' : formatDuration(avg),
      note: target ? `target ${formatDuration(target)}` : avg == null ? 'no visit ended in this range' : 'visits that ended',
      tone: avg != null && target != null && avg > target ? 'amber' : 'default'
    },
    { label: 'Idle inside', value: formatDuration(s.idleMinutes), note: 'engine on, not moving', tone: 'default' },
    { label: 'Rule breaches', value: s.breaches.toLocaleString('en-US'), note: parts.join(' · ') || 'none', tone: s.breaches > 0 ? 'red' : 'default' }
  ];
}

const hourName = (h: number): string => `${h % 12 === 0 ? 12 : h % 12} ${h < 12 ? 'AM' : 'PM'}`;

/** 24 bars, scaled to the busiest hour; orange where a dwell rule was breached. */
export function occupancyBars(s: GeofenceVisitsSummary | null, maxHeight = 64): OccupancyBar[] {
  const counts = s?.occupancyToday?.length === 24 ? s.occupancyToday : Array<number>(24).fill(0);
  const breaches = s?.dwellBreachHoursToday?.length === 24 ? s.dwellBreachHoursToday : Array<boolean>(24).fill(false);
  const max = Math.max(1, ...counts);
  return counts.map((count, hour) => ({
    hour,
    count,
    breach: !!breaches[hour],
    heightPx: count === 0 ? 2 : Math.round(8 + (count / max) * (maxHeight - 8)),
    label: `${hourName(hour)}: ${plural(count, 'vehicle')} inside${breaches[hour] ? ', dwell rule breached' : ''}`
  }));
}

/** History deep link around a visit: five minutes either side, ending no later than now. */
export function replayParams(visit: Pick<GeofenceVisitRow, 'vehicleId' | 'enteredUtc' | 'exitedUtc'>, zone: string, now = new Date()): Record<string, string> {
  const entered = toDate(visit.enteredUtc) ?? now;
  const exited = toDate(visit.exitedUtc ?? null) ?? now;
  const from = new Date(entered.getTime() - 5 * 60_000);
  const to = new Date(Math.min(now.getTime(), exited.getTime() + 5 * 60_000));
  return {
    vehicleId: String(visit.vehicleId),
    preset: 'custom',
    from: toZonedDateTimeLocal(from, zone),
    to: toZonedDateTimeLocal(to, zone)
  };
}

/** Days between two yyyy-MM-dd dates (to − from); NaN when either is not a date. */
export function daySpan(from: string, to: string): number {
  const a = Date.parse(`${from}T00:00:00Z`);
  const b = Date.parse(`${to}T00:00:00Z`);
  return Number.isFinite(a) && Number.isFinite(b) ? Math.round((b - a) / 86_400_000) : NaN;
}

/** Short zone name for the date filter, e.g. "GMT+5". */
export function zoneShortName(zone: string): string {
  try {
    return new Intl.DateTimeFormat('en-US', { timeZone: zone, timeZoneName: 'short' })
      .formatToParts(new Date())
      .find(p => p.type === 'timeZoneName')?.value ?? zone;
  } catch {
    return zone;
  }
}

@Component({
  selector: 'app-geofence-visits',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, MatIconModule, MatMenuModule],
  templateUrl: './geofence-visits.component.html',
  styleUrls: ['./geofence-visits.component.scss']
})
export class GeofenceVisitsComponent implements OnInit, OnDestroy {
  private readonly api = inject(GeofenceWorkspaceService);
  private readonly vehiclesApi = inject(VehicleService);
  private readonly driversApi = inject(DriverService);
  private readonly time = inject(TenantTimeService);
  private readonly exporter = inject(ExportService);
  private readonly toast = inject(UiToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly statusOptions = VISIT_STATUS_OPTIONS;
  readonly groupOptions: { value: GeofenceVisitGroupBy; label: string }[] = [
    { value: 'visit', label: 'Visit' },
    { value: 'vehicle', label: 'Vehicle' },
    { value: 'driver', label: 'Driver' },
    { value: 'geofence', label: 'Geofence' },
    { value: 'day', label: 'Day' }
  ];
  readonly pageSize = PAGE_SIZE;
  readonly hourTicks = ['12 AM', '6 AM', '12 PM', '6 PM', '12 AM'];

  fences: GeofenceListItem[] = [];
  vehicles: VehicleListItem[] = [];
  drivers: { id: number; name: string }[] = [];

  geofenceId: number | null = null;
  vehicleIds: number[] = [];
  driverId: number | null = null;
  from = '';
  to = '';
  status: GeofenceVisitStatus | 'all' = 'all';
  groupBy: GeofenceVisitGroupBy = 'visit';
  page = 1;

  rows: GeofenceVisitRow[] = [];
  total = 0;
  groups: GeofenceVisitGroups | null = null;
  loading = false;
  error: string | null = null;
  summary: GeofenceVisitsSummary | null = null;
  kpis: VisitKpi[] = [];
  bars: OccupancyBar[] = occupancyBars(null);
  rangeError: string | null = null;
  exporting = false;

  vehiclePickerOpen = false;
  vehicleSearch = '';
  readonly zone = this.time.timeZone();
  readonly zoneLabel = zoneShortName(this.zone);
  readonly today = toZonedDateInput(new Date(), this.zone);

  private tableSub: Subscription | null = null;
  private summarySub: Subscription | null = null;
  private readonly subs = new Subscription();

  ngOnInit(): void {
    this.readQuery();
    this.subs.add(this.api.list().pipe(catchError(() => of([] as GeofenceListItem[]))).subscribe(f => {
      this.fences = (f ?? []).slice().sort((a, b) => a.name.localeCompare(b.name));
    }));
    this.subs.add(this.vehiclesApi.getAll(1, 500).pipe(catchError(() => of(null))).subscribe(r => (this.vehicles = r?.items ?? [])));
    this.subs.add(this.driversApi.getAll({ page: 1, pageSize: 500 }).pipe(catchError(() => of(null))).subscribe(r => {
      this.drivers = (r?.items ?? []).map(d => ({ id: d.id, name: d.fullName })).sort((a, b) => a.name.localeCompare(b.name));
    }));
    this.reload();
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
    this.tableSub?.unsubscribe();
    this.summarySub?.unsubscribe();
  }

  private readQuery(): void {
    const q = this.route.snapshot.queryParamMap;
    const num = (k: string) => {
      const v = Number(q.get(k));
      return Number.isFinite(v) && v > 0 ? v : null;
    };
    this.geofenceId = num('geofenceId');
    this.driverId = num('driverId');
    this.vehicleIds = (q.get('vehicleIds') ?? '').split(',').map(Number).filter(n => Number.isFinite(n) && n > 0);
    const isDate = (v: string | null) => !!v && /^\d{4}-\d{2}-\d{2}$/.test(v);
    this.to = isDate(q.get('to')) ? q.get('to')! : this.today;
    this.from = isDate(q.get('from')) ? q.get('from')! : toZonedDateInput(zonedAddDays(new Date(), -6, this.zone), this.zone);
    const status = q.get('status');
    if (this.statusOptions.some(o => o.value === status)) this.status = status as GeofenceVisitStatus | 'all';
    const groupBy = q.get('groupBy');
    if (this.groupOptions.some(o => o.value === groupBy)) this.groupBy = groupBy as GeofenceVisitGroupBy;
    this.page = num('page') ?? 1;
  }

  private writeQuery(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      replaceUrl: true,
      queryParams: {
        geofenceId: this.geofenceId,
        vehicleIds: this.vehicleIds.length ? this.vehicleIds.join(',') : null,
        driverId: this.driverId,
        from: this.from,
        to: this.to,
        status: this.status === 'all' ? null : this.status,
        groupBy: this.groupBy === 'visit' ? null : this.groupBy,
        page: this.page > 1 ? this.page : null
      }
    });
  }

  private get query(): GeofenceVisitQuery {
    return {
      geofenceId: this.geofenceId,
      vehicleIds: this.vehicleIds,
      driverId: this.driverId,
      from: this.from,
      to: this.to,
      status: this.status,
      page: this.page,
      pageSize: this.pageSize
    };
  }

  /** Any filter change: back to page 1, then table and KPIs. */
  onFilterChange(): void {
    this.page = 1;
    this.reload();
  }

  private reload(): void {
    const span = daySpan(this.from, this.to);
    if (!Number.isFinite(span)) this.rangeError = 'Pick both dates.';
    else if (span < 0) this.rangeError = 'The start date is after the end date.';
    else if (span > MAX_RANGE_DAYS) this.rangeError = 'Pick at most one year.';
    else this.rangeError = null;
    this.writeQuery();
    if (this.rangeError) {
      this.rows = [];
      this.total = 0;
      this.groups = null;
      return;
    }
    this.loadTable();
    this.loadSummary();
  }

  private loadTable(): void {
    this.tableSub?.unsubscribe();
    this.loading = true;
    this.error = null;
    const done = () => (this.loading = false);
    const fail = (err: unknown) => {
      done();
      this.error = apiErrorMessage(err, 'Visits could not be loaded.');
    };
    if (this.groupBy === 'visit') {
      this.groups = null;
      this.tableSub = this.api.visits(this.query).subscribe({
        next: p => {
          done();
          this.rows = p?.items ?? [];
          this.total = p?.total ?? 0;
        },
        error: fail
      });
    } else {
      this.rows = [];
      this.tableSub = this.api.visitGroups(this.groupBy, this.query).subscribe({
        next: g => {
          done();
          this.groups = g;
        },
        error: fail
      });
    }
  }

  private loadSummary(): void {
    this.summarySub?.unsubscribe();
    this.summarySub = this.api.visitsSummary(this.query).pipe(catchError(() => of(null))).subscribe(s => {
      this.summary = s;
      this.kpis = visitKpis(s);
      this.bars = occupancyBars(s);
    });
  }

  /** One sentence for screen readers; the hidden list under the bars has every hour. */
  get occupancySummary(): string {
    const busiest = this.bars.reduce((a, b) => (b.count > a.count ? b : a), this.bars[0]);
    if (!busiest?.count) return 'No vehicles were inside today.';
    const breaches = this.bars.filter(b => b.breach).length;
    return `Busiest at ${busiest.label}.${breaches ? ` Dwell rule breached in ${plural(breaches, 'hour')}.` : ''}`;
  }

  // ---- paging -----------------------------------------------------------------------------------------------------

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.total / this.pageSize));
  }

  get showingText(): string {
    if (!this.total) return 'No visits';
    const first = (this.page - 1) * this.pageSize + 1;
    const last = Math.min(this.total, first + this.rows.length - 1);
    return `Showing ${first.toLocaleString('en-US')}–${last.toLocaleString('en-US')} of ${plural(this.total, 'visit')}`;
  }

  goToPage(page: number): void {
    const p = Math.min(this.pageCount, Math.max(1, page));
    if (p === this.page) return;
    this.page = p;
    this.writeQuery();
    this.loadTable();
  }

  // ---- filters ----------------------------------------------------------------------------------------------------

  get vehicleButtonText(): string {
    if (!this.vehicleIds.length) return `All ${this.vehicles.length || ''}`.trim();
    if (this.vehicleIds.length === 1) {
      const v = this.vehicles.find(x => x.id === this.vehicleIds[0]);
      return v ? v.registrationNumber || v.name : '1 vehicle';
    }
    return `${this.vehicleIds.length} vehicles`;
  }

  get vehicleChoices(): VehicleListItem[] {
    const q = this.vehicleSearch.trim().toLowerCase();
    return (q ? this.vehicles.filter(v => [v.name, v.registrationNumber, v.driverName].some(s => (s ?? '').toLowerCase().includes(q))) : this.vehicles).slice(0, 200);
  }

  toggleVehicle(id: number, on: boolean): void {
    this.vehicleIds = on ? [...this.vehicleIds, id] : this.vehicleIds.filter(x => x !== id);
    this.onFilterChange();
  }

  clearVehicles(): void {
    this.vehicleIds = [];
    this.onFilterChange();
  }

  get geofenceName(): string {
    return this.fences.find(f => f.id === this.geofenceId)?.name ?? 'All geofences';
  }

  // ---- rows -------------------------------------------------------------------------------------------------------

  badge(status: string): Badge {
    return visitStatusBadge(status);
  }

  when(utc: string | null | undefined): string {
    return utc ? formatDayTime(zonedParts(utc, this.zone)) : '—';
  }

  duration(minutes: number | null | undefined): string {
    return minutes == null ? '—' : formatDuration(minutes);
  }

  groupLabel(key: string, label: string): string {
    if (this.groups?.groupBy !== 'day') return label;
    return this.time.formatDate(key, { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' });
  }

  get groupHeading(): string {
    return this.groupOptions.find(o => o.value === this.groupBy)?.label ?? 'Group';
  }

  replay(row: GeofenceVisitRow): void {
    void this.router.navigate(['/gps-tracking/history'], { queryParams: replayParams(row, this.zone) });
  }

  // ---- export -----------------------------------------------------------------------------------------------------

  export(kind: 'excel' | 'pdf'): void {
    if (this.exporting || this.rangeError) return;
    this.exporting = true;
    const meta = {
      filename: `geofence-visits-${this.from}-to-${this.to}`,
      title: 'Geofence visits and time on site',
      subtitle: `${this.geofenceName} · ${this.from} – ${this.to} (${this.zoneLabel})${this.status !== 'all' ? ` · ${this.statusOptions.find(s => s.value === this.status)?.label}` : ''}`,
      sheetName: 'Visits'
    };
    const finish = () => (this.exporting = false);

    if (this.groupBy !== 'visit') {
      const items = this.groups?.items ?? [];
      const columns: ExportColumn<(typeof items)[number]>[] = [
        { header: this.groupHeading, accessor: g => this.groupLabel(g.key, g.label), excelWidth: 26, pdfWeight: 2 },
        { header: 'Visits', accessor: g => g.visits, align: 'right' },
        { header: 'Vehicles', accessor: g => g.vehicles, align: 'right' },
        { header: 'Inside now', accessor: g => g.insideNow, align: 'right' },
        { header: 'Time on site', accessor: g => formatDuration(g.totalMinutes) },
        { header: 'Avg', accessor: g => (g.avgMinutes == null ? '—' : formatDuration(g.avgMinutes)) },
        { header: 'Idle inside', accessor: g => formatDuration(g.idleMinutes) },
        { header: 'Breaches', accessor: g => g.breaches, align: 'right' }
      ];
      this.write(kind, items, columns, meta).finally(finish);
      return;
    }

    const pages = Math.min(Math.ceil(this.total / EXPORT_PAGE_SIZE), MAX_EXPORT_ROWS / EXPORT_PAGE_SIZE);
    if (!pages) {
      finish();
      this.toast.info('There are no visits to export.');
      return;
    }
    const all$: Observable<GeofenceVisitRow[]> = from(Array.from({ length: pages }, (_, i) => i + 1)).pipe(
      concatMap(page => this.api.visits({ ...this.query, page, pageSize: EXPORT_PAGE_SIZE })),
      reduce((acc, p) => acc.concat(p?.items ?? []), [] as GeofenceVisitRow[])
    );
    this.subs.add(all$.subscribe({
      next: rows => {
        if (this.total > MAX_EXPORT_ROWS) this.toast.warning(`Exported the newest ${MAX_EXPORT_ROWS.toLocaleString('en-US')} visits. Narrow the dates for the rest.`);
        const columns: ExportColumn<GeofenceVisitRow>[] = [
          { header: 'Geofence', accessor: r => r.geofenceName, excelWidth: 24, pdfWeight: 1.5 },
          { header: 'Vehicle', accessor: r => r.vehicleLabel, excelWidth: 16 },
          { header: 'Driver', accessor: r => r.driverName || 'No driver', excelWidth: 18 },
          { header: 'Entered', accessor: r => this.when(r.enteredUtc), excelWidth: 20 },
          { header: 'Exited', accessor: r => this.when(r.exitedUtc), excelWidth: 20 },
          { header: 'Time on site', accessor: r => formatDuration(r.durationMinutes) },
          { header: 'Idle inside', accessor: r => formatDuration(r.idleMinutes) },
          { header: 'Max speed (km/h)', accessor: r => Math.round(r.maxSpeedKmh), align: 'right' },
          { header: 'Status', accessor: r => visitStatusBadge(r.status).label }
        ];
        this.write(kind, rows, columns, meta).finally(finish);
      },
      error: err => {
        finish();
        this.toast.error(apiErrorMessage(err, 'The export failed.'));
      }
    }));
  }

  private async write<T>(kind: 'excel' | 'pdf', rows: T[], columns: ExportColumn<T>[], meta: { filename: string; title: string; subtitle: string; sheetName: string }): Promise<void> {
    try {
      if (kind === 'excel') this.exporter.exportExcel(rows, columns, meta);
      else await this.exporter.exportPdf(rows, columns, { ...meta, orientation: 'landscape' });
    } catch {
      this.toast.error('The file could not be created.');
    }
  }

  trackRow = (_: number, r: GeofenceVisitRow) => r.id;
}
