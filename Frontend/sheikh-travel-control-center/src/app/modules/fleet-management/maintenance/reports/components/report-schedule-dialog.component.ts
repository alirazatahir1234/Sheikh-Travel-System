import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MaintenanceService } from '../../../../../core/services/maintenance.service';
import { UiToastService } from '../../../../../shared/components/ui/toast/ui-toast.service';
import { MaintenanceReportFilters } from '../../../../../core/models/maintenance.model';
import { apiErrorMessage } from '../../../../../core/utils/api-error.util';

@Component({
  selector: 'report-schedule-dialog',
  standalone: true,
  imports: [FormsModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (open()) {
      <div class="overlay" (click)="onBackdrop($event)">
        <div class="modal" role="dialog">
          <header class="modal-head">
            <h3>Schedule Email Report</h3>
            <button type="button" class="close" (click)="close.emit()">×</button>
          </header>
          <form class="form" (ngSubmit)="submit()">
            <p class="hint">Report: <strong>{{ reportType() }}</strong> — email delivery is queued (SMTP integration pending).</p>
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
            <footer class="footer">
              <button type="button" class="btn-muted" (click)="close.emit()">Cancel</button>
              <button type="submit" class="btn-primary" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Schedule' }}</button>
            </footer>
          </form>
        </div>
      </div>
    }
  `,
  styles: [`
    .overlay { position: fixed; inset: 0; background: rgba(0,0,0,.5); z-index: 1000; display: flex; align-items: center; justify-content: center; padding: 1rem; }
    .modal { background: #fff; border-radius: 12px; width: 100%; max-width: 440px; overflow: hidden; }
    .modal-head { display: flex; justify-content: space-between; align-items: center; padding: 1.25rem 1.5rem 1rem; border-bottom: 1px solid #f1f5f9; }
    .modal-head h3 { margin: 0; font-size: 1rem; font-weight: 800; }
    .close { border: none; background: #f1f5f9; width: 28px; height: 28px; border-radius: 6px; cursor: pointer; }
    .form { padding: 1.25rem 1.5rem 1.5rem; display: grid; gap: 0.875rem; }
    .hint { margin: 0; font-size: 0.8125rem; color: #64748b; }
    label { display: grid; gap: 0.35rem; font-size: 0.8125rem; font-weight: 600; color: #334155; }
    label small { font-weight: 500; color: #94a3b8; }
    select, input { padding: 0.5rem 0.625rem; border: 1px solid #e2e8f0; border-radius: 8px; }
    .footer { display: flex; justify-content: flex-end; gap: 0.5rem; }
    .btn-primary { background: #0B6B50; color: #fff; border: none; border-radius: 8px; padding: 0.5rem 1rem; font-weight: 600; cursor: pointer; }
    .btn-muted { background: #f1f5f9; color: #475569; border: none; border-radius: 8px; padding: 0.5rem 1rem; cursor: pointer; }
    .btn-primary:disabled { opacity: 0.6; }
  `]
})
export class ReportScheduleDialogComponent {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly toast = inject(UiToastService);

  readonly open = input(false);
  readonly reportType = input('cost-analysis');
  readonly filters = input<MaintenanceReportFilters>({});
  readonly close = output<void>();
  readonly saved = output<void>();

  readonly saving = signal(false);
  frequency = 'Weekly';
  recipients = '';

  constructor() {
    effect(() => {
      if (!this.open()) return;
      this.frequency = 'Weekly';
      this.recipients = '';
    }, { allowSignalWrites: true });
  }

  onBackdrop(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('overlay')) this.close.emit();
  }

  submit(): void {
    if (!this.recipients.trim()) return;
    this.saving.set(true);
    this.maintenanceService.createReportSchedule({
      reportType: this.reportType(),
      filters: this.filters(),
      frequency: this.frequency,
      recipients: this.recipients.trim()
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success('Report schedule saved (email delivery pending)');
        this.saved.emit();
        this.close.emit();
      },
      error: err => {
        this.saving.set(false);
        this.toast.error(apiErrorMessage(err, 'Failed to schedule report'));
      }
    });
  }
}
