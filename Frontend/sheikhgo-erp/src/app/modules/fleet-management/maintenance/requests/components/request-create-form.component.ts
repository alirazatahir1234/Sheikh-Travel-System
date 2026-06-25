import { ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, effect, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { VehicleListItem } from '../../../../../core/models/vehicle.model';
import { CreateMaintenanceRequestPayload, ISSUE_CATEGORIES } from '../../../../../core/models/maintenance.model';
import {
  defaultCreateMaintenanceRequestForm,
  DESCRIPTION_MAX_LENGTH,
  DESCRIPTION_MIN_LENGTH,
  REQUEST_PRIORITIES,
  REQUEST_TYPES,
  validateCreateMaintenanceRequest
} from '../utils/request-form.util';

type RequestFormField = 'vehicleId' | 'priority' | 'issueCategory' | 'requestType' | 'description';

@Component({
  selector: 'request-create-form',
  standalone: true,
  imports: [ReactiveFormsModule, MatIconModule, MatTooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="form" [formGroup]="form" (ngSubmit)="onSubmit()" novalidate>
      <h3>New Service Request</h3>

      <label [class.field--invalid]="showError('vehicleId')">
        <span>Vehicle <span class="req" aria-hidden="true">*</span></span>
        <select formControlName="vehicleId" [attr.aria-invalid]="showError('vehicleId')">
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
          formControlName="priority"
          [attr.aria-invalid]="showError('priority')"
          [matTooltip]="showError('priority') ? fieldError('priority') : ''"
          matTooltipPosition="above">
          <option [ngValue]="''">Select priority</option>
          @for (p of priorities; track p) {
            <option [ngValue]="p">{{ p }}</option>
          }
        </select>
        @if (showError('priority')) {
          <span class="field-error">{{ fieldError('priority') }}</span>
        }
      </label>

      <label [class.field--invalid]="showError('issueCategory')">
        <span>Category <span class="req" aria-hidden="true">*</span></span>
        <select
          formControlName="issueCategory"
          [attr.aria-invalid]="showError('issueCategory')"
          [matTooltip]="showError('issueCategory') ? fieldError('issueCategory') : ''"
          matTooltipPosition="above">
          <option [ngValue]="''">Select category</option>
          @for (c of categories; track c) {
            <option [ngValue]="c">{{ c }}</option>
          }
        </select>
        @if (showError('issueCategory')) {
          <span class="field-error">{{ fieldError('issueCategory') }}</span>
        }
      </label>

      <label [class.field--invalid]="showError('requestType')">
        <span>Type <span class="req" aria-hidden="true">*</span></span>
        <select
          formControlName="requestType"
          [attr.aria-invalid]="showError('requestType')"
          [matTooltip]="showError('requestType') ? fieldError('requestType') : ''"
          matTooltipPosition="above">
          <option [ngValue]="''">Select type</option>
          @for (t of requestTypes; track t) {
            <option [ngValue]="t">{{ t }}</option>
          }
        </select>
        @if (showError('requestType')) {
          <span class="field-error">{{ fieldError('requestType') }}</span>
        }
      </label>

      <label class="full" [class.field--invalid]="showError('description')">
        <span>Description <span class="req" aria-hidden="true">*</span></span>
        <textarea
          formControlName="description"
          rows="3"
          [attr.aria-invalid]="showError('description')">
        </textarea>
        <span class="char-hint">{{ descriptionLength }}/{{ descriptionMaxLength }}</span>
        @if (showError('description')) {
          <span class="field-error">{{ fieldError('description') }}</span>
        }
      </label>

      <div class="actions">
        <button type="button" class="btn-cancel" (click)="onCancel()">Cancel</button>
        <button type="submit" class="btn-submit" [disabled]="saving()">
          {{ saving() ? 'Creating…' : 'Create Request' }}
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
    .btn-cancel,
    .btn-submit {
      border-radius: 8px;
      padding: 0.5rem 1rem;
      font-weight: 700;
      cursor: pointer;
      border: 1px solid #e2e8f0;
      font: inherit;
    }
    .btn-cancel { background: #fff; color: #0f172a; }
    .btn-submit {
      background: #0b6b50;
      color: #fff;
      border-color: #0b6b50;
      opacity: 1;
      min-width: 9.5rem;
    }
    .btn-submit:disabled {
      opacity: 0.72;
      cursor: wait;
    }
    @media (max-width: 640px) {
      .form { grid-template-columns: 1fr; padding: 1rem; }
    }
  `]
})
export class RequestCreateFormComponent {
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  private readonly cdr = inject(ChangeDetectorRef);
  private lastResetKey = -1;

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
  readonly errors = signal<Partial<Record<RequestFormField, string>>>({});
  readonly formValid = signal(false);

  readonly form = this.fb.nonNullable.group({
    vehicleId: [0, [Validators.required, Validators.min(1)]],
    priority: ['', Validators.required],
    issueCategory: ['', Validators.required],
    requestType: ['', Validators.required],
    description: [
      '',
      [
        Validators.required,
        Validators.minLength(DESCRIPTION_MIN_LENGTH),
        Validators.maxLength(DESCRIPTION_MAX_LENGTH)
      ]
    ]
  });

  constructor() {
    this.form.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.formValid.set(this.isValid());
      if (this.submitted()) {
        this.syncErrors();
      }
      this.cdr.markForCheck();
    });

    effect(() => {
      const key = this.resetKey();
      if (key === this.lastResetKey) return;
      this.lastResetKey = key;
      this.reset();
    });

    effect(() => {
      this.vehicles();
      this.cdr.markForCheck();
    });

    this.formValid.set(this.isValid());
  }

  get descriptionLength(): number {
    return this.form.controls.description.value.length;
  }

  onSubmit(): void {
    this.submitted.set(true);
    this.form.markAllAsTouched();
    this.syncErrors();
    this.formValid.set(this.isValid());
    if (!this.formValid()) return;

    const payload = this.getPayload();
    this.submit.emit({ ...payload, description: payload.description.trim() });
  }

  onCancel(): void {
    this.reset();
    this.cancel.emit();
  }

  reset(): void {
    const defaults = defaultCreateMaintenanceRequestForm();
    this.form.reset(defaults);
    this.submitted.set(false);
    this.errors.set({});
    this.formValid.set(this.isValid());
  }

  showError(field: RequestFormField): boolean {
    const control = this.form.controls[field];
    return (this.submitted() || control.touched) && !!this.errors()[field];
  }

  fieldError(field: RequestFormField): string {
    return this.errors()[field] ?? '';
  }

  private isValid(): boolean {
    return validateCreateMaintenanceRequest(this.getPayload()).valid;
  }

  private syncErrors(): void {
    const result = validateCreateMaintenanceRequest(this.getPayload());
    this.errors.set(result.errors as Partial<Record<RequestFormField, string>>);
  }

  private getPayload(): CreateMaintenanceRequestPayload {
    const raw = this.form.getRawValue();
    return {
      vehicleId: Number(raw.vehicleId) || 0,
      priority: String(raw.priority ?? '').trim(),
      issueCategory: String(raw.issueCategory ?? '').trim(),
      requestType: String(raw.requestType ?? '').trim(),
      description: String(raw.description ?? '')
    };
  }
}
