import { ChangeDetectionStrategy, Component, effect, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { VehicleListItem } from '../../../../../core/models/vehicle.model';
import { CreateMaintenanceRequestPayload, ISSUE_CATEGORIES } from '../../../../../core/models/maintenance.model';
import {
  defaultCreateMaintenanceRequestForm,
  DESCRIPTION_MAX_LENGTH,
  REQUEST_PRIORITIES,
  REQUEST_TYPES,
  validateCreateMaintenanceRequest
} from '../utils/request-form.util';

@Component({
  selector: 'request-create-form',
  standalone: true,
  imports: [FormsModule, MatIconModule, MatTooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="form" (ngSubmit)="onSubmit()" novalidate>
      <h3>New Service Request</h3>

      <label [class.field--invalid]="showError('vehicleId')">
        <span>Vehicle <span class="req" aria-hidden="true">*</span></span>
        <select
          [(ngModel)]="form.vehicleId"
          name="vehicleId"
          [attr.aria-invalid]="showError('vehicleId')">
          <option [ngValue]="0">Select vehicle</option>
          @for (v of vehicles(); track v.id) {
            <option [ngValue]="v.id">{{ v.name }} ({{ v.registrationNumber }})</option>
          }
        </select>
        @if (showError('vehicleId')) {
          <span class="field-error">{{ fieldError('vehicleId') }}</span>
        }
      </label>

      <label [class.field--invalid]="showError('priority')">
        <span class="label-row">
          <span>Priority <span class="req" aria-hidden="true">*</span></span>
          <button
            type="button"
            class="field-hint"
            matTooltip="Priority is required"
            matTooltipPosition="above"
            aria-label="Priority is required">
            <mat-icon>info</mat-icon>
          </button>
        </span>
        <select
          [(ngModel)]="form.priority"
          name="priority"
          [attr.aria-invalid]="showError('priority')"
          [matTooltip]="showError('priority') ? fieldError('priority') : (!form.priority ? 'Priority is required' : '')"
          matTooltipPosition="above">
          <option value="">Select priority</option>
          @for (p of priorities; track p) {
            <option [value]="p">{{ p }}</option>
          }
        </select>
        @if (showError('priority')) {
          <span class="field-error">{{ fieldError('priority') }}</span>
        }
      </label>

      <label [class.field--invalid]="showError('issueCategory')">
        <span>Category <span class="req" aria-hidden="true">*</span></span>
        <select
          [(ngModel)]="form.issueCategory"
          name="category"
          [attr.aria-invalid]="showError('issueCategory')"
          [matTooltip]="showError('issueCategory') ? fieldError('issueCategory') : (!form.issueCategory ? 'Category is required' : '')"
          matTooltipPosition="above">
          <option value="">Select category</option>
          @for (c of categories; track c) {
            <option [value]="c">{{ c }}</option>
          }
        </select>
        @if (showError('issueCategory')) {
          <span class="field-error">{{ fieldError('issueCategory') }}</span>
        }
      </label>

      <label [class.field--invalid]="showError('requestType')">
        <span>Type <span class="req" aria-hidden="true">*</span></span>
        <select
          [(ngModel)]="form.requestType"
          name="type"
          [attr.aria-invalid]="showError('requestType')"
          [matTooltip]="showError('requestType') ? fieldError('requestType') : (!form.requestType ? 'Type is required' : '')"
          matTooltipPosition="above">
          <option value="">Select type</option>
          @for (t of requestTypes; track t) {
            <option [value]="t">{{ t }}</option>
          }
        </select>
        @if (showError('requestType')) {
          <span class="field-error">{{ fieldError('requestType') }}</span>
        }
      </label>

      <label class="full" [class.field--invalid]="showError('description')">
        <span>Description <span class="req" aria-hidden="true">*</span></span>
        <textarea
          [(ngModel)]="form.description"
          name="description"
          rows="3"
          [maxlength]="descriptionMaxLength"
          [attr.aria-invalid]="showError('description')">
        </textarea>
        <span class="char-hint">{{ descriptionLength }}/{{ descriptionMaxLength }}</span>
        @if (showError('description')) {
          <span class="field-error">{{ fieldError('description') }}</span>
        }
      </label>

      <div class="actions">
        <button type="button" (click)="onCancel()">Cancel</button>
        <button type="submit" [disabled]="saving()">
          Create Request
        </button>
      </div>
    </form>
  `,
  styles: [`
    .form {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 0.75rem;
      background: #fff;
      border: 1px solid #e2e8f0;
      border-radius: 14px;
      padding: 1.25rem;
      margin-bottom: 1rem;
    }
    h3 { grid-column: 1 / -1; margin: 0 0 0.25rem; color: #0b6b50; }
    label { display: flex; flex-direction: column; gap: 0.25rem; font-size: 0.8125rem; font-weight: 600; }
    .label-row { display: flex; align-items: center; justify-content: space-between; gap: 0.375rem; }
    .req { color: #dc2626; }
    .field-hint {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 1.25rem;
      height: 1.25rem;
      padding: 0;
      border: none;
      background: transparent;
      color: #94a3b8;
      cursor: help;
    }
    .field-hint mat-icon { font-size: 1rem; width: 1rem; height: 1rem; }
    .full { grid-column: 1 / -1; }
    select, textarea {
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      padding: 0.5rem;
      font: inherit;
      color: #0f172a;
      background: #fff;
    }
    select option[value=""] { color: #94a3b8; }
    select option:not([value=""]) { color: #0f172a; }
    .field--invalid select,
    .field--invalid textarea { border-color: #dc2626; }
    .field-error { color: #dc2626; font-size: 0.75rem; font-weight: 500; }
    .char-hint { align-self: flex-end; font-size: 0.6875rem; font-weight: 500; color: #94a3b8; }
    .actions { grid-column: 1 / -1; display: flex; gap: 0.5rem; justify-content: flex-end; }
    button {
      border-radius: 8px;
      padding: 0.5rem 1rem;
      font-weight: 700;
      cursor: pointer;
      border: 1px solid #e2e8f0;
      background: #fff;
    }
    button[type="submit"] { background: #0b6b50; color: #fff; border-color: #0b6b50; }
    button:disabled { opacity: 0.5; cursor: not-allowed; }
    @media (max-width: 640px) {
      .form { grid-template-columns: 1fr; padding: 1rem; }
    }
  `]
})
export class RequestCreateFormComponent {
  readonly categories = ISSUE_CATEGORIES;
  readonly priorities = REQUEST_PRIORITIES;
  readonly requestTypes = REQUEST_TYPES;
  readonly descriptionMaxLength = DESCRIPTION_MAX_LENGTH;
  readonly vehicles = input<VehicleListItem[]>([]);
  readonly saving = input(false);
  readonly resetKey = input(0);
  readonly submit = output<CreateMaintenanceRequestPayload>();
  readonly cancel = output<void>();

  readonly submitted = signal(false);
  readonly errors = signal<Partial<Record<string, string>>>({});

  form: CreateMaintenanceRequestPayload = defaultCreateMaintenanceRequestForm();

  constructor() {
    effect(() => {
      this.resetKey();
      this.reset();
    });
  }

  get descriptionLength(): number {
    return String(this.form.description ?? '').length;
  }

  onSubmit(): void {
    this.submitted.set(true);
    const result = validateCreateMaintenanceRequest(this.form);
    this.errors.set(result.errors);
    if (!result.valid) return;
    this.submit.emit({ ...this.form, description: this.form.description.trim() });
  }

  onCancel(): void {
    this.reset();
    this.cancel.emit();
  }

  reset(): void {
    this.form = defaultCreateMaintenanceRequestForm();
    this.submitted.set(false);
    this.errors.set({});
  }

  showError(field: string): boolean {
    return this.submitted() && !!this.errors()[field];
  }

  fieldError(field: string): string {
    return this.errors()[field] ?? '';
  }
}
