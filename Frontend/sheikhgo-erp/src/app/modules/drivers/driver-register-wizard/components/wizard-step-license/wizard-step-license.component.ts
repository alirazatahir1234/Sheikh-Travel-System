import { ChangeDetectionStrategy, Component, ElementRef, input, output, signal, viewChild } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { vehicleUploadSizeError, UPLOAD_MAX_SIZE_LABEL } from '../../../../../core/utils/upload-url.util';
import { DriverDocSlot, DriverDocType } from '../../models/driver-wizard.model';

@Component({
  selector: 'app-wizard-step-license',
  standalone: true,
  imports: [ReactiveFormsModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-6">
      <section class="wizard-card">
        <h2 class="wizard-card-title">License Information</h2>
        <div class="form-grid" [formGroup]="form()">
          <label class="field">
            <span>License Number *</span>
            <input formControlName="licenseNumber" class="input" placeholder="e.g. DXB-1234567" />
            @if (showError('licenseNumber', 'duplicate')) {
              <span class="field-error">This license number is already registered</span>
            } @else if (showError('licenseNumber')) {
              <span class="field-error">License number is required</span>
            }
          </label>
          <label class="field">
            <span>License Expiry *</span>
            <input formControlName="licenseExpiryDate" type="date" class="input" [attr.min]="minLicenseExpiry()" />
          </label>
          <label class="field full">
            <span>CNIC / Emirates ID</span>
            <input formControlName="cnic" class="input" placeholder="784-XXXX-XXXXXXX-X" />
          </label>
        </div>
      </section>

      <section class="wizard-card">
        <h2 class="wizard-card-title">Verification Documents</h2>
        <p class="wizard-card-desc">Upload license, medical certificate, and background check documents (JPG, PNG, or PDF).</p>
        <p class="upload-limit-note">
          <mat-icon>info</mat-icon>
          <span>{{ uploadMaxSizeLabel }}</span>
        </p>
        @if (sizeError()) {
          <p class="upload-validation-error">
            <mat-icon>error_outline</mat-icon>
            <span>{{ sizeError() }}</span>
          </p>
        }
        <div class="doc-grid">
          @for (slot of docSlots(); track slot.type) {
            <div
              class="doc-zone"
              [class.has-file]="!!slot.file"
              [class.doc-zone--error]="!!sizeError()"
              (click)="openDocPicker(slot.type)">
              @if (slot.previewUrl) {
                <mat-icon class="doc-zone-icon text-emerald-600">check_circle</mat-icon>
                <span class="doc-zone-label">{{ slot.label }}</span>
                <span class="doc-zone-hint">Uploaded</span>
              } @else {
                <mat-icon class="doc-zone-icon">upload_file</mat-icon>
                <span class="doc-zone-label">{{ slot.label }}</span>
                <span class="doc-zone-hint">Click to upload</span>
                <span class="doc-zone-limit">{{ uploadMaxSizeLabel }}</span>
              }
            </div>
          }
        </div>
        <input
          #docFileInput
          type="file"
          class="hidden"
          accept="image/jpeg,image/png,application/pdf"
          (change)="onFileChange($event)" />
      </section>
    </div>
  `,
  styleUrls: ['../wizard-step-personal/wizard-step-shared.scss']
})
export class WizardStepLicenseComponent {
  readonly form = input.required<FormGroup>();
  readonly minLicenseExpiry = input('');
  readonly docSlots = input.required<DriverDocSlot[]>();
  readonly docSelected = output<{ type: DriverDocType; file: File | null }>();

  private readonly docFileInput = viewChild<ElementRef<HTMLInputElement>>('docFileInput');
  private pendingType: DriverDocType | null = null;
  readonly sizeError = signal<string | null>(null);
  readonly uploadMaxSizeLabel = UPLOAD_MAX_SIZE_LABEL;

  showError(controlName: string, errorKey?: string): boolean {
    const c = this.form().get(controlName);
    if (!c || (!c.touched && !c.dirty)) return false;
    if (errorKey) return !!c.hasError(errorKey);
    return c.invalid;
  }

  openDocPicker(type: DriverDocType): void {
    this.sizeError.set(null);
    this.pendingType = type;
    this.docFileInput()?.nativeElement.click();
  }

  onFileChange(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    if (this.pendingType && file) {
      const error = vehicleUploadSizeError(file);
      if (error) {
        this.sizeError.set(error);
      } else {
        this.sizeError.set(null);
        this.docSelected.emit({ type: this.pendingType, file });
      }
    }
    this.pendingType = null;
    (event.target as HTMLInputElement).value = '';
  }
}
