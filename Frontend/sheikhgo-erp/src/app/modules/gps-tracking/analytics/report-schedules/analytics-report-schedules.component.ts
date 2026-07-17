import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { GpsTrackingService } from '../../../../core/services/gps-tracking.service';
import { AnalyticsReportSchedule } from '../../../../core/models/gps-tracking.model';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';

const REPORT_TYPES: { value: string; label: string }[] = [
  { value: 'fleet-summary', label: 'Fleet Summary' },
  { value: 'driver-performance', label: 'Driver Performance' },
  { value: 'cost', label: 'Cost' }
];

@Component({
  selector: 'analytics-report-schedules',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule],
  template: `
    <div class="metric-card-header">
      <h3 class="chart-title">Scheduled Reports</h3>
      <button type="button" class="btn btn-secondary" (click)="formOpen = !formOpen">
        <mat-icon>{{ formOpen ? 'close' : 'add' }}</mat-icon> {{ formOpen ? 'Cancel' : 'New Schedule' }}
      </button>
    </div>
    <p class="muted-note" style="text-align: left; padding: 0 0 8px;">
      Reports are queued on schedule — email delivery is stubbed pending SMTP integration.
    </p>

    <form *ngIf="formOpen" class="schedule-form" (ngSubmit)="submit()">
      <label>Report Type
        <select [(ngModel)]="reportType" name="reportType" required>
          <option *ngFor="let t of reportTypes" [value]="t.value">{{ t.label }}</option>
        </select>
      </label>
      <label>Frequency
        <select [(ngModel)]="frequency" name="frequency" required>
          <option value="Daily">Daily</option>
          <option value="Weekly">Weekly</option>
          <option value="Monthly">Monthly</option>
        </select>
      </label>
      <label>Recipients <small>(comma-separated emails)</small>
        <input [(ngModel)]="recipients" name="recipients" required placeholder="ops@company.com, fleet@company.com" />
      </label>
      <button type="submit" class="btn btn-primary" [disabled]="saving">{{ saving ? 'Saving…' : 'Schedule' }}</button>
    </form>

    <table class="schedule-table" *ngIf="schedules.length">
      <thead>
        <tr>
          <th>Report</th><th>Frequency</th><th>Recipients</th><th>Next Run</th><th>Status</th><th></th>
        </tr>
      </thead>
      <tbody>
        <tr *ngFor="let s of schedules">
          <td>{{ typeLabel(s.reportType) }}</td>
          <td>{{ s.frequency }}</td>
          <td>{{ s.recipients }}</td>
          <td>{{ s.nextRunAt ? (s.nextRunAt | date: 'medium') : '—' }}</td>
          <td>
            <span class="badge" [ngClass]="s.isActive ? 'badge-green' : 'badge-gray'">{{ s.isActive ? 'Active' : 'Paused' }}</span>
          </td>
          <td class="actions">
            <button type="button" class="btn btn-secondary" (click)="toggleActive(s)">{{ s.isActive ? 'Pause' : 'Resume' }}</button>
            <button type="button" class="btn btn-secondary" (click)="remove(s)">Delete</button>
          </td>
        </tr>
      </tbody>
    </table>

    <p *ngIf="!loading && !schedules.length" class="muted-note">No scheduled reports yet.</p>
  `,
  styles: [`
    .metric-card-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 8px; }
    .chart-title { font-size: 14px; font-weight: 600; color: #1a202c; margin: 0; }
    .muted-note { font-size: 12px; color: #9ca3af; }
    .schedule-form {
      display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 12px;
      align-items: end; background: #f9fafb; border-radius: 8px; padding: 14px; margin-bottom: 16px;
    }
    .schedule-form label { display: grid; gap: 4px; font-size: 12px; font-weight: 600; color: #374151; }
    .schedule-form label small { font-weight: 500; color: #9ca3af; }
    .schedule-form select, .schedule-form input {
      padding: 8px 10px; border: 1px solid #d1d5db; border-radius: 6px; font-size: 13px;
    }
    .schedule-table { width: 100%; border-collapse: collapse; font-size: 13px; }
    .schedule-table th { text-align: left; padding: 8px; color: #6b7280; font-weight: 600; border-bottom: 1px solid #e5e7eb; }
    .schedule-table td { padding: 8px; border-bottom: 1px solid #f3f4f6; color: #374151; }
    .actions { display: flex; gap: 6px; }
    .badge { display: inline-block; padding: 2px 8px; border-radius: 12px; font-size: 11px; font-weight: 600; }
    .badge-green { background: #d1fae5; color: #059669; }
    .badge-gray { background: #f3f4f6; color: #6b7280; }
    .btn {
      display: inline-flex; align-items: center; gap: 6px; padding: 6px 12px;
      border-radius: 6px; font-size: 12px; font-weight: 500; cursor: pointer; border: none;
    }
    .btn-primary { background: #0f766e; color: #fff; }
    .btn-primary:hover { background: #0d6460; }
    .btn-secondary { background: #f3f4f6; color: #374151; border: 1px solid #d1d5db; }
    .btn-secondary:hover { background: #e5e7eb; }
    .btn:disabled { opacity: .5; cursor: not-allowed; }
  `]
})
export class AnalyticsReportSchedulesComponent implements OnInit {
  private readonly gps = inject(GpsTrackingService);
  private readonly toast = inject(UiToastService);

  readonly reportTypes = REPORT_TYPES;

  schedules: AnalyticsReportSchedule[] = [];
  loading = false;
  formOpen = false;
  saving = false;

  reportType = REPORT_TYPES[0].value;
  frequency = 'Weekly';
  recipients = '';

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.gps.getAnalyticsReportSchedules().subscribe({
      next: rows => { this.schedules = rows; this.loading = false; },
      error: () => { this.schedules = []; this.loading = false; }
    });
  }

  typeLabel(value: string): string {
    return REPORT_TYPES.find(t => t.value === value)?.label ?? value;
  }

  submit(): void {
    if (!this.recipients.trim()) return;
    this.saving = true;
    this.gps.createAnalyticsReportSchedule({
      reportType: this.reportType,
      filters: {},
      frequency: this.frequency,
      recipients: this.recipients.trim()
    }).subscribe({
      next: () => {
        this.saving = false;
        this.toast.success('Report schedule saved (email delivery pending)');
        this.recipients = '';
        this.formOpen = false;
        this.load();
      },
      error: err => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Failed to schedule report'));
      }
    });
  }

  toggleActive(schedule: AnalyticsReportSchedule): void {
    this.gps.updateAnalyticsReportSchedule(schedule.id, { isActive: !schedule.isActive }).subscribe({
      next: () => this.load(),
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to update schedule'))
    });
  }

  remove(schedule: AnalyticsReportSchedule): void {
    this.gps.deleteAnalyticsReportSchedule(schedule.id).subscribe({
      next: () => {
        this.toast.success('Report schedule deleted');
        this.load();
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to delete schedule'))
    });
  }
}
