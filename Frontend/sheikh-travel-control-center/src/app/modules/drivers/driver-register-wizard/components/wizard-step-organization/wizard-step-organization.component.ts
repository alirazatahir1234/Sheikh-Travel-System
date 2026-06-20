import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { UiSelectOption } from '../../../../../shared/components/ui/types/ui.types';

@Component({
  selector: 'app-wizard-step-organization',
  standalone: true,
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-6">
      <section class="wizard-card">
        <h2 class="wizard-card-title">Organization Assignment</h2>
        <p class="wizard-card-desc">Assign the driver to a branch and department within your fleet network.</p>
        <div class="form-grid" [formGroup]="form()">
          <label class="field">
            <span>Branch</span>
            <select formControlName="branchId" class="input">
              <option value="">Select branch…</option>
              @for (b of branchOptions(); track b.value) {
                <option [value]="b.value">{{ b.label }}</option>
              }
            </select>
          </label>
          <label class="field">
            <span>Department</span>
            <select formControlName="departmentId" class="input">
              <option value="">Select department…</option>
              @for (d of departmentOptions(); track d.value) {
                <option [value]="d.value">{{ d.label }}</option>
              }
            </select>
          </label>
        </div>
      </section>

      <section class="wizard-card">
        <h2 class="wizard-card-title">Registration Summary</h2>
        <dl class="review-grid">
          <div class="review-row">
            <dt>Driver Name</dt>
            <dd>{{ driverName() }}</dd>
          </div>
          <div class="review-row">
            <dt>Mobile</dt>
            <dd>{{ phone() }}</dd>
          </div>
          <div class="review-row">
            <dt>License Number</dt>
            <dd>{{ licenseNumber() || '—' }}</dd>
          </div>
          <div class="review-row">
            <dt>License Expiry</dt>
            <dd>{{ licenseExpiry() || '—' }}</dd>
          </div>
          <div class="review-row">
            <dt>Branch</dt>
            <dd>{{ branchLabel() }}</dd>
          </div>
          <div class="review-row">
            <dt>Documents</dt>
            <dd>{{ documentsCount() }} / 3 uploaded</dd>
          </div>
        </dl>
        @if (validationErrors().length) {
          <ul class="mt-4 space-y-1 text-sm text-red-600">
            @for (e of validationErrors(); track e) {
              <li>{{ e }}</li>
            }
          </ul>
        }
      </section>
    </div>
  `,
  styleUrls: ['../wizard-step-personal/wizard-step-shared.scss']
})
export class WizardStepOrganizationComponent {
  readonly form = input.required<FormGroup>();
  readonly branchOptions = input<UiSelectOption[]>([]);
  readonly departmentOptions = input<UiSelectOption[]>([]);
  readonly driverName = input('');
  readonly phone = input('');
  readonly licenseNumber = input('');
  readonly licenseExpiry = input('');
  readonly branchLabel = input('—');
  readonly documentsCount = input(0);
  readonly validationErrors = input<string[]>([]);
}
