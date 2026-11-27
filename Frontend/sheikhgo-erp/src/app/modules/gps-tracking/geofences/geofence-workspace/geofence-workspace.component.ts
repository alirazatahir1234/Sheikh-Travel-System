/// <reference types="google.maps" />
import {
  AfterViewInit, ChangeDetectorRef, Component, ElementRef, NgZone, OnDestroy, OnInit, ViewChild, inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatDialog } from '@angular/material/dialog';
import { Subscription, forkJoin, interval, of } from 'rxjs';
import { catchError, filter } from 'rxjs/operators';
import { GeofenceWorkspaceService } from '../geofence-workspace.service';
import {
  GeofenceActivityItem,
  GeofenceAreaType,
  GeofenceBulkAction,
  GeofenceDetail,
  GeofenceGroup,
  GeofenceInsideVehicle,
  GeofenceListItem,
  GeofenceRecipientOptions,
  GeofenceShape,
  GeofenceSummary
} from '../geofence.models';
import {
  formatArea,
  isInsideShape,
  normalizeAreaType,
  shapeFromApi
} from '../geofence-geometry.util';
import {
  Badge,
  SHAPE_LABELS,
  TIME_OPTIONS,
  activityLabel,
  formatDuration,
  listMeta,
  notifySummary,
  plural,
  ruleViews,
  scheduleLabel,
  severityLabel
} from '../geofence-format.util';
import { duplicatePayload } from '../geofence-editor.model';
import {
  ImportCandidate,
  MAX_IMPORT_ITEMS,
  parseGeofenceFile,
  toCsv,
  toGeoJson,
  toKml
} from '../geofence-import-export.util';
import {
  GeofenceDrawTool,
  GmapHtmlLabel,
  MeasureTool,
  RenderedFence,
  VehicleTone,
  createHtmlLabel,
  labelAnchor,
  renderFence,
  shade,
  vehicleMarker
} from '../geofence-map';
import { GpsTrackingService } from '../../../../core/services/gps-tracking.service';
import { VehicleService } from '../../../../core/services/vehicle.service';
import { PlatformService } from '../../../../core/services/platform.service';
import { DriverService } from '../../../../core/services/driver.service';
import { AuthService } from '../../../../core/services/auth.service';
import { TenantTimeService } from '../../../../core/services/tenant-time.service';
import { GoogleMapsLoaderService } from '../../../../core/services/google-maps-loader.service';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { triggerGmapResize } from '../../../../core/google-maps/gmap-theme';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { toZonedDateInput } from '../../../../core/utils/zoned-time.util';
import { VehicleLocation } from '../../../../core/models/gps-tracking.model';
import { VehicleListItem } from '../../../../core/models/vehicle.model';
import { Branch, Department } from '../../../../core/models/platform.model';

export type GeofenceListFilter = 'all' | 'inside' | 'restricted' | 'unassigned' | 'inactive';
export type WorkspaceTool = 'select' | GeofenceAreaType | 'measure';
type DetailTab = 'overview' | 'rules' | 'vehicles' | 'activity';

export interface KpiCard {
  label: string;
  value: string;
  note: string;
  tone: 'default' | 'teal' | 'red';
}

export interface FenceGroupView {
  key: string;
  name: string;
  items: GeofenceListItem[];
}

interface MapFence {
  fence: GeofenceListItem;
  shape: GeofenceShape;
  rendered: RenderedFence;
  label: GmapHtmlLabel | null;
}

const VEHICLE_REFRESH_MS = 30_000;
const LIST_REFRESH_MS = 60_000;

/** Moves focus into a dialog once Angular has rendered it. */
export function focusSoon(id: string): void {
  setTimeout(() => document.getElementById(id)?.focus(), 0);
}

/** "Unassigned": nothing would ever be checked against it. */
export function isUnassigned(f: GeofenceListItem): boolean {
  return !f.appliesToAll && f.vehiclesCovered === 0 && (f.driversAssigned ?? 0) === 0;
}

export function matchesFilter(f: GeofenceListItem, filter: GeofenceListFilter): boolean {
  switch (filter) {
    case 'inside': return f.insideNow > 0;
    case 'restricted': return f.isRestricted;
    case 'unassigned': return isUnassigned(f);
    case 'inactive': return !f.isActive;
    default: return true;
  }
}

export function matchesSearch(f: GeofenceListItem, query: string): boolean {
  const q = query.trim().toLowerCase();
  if (!q) return true;
  return [f.name, f.type, f.address, f.groupName].some(v => (v ?? '').toLowerCase().includes(q));
}

/** The six KPI cards of the main screen, from GET /geofences/summary. */
export function kpiCards(s: GeofenceSummary | null): KpiCard[] {
  if (!s) return [];
  const breakdown = [
    [s.restrictedEntriesToday, 'restricted'],
    [s.overstaysToday, 'overstay'],
    [s.overspeedsToday, 'overspeed'],
    [s.idleAlertsToday, 'idle'],
    [s.afterHoursExitsToday, 'after hours'],
    [s.notArrivedToday, 'not arrived']
  ].filter(([n]) => (n as number) > 0).map(([n, label]) => `${n} ${label}`);
  const avg = s.avgMinutesOnSiteLast7Days;
  return [
    { label: 'Geofences', value: s.total.toLocaleString('en-US'), note: `${s.active.toLocaleString('en-US')} active`, tone: 'default' },
    { label: 'Vehicles inside now', value: s.vehiclesInsideNow.toLocaleString('en-US'), note: `of ${s.vehiclesAssigned.toLocaleString('en-US')} assigned`, tone: 'teal' },
    { label: 'Entries today', value: s.entriesToday.toLocaleString('en-US'), note: '', tone: 'default' },
    { label: 'Exits today', value: s.exitsToday.toLocaleString('en-US'), note: '', tone: 'default' },
    { label: 'Violations today', value: s.violationsToday.toLocaleString('en-US'), note: breakdown.join(' · ') || 'none', tone: s.violationsToday > 0 ? 'red' : 'default' },
    { label: 'Avg time on site', value: avg == null ? '—' : formatDuration(avg), note: avg == null ? 'no visits ended in 7 days' : 'last 7 days', tone: 'default' }
  ];
}

/** List rows grouped by their geofence group, in the groups' order; geofences without a group come last. */
export function groupFences(items: readonly GeofenceListItem[], groups: readonly GeofenceGroup[]): FenceGroupView[] {
  const order = new Map(groups.map((g, i) => [g.id, g.sortOrder * 1000 + i]));
  const byKey = new Map<string, FenceGroupView>();
  for (const f of items) {
    const key = f.groupId ? `g${f.groupId}` : 'none';
    let view = byKey.get(key);
    if (!view) {
      view = { key, name: f.groupId ? f.groupName || 'Group' : 'No group', items: [] };
      byKey.set(key, view);
    }
    view.items.push(f);
  }
  const rank = (v: FenceGroupView) => (v.key === 'none' ? Number.MAX_SAFE_INTEGER : order.get(Number(v.key.slice(1))) ?? Number.MAX_SAFE_INTEGER - 1);
  const views = [...byKey.values()].sort((a, b) => rank(a) - rank(b) || a.name.localeCompare(b.name));
  for (const v of views) v.items.sort((a, b) => a.name.localeCompare(b.name));
  return views;
}

@Component({
  selector: 'app-geofence-workspace',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, MatIconModule, MatMenuModule],
  templateUrl: './geofence-workspace.component.html',
  styleUrls: ['./geofence-workspace.component.scss']
})
export class GeofenceWorkspaceComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('mapEl') mapEl?: ElementRef<HTMLDivElement>;
  @ViewChild('importInput') importInput?: ElementRef<HTMLInputElement>;

  private readonly api = inject(GeofenceWorkspaceService);
  private readonly gps = inject(GpsTrackingService);
  private readonly vehiclesApi = inject(VehicleService);
  private readonly platform = inject(PlatformService);
  private readonly driversApi = inject(DriverService);
  private readonly auth = inject(AuthService);
  private readonly time = inject(TenantTimeService);
  private readonly mapsLoader = inject(GoogleMapsLoaderService);
  private readonly toast = inject(UiToastService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly zone = inject(NgZone);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly canManage = this.auth.hasPermission('Gps.GeofenceManage');
  readonly shapeLabels = SHAPE_LABELS;
  readonly maxImport = MAX_IMPORT_ITEMS;
  readonly filters: { value: GeofenceListFilter; label: string }[] = [
    { value: 'all', label: 'All' },
    { value: 'inside', label: 'Inside now' },
    { value: 'restricted', label: 'Restricted' },
    { value: 'unassigned', label: 'Unassigned' },
    { value: 'inactive', label: 'Inactive' }
  ];
  readonly drawTools: { value: WorkspaceTool; label: string }[] = [
    { value: 'select', label: 'Select' },
    { value: 'circle', label: 'Circle' },
    { value: 'polygon', label: 'Polygon' },
    { value: 'rectangle', label: 'Rectangle' },
    { value: 'corridor', label: 'Corridor' },
    { value: 'measure', label: 'Measure' }
  ];

  fences: GeofenceListItem[] = [];
  groups: GeofenceGroup[] = [];
  summary: GeofenceSummary | null = null;
  kpis: KpiCard[] = [];
  loading = true;
  loadError: string | null = null;

  search = '';
  filter: GeofenceListFilter = 'all';
  ticked = new Set<number>();
  busy = false;

  selectedId: number | null = null;
  detail: GeofenceDetail | null = null;
  detailLoading = false;
  detailError: string | null = null;
  inside: GeofenceInsideVehicle[] = [];
  today: GeofenceActivityItem[] = [];
  tab: DetailTab = 'overview';
  activityDay = '';
  dayActivity: GeofenceActivityItem[] = [];
  dayActivityLoading = false;

  vehicles: VehicleListItem[] = [];
  branches: Branch[] = [];
  departments: Department[] = [];
  drivers: { id: number; name: string }[] = [];
  recipients: GeofenceRecipientOptions | null = null;

  mapError: string | null = null;
  mapReady = false;
  satellite = false;
  showVehicles = true;
  showLabels = true;
  tool: WorkspaceTool = 'select';
  hint = '';
  liveVehicles: VehicleLocation[] = [];

  /** Assign-vehicles picker for one geofence or the ticked ones. */
  assignFor: number[] | null = null;
  assignSearch = '';
  assignPicked = new Set<number>();

  newGroupFor: number[] | null = null;
  newGroupName = '';

  importState: {
    fileName: string;
    items: ImportCandidate[];
    error: string | null;
    running: boolean;
    result: { created: number; skipped: { name: string; reason: string }[] } | null;
  } | null = null;

  private map: google.maps.Map | null = null;
  private mapFences: MapFence[] = [];
  private vehicleMarkers: google.maps.Marker[] = [];
  private drawTool: GeofenceDrawTool | null = null;
  private measureTool: MeasureTool | null = null;
  private fittedOnce = false;
  private resizeObserver: ResizeObserver | null = null;
  private readonly subs = new Subscription();

  ngOnInit(): void {
    const selected = Number(this.route.snapshot.queryParamMap.get('selected'));
    if (Number.isFinite(selected) && selected > 0) this.selectedId = selected;
    this.activityDay = toZonedDateInput(new Date(), this.time.timeZone());
    this.load();
    this.loadLookups();
    // Timers tick outside Angular so the page counts as stable between refreshes; each refresh re-enters the zone.
    this.zone.runOutsideAngular(() => {
      this.subs.add(interval(LIST_REFRESH_MS).pipe(filter(() => !document.hidden))
        .subscribe(() => this.zone.run(() => this.refreshCounts())));
      this.subs.add(interval(VEHICLE_REFRESH_MS).pipe(filter(() => !document.hidden))
        .subscribe(() => this.zone.run(() => this.loadVehicles())));
    });
  }

  ngAfterViewInit(): void {
    setTimeout(() => void this.initMap(), 0);
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
    this.resizeObserver?.disconnect();
    this.drawTool?.destroy();
    this.measureTool?.stop();
    this.clearMapFences();
    this.clearVehicleMarkers();
    this.map = null;
  }

  // ---- data -------------------------------------------------------------------------------------------------------

  load(): void {
    this.loading = true;
    this.loadError = null;
    this.subs.add(this.api.list().subscribe({
      next: rows => {
        this.fences = rows ?? [];
        this.loading = false;
        this.pruneTicks();
        this.renderFences();
        if (this.selectedId != null) {
          if (this.fences.some(f => f.id === this.selectedId)) this.loadDetail(this.selectedId);
          else this.clearSelection();
        }
      },
      error: err => {
        this.loading = false;
        this.loadError = apiErrorMessage(err, 'Geofences could not be loaded.');
      }
    }));
    this.loadSummary();
    this.subs.add(this.api.groups().pipe(catchError(() => of([] as GeofenceGroup[]))).subscribe(g => {
      this.groups = g ?? [];
    }));
  }

  private loadSummary(): void {
    this.subs.add(this.api.summary().pipe(catchError(() => of(null))).subscribe(s => {
      this.summary = s;
      this.kpis = kpiCards(s);
    }));
  }

  /** Inside-now and today's counts move all day; the shapes rarely do. */
  private refreshCounts(): void {
    this.loadSummary();
    this.subs.add(this.api.list().pipe(catchError(() => of(null))).subscribe(rows => {
      if (!rows) return;
      this.fences = rows;
      this.pruneTicks();
      this.renderFences();
    }));
    if (this.selectedId != null) {
      const id = this.selectedId;
      this.subs.add(this.api.inside(id).pipe(catchError(() => of(null))).subscribe(v => {
        if (v && this.selectedId === id) this.inside = v;
      }));
    }
  }

  private loadLookups(): void {
    this.subs.add(this.vehiclesApi.getAll(1, 500).pipe(catchError(() => of(null))).subscribe(r => {
      this.vehicles = r?.items ?? [];
    }));
    this.subs.add(this.platform.getBranches().pipe(catchError(() => of([] as Branch[]))).subscribe(b => (this.branches = b ?? [])));
    this.subs.add(this.platform.getDepartments().pipe(catchError(() => of([] as Department[]))).subscribe(d => (this.departments = d ?? [])));
    this.subs.add(this.driversApi.getAll({ page: 1, pageSize: 500 }).pipe(catchError(() => of(null))).subscribe(r => {
      this.drivers = (r?.items ?? []).map(d => ({ id: d.id, name: d.fullName }));
    }));
    if (this.canManage) {
      this.subs.add(this.api.recipientOptions().pipe(catchError(() => of(null))).subscribe(r => (this.recipients = r)));
    }
    this.loadVehicles();
  }

  private loadVehicles(): void {
    this.subs.add(this.gps.getAllVehicleLocations().pipe(catchError(() => of([] as VehicleLocation[]))).subscribe(rows => {
      this.liveVehicles = (rows ?? []).filter(v => v.hasGps !== false && Number.isFinite(v.latitude) && Number.isFinite(v.longitude)
        && !(v.latitude === 0 && v.longitude === 0));
      this.renderVehicles();
    }));
  }

  // ---- list -------------------------------------------------------------------------------------------------------

  get visibleFences(): GeofenceListItem[] {
    return this.fences.filter(f => matchesFilter(f, this.filter) && matchesSearch(f, this.search));
  }

  get groupedFences(): FenceGroupView[] {
    return groupFences(this.visibleFences, this.groups);
  }

  filterCount(value: GeofenceListFilter): number {
    return this.fences.filter(f => matchesFilter(f, value)).length;
  }

  meta(f: GeofenceListItem): string {
    return listMeta(f);
  }

  dotRadius(f: GeofenceListItem): string {
    const t = normalizeAreaType(f.areaType);
    return t === 'rectangle' ? '0' : t === 'polygon' ? '2px' : '6px';
  }

  toggleTick(id: number, on: boolean): void {
    if (on) this.ticked.add(id);
    else this.ticked.delete(id);
  }

  tickAllShown(): void {
    for (const f of this.visibleFences) this.ticked.add(f.id);
  }

  clearTicks(): void {
    this.ticked.clear();
  }

  private pruneTicks(): void {
    const ids = new Set(this.fences.map(f => f.id));
    for (const id of [...this.ticked]) if (!ids.has(id)) this.ticked.delete(id);
  }

  get tickedIds(): number[] {
    return [...this.ticked];
  }

  // ---- selection and detail ---------------------------------------------------------------------------------------

  select(id: number): void {
    if (this.selectedId === id && this.detail) return;
    this.selectedId = id;
    this.loadDetail(id);
    this.renderFences();
    this.focusFence(id);
    void this.router.navigate([], { relativeTo: this.route, queryParams: { selected: id }, queryParamsHandling: 'merge', replaceUrl: true });
  }

  clearSelection(): void {
    this.selectedId = null;
    this.detail = null;
    this.inside = [];
    this.today = [];
    this.renderFences();
    void this.router.navigate([], { relativeTo: this.route, queryParams: { selected: null }, queryParamsHandling: 'merge', replaceUrl: true });
  }

  private loadDetail(id: number): void {
    this.detailLoading = true;
    this.detailError = null;
    this.subs.add(forkJoin({
      detail: this.api.detail(id),
      inside: this.api.inside(id).pipe(catchError(() => of([] as GeofenceInsideVehicle[]))),
      today: this.api.activity(id).pipe(catchError(() => of([] as GeofenceActivityItem[])))
    }).subscribe({
      next: r => {
        if (this.selectedId !== id) return;
        this.detail = r.detail;
        this.inside = r.inside ?? [];
        this.today = r.today ?? [];
        this.detailLoading = false;
        if (this.tab === 'activity') this.loadDayActivity();
      },
      error: err => {
        if (this.selectedId !== id) return;
        this.detailLoading = false;
        this.detail = null;
        this.detailError = apiErrorMessage(err, 'This geofence could not be loaded.');
      }
    }));
  }

  setTab(tab: DetailTab): void {
    this.tab = tab;
    if (tab === 'activity') this.loadDayActivity();
  }

  loadDayActivity(): void {
    if (this.selectedId == null || !this.activityDay) return;
    const id = this.selectedId;
    this.dayActivityLoading = true;
    this.subs.add(this.api.activity(id, this.activityDay).pipe(catchError(() => of([] as GeofenceActivityItem[]))).subscribe(rows => {
      if (this.selectedId !== id) return;
      this.dayActivity = rows ?? [];
      this.dayActivityLoading = false;
    }));
  }

  get selectedItem(): GeofenceListItem | null {
    return this.fences.find(f => f.id === this.selectedId) ?? null;
  }

  get todayMax(): string {
    return toZonedDateInput(new Date(), this.time.timeZone());
  }

  detailMeta(d: GeofenceDetail): string {
    const parts = [d.type || 'No type', SHAPE_LABELS[normalizeAreaType(d.areaType)]];
    if (d.areaSquareMeters) parts.push(formatArea(d.areaSquareMeters));
    if (d.address) parts.push(d.address);
    return parts.join(' · ');
  }

  rules(d: GeofenceDetail) {
    return ruleViews(d);
  }

  severity(s: 'info' | 'warning' | 'critical'): string {
    return severityLabel(s);
  }

  schedule(d: GeofenceDetail): string {
    return scheduleLabel(d.scheduleDays, d.scheduleStart, d.scheduleEnd);
  }

  notify(d: GeofenceDetail): string {
    const roleNames = d.recipientRoleIds.map(id => this.recipients?.roles.find(r => r.id === id)?.name).filter((n): n is string => !!n);
    const userNames = d.recipientUserIds.map(id => this.recipients?.users.find(u => u.id === id)?.name).filter((n): n is string => !!n);
    let names = [...roleNames, ...userNames];
    const total = d.recipientRoleIds.length + d.recipientUserIds.length;
    if (names.length < total) names = total ? [plural(total, 'chosen recipient')] : [];
    return notifySummary(d.notifyChannels, names, { days: d.scheduleDays, start: d.scheduleStart, end: d.scheduleEnd });
  }

  activity(item: GeofenceActivityItem): Badge {
    return activityLabel(item.kind, this.detail);
  }

  who(item: { vehicleLabel?: string | null; driverName?: string | null }): string {
    return [item.vehicleLabel, item.driverName].filter(Boolean).join(' · ');
  }

  timeOf(utc: string): string {
    return this.time.formatTime(utc, TIME_OPTIONS);
  }

  duration(minutes: number): string {
    return formatDuration(minutes);
  }

  vehicleNames(ids: readonly number[]): string[] {
    return ids.map(id => {
      const v = this.vehicles.find(x => x.id === id);
      return v ? (v.registrationNumber || v.name) : `Vehicle #${id}`;
    });
  }

  branchNames(ids: readonly number[]): string[] {
    return ids.map(id => this.branches.find(b => b.id === id)?.name ?? `Branch #${id}`);
  }

  departmentNames(ids: readonly number[]): string[] {
    return ids.map(id => this.departments.find(d => d.id === id)?.name ?? `Department #${id}`);
  }

  driverNames(ids: readonly number[]): string[] {
    return ids.map(id => this.drivers.find(d => d.id === id)?.name ?? `Driver #${id}`);
  }

  // ---- actions ----------------------------------------------------------------------------------------------------

  runBulk(action: GeofenceBulkAction, ids: number[], extra?: { groupId?: number | null; vehicleIds?: number[] }): void {
    if (!ids.length || this.busy) return;
    const go = () => {
      this.busy = true;
      this.subs.add(this.api.bulk({ ids, action, groupId: extra?.groupId ?? null, vehicleIds: extra?.vehicleIds ?? null }).subscribe({
        next: r => {
          this.busy = false;
          this.toast.success(this.bulkMessage(action, r?.affected ?? ids.length));
          if (action === 'delete' && this.selectedId != null && ids.includes(this.selectedId)) this.clearSelection();
          for (const id of ids) if (action === 'delete') this.ticked.delete(id);
          this.load();
          if (this.selectedId != null && ids.includes(this.selectedId)) this.loadDetail(this.selectedId);
        },
        error: err => {
          this.busy = false;
          this.toast.error(apiErrorMessage(err, 'That did not work. Please try again.'));
        }
      }));
    };
    if (action !== 'delete') {
      go();
      return;
    }
    const names = ids.length === 1 ? `“${this.fences.find(f => f.id === ids[0])?.name ?? 'this geofence'}”` : plural(ids.length, 'geofence');
    const data: ConfirmDialogData = {
      title: ids.length === 1 ? 'Delete geofence?' : `Delete ${plural(ids.length, 'geofence')}?`,
      message: `${names} will stop recording visits and alerts. Past visits stay in reports.`,
      confirmText: 'Delete',
      confirmColor: 'warn'
    };
    this.subs.add(this.dialog.open(ConfirmDialogComponent, { data, width: '420px' }).afterClosed().subscribe(ok => {
      if (ok) go();
    }));
  }

  private bulkMessage(action: GeofenceBulkAction, n: number): string {
    switch (action) {
      case 'enable': return `${plural(n, 'geofence')} switched on.`;
      case 'disable': return `${plural(n, 'geofence')} switched off.`;
      case 'delete': return `${plural(n, 'geofence')} deleted.`;
      case 'group': return `${plural(n, 'geofence')} moved.`;
      default: return `${plural(n, 'vehicle assignment')} added.`;
    }
  }

  moveToGroup(ids: number[], groupId: number | null): void {
    this.runBulk('group', ids, { groupId });
  }

  openNewGroup(ids: number[]): void {
    this.newGroupFor = ids;
    this.newGroupName = '';
    focusSoon('gw-group-name');
  }

  createGroupAndMove(): void {
    const name = this.newGroupName.trim();
    const ids = this.newGroupFor;
    if (!name || !ids) return;
    this.busy = true;
    this.subs.add(this.api.createGroup(name, this.groups.length).subscribe({
      next: id => {
        this.busy = false;
        this.newGroupFor = null;
        this.runBulk('group', ids, { groupId: Number(id) });
      },
      error: err => {
        this.busy = false;
        this.toast.error(apiErrorMessage(err, 'The group could not be created.'));
      }
    }));
  }

  duplicate(): void {
    const d = this.detail;
    if (!d || this.busy) return;
    const shape = shapeFromApi(d);
    if (!shape) {
      this.toast.error('This geofence’s shape could not be read, so it cannot be copied.');
      return;
    }
    this.busy = true;
    this.subs.add(this.api.create(duplicatePayload(d, shape)).subscribe({
      next: id => {
        this.busy = false;
        this.toast.success(`Copied as “${d.name.slice(0, 92)} (copy)”.`);
        this.selectedId = Number(id);
        this.load();
      },
      error: err => {
        this.busy = false;
        this.toast.error(apiErrorMessage(err, 'The geofence could not be copied.'));
      }
    }));
  }

  openAssign(ids: number[]): void {
    this.assignFor = ids;
    this.assignSearch = '';
    this.assignPicked = new Set();
    focusSoon('gw-assign-search');
  }

  get assignChoices(): VehicleListItem[] {
    const q = this.assignSearch.trim().toLowerCase();
    const list = q
      ? this.vehicles.filter(v => [v.name, v.registrationNumber, v.driverName].some(s => (s ?? '').toLowerCase().includes(q)))
      : this.vehicles;
    return list.slice(0, 200);
  }

  get assignCoversAll(): boolean {
    return (this.assignFor ?? []).some(id => this.fences.find(f => f.id === id)?.appliesToAll);
  }

  toggleAssign(id: number, on: boolean): void {
    if (on) this.assignPicked.add(id);
    else this.assignPicked.delete(id);
  }

  confirmAssign(): void {
    const ids = this.assignFor;
    if (!ids?.length || !this.assignPicked.size) return;
    this.assignFor = null;
    this.runBulk('assign', ids, { vehicleIds: [...this.assignPicked] });
  }

  goToNew(): void {
    void this.router.navigate(['/gps-tracking/geofences/new']);
  }

  // ---- import / export --------------------------------------------------------------------------------------------

  pickImportFile(): void {
    this.importInput?.nativeElement.click();
  }

  onImportFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (file.size > 10 * 1024 * 1024) {
      this.toast.error('That file is over 10 MB. Split it into smaller files.');
      return;
    }
    file.text().then(text => this.zone.run(() => {
      const parsed = parseGeofenceFile(file.name, text);
      this.importState = { fileName: file.name, items: parsed.items, error: parsed.error, running: false, result: null };
    })).catch(() => this.zone.run(() => this.toast.error('The file could not be read.')));
  }

  get importReady(): ImportCandidate[] {
    return (this.importState?.items ?? []).filter(i => i.payload && !i.error);
  }

  runImport(): void {
    const state = this.importState;
    const items = this.importReady;
    if (!state || !items.length || state.running || state.error) return;
    state.running = true;
    this.subs.add(this.api.import(items.map(i => i.payload!)).subscribe({
      next: r => {
        state.running = false;
        state.result = { created: r?.created ?? 0, skipped: r?.skipped ?? [] };
        this.load();
      },
      error: err => {
        state.running = false;
        state.error = apiErrorMessage(err, 'The import failed. Nothing was created.');
      }
    }));
  }

  closeImport(): void {
    this.importState = null;
  }

  export(format: 'geojson' | 'kml' | 'csv', onlyTicked: boolean): void {
    const items = onlyTicked ? this.fences.filter(f => this.ticked.has(f.id)) : this.fences;
    if (!items.length) {
      this.toast.info('There are no geofences to export.');
      return;
    }
    const stamp = toZonedDateInput(new Date(), this.time.timeZone());
    const [text, type, ext] =
      format === 'kml' ? [toKml(items), 'application/vnd.google-earth.kml+xml', 'kml']
      : format === 'csv' ? [toCsv(items), 'text/csv;charset=utf-8', 'csv']
      : [toGeoJson(items), 'application/geo+json', 'geojson'];
    const url = URL.createObjectURL(new Blob([format === 'csv' ? `﻿${text}` : text], { type }));
    const a = document.createElement('a');
    a.href = url;
    a.download = `geofences-${stamp}.${ext}`;
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }

  // ---- map --------------------------------------------------------------------------------------------------------

  private async initMap(): Promise<void> {
    if (!this.mapEl?.nativeElement || this.map) return;
    if (!this.mapsLoader.isConfigured) {
      this.mapError = 'Google Maps is not set up for this site, so the map cannot be shown.';
      return;
    }
    this.subs.add(this.mapsLoader.authFailures$.subscribe(msg => this.zone.run(() => (this.mapError = msg))));
    try {
      const ok = await this.mapsLoader.load();
      if (!ok) throw new Error(this.mapsLoader.failureMessage || 'Google Maps failed to load.');
      await this.mapsLoader.importLibrary('maps');
      if (this.mapsLoader.authFailed) throw new Error(this.mapsLoader.failureMessage || 'Google Maps rejected the key.');
    } catch (err) {
      this.zone.run(() => (this.mapError = err instanceof Error ? err.message : 'Google Maps failed to load.'));
      return;
    }

    this.map = new google.maps.Map(this.mapEl.nativeElement, {
      center: { lat: 31.52, lng: 74.35 },
      zoom: 11,
      renderingType: google.maps.RenderingType.RASTER,
      mapTypeControl: false,
      streetViewControl: false,
      fullscreenControl: false,
      zoomControl: true,
      zoomControlOptions: { position: google.maps.ControlPosition.RIGHT_BOTTOM },
      gestureHandling: 'greedy',
      clickableIcons: false
    });
    this.drawTool = new GeofenceDrawTool({
      map: this.map,
      color: () => '#0f766e',
      corridorWidth: () => 200,
      onHint: text => this.zone.run(() => (this.hint = text)),
      onComplete: shape => this.zone.run(() => this.onShapeDrawn(shape))
    });
    this.measureTool = new MeasureTool(this.map, () => this.zone.run(() => (this.hint = 'Click more points to extend the line. Pick Select to finish.')));
    if (typeof ResizeObserver !== 'undefined') {
      this.resizeObserver = new ResizeObserver(() => triggerGmapResize(this.map));
      this.resizeObserver.observe(this.mapEl.nativeElement);
    }
    this.zone.run(() => {
      this.mapReady = true;
      this.renderFences();
      this.renderVehicles();
      this.cdr.detectChanges();
    });
  }

  setTool(tool: WorkspaceTool): void {
    if (!this.map) return;
    this.drawTool?.cancel();
    this.measureTool?.stop();
    this.tool = tool;
    this.hint = '';
    if (tool === 'measure') {
      this.measureTool?.start();
      this.hint = 'Click points on the map to measure the distance between them.';
    } else if (tool !== 'select') {
      if (!this.canManage) {
        this.tool = 'select';
        return;
      }
      this.drawTool?.start(tool);
    }
    this.renderFences();
  }

  /** A shape drawn on the main map opens the editor with it, ready to name and set rules. */
  private onShapeDrawn(shape: GeofenceShape): void {
    this.tool = 'select';
    void this.router.navigate(['/gps-tracking/geofences/new'], { state: { shape } });
  }

  toggleSatellite(on: boolean): void {
    this.satellite = on;
    this.map?.setMapTypeId(on ? 'hybrid' : 'roadmap');
  }

  toggleLayer(layer: 'vehicles' | 'labels'): void {
    if (layer === 'vehicles') {
      this.showVehicles = !this.showVehicles;
      this.renderVehicles();
    } else {
      this.showLabels = !this.showLabels;
      this.renderFences();
    }
  }

  fitAll(): void {
    if (!this.map) return;
    const bounds = new google.maps.LatLngBounds();
    let any = false;
    for (const f of this.mapFences) {
      const b = f.rendered.bounds();
      if (b) {
        bounds.union(b);
        any = true;
      }
    }
    if (any) this.map.fitBounds(bounds, 48);
  }

  private focusFence(id: number): void {
    const f = this.mapFences.find(m => m.fence.id === id);
    const b = f?.rendered.bounds();
    if (!this.map || !b) return;
    const view = this.map.getBounds();
    if (!view || !view.contains(b.getNorthEast()) || !view.contains(b.getSouthWest())) this.map.fitBounds(b, 96);
  }

  private renderFences(): void {
    if (!this.map) return;
    this.clearMapFences();
    const clickable = this.tool === 'select';
    for (const fence of this.fences) {
      const shape = shapeFromApi(fence);
      if (!shape) continue;
      const color = fence.color || '#0f766e';
      const selected = fence.id === this.selectedId;
      const open = clickable ? () => this.zone.run(() => this.select(fence.id)) : undefined;
      const rendered = renderFence(this.map, shape, { color, restricted: fence.isRestricted, active: fence.isActive, selected }, open);
      let label: GmapHtmlLabel | null = null;
      const anchor = labelAnchor(shape);
      if (this.showLabels && anchor) {
        const text = fence.insideNow > 0 ? `${fence.name} · ${fence.insideNow}` : fence.name;
        label = createHtmlLabel(this.map, anchor, text, fence.isActive ? shade(color) : '#64748b', open);
      }
      this.mapFences.push({ fence, shape, rendered, label });
    }
    if (!this.fittedOnce && this.mapFences.length) {
      this.fittedOnce = true;
      if (this.selectedId != null) this.focusFence(this.selectedId);
      else this.fitAll();
    }
  }

  private clearMapFences(): void {
    for (const f of this.mapFences) {
      f.rendered.remove();
      f.label?.setMap(null);
    }
    this.mapFences = [];
  }

  /** Inside a geofence, outside, or in violation (inside a restricted zone). */
  toneFor(v: VehicleLocation): VehicleTone {
    const p = { lat: v.latitude, lng: v.longitude };
    let inside = false;
    for (const f of this.mapFences) {
      if (!f.fence.isActive || !isInsideShape(p, f.shape)) continue;
      if (f.fence.isRestricted) return 'violation';
      inside = true;
    }
    return inside ? 'inside' : 'outside';
  }

  private renderVehicles(): void {
    this.clearVehicleMarkers();
    if (!this.map || !this.showVehicles) return;
    for (const v of this.liveVehicles) {
      const title = [v.registrationNumber || v.vehicleName, v.driverName, `${Math.round(v.speed || 0)} km/h`].filter(Boolean).join(' · ');
      this.vehicleMarkers.push(vehicleMarker(this.map, { lat: v.latitude, lng: v.longitude }, this.toneFor(v), title));
    }
  }

  private clearVehicleMarkers(): void {
    for (const m of this.vehicleMarkers) m.setMap(null);
    this.vehicleMarkers = [];
  }

  trackFence = (_: number, f: GeofenceListItem) => f.id;
  trackGroup = (_: number, g: FenceGroupView) => g.key;
}
