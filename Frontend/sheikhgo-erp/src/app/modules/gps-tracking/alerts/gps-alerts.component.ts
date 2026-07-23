import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { GpsAlertEvent, GpsAlertRule, Geofence, GpsAlertStats } from '../../../core/models/gps-tracking.model';

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
  activeTab: 'rules' | 'events' = 'events';
  showRuleForm = false;
  selectedEvent: GpsAlertEvent | null = null;
  statusFilter: string | null = null;
  readStateFilter: string | null = 'unread';
  severityFilter: string | null = null;
  datePreset = 'today';

  ruleForm!: ReturnType<FormBuilder['group']>;
  private refreshTimer: ReturnType<typeof setInterval> | null = null;

  constructor(
    private gps: GpsTrackingService,
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

  loadEvents(): void {
    this.loading = true;
    this.gps.getAlertStats().subscribe({ next: s => { this.stats = s; } });
    this.gps.getAlertEvents(undefined, {
      status: this.statusFilter ?? undefined,
      readState: this.readStateFilter ?? undefined,
      severity: this.severityFilter ?? undefined,
      datePreset: this.datePreset
    }).subscribe({
      next: e => {
        this.events = e;
        if (this.selectedEvent) {
          this.selectedEvent = e.find(item => item.id === this.selectedEvent?.id) ?? null;
        }
        this.loading = false;
      },
      error: () => { this.loading = false; }
    });
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
    this.gps.acknowledgeAlert(event.id).subscribe({
      next: () => { this.toast.success('Alert acknowledged'); this.loadEvents(); }
    });
  }

  markRead(event: GpsAlertEvent): void {
    this.gps.markRead(event.id).subscribe({
      next: () => { this.toast.success('Alert marked as read'); this.loadEvents(); }
    });
  }

  resolve(event: GpsAlertEvent): void {
    this.gps.resolveAlert(event.id).subscribe({
      next: () => { this.toast.success('Alert resolved'); this.loadEvents(); }
    });
  }

  archive(event: GpsAlertEvent): void {
    this.gps.archiveAlert(event.id).subscribe({
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
