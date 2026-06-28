import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { UiToastService } from '../../../../../shared/components/ui/toast/ui-toast.service';
import { inject } from '@angular/core';
import { UiStatusBadgeComponent } from '../../../../../shared/components/ui/status-badge/ui-status-badge.component';
import { UiButtonComponent } from '../../../../../shared/components/ui/button/ui-button.component';
import {
  DRIVER_GENDER_OPTIONS,
  DRIVER_NATIONALITY_OPTIONS
} from '../../models/driver-wizard.model';
import { PHONE_COUNTRY_CODES } from '../../utils/driver-wizard.validators';
import { PhoneDigitsOnlyDirective } from '../../../../../shared/directives/phone-digits-only.directive';
import { UPLOAD_MAX_SIZE_LABEL, vehicleUploadSizeError } from '../../../../../core/utils/upload-url.util';

@Component({
  selector: 'app-wizard-step-personal',
  standalone: true,
  imports: [ReactiveFormsModule, MatIconModule, UiStatusBadgeComponent, UiButtonComponent, PhoneDigitsOnlyDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-6">
      <section class="wizard-card profile-header-card">
        <div class="profile-header-row">
          <div class="photo-block">
            <h2 class="wizard-card-title">Driver Photo</h2>
            <p class="wizard-card-desc">Passport-style JPG or PNG. Max 2 MB.</p>
            <div class="photo-row">
              <button type="button" class="photo-upload" [class.photo-upload--error]="!!sizeError()" (click)="photoInput.click()">
                @if (showPhoto()) {
                  <img [src]="photoPreview()" alt="Driver photo" class="photo-img" (error)="onPhotoError()" />
                } @else {
                  <span class="photo-initials">{{ photoInitials() }}</span>
                }
              </button>
              <input #photoInput type="file" accept="image/jpeg,image/png,image/webp" class="hidden" (change)="onPhotoChange($event)" />
              <div class="photo-actions">
                <ui-button size="sm" variant="outline" icon="upload" (clicked)="photoInput.click()">
                  {{ showPhoto() ? 'Replace' : 'Upload' }}
                </ui-button>
                @if (showPhoto()) {
                  <ui-button size="sm" variant="ghost" icon="delete" (clicked)="clearPhotoError(); photoRemoved.emit(); photoLoadFailed.set(false)">Remove</ui-button>
                }
                <ui-button size="sm" variant="ghost" icon="crop" (clicked)="onCropHint()">Crop</ui-button>
              </div>
            </div>
            @if (sizeError()) {
              <p class="upload-validation-error">
                <mat-icon>error_outline</mat-icon>
                <span>{{ sizeError() }}</span>
              </p>
            }
          </div>

          <div class="driver-summary">
            <h3 class="summary-title">{{ fullName() }}</h3>
            <dl class="summary-grid">
              <div class="summary-item">
                <dt>Driver Code</dt>
                <dd class="summary-code">
                  {{ driverCode() }}
                  <button type="button" class="copy-btn" (click)="copyCode()" title="Copy Driver Code" aria-label="Copy Driver Code">
                    <mat-icon>content_copy</mat-icon>
                  </button>
                </dd>
              </div>
              <div class="summary-item">
                <dt>Current Status</dt>
                <dd><ui-status-badge [status]="statusBadgeVariant()" [label]="driverStatus()" /></dd>
              </div>
              <div class="summary-item">
                <dt>Branch</dt>
                <dd [class.summary-warning]="branchSummary().warning">
                  @if (branchSummary().warning) { <mat-icon class="warn-icon">warning</mat-icon> }
                  {{ branchSummary().text }}
                </dd>
              </div>
              <div class="summary-item">
                <dt>Department</dt>
                <dd [class.summary-warning]="departmentSummary().warning">
                  @if (departmentSummary().warning) { <mat-icon class="warn-icon">warning</mat-icon> }
                  {{ departmentSummary().text }}
                </dd>
              </div>
              <div class="summary-item">
                <dt>Assigned Vehicle</dt>
                <dd>
                  @if (vehicleUnassigned()) {
                    <ui-status-badge status="pending" label="Unassigned" />
                  } @else {
                    {{ vehicleLabel() }}
                  }
                </dd>
              </div>
              <div class="summary-item">
                <dt>Hire Date</dt>
                <dd [class.summary-muted]="hireDatePending()">{{ hireDateLabel() }}</dd>
              </div>
            </dl>
            @if (statusTimeline().length) {
              <div class="status-timeline">
                <p class="timeline-title">Status Timeline</p>
                <ul class="timeline-list">
                  @for (event of statusTimeline(); track event.label + event.date) {
                    <li class="timeline-item">
                      <span class="timeline-dot"></span>
                      <div>
                        <p class="timeline-label">{{ event.label }}</p>
                        <p class="timeline-date">{{ event.date }}</p>
                      </div>
                    </li>
                  }
                </ul>
              </div>
            }
            @if (isEditMode() && updatedAtLabel()) {
              <div class="updated-meta">
                <mat-icon>history</mat-icon>
                <span>Updated: {{ updatedAtLabel() }}</span>
              </div>
            }
          </div>
        </div>
      </section>

      <section class="wizard-card">
        <h2 class="wizard-card-title">Detailed Information</h2>

        <div [formGroup]="form()">

          <!-- Basic Information -->
          <p class="form-group-label">Basic Information</p>
          <div class="form-grid">
            <label class="field">
              <span>First Name <span class="req">*</span></span>
              <input formControlName="firstName" class="input" [class.input--error]="showError('firstName')" placeholder="e.g. Salim" />
              @if (showError('firstName')) { <span class="field-error">First name is required</span> }
            </label>
            <label class="field">
              <span>Last Name <span class="req">*</span></span>
              <input formControlName="lastName" class="input" [class.input--error]="showError('lastName')" placeholder="e.g. Al-Mansoor" />
              @if (showError('lastName')) { <span class="field-error">Last name is required</span> }
            </label>
            <label class="field full">
              <span>Full Name (Auto-generated)</span>
              <div class="readonly-name">
                <mat-icon class="readonly-lock">lock</mat-icon>
                <span>{{ fullName() }}</span>
              </div>
            </label>
            <label class="field">
              <span>Date of Birth <span class="req">*</span></span>
              @for (key of [formLoadKey()]; track key) {
                <input
                  formControlName="dateOfBirth"
                  type="date"
                  class="input date-input"
                  [class.input--error]="showError('dateOfBirth')"
                  [attr.max]="maxDateOfBirth()"
                  [attr.min]="minDateOfBirth()" />
              }
              @if (showError('dateOfBirth', 'futureDate')) {
                <span class="field-error">Date of birth cannot be in the future</span>
              } @else if (showError('dateOfBirth', 'minAge')) {
                <span class="field-error">Driver must be at least 18 years old</span>
              } @else if (showError('dateOfBirth')) {
                <span class="field-error">Date of birth is required</span>
              } @else {
                <span class="field-hint">Minimum age: 18 years</span>
              }
            </label>
            <label class="field">
              <span>Gender <span class="req">*</span></span>
              <select formControlName="gender" class="input">
                @if (!form().get('gender')?.value) {
                  <option value="" disabled hidden>Select gender</option>
                }
                @for (g of genderOptions; track g) {
                  <option [value]="g">{{ g }}</option>
                }
              </select>
            </label>
            <label class="field">
              <span>Nationality <span class="req">*</span></span>
              <select formControlName="nationality" class="input" [class.input--error]="showError('nationality')">
                @for (n of nationalityOptions; track n) {
                  <option [value]="n">{{ n }}</option>
                }
              </select>
              @if (showError('nationality')) {
                <span class="field-error">Nationality is required</span>
              }
            </label>
          </div>

          <!-- Contact Information -->
          <p class="form-group-label">Contact Information</p>
          <div class="form-grid">
            <label class="field">
              <span>Mobile Number <span class="req">*</span></span>
              <div class="phone-row" [class.phone-row--error]="showError('phoneLocal')">
                <select formControlName="phoneCountryCode" class="input phone-code-select" [class.input--error]="showError('phoneLocal')">
                  @for (c of phoneCountryCodes; track c.code) {
                    <option [value]="c.code">{{ c.flag }} {{ c.code }}</option>
                  }
                </select>
                <input
                  formControlName="phoneLocal"
                  type="tel"
                  class="input phone-local"
                  [class.input--error]="showError('phoneLocal')"
                  placeholder="501234567"
                  inputmode="numeric" />
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
            <label class="field">
              <span>Email Address <span class="req">*</span></span>
              <input formControlName="email" type="email" class="input" [class.input--error]="showError('email')" placeholder="salim.mansoor@example.com" />
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
          </div>

          <!-- Emergency Contact -->
          <p class="form-group-label">Emergency Contact</p>
          <div class="form-grid">
            <label class="field">
              <span>Contact Name <span class="req">*</span></span>
              <input formControlName="emergencyContactName" class="input" [class.input--error]="showError('emergencyContactName')" placeholder="e.g. Layla Mansoor" />
              @if (showError('emergencyContactName')) {
                <span class="field-error">Emergency contact name is required</span>
              }
            </label>
            <label class="field">
              <span>Contact Phone <span class="req">*</span></span>
              <input
                formControlName="emergencyContactPhone"
                type="tel"
                class="input"
                [class.input--error]="showError('emergencyContactPhone')"
                placeholder="e.g. 971509876543"
                inputmode="numeric" />
              @if (showError('emergencyContactPhone', 'phoneLength')) {
                <span class="field-error">Enter a valid phone number (7–15 digits)</span>
              } @else if (showError('emergencyContactPhone')) {
                <span class="field-error">Emergency contact phone is required</span>
              }
            </label>
          </div>

        </div>
      </section>
    </div>
  `,
  styleUrls: ['./wizard-step-shared.scss']
})
export class WizardStepPersonalComponent {
  private readonly toast = inject(UiToastService);

  readonly form = input.required<FormGroup>();
  readonly formLoadKey = input(0);
  readonly photoPreview = input<string | undefined>();
  readonly photoInitials = input('?');
  readonly driverCode = input('ST-DRV-2024-000');
  readonly fullName = input('—');
  readonly isEditMode = input(false);
  readonly driverStatus = input('Draft');
  readonly branchSummary = input<{ text: string; warning: boolean }>({ text: '—', warning: false });
  readonly departmentSummary = input<{ text: string; warning: boolean }>({ text: '—', warning: false });
  readonly vehicleUnassigned = input(true);
  readonly vehicleLabel = input('Unassigned');
  readonly hireDateLabel = input('—');
  readonly statusTimeline = input<{ label: string; date: string }[]>([]);
  readonly updatedAtLabel = input<string | null>(null);
  readonly maxDateOfBirth = input('');
  readonly minDateOfBirth = input('');
  readonly validationAttempted = input(0);
  readonly photoSelected = output<File | null>();
  readonly photoRemoved = output<void>();

  readonly genderOptions = DRIVER_GENDER_OPTIONS;
  readonly nationalityOptions = DRIVER_NATIONALITY_OPTIONS;
  readonly phoneCountryCodes = PHONE_COUNTRY_CODES;
  readonly uploadMaxSizeLabel = UPLOAD_MAX_SIZE_LABEL;
  readonly sizeError = signal<string | null>(null);
  readonly photoLoadFailed = signal(false);

  showPhoto(): boolean {
    return !!this.photoPreview() && !this.photoLoadFailed();
  }

  onPhotoError(): void {
    this.photoLoadFailed.set(true);
  }

  clearPhotoError(): void {
    this.sizeError.set(null);
  }

  onPhotoChange(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    if (!file) return;

    const error = vehicleUploadSizeError(file);
    if (error) {
      this.sizeError.set(error);
      (event.target as HTMLInputElement).value = '';
      return;
    }

    this.sizeError.set(null);
    this.photoLoadFailed.set(false);
    this.photoSelected.emit(file);
    (event.target as HTMLInputElement).value = '';
  }

  onCropHint(): void {
    this.toast.success('Upload a pre-cropped passport-style photo (JPG/PNG). In-app crop coming soon.');
  }

  statusBadgeVariant(): 'valid' | 'pending' | 'inactive' {
    const s = this.driverStatus().toLowerCase();
    if (['available', 'verified', 'assigned'].includes(s)) return 'valid';
    if (s === 'draft') return 'inactive';
    if (s === 'suspended') return 'pending';
    return 'pending';
  }

  hireDatePending(): boolean {
    return this.hireDateLabel().toLowerCase().includes('pending');
  }

  showError(controlName: string, errorKey?: string): boolean {
    const c = this.form().get(controlName);
    if (!c) return false;
    const show = c.touched || c.dirty || this.validationAttempted() > 0;
    if (!show) return false;
    if (errorKey) return !!c.hasError(errorKey);
    return c.invalid;
  }

  copyCode(): void {
    void navigator.clipboard.writeText(this.driverCode()).then(() => {
      this.toast.success('Copied successfully');
    });
  }
}
