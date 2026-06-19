import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { inject } from '@angular/core';
import { UiStatusBadgeComponent } from '../../../../../shared/components/ui/status-badge/ui-status-badge.component';
import { UiButtonComponent } from '../../../../../shared/components/ui/button/ui-button.component';
import {
  DRIVER_GENDER_OPTIONS,
  DRIVER_NATIONALITY_OPTIONS
} from '../../models/driver-wizard.model';
import { PHONE_COUNTRY_CODES } from '../../utils/driver-wizard.validators';

@Component({
  selector: 'app-wizard-step-personal',
  standalone: true,
  imports: [ReactiveFormsModule, MatIconModule, UiStatusBadgeComponent, UiButtonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-6">
      <section class="wizard-card">
        <h2 class="wizard-card-title">Driver Profile Photo</h2>
        <p class="wizard-card-desc">
          Upload a clear front-facing photo. Max 5MB, JPG or PNG format.
        </p>
        <div class="photo-section">
          <button type="button" class="photo-upload" (click)="photoInput.click()">
            @if (photoPreview()) {
              <img [src]="photoPreview()" alt="Driver photo" class="photo-img" />
            } @else {
              <mat-icon class="photo-icon">photo_camera</mat-icon>
              <span class="photo-label">Upload Photo</span>
            }
          </button>
          <input #photoInput type="file" accept="image/jpeg,image/png,image/webp" class="hidden" (change)="onPhotoChange($event)" />
          <div class="photo-actions">
            <ui-button size="sm" variant="outline" icon="upload" (clicked)="photoInput.click()">
              {{ photoPreview() ? 'Replace' : 'Upload' }}
            </ui-button>
            @if (photoPreview()) {
              <ui-button size="sm" variant="ghost" icon="delete" (clicked)="photoRemoved.emit()">Remove</ui-button>
            }
          </div>
        </div>
        <div class="driver-code-row">
          <span class="driver-code-label">Driver Code: <strong>{{ driverCode() }}</strong></span>
          <div class="driver-code-actions">
            <ui-status-badge status="valid" label="Auto-generated" />
            <button type="button" class="copy-btn" (click)="copyCode()" title="Copy driver code">
              <mat-icon>content_copy</mat-icon>
            </button>
          </div>
        </div>
      </section>

      <section class="wizard-card">
        <h2 class="wizard-card-title">Detailed Information</h2>
        <div class="form-grid" [formGroup]="form()">
          <label class="field">
            <span>First Name <span class="req">*</span></span>
            <input formControlName="firstName" class="input" placeholder="e.g. Salim" />
            @if (showError('firstName')) { <span class="field-error">First name is required</span> }
          </label>
          <label class="field">
            <span>Last Name <span class="req">*</span></span>
            <input formControlName="lastName" class="input" placeholder="e.g. Al-Mansoor" />
            @if (showError('lastName')) { <span class="field-error">Last name is required</span> }
          </label>
          <label class="field full">
            <span>Full Name (As per Passport/ID)</span>
            <input class="input input-readonly" [value]="fullName()" readonly tabindex="-1" />
          </label>
          <label class="field">
            <span>Date of Birth <span class="req">*</span></span>
            <input
              formControlName="dateOfBirth"
              type="date"
              class="input"
              [attr.max]="maxDateOfBirth()"
              [attr.min]="minDateOfBirth()" />
            @if (showError('dateOfBirth', 'futureDate')) {
              <span class="field-error">Date of birth cannot be in the future</span>
            } @else if (showError('dateOfBirth', 'minAge')) {
              <span class="field-error">Driver must be at least 18 years old</span>
            } @else if (showError('dateOfBirth')) {
              <span class="field-error">Date of birth is required</span>
            }
          </label>
          <label class="field">
            <span>Gender <span class="req">*</span></span>
            <select formControlName="gender" class="input">
              @for (g of genderOptions; track g) {
                <option [value]="g">{{ g }}</option>
              }
            </select>
          </label>
          <label class="field">
            <span>Nationality <span class="req">*</span></span>
            <select formControlName="nationality" class="input">
              @for (n of nationalityOptions; track n) {
                <option [value]="n">{{ n }}</option>
              }
            </select>
          </label>
          <label class="field">
            <span>Mobile Number <span class="req">*</span></span>
            <div class="phone-row">
              <select formControlName="phoneCountryCode" class="input phone-code-select">
                @for (c of phoneCountryCodes; track c.code) {
                  <option [value]="c.code">{{ c.code }}</option>
                }
              </select>
              <input formControlName="phoneLocal" class="input phone-local" placeholder="50 123 4567" inputmode="numeric" />
            </div>
            @if (showError('phoneLocal', 'duplicate')) {
              <span class="field-error">This mobile number is already registered</span>
            } @else if (showError('phoneLocal', 'phoneFormat')) {
              <span class="field-error">Invalid number format for selected country</span>
            } @else if (showError('phoneLocal', 'phoneLength')) {
              <span class="field-error">Invalid mobile number length</span>
            } @else if (showError('phoneLocal')) {
              <span class="field-error">Mobile number is required</span>
            }
          </label>
          <label class="field full">
            <span>Email Address <span class="req">*</span></span>
            <input formControlName="email" type="email" class="input" placeholder="salim.mansoor@example.com" />
            @if (showError('email', 'duplicate')) {
              <span class="field-error">This email is already registered</span>
            } @else if (showError('email', 'email')) {
              <span class="field-error">Enter a valid email address</span>
            } @else if (showError('email')) {
              <span class="field-error">Email is required</span>
            }
          </label>
          <label class="field full">
            <span>Residential Address</span>
            <textarea formControlName="address" class="input textarea" rows="3" placeholder="Suite 402, Business Bay, Dubai, UAE"></textarea>
          </label>
          <label class="field">
            <span>Emergency Contact Name</span>
            <input formControlName="emergencyContactName" class="input" placeholder="e.g. Layla Mansoor" />
          </label>
          <label class="field">
            <span>Emergency Contact Phone</span>
            <input formControlName="emergencyContactPhone" class="input" placeholder="e.g. +971 50 987 6543" inputmode="tel" />
          </label>
        </div>
      </section>
    </div>
  `,
  styleUrls: ['./wizard-step-shared.scss']
})
export class WizardStepPersonalComponent {
  private readonly snackBar = inject(MatSnackBar);

  readonly form = input.required<FormGroup>();
  readonly photoPreview = input<string | undefined>();
  readonly driverCode = input('ST-DRV-2024-000');
  readonly fullName = input('—');
  readonly maxDateOfBirth = input('');
  readonly minDateOfBirth = input('');
  readonly photoSelected = output<File | null>();
  readonly photoRemoved = output<void>();

  readonly genderOptions = DRIVER_GENDER_OPTIONS;
  readonly nationalityOptions = DRIVER_NATIONALITY_OPTIONS;
  readonly phoneCountryCodes = PHONE_COUNTRY_CODES;

  onPhotoChange(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    this.photoSelected.emit(file);
    (event.target as HTMLInputElement).value = '';
  }

  showError(controlName: string, errorKey?: string): boolean {
    const c = this.form().get(controlName);
    if (!c || !c.touched && !c.dirty) return false;
    if (errorKey) return !!c.hasError(errorKey);
    return c.invalid;
  }

  copyCode(): void {
    void navigator.clipboard.writeText(this.driverCode()).then(() => {
      this.snackBar.open('Driver code copied', 'Close', { duration: 2000 });
    });
  }
}
