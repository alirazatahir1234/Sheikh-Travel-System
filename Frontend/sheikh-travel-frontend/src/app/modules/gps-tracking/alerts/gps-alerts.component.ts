import { Component, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MatSnackBar } from '@angular/material/snack-bar';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { GpsAlertEvent, GpsAlertRule } from '../../../core/models/gps-tracking.model';

@Component({
  selector: 'app-gps-alerts',
  templateUrl: './gps-alerts.component.html',
  styleUrls: ['./gps-alerts.component.scss']
})
export class GpsAlertsComponent implements OnInit {
  rules: GpsAlertRule[] = [];
  events: GpsAlertEvent[] = [];
  loading = false;
  showUnackOnly = true;

  ruleForm!: ReturnType<FormBuilder['group']>;

  constructor(
    private gps: GpsTrackingService,
    private fb: FormBuilder,
    private snack: MatSnackBar
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
  }

  load(): void {
    this.loading = true;
    this.gps.getAlertRules().subscribe({ next: r => { this.rules = r; } });
    this.gps.getAlertEvents(undefined, this.showUnackOnly).subscribe({
      next: e => {
        this.events = e;
        this.loading = false;
      },
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
      next: () => {
        this.snack.open('Alert rule created', 'OK', { duration: 2500 });
        this.load();
      }
    });
  }

  acknowledge(event: GpsAlertEvent): void {
    this.gps.acknowledgeAlert(event.id).subscribe({ next: () => this.load() });
  }
}
