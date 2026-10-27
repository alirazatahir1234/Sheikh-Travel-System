import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { AuthService } from '../../../core/services/auth.service';
import { PlatformService } from '../../../core/services/platform.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { GpsAlertEvent, GpsAlertRule, Geofence, GpsAlertStats } from '../../../core/models/gps-tracking.model';
import { Tenant } from '../../../core/models/platform.model';

@Component({
  standalone: false,
  selector: 'app-gps-alerts',
  templateUrl: './gps-alerts.component.html',
  styleUrls: ['./gps-alerts.component.scss']
})
export class GpsAlertsComponent implements OnInit, OnDestroy {
  rules: GpsAlertRule[] = [];
  events: GpsAlertEvent[] = [];
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
  selectedEvent: GpsAlertEvent | null = null;
  statusFilter: string | null = null;
  readStateFilter: string | null = 'unread';
  severityFilter: string | null = null;
  datePreset = 'today';
  isSuperAdmin = false;
  /** Super Admin: all tenants vs current company (X-Tenant-Id). */
  scopeMode: 'all' | 'company' = 'company';
  companyFilterId: number | null = null;
  companies: Tenant[] = [];
  companiesLoading = false;

  ruleForm!: ReturnType<FormBuilder['group']>;
  private refreshTimer: ReturnType<typeof setInterval> | null = null;

  constructor(
    private gps: GpsTrackingService,
    private auth: AuthService,
    private platform: PlatformService,
    private fb: FormBuilder,
    private toast: UiToastService
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
    if (this.isSuperAdmin) {
      this.scopeMode = 'all';
      this.loadCompanies();
    }
    this.load();
    this.gps.getGeofences().subscribe({ next: g => { this.geofences = g; } });
    this.refreshTimer = setInterval(() => this.loadEvents(), 30_000);
  }

  ngOnDestroy(): void {
    if (this.refreshTimer) clearInterval(this.refreshTimer);
  }

  load(): void {
    this.loading = true;
    this.gps.getAlertRules().subscribe({ next: r => { this.rules = r; } });
    this.loadEvents();
  }

  get showCompanyColumn(): boolean {
    return this.isSuperAdmin && this.scopeMode === 'all';
  }

  setScopeMode(mode: 'all' | 'company'): void {
    if (this.scopeMode === mode) return;
    this.scopeMode = mode;
    if (mode === 'company') this.companyFilterId = null;
    this.loadEvents();
  }

  onCompanyFilterChange(tenantId: number | null): void {
    this.companyFilterId = tenantId;
    this.loadEvents();
  }

  loadEvents(): void {
    this.loading = true;
    this.loadError = null;
    const filterOpts = {
      status: this.statusFilter ?? undefined,
      readState: this.readStateFilter ?? undefined,
      severity: this.severityFilter ?? undefined,
      datePreset: this.datePreset
    };
    const usePlatform = this.isSuperAdmin && this.scopeMode === 'all';
    const stats$ = usePlatform
      ? this.gps.getPlatformAlertStats(this.companyFilterId)
      : this.gps.getAlertStats();
    const events$ = usePlatform
      ? this.gps.getPlatformAlertEvents(this.companyFilterId, filterOpts)
      : this.gps.getAlertEvents(undefined, filterOpts);

    stats$.subscribe({
      next: s => { this.stats = s; },
      error: err => {
        this.toast.error(err?.error?.message ?? 'Failed to load alert stats');
      }
    });
    events$.subscribe({
      next: e => {
        this.events = e;
        if (this.selectedEvent) {
          this.selectedEvent = e.find(item => item.id === this.selectedEvent?.id) ?? null;
        }
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.events = [];
        this.loadError = err?.error?.message || err?.message || 'Failed to load alerts.';
        this.toast.error(this.loadError ?? 'Failed to load alerts.');
      }
    });
  }

  private loadCompanies(): void {
    this.companiesLoading = true;
    this.platform.getTenants().subscribe({
      next: list => {
        this.companies = (list ?? []).filter(t => t.isActive !== false);
        this.companiesLoading = false;
      },
      error: () => {
        this.companiesLoading = false;
      }
    });
  }

  private tenantScopeFor(event: GpsAlertEvent): number | null | undefined {
    return this.showCompanyColumn && event.tenantId ? event.tenantId : undefined;
  }

  toggleLifecycle(value: string): void {
    if (value === 'unread' || value === 'read') {
      this.readStateFilter = this.readStateFilter === value ? null : value;
      this.statusFilter = null;
    } else {
      this.statusFilter = this.statusFilter === value ? null : value;
      this.readStateFilter = null;
    }
    this.loadEvents();
  }

  toggleSeverity(value: string): void {
    this.severityFilter = this.severityFilter === value ? null : value;
    this.loadEvents();
  }

  setDatePreset(value: string): void {
    this.datePreset = value;
    this.loadEvents();
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

  acknowledge(event: GpsAlertEvent): void {
    this.gps.acknowledgeAlert(event.id, this.tenantScopeFor(event)).subscribe({
      next: () => { this.toast.success('Alert acknowledged'); this.loadEvents(); }
    });
  }

  markRead(event: GpsAlertEvent): void {
    this.gps.markRead(event.id, this.tenantScopeFor(event)).subscribe({
      next: () => { this.toast.success('Alert marked as read'); this.loadEvents(); }
    });
  }

  resolve(event: GpsAlertEvent): void {
    this.gps.resolveAlert(event.id, undefined, this.tenantScopeFor(event)).subscribe({
      next: () => { this.toast.success('Alert resolved'); this.loadEvents(); }
    });
  }

  archive(event: GpsAlertEvent): void {
    this.gps.archiveAlert(event.id, undefined, this.tenantScopeFor(event)).subscribe({
      next: () => { this.toast.success('Alert archived'); this.loadEvents(); }
    });
  }

  openEvent(event: GpsAlertEvent): void {
    this.selectedEvent = event;
  }

  closeDetail(): void {
    this.selectedEvent = null;
  }

  isLifecycleSelected(value: string): boolean {
    return (value === 'unread' && this.readStateFilter === 'unread')
      || (value === 'read' && this.readStateFilter === 'read')
      || this.statusFilter === value;
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

  eventTypeBadge(type: string): string {
    if (type === 'speed_exceeded') return 'badge-red';
    if (type === 'geofence_enter') return 'badge-amber';
    if (type === 'geofence_exit') return 'badge-blue';
    return 'badge-gray';
  }

  eventTypeLabel(type: string): string {
    return type.replace(/_/g, ' ').replace(/\b\w/g, c => c.toUpperCase());
  }
}
