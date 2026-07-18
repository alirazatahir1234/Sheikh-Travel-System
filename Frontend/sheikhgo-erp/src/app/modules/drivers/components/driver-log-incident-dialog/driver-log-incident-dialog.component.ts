import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  model,
  output,
  signal
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { DriverListItem } from '../../../../core/models/driver.model';
import { DriverService } from '../../../../core/services/driver.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { UiButtonComponent } from '../../../../shared/components/ui/button/ui-button.component';
import { UiModalComponent } from '../../../../shared/components/ui/modal/ui-modal.component';
import { UiSelectComponent } from '../../../../shared/components/ui/select/ui-select.component';
import { UiSelectOption } from '../../../../shared/components/ui/types/ui.types';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';

export interface LogIncidentResult {
  driverId: number;
  driverName: string;
}

const INCIDENT_TYPE_OPTIONS: UiSelectOption[] = [
  { value: 'Overspeed', label: 'Overspeed' },
  { value: 'Accident', label: 'Accident' },
  { value: 'Complaint', label: 'Complaint' },
  { value: 'Policy', label: 'Policy Breach' }
];

const SEVERITY_OPTIONS: UiSelectOption[] = [
  { value: 'Low', label: 'Low' },
  { value: 'Medium', label: 'Medium' },
  { value: 'High', label: 'High' }
];

@Component({
  selector: 'driver-log-incident-dialog',
  standalone: true,
  imports: [FormsModule, UiModalComponent, UiButtonComponent, UiSelectComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-modal [(open)]="open" title="Log Incident" size="md" (closed)="onClosed()">
      <p class="hint">Record a driver-related incident. It will appear in the driver's incident history.</p>

      <div class="form-stack">
        <ui-select
          class="block"
          label="Driver"
          [options]="driverOptions()"
          [(ngModel)]="selectedDriverId"
          [required]="true"
          [searchable]="true"
          searchPlaceholder="Search drivers…" />

        <ui-select
          class="block"
          label="Incident Type"
          [options]="incidentTypeOptions"
          [(ngModel)]="incidentType"
          [required]="true" />

        <ui-select
          class="block"
          label="Severity"
          [options]="severityOptions"
          [(ngModel)]="severity"
          [required]="true" />

        <label class="field">
          <span class="field-label">Description <span class="req">*</span></span>
          <textarea
            class="textarea"
            rows="3"
            maxlength="1000"
            placeholder="Describe what happened…"
            [(ngModel)]="description"></textarea>
        </label>
      </div>

      <div modal-footer class="flex justify-end gap-2">
        <ui-button variant="ghost" [disabled]="submitting()" (clicked)="open.set(false)">Cancel</ui-button>
        <ui-button
          variant="primary"
          icon="report_problem"
          [loading]="submitting()"
          [disabled]="!canSubmit() || submitting()"
          (clicked)="submit()">
          Log Incident
        </ui-button>
      </div>
    </ui-modal>
  `,
  styles: [`
    .hint {
      font-size: 0.8125rem;
      color: #64748b;
      margin: 0 0 1rem;
      line-height: 1.45;
    }

    .form-stack {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    .field {
      display: flex;
      flex-direction: column;
      gap: 0.375rem;
    }

    .field-label {
      font-size: 0.8125rem;
      font-weight: 600;
      color: #0f172a;
    }

    .req {
      color: #dc2626;
    }

    .textarea {
      width: 100%;
      box-sizing: border-box;
      border: 1px solid #e2e8f0;
      border-radius: 0.375rem;
      padding: 0.625rem 0.75rem;
      font-size: 0.875rem;
      color: #0f172a;
      resize: vertical;
      min-height: 5rem;
      font-family: inherit;
    }

    .textarea:focus {
      outline: 2px solid rgba(0, 107, 84, 0.35);
      outline-offset: 1px;
      border-color: #006b54;
    }
  `]
})
export class DriverLogIncidentDialogComponent {
  private readonly driverService = inject(DriverService);
  private readonly toast = inject(UiToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly open = model(false);
  readonly logged = output<LogIncidentResult>();

  readonly incidentTypeOptions = INCIDENT_TYPE_OPTIONS;
  readonly severityOptions = SEVERITY_OPTIONS;

  readonly driverOptions = signal<UiSelectOption[]>([]);
  readonly submitting = signal(false);

  private driverNameById = new Map<number, string>();

  selectedDriverId: string | null = null;
  incidentType = 'Overspeed';
  severity = 'Medium';
  description = '';

  show(preselectDriverId?: number | null): void {
    this.resetForm(preselectDriverId);
    this.driverService
      .getAll({ page: 1, pageSize: 500 })
      .pipe(
        catchError(() => of({ items: [] as DriverListItem[] })),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(result => {
        const items = result.items ?? [];
        this.driverNameById = new Map(items.map(d => [d.id, d.fullName]));
        this.driverOptions.set(
          items.map(d => ({
            value: String(d.id),
            label: `${d.fullName}${d.driverCode ? ` (${d.driverCode})` : d.phone ? ` (${d.phone})` : ''}`
          }))
        );
        if (preselectDriverId && items.some(d => d.id === preselectDriverId)) {
          this.selectedDriverId = String(preselectDriverId);
        } else if (!this.selectedDriverId && items.length === 1) {
          this.selectedDriverId = String(items[0].id);
        }
      });
    this.open.set(true);
  }

  canSubmit(): boolean {
    return !!this.selectedDriverId && this.description.trim().length > 0;
  }

  submit(): void {
    const driverId = Number(this.selectedDriverId);
    const description = this.description.trim();
    if (!Number.isFinite(driverId) || driverId <= 0 || !description) {
      this.toast.warning('Select a driver and enter a description');
      return;
    }

    this.submitting.set(true);
    this.driverService
      .createViolation(driverId, {
        violationType: this.incidentType,
        severity: this.severity,
        occurredAt: new Date().toISOString(),
        description
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.submitting.set(false);
          const driverName = this.driverNameById.get(driverId) ?? 'Driver';
          this.toast.success('Incident logged successfully');
          this.logged.emit({ driverId, driverName });
          this.open.set(false);
        },
        error: err => {
          this.submitting.set(false);
          this.toast.error(apiErrorMessage(err, 'Failed to log incident'));
        }
      });
  }

  onClosed(): void {
    this.submitting.set(false);
  }

  private resetForm(preselectDriverId?: number | null): void {
    this.selectedDriverId = preselectDriverId ? String(preselectDriverId) : null;
    this.incidentType = 'Overspeed';
    this.severity = 'Medium';
    this.description = '';
    this.submitting.set(false);
  }
}
