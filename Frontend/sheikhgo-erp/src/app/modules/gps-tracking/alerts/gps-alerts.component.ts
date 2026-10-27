import { Component, HostListener, OnInit, OnDestroy } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription, forkJoin, of } from 'rxjs';
import { FormBuilder, Validators } from '@angular/forms';
import { catchError, finalize } from 'rxjs/operators';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { AuthService } from '../../../core/services/auth.service';
import { PlatformService } from '../../../core/services/platform.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { DriverService } from '../../../core/services/driver.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import {
  GpsAlertEvent,
  GpsAlertRule,
  Geofence,
  GpsAlertStats,
  GpsAlertNotificationSetting
} from '../../../core/models/gps-tracking.model';
import { Tenant } from '../../../core/models/platform.model';
import { VehicleListItem, VehicleStatus } from '../../../core/models/vehicle.model';
import { DriverListItem } from '../../../core/models/driver.model';
import {
  GPS_ALERT_EVENT_TYPES,
  GpsAlertDisplayGroup,
  GpsAlertLifecycleTab,
  GpsAlertQuietHours,
  alertRowKey,
  alertsToCsv,
  downloadTextFile,
  groupSimilarAlerts,
  isQuietHoursNow,
  isSnoozed,
  isSosEvent,
  needsEscalation,
  readMutedEventTypes,
  readQuietHours,
  readSnoozeMap,
  snoozeAlert,
  sortAlertsBySeverity,
  writeMutedEventTypes,
  writeQuietHours
} from './gps-alerts.helpers';

@Component({
  standalone: false,
  selector: 'app-gps-alerts',
  templateUrl: './gps-alerts.component.html',
  styleUrls: ['./gps-alerts.component.scss']
})
export class GpsAlertsComponent implements OnInit, OnDestroy {
  rules: GpsAlertRule[] = [];
  rawEvents: GpsAlertEvent[] = [];
  displayGroups: GpsAlertDisplayGroup[] = [];
  geofences: Geofence[] = [];
  stats: GpsAlertStats = {
    total: 0,
    today: 0,
    unread: 0,
    active: 0,
    critical: 0,
    resolved: 0,
    archived: 0
  };
  loading = false;
  loadError: string | null = null;
  activeTab: 'rules' | 'events' = 'events';
  showRuleForm = false;
  showPrefsPanel = false;
  selectedEvent: GpsAlertEvent | null = null;

  lifecycleTab: GpsAlertLifecycleTab = 'open';
  severityFilter: string | null = null;
  eventTypeFilter: string | null = null;
  datePreset = 'last24';
  vehicleFilterId: number | null = null;
  driverFilterId: number | null = null;
  groupSimilar = true;

  eventsViewMode: 'list' | 'grid' = 'list';
  pageIndex = 0;
  pageSize = 25;
  readonly pageSizeOptions = [10, 25, 50];

  isSuperAdmin = false;
  platformCrossTenantLimited = false;
  companyFilterId: number | null = null;
  companies: Tenant[] = [];
  companiesLoading = false;
  vehicles: VehicleListItem[] = [];
  drivers: DriverListItem[] = [];

  lastRefreshedAt: Date | null = null;
  refreshInFlight = false;
  escalatedAlerts: GpsAlertEvent[] = [];
  selectedKeys = new Set<string>();
  snoozeMap: Record<string, string> = {};
  mutedTypes = new Set<string>();
  quietHours: GpsAlertQuietHours = readQuietHours();
  notificationSettings: GpsAlertNotificationSetting[] = [];
  prefsSaving = false;

  readonly eventTypes = GPS_ALERT_EVENT_TYPES;

  ruleForm!: ReturnType<FormBuilder['group']>;
  private printAllGroups = false;
  private refreshTimer: ReturnType<typeof setInterval> | null = null;
  private platformAlertsSub: Subscription | null = null;
  private routeSub: Subscription | null = null;

  constructor(
    private gps: GpsTrackingService,
    private auth: AuthService,
    private platform: PlatformService,
    private vehiclesApi: VehicleService,
    private driversApi: DriverService,
    private fb: FormBuilder,
    private toast: UiToastService,
    private router: Router,
    private route: ActivatedRoute
  ) {
    this.ruleForm = this.fb.group({
      vehicleId: [null as number | null],
      speedLimitKmh: [100, [Validators.required, Validators.min(1)]],
      geofenceId: [null as number | null],
      alertOnEnter: [true],
      alertOnExit: [true]
    });
  }

  ngOnInit(): void {
    this.isSuperAdmin = this.auth.hasRole('SUPER_ADMIN');
    this.snoozeMap = readSnoozeMap();
    this.mutedTypes = readMutedEventTypes();
    this.quietHours = readQuietHours();
    this.applyLifecycleTab(false);

    if (this.isSuperAdmin) {
      this.loadCompanies();
      this.platformAlertsSub = this.gps.platformAlertsApiMissing$.subscribe(missing => {
        this.platformCrossTenantLimited = missing;
      });
    }

    this.loadFilterOptions();
    this.loadNotificationSettings();

    this.routeSub = this.route.queryParamMap.subscribe(params => {
      const vehicleId = params.get('vehicleId');
      const alertId = params.get('alertId');
      if (vehicleId) this.vehicleFilterId = Number(vehicleId);
      if (alertId) this.pendingAlertId = Number(alertId);
      this.loadEvents();
    });

    this.load();
    this.gps.getGeofences().subscribe({ next: g => { this.geofences = g; } });
    this.refreshTimer = setInterval(() => this.loadEvents(true), 30_000);
  }

  private pendingAlertId: number | null = null;

  ngOnDestroy(): void {
    if (this.refreshTimer) clearInterval(this.refreshTimer);
    this.platformAlertsSub?.unsubscribe();
    this.routeSub?.unsubscribe();
  }

  get showCompanyColumn(): boolean {
    return this.isSuperAdmin && this.companyFilterId == null;
  }

  get kpiCritical(): number {
    return this.stats.critical ?? this.rawEvents.filter(
      e => (e.severity ?? '').toLowerCase() === 'critical' && e.status !== 'resolved' && e.status !== 'archived'
    ).length;
  }

  get kpiSos(): number {
    return this.rawEvents.filter(e => isSosEvent(e) && e.status !== 'resolved' && e.status !== 'archived').length;
  }

  get selectedCount(): number {
    return this.selectedKeys.size;
  }

  get paginatedDisplayGroups(): GpsAlertDisplayGroup[] {
    const start = this.pageIndex * this.pageSize;
    return this.displayGroups.slice(start, start + this.pageSize);
  }

  /** Full filtered set for print/PDF; paginated slice otherwise. */
  get visibleDisplayGroups(): GpsAlertDisplayGroup[] {
    return this.printAllGroups ? this.displayGroups : this.paginatedDisplayGroups;
  }

  @HostListener('window:beforeprint')
  onBeforePrint(): void {
    this.printAllGroups = true;
  }

  @HostListener('window:afterprint')
  onAfterPrint(): void {
    this.printAllGroups = false;
  }

  get pageRangeStart(): number {
    if (!this.displayGroups.length) return 0;
    return this.pageIndex * this.pageSize + 1;
  }

  get pageRangeEnd(): number {
    return Math.min((this.pageIndex + 1) * this.pageSize, this.displayGroups.length);
  }

  get allSelected(): boolean {
    const keys = this.paginatedDisplayGroups.map(g => alertRowKey(g.representative));
    return keys.length > 0 && keys.every(k => this.selectedKeys.has(k));
  }

  load(): void {
    this.gps.getAlertRules().subscribe({ next: r => { this.rules = r; } });
  }

  loadFilterOptions(): void {
    const tenantScope = this.isSuperAdmin ? this.companyFilterId : undefined;
    this.vehiclesApi.getAll(1, 500, false, tenantScope ?? undefined).subscribe({
      next: r => {
        this.vehicles = r.items.filter(v => v.status !== VehicleStatus.Retired);
      }
    });
    this.driversApi.getAll({ page: 1, pageSize: 500 }).subscribe({
      next: r => { this.drivers = r.items; }
    });
  }

  onCompanyFilterChange(tenantId: number | null): void {
    this.companyFilterId = tenantId;
    this.resetPagination();
    this.loadFilterOptions();
    this.loadEvents();
  }

  setLifecycleTab(tab: GpsAlertLifecycleTab): void {
    this.lifecycleTab = tab;
    this.resetPagination();
    this.applyLifecycleTab(true);
  }

  private applyLifecycleTab(reload: boolean): void {
    if (reload) this.loadEvents();
  }

  private apiFilterOpts() {
    const status = this.lifecycleTab === 'open'
      ? 'active'
      : this.lifecycleTab === 'acknowledged'
        ? 'acknowledged'
        : 'resolved';
    return {
      status,
      severity: this.severityFilter ?? undefined,
      eventType: this.eventTypeFilter ?? undefined,
      datePreset: this.datePreset,
      vehicleId: this.vehicleFilterId ?? undefined,
      driverId: this.driverFilterId ?? undefined
    };
  }

  loadEvents(silent = false): void {
    if (!silent) this.loading = true;
    this.refreshInFlight = true;
    this.loadError = null;
    const filterOpts = this.apiFilterOpts();

    const stats$ = this.isSuperAdmin
      ? this.gps.getPlatformAlertStats(this.companyFilterId)
      : this.gps.getAlertStats();
    const { vehicleId, driverId, ...rest } = filterOpts;
    const events$ = this.isSuperAdmin
      ? this.gps.getPlatformAlertEvents(this.companyFilterId, { ...rest, vehicleId, driverId })
      : this.gps.getAlertEvents(vehicleId, { ...rest, driverId });

    stats$.subscribe({
      next: s => { this.stats = s; },
      error: err => {
        if (!silent) this.toast.error(err?.error?.message ?? 'Failed to load alert stats');
      }
    });

    events$.pipe(
      finalize(() => {
        this.loading = false;
        this.refreshInFlight = false;
        this.lastRefreshedAt = new Date();
      })
    ).subscribe({
      next: e => {
        this.rawEvents = this.applyClientFilters(e);
        this.rebuildDisplay();
        this.escalatedAlerts = this.rawEvents.filter(ev => needsEscalation(ev));
        if (this.pendingAlertId) {
          const match = this.flatEvents().find(x => x.id === this.pendingAlertId);
          if (match) this.selectedEvent = match;
          this.pendingAlertId = null;
        }
        if (this.selectedEvent) {
          this.selectedEvent = this.flatEvents().find(item => alertRowKey(item) === alertRowKey(this.selectedEvent!)) ?? null;
        }
      },
      error: err => {
        this.rawEvents = [];
        this.displayGroups = [];
        this.loadError = err?.error?.message || err?.message || 'Failed to load alerts.';
        if (!silent) this.toast.error(this.loadError ?? 'Failed to load alerts.');
      }
    });
  }

  private applyClientFilters(events: GpsAlertEvent[]): GpsAlertEvent[] {
    let list = events.filter(e => !isSnoozed(e, this.snoozeMap));
    list = list.filter(e => !this.mutedTypes.has((e.eventType ?? '').toLowerCase()));
    return sortAlertsBySeverity(list);
  }

  private rebuildDisplay(): void {
    this.displayGroups = this.groupSimilar
      ? groupSimilarAlerts(this.rawEvents)
      : this.rawEvents.map(e => ({
        key: alertRowKey(e),
        representative: e,
        events: [e],
        count: 1
      }));
    this.syncPaginationBounds();
  }

  private resetPagination(): void {
    this.pageIndex = 0;
  }

  private syncPaginationBounds(): void {
    const maxPage = Math.max(0, Math.ceil(this.displayGroups.length / this.pageSize) - 1);
    if (this.pageIndex > maxPage) this.pageIndex = maxPage;
  }

  setEventsViewMode(mode: 'list' | 'grid'): void {
    this.eventsViewMode = mode;
  }

  onAlertsPage(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.syncPaginationBounds();
  }

  flatEvents(): GpsAlertEvent[] {
    return this.displayGroups.flatMap(g => g.events);
  }

  toggleSeverity(value: string): void {
    this.severityFilter = this.severityFilter === value ? null : value;
    this.resetPagination();
    this.loadEvents();
  }

  toggleEventType(value: string): void {
    this.eventTypeFilter = this.eventTypeFilter === value ? null : value;
    this.resetPagination();
    this.loadEvents();
  }

  setDatePreset(value: string): void {
    this.datePreset = value;
    this.resetPagination();
    this.loadEvents();
  }

  onVehicleFilterChange(id: number | null): void {
    this.vehicleFilterId = id;
    this.resetPagination();
    this.loadEvents();
  }

  onDriverFilterChange(id: number | null): void {
    this.driverFilterId = id;
    this.resetPagination();
    this.loadEvents();
  }

  toggleGroupSimilar(): void {
    this.groupSimilar = !this.groupSimilar;
    this.resetPagination();
    this.rebuildDisplay();
  }

  toggleSelectAll(checked: boolean): void {
    const pageEvents = this.paginatedDisplayGroups.map(g => g.representative);
    if (checked) {
      pageEvents.forEach(e => this.selectedKeys.add(alertRowKey(e)));
    } else {
      pageEvents.forEach(e => this.selectedKeys.delete(alertRowKey(e)));
    }
    this.selectedKeys = new Set(this.selectedKeys);
  }

  toggleSelect(event: GpsAlertEvent, checked: boolean): void {
    const key = alertRowKey(event);
    if (checked) this.selectedKeys.add(key);
    else this.selectedKeys.delete(key);
    this.selectedKeys = new Set(this.selectedKeys);
  }

  isSelected(event: GpsAlertEvent): boolean {
    return this.selectedKeys.has(alertRowKey(event));
  }

  bulkAcknowledge(): void {
    const targets = this.flatEvents().filter(e => this.selectedKeys.has(alertRowKey(e)) && e.canAcknowledge);
    if (!targets.length) {
      this.toast.warning('No acknowledgable alerts selected.');
      return;
    }
    forkJoin(
      targets.map(e => this.gps.acknowledgeAlert(e.id, this.tenantScopeFor(e)).pipe(catchError(() => of(false))))
    ).subscribe(() => {
      this.toast.success(`Acknowledged ${targets.length} alert(s).`);
      this.selectedKeys.clear();
      this.loadEvents();
    });
  }

  bulkResolve(): void {
    const targets = this.flatEvents().filter(e => this.selectedKeys.has(alertRowKey(e)) && e.canResolve);
    if (!targets.length) {
      this.toast.warning('No resolvable alerts selected.');
      return;
    }
    forkJoin(
      targets.map(e => this.gps.resolveAlert(e.id, 'Bulk resolve', this.tenantScopeFor(e)).pipe(catchError(() => of(false))))
    ).subscribe(() => {
      this.toast.success(`Resolved ${targets.length} alert(s).`);
      this.selectedKeys.clear();
      this.loadEvents();
    });
  }

  snooze(event: GpsAlertEvent, hours: number, ev?: Event): void {
    ev?.stopPropagation();
    snoozeAlert(event, hours);
    this.snoozeMap = readSnoozeMap();
    this.loadEvents(true);
    this.rebuildDisplay();
    this.toast.success(`Snoozed ${hours}h`);
  }

  muteEventType(type: string): void {
    this.mutedTypes.add(type.toLowerCase());
    writeMutedEventTypes(this.mutedTypes);
    this.loadEvents();
    this.toast.success(`Muted ${this.eventTypeLabel(type)} alerts on this device`);
  }

  unmuteEventType(type: string): void {
    this.mutedTypes.delete(type.toLowerCase());
    writeMutedEventTypes(this.mutedTypes);
    this.loadEvents();
  }

  private loadCompanies(): void {
    this.companiesLoading = true;
    this.platform.getTenants().subscribe({
      next: list => {
        this.companies = (list ?? []).filter(t => t.isActive !== false);
        this.companiesLoading = false;
      },
      error: () => { this.companiesLoading = false; }
    });
  }

  loadNotificationSettings(): void {
    this.gps.getAlertNotificationSettings().subscribe({
      next: s => { this.notificationSettings = s ?? []; },
      error: () => { this.notificationSettings = []; }
    });
  }

  saveNotificationSettings(): void {
    this.prefsSaving = true;
    writeQuietHours(this.quietHours);
    this.gps.updateAlertNotificationSettings(this.notificationSettings).pipe(
      finalize(() => { this.prefsSaving = false; })
    ).subscribe({
      next: () => {
        this.toast.success('Notification preferences saved');
        this.showPrefsPanel = false;
      },
      error: err => this.toast.error(err?.error?.message ?? 'Save failed')
    });
  }

  quietHoursActive(): boolean {
    return isQuietHoursNow(this.quietHours);
  }

  private tenantScopeFor(event: GpsAlertEvent): number | null | undefined {
    if (!this.isSuperAdmin) return undefined;
    return event.tenantId ?? this.companyFilterId ?? undefined;
  }

  createRule(): void {
    if (this.ruleForm.invalid) return;
    const v = this.ruleForm.getRawValue();
    this.gps.createAlertRule({
      vehicleId: v.vehicleId ?? undefined,
      speedLimitKmh: Number(v.speedLimitKmh),
      geofenceId: v.geofenceId ?? undefined,
      alertOnEnter: !!v.alertOnEnter,
      alertOnExit: !!v.alertOnExit
    }).subscribe({
      next: () => { this.toast.success('Alert rule created'); this.showRuleForm = false; this.load(); },
      error: err => this.toast.error(err?.error?.message ?? 'Create failed')
    });
  }

  acknowledge(event: GpsAlertEvent, ev?: Event): void {
    ev?.stopPropagation();
    this.gps.acknowledgeAlert(event.id, this.tenantScopeFor(event)).subscribe({
      next: () => { this.toast.success('Alert acknowledged'); this.loadEvents(); }
    });
  }

  markRead(event: GpsAlertEvent, ev?: Event): void {
    ev?.stopPropagation();
    this.gps.markRead(event.id, this.tenantScopeFor(event)).subscribe({
      next: () => { this.toast.success('Alert marked as read'); this.loadEvents(); }
    });
  }

  resolve(event: GpsAlertEvent, ev?: Event): void {
    ev?.stopPropagation();
    this.gps.resolveAlert(event.id, undefined, this.tenantScopeFor(event)).subscribe({
      next: () => { this.toast.success('Alert resolved'); this.loadEvents(); }
    });
  }

  markFalsePositive(event: GpsAlertEvent, ev?: Event): void {
    ev?.stopPropagation();
    this.gps.resolveAlert(
      event.id,
      'False positive — tuning feedback (GPS alerts UI)',
      this.tenantScopeFor(event)
    ).subscribe({
      next: () => { this.toast.success('Marked false positive'); this.loadEvents(); }
    });
  }

  archive(event: GpsAlertEvent, ev?: Event): void {
    ev?.stopPropagation();
    this.gps.archiveAlert(event.id, undefined, this.tenantScopeFor(event)).subscribe({
      next: () => { this.toast.success('Alert archived'); this.loadEvents(); }
    });
  }

  openEvent(event: GpsAlertEvent): void {
    this.selectedEvent = event;
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { alertId: event.id, vehicleId: event.vehicleId },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }

  closeDetail(): void {
    this.selectedEvent = null;
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { alertId: null },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }

  jumpToLive(event: GpsAlertEvent, ev: Event): void {
    ev.stopPropagation();
    void this.router.navigate(['/gps-tracking/live'], { queryParams: { vehicleId: event.vehicleId } });
  }

  jumpToHistory(event: GpsAlertEvent, ev: Event): void {
    ev.stopPropagation();
    const ts = new Date(event.timestamp);
    void this.router.navigate(['/gps-tracking/history'], {
      queryParams: {
        vehicleId: event.vehicleId,
        preset: 'custom',
        from: ts.toISOString()
      }
    });
  }

  exportCsv(): void {
    const rows = this.flatEvents();
    if (!rows.length) {
      this.toast.warning('Nothing to export.');
      return;
    }
    downloadTextFile(
      `gps-alerts-${new Date().toISOString().slice(0, 10)}.csv`,
      alertsToCsv(rows),
      'text/csv;charset=utf-8'
    );
  }

  exportPdf(): void {
    window.print();
  }

  statusLabel(event: GpsAlertEvent): string {
    if (!event.readAt && event.status === 'active') return 'Unread';
    if (event.readAt && event.status === 'active') return 'Read';
    return this.eventTypeLabel(event.status ?? 'active');
  }

  statusClass(event: GpsAlertEvent): string {
    const status = (event.status ?? '').toLowerCase();
    if (!event.readAt && status === 'active') return 'badge-blue';
    if (status === 'archived') return 'badge-gray';
    if (status === 'resolved') return 'badge-green';
    if (status === 'acknowledged') return 'badge-amber';
    return 'badge-blue';
  }

  severityClass(severity?: string | null): string {
    switch ((severity ?? 'medium').toLowerCase()) {
      case 'critical': return 'sev-critical';
      case 'high': return 'sev-high';
      case 'medium': return 'sev-medium';
      case 'low': return 'sev-low';
      default: return 'sev-medium';
    }
  }

  severityBadge(severity?: string | null): string {
    switch ((severity ?? 'medium').toLowerCase()) {
      case 'critical': return 'badge-red';
      case 'high': return 'badge-amber';
      case 'low': return 'badge-gray';
      default: return 'badge-blue';
    }
  }

  eventTypeBadge(type: string): string {
    if (type === 'speed_exceeded' || type === 'road_speed_exceeded') return 'badge-red';
    if (type === 'sos') return 'badge-red';
    if (type === 'geofence_enter') return 'badge-amber';
    if (type === 'geofence_exit') return 'badge-blue';
    return 'badge-gray';
  }

  eventTypeLabel(type: string): string {
    return type.replace(/_/g, ' ').replace(/\b\w/g, c => c.toUpperCase());
  }

  inlineMeta(event: GpsAlertEvent): string {
    const parts = [
      event.registrationNumber ? `Plate ${event.registrationNumber}` : null,
      event.driverName ? event.driverName : null,
      this.isSuperAdmin && event.tenantName ? event.tenantName : null,
      `${event.speed ?? 0} km/h`
    ].filter(Boolean);
    return parts.join(' · ');
  }

  lastUpdatedLabel(): string {
    if (!this.lastRefreshedAt) return 'Not loaded yet';
    return this.lastRefreshedAt.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
  }
}
