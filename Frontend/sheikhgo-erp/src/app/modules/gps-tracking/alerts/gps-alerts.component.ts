import { Component, HostListener, OnInit, OnDestroy } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription, forkJoin, of } from 'rxjs';
import { FormBuilder, Validators } from '@angular/forms';
import { catchError, finalize, skip } from 'rxjs/operators';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { AuthService } from '../../../core/services/auth.service';
import { DriverService } from '../../../core/services/driver.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import {
  GpsAlertEvent,
  GpsAlertRule,
  Geofence,
  GpsAlertStats,
  GpsAlertNotificationSetting
} from '../../../core/models/gps-tracking.model';
import { DriverListItem } from '../../../core/models/driver.model';
import { GpsScopeFilterService } from '../gps-scope-filter.service';
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
  normalizeGpsAlertEventType,
  readMutedEventTypes,
  readQuietHours,
  readSnoozeMap,
  snoozeAlert,
  sortAlertsBySeverity,
  writeMutedEventTypes,
  writeQuietHours
} from './gps-alerts.helpers';
import { formatTimeInZone } from '../../../core/utils/zoned-time.util';
import { readStationary24hAlertPref, storeStationary24hAlertPref } from '../utils/history-fleet-context.util';

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
  /** Per-browser preference (moved here from the GPS History filter bar). */
  alertStationary24h = readStationary24hAlertPref();
  showPrefsPanel = false;
  selectedEvent: GpsAlertEvent | null = null;

  lifecycleTab: GpsAlertLifecycleTab = 'open';
  severityFilter: string | null = null;
  eventTypeFilter: string | null = null;
  datePreset = 'last24';
  driverFilterId: number | null = null;
  groupSimilar = true;

  eventsViewMode: 'list' | 'grid' = 'list';
  pageIndex = 0;
  pageSize = 25;
  readonly pageSizeOptions = [10, 25, 50];
  tableSearch = '';
  private searchQuery = '';

  isSuperAdmin = false;
  platformCrossTenantLimited = false;
  drivers: DriverListItem[] = [];
  private allDisplayGroups: GpsAlertDisplayGroup[] = [];

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
  private scopeSub: Subscription | null = null;

  constructor(
    private gps: GpsTrackingService,
    private auth: AuthService,
    private scopeFilter: GpsScopeFilterService,
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
      this.platformAlertsSub = this.gps.platformAlertsApiMissing$.subscribe(missing => {
        this.platformCrossTenantLimited = missing;
      });
    }

    this.loadFilterOptions();
    this.loadNotificationSettings();

    this.scopeSub = this.scopeFilter.scope$.pipe(skip(1)).subscribe(() => {
      this.resetPagination();
      this.loadEvents();
    });

    this.routeSub = this.route.queryParamMap.subscribe(params => {
      const vehicleId = params.get('vehicleId');
      const alertId = params.get('alertId');
      const tab = params.get('tab');
      if (tab === 'rules') this.activeTab = 'rules';
      else if (tab === 'events') this.activeTab = 'events';
      if (vehicleId) {
        const id = Number(vehicleId);
        if (Number.isFinite(id) && id > 0) this.scopeFilter.setVehicle(id);
      }
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
    this.scopeSub?.unsubscribe();
  }

  get companyFilterId(): number | null {
    return this.scopeFilter.companyFilterId;
  }

  get vehicleFilterId(): number | null {
    return this.scopeFilter.vehicleFilterId;
  }

  /** Super Admin with no company — do not fetch cross-tenant alerts. */
  get needsCompanySelection(): boolean {
    return !this.scopeFilter.isFleetScopeReady;
  }

  /** Company column only when multi-tenant rows could appear (never without company pick). */
  get showCompanyColumn(): boolean {
    return false;
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
    this.driversApi.getAll({ page: 1, pageSize: 500 }).subscribe({
      next: r => { this.drivers = r.items; }
    });
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
    if (!this.scopeFilter.isFleetScopeReady) {
      this.rawEvents = [];
      this.displayGroups = [];
      this.escalatedAlerts = [];
      this.stats = {
        total: 0,
        today: 0,
        unread: 0,
        active: 0,
        critical: 0,
        resolved: 0,
        archived: 0
      };
      this.loading = false;
      this.refreshInFlight = false;
      this.loadError = null;
      return;
    }

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
    list = list.filter(e => {
      const raw = (e.eventType ?? '').toLowerCase();
      const norm = normalizeGpsAlertEventType(e.eventType);
      return !this.mutedTypes.has(raw) && !this.mutedTypes.has(norm);
    });
    return sortAlertsBySeverity(list);
  }

  private rebuildDisplay(): void {
    this.allDisplayGroups = this.groupSimilar
      ? groupSimilarAlerts(this.rawEvents)
      : this.rawEvents.map(e => ({
        key: alertRowKey(e),
        representative: e,
        events: [e],
        count: 1
      }));
    this.applySearchFilter();
  }

  private applySearchFilter(): void {
    const q = this.searchQuery.trim().toLowerCase();
    if (!q) {
      this.displayGroups = this.allDisplayGroups;
    } else {
      this.displayGroups = this.allDisplayGroups.filter(g => {
        const e = g.representative;
        const hay = [
          e.message,
          e.eventType,
          e.severity,
          e.vehicleName,
          e.registrationNumber,
          e.driverName,
          e.status
        ].map(x => (x ?? '').toString().toLowerCase()).join(' ');
        return hay.includes(q);
      });
    }
    this.syncPaginationBounds();
  }

  onTableSearch(): void {
    this.searchQuery = this.tableSearch;
    this.resetPagination();
    this.applySearchFilter();
  }

  onEventTypeSelect(value: string | null): void {
    this.eventTypeFilter = value || null;
    this.resetPagination();
    this.loadEvents();
  }

  onSeveritySelect(value: string | null): void {
    this.severityFilter = value || null;
    this.resetPagination();
    this.loadEvents();
  }

  resetFilters(): void {
    this.lifecycleTab = 'open';
    this.severityFilter = null;
    this.eventTypeFilter = null;
    this.datePreset = 'last24';
    this.driverFilterId = null;
    this.tableSearch = '';
    this.searchQuery = '';
    this.resetPagination();
    this.loadEvents();
  }

  applyFilters(): void {
    this.resetPagination();
    this.loadEvents();
  }

  saveFilterPreset(): void {
    try {
      localStorage.setItem('sheikhgo.gps-alert-filter-preset.v1', JSON.stringify({
        lifecycleTab: this.lifecycleTab,
        severityFilter: this.severityFilter,
        eventTypeFilter: this.eventTypeFilter,
        datePreset: this.datePreset,
        driverFilterId: this.driverFilterId,
        groupSimilar: this.groupSimilar
      }));
      this.toast.success('Filter saved on this device');
    } catch {
      this.toast.error('Could not save filter');
    }
  }

  plateMeta(event: GpsAlertEvent): string {
    const plate = event.registrationNumber ? `Plate ${event.registrationNumber}` : null;
    const speed = `${event.speed ?? 0} km/h`;
    return [plate, speed].filter(Boolean).join(' · ');
  }

  driverPhoneFor(event: GpsAlertEvent): string {
    if (!event.driverId) return '—';
    const match = this.drivers.find(d => d.id === event.driverId);
    return match?.phone?.trim() || '—';
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

  async onAlertStationary24hChange(enabled: boolean): Promise<void> {
    this.alertStationary24h = enabled;
    storeStationary24hAlertPref(enabled);
    if (enabled && typeof Notification !== 'undefined' && Notification.permission === 'default') {
      await Notification.requestPermission();
    }
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
    const t = normalizeGpsAlertEventType(type);
    if (t === 'speed_exceeded' || t === 'road_speed_exceeded') return 'badge-red';
    if (t === 'sos') return 'badge-red';
    if (t === 'geofence_enter') return 'badge-amber';
    if (t === 'geofence_exit') return 'badge-blue';
    if (t === 'online' || t === 'ignition_on') return 'badge-green';
    if (t === 'offline') return 'badge-gray';
    return 'badge-gray';
  }

  eventTypeLabel(type: string): string {
    const t = normalizeGpsAlertEventType(type);
    if (t === 'sos') return 'SOS';
    return t.replace(/_/g, ' ').replace(/\b\w/g, c => c.toUpperCase());
  }

  eventTypeIcon(type: string): string {
    const t = normalizeGpsAlertEventType(type);
    const icons: Record<string, string> = {
      sos: 'sos',
      speed_exceeded: 'speed',
      road_speed_exceeded: 'traffic',
      geofence_enter: 'login',
      geofence_exit: 'logout',
      offline: 'cloud_off',
      online: 'cloud_done',
      ignition_on: 'power',
      ignition_off: 'power_off',
      low_battery: 'battery_alert',
      power_cut: 'electrical_services',
      fuel_station_stop: 'local_gas_station',
      fuel_fraud: 'warning'
    };
    return icons[t] ?? 'notifications';
  }

  eventTypeChipModifier(type: string): string {
    const t = normalizeGpsAlertEventType(type);
    if (t === 'sos') return 'type-chip--sos';
    if (t === 'speed_exceeded' || t === 'road_speed_exceeded') return 'type-chip--speed';
    if (t === 'geofence_enter') return 'type-chip--geofence-in';
    if (t === 'geofence_exit') return 'type-chip--geofence-out';
    if (t === 'online') return 'type-chip--ignition';
    if (t === 'offline') return 'type-chip--connectivity';
    if (t === 'ignition_on' || t === 'ignition_off') return 'type-chip--ignition';
    if (t === 'low_battery' || t === 'power_cut') return 'type-chip--power';
    if (t === 'fuel_station_stop' || t === 'fuel_fraud') return 'type-chip--fuel';
    return 'type-chip--neutral';
  }

  severityIcon(severity: string): string {
    switch (severity) {
      case 'critical': return 'report';
      case 'high': return 'priority_high';
      case 'medium': return 'info';
      case 'low': return 'low_priority';
      default: return 'info';
    }
  }

  severityChipModifier(severity: string): string {
    return `type-chip--sev-${severity}`;
  }

  datePresetIcon(preset: string): string {
    switch (preset) {
      case 'last24': return 'schedule';
      case 'today': return 'today';
      case 'yesterday': return 'history';
      case 'last7': return 'date_range';
      case 'last30': return 'calendar_month';
      default: return 'event';
    }
  }

  datePresetLabel(preset: string): string {
    switch (preset) {
      case 'last24': return 'Last 24h';
      case 'today': return 'Today';
      case 'yesterday': return 'Yesterday';
      case 'last7': return 'Last 7 days';
      case 'last30': return 'Last 30 days';
      default: return preset;
    }
  }

  readonly datePresets = ['last24', 'today', 'yesterday', 'last7', 'last30'] as const;
  readonly severityLevels = ['critical', 'high', 'medium', 'low'] as const;

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
    return formatTimeInZone(this.lastRefreshedAt, { hour: '2-digit', minute: '2-digit', second: '2-digit' });
  }
}
