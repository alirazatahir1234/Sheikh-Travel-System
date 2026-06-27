import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { GpsAlertEvent, GpsAlertRule, Geofence } from '../../../core/models/gps-tracking.model';

@Component({
  selector: 'app-gps-alerts',
  templateUrl: './gps-alerts.component.html',
  styleUrls: ['./gps-alerts.component.scss']
})
export class GpsAlertsComponent implements OnInit, OnDestroy {
  rules: GpsAlertRule[] = [];
  events: GpsAlertEvent[] = [];
  geofences: Geofence[] = [];
  loading = false;
  activeTab: 'rules' | 'events' = 'events';
  showUnackOnly = true;
  showRuleForm = false;

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
    this.gps.getAlertEvents(undefined, this.showUnackOnly).subscribe({
      next: e => { this.events = e; this.loading = false; },
      error: () => { this.loading = false; }
    });
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
