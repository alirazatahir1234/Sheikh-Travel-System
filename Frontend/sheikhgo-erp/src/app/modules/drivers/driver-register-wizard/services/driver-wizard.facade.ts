import { Injectable, computed, inject, signal, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormBuilder, FormGroup, ValidationErrors, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { DriverService } from '../../../../core/services/driver.service';
import { PlatformService } from '../../../../core/services/platform.service';
import {
  CreateDriverDto,
  Driver,
  DriverStatus,
  UpdateDriverDto,
  driverDisplayName,
  DriverStatusLabels,
  splitDriverFullName,
  sanitizeCreateDriverDto
} from '../../../../core/models/driver.model';
import { dateInputToIso, toDateInputValue, todayDateInputValue, parseApiDate, formatAbsoluteDateTime, formatRelativeTime } from '../../../../core/utils/date-input.util';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { COMPANY_NAME } from '../../../../core/constants/app-brand';
import { vehicleUploadSizeError, resolveDriverPhotoUrl, resolveUploadUrl } from '../../../../core/utils/upload-url.util';
import { UiSelectOption } from '../../../../shared/components/ui/types/ui.types';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import {
  DRIVER_DOC_SLOTS,
  DRIVER_WIZARD_DRAFT_KEY,
  DRIVER_WIZARD_STEPS,
  DriverDocSlot,
  DriverDocType,
  DriverWizardDraft,
  DriverWizardStepId
} from '../models/driver-wizard.model';
import {
  PHONE_COUNTRY_CODES,
  buildFullName,
  calcStepFieldProgress,
  calcProfileCompletion,
  calcOrgFieldProgress,
  dateOfBirthValidator,
  emergencyContactPhoneValidator,
  maxDateOfBirthInputValue,
  minDateOfBirthInputValue,
  phoneLocalValidator,
  phoneCodeForNationality
} from '../utils/driver-wizard.validators';

function requiredTrimmed() {
  return (control: AbstractControl): ValidationErrors | null => {
    const v = String(control.value ?? '').trim();
    return v ? null : { required: true };
  };
}

function previewDriverCode(): string {
  const year = new Date().getFullYear();
  const seq = Math.floor(Math.random() * 900 + 100);
  return `ST-DRV-${year}-${seq}`;
}

const PERSONAL_FIELDS = [
  { name: 'firstName' },
  { name: 'lastName' },
  { name: 'dateOfBirth' },
  { name: 'gender' },
  { name: 'nationality' },
  { name: 'phoneLocal' },
  { name: 'email' },
  { name: 'emergencyContactName' },
  { name: 'emergencyContactPhone' }
];

const LICENSE_FIELDS = [
  { name: 'licenseNumber' },
  { name: 'licenseExpiryDate' }
];

const ORG_FIELDS = [
  { name: 'branchId' }
];

@Injectable()
export class DriverWizardFacade {
  private readonly fb = inject(FormBuilder);
  private readonly driverService = inject(DriverService);
  private readonly platformService = inject(PlatformService);
  private readonly router = inject(Router);
  private readonly toast = inject(UiToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly steps = DRIVER_WIZARD_STEPS;
  readonly phoneCountryCodes = PHONE_COUNTRY_CODES;
  readonly maxDateOfBirth = maxDateOfBirthInputValue();
  readonly minDateOfBirth = minDateOfBirthInputValue();
  readonly currentStep = signal<DriverWizardStepId>('personal');
  readonly driverId = signal<number | null>(null);
  readonly loadedDriver = signal<Driver | null>(null);
  readonly driverCode = signal<string | null>(null);
  readonly isEditMode = signal(false);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly draftSaving = signal(false);
  readonly attemptedSubmit = signal(false);
  /** Bumped when step/submit validation runs so OnPush child steps re-render field errors. */
  readonly validationAttempted = signal(0);
  readonly lastSavedAt = signal<Date | null>(null);
  /** Server record timestamp — shown as absolute "Last updated on …" in edit mode. */
  readonly recordUpdatedAt = signal<Date | null>(null);
  readonly photoPreviewUrl = signal<string | null>(null);
  readonly photoFile = signal<File | null>(null);
  readonly branchOptions = signal<UiSelectOption[]>([]);
  readonly departmentOptions = signal<UiSelectOption[]>([]);
  readonly minLicenseExpiry = todayDateInputValue();
  readonly previewCode = signal(previewDriverCode());
  readonly formValues = signal<Record<string, unknown>>({});
  /** Bumped after API load so date inputs re-bind reliably in edit mode. */
  readonly formLoadKey = signal(0);

  readonly docSlots = signal<DriverDocSlot[]>(
    DRIVER_DOC_SLOTS.map(s => ({ ...s, file: null, previewUrl: null }))
  );

  readonly form: FormGroup = this.fb.group({
    firstName: ['', [requiredTrimmed(), Validators.maxLength(100)]],
    lastName: ['', [requiredTrimmed(), Validators.maxLength(100)]],
    phoneLocal: ['', [requiredTrimmed()]],
    phoneCountryCode: ['+971'],
    email: ['', [requiredTrimmed(), Validators.email]],
    nationality: ['United Arab Emirates', [requiredTrimmed(), Validators.maxLength(80)]],
    dateOfBirth: ['', dateOfBirthValidator()],
    gender: ['Male', requiredTrimmed()],
    address: [''],
    emergencyContactName: ['', [requiredTrimmed(), Validators.maxLength(100)]],
    emergencyContactPhone: ['', [emergencyContactPhoneValidator()]],
    licenseNumber: ['', [requiredTrimmed(), Validators.maxLength(30)]],
    licenseExpiryDate: ['', Validators.required],
    cnic: [''],
    branchId: [''],
    departmentId: [''],
    status: [DriverStatus.Available],
    isActive: [true]
  });

  readonly previewName = computed(() => {
    const v = this.formValues();
    return driverDisplayName({
      firstName: String(v['firstName'] ?? ''),
      lastName: String(v['lastName'] ?? ''),
      fullName: ''
    }) || 'New Driver';
  });

  readonly previewFullName = computed(() => {
    const v = this.formValues();
    const name = buildFullName(String(v['firstName'] ?? ''), String(v['lastName'] ?? ''));
    return name || '—';
  });

  readonly previewInitials = computed(() => {
    const name = this.previewFullName();
    if (name === '—') return '?';
    return name.split(' ').slice(0, 2).map(w => w[0] ?? '').join('').toUpperCase();
  });

  readonly previewDriverCode = computed(() => this.driverCode() ?? this.previewCode());

  private stepFieldMap(): Record<DriverWizardStepId, { name: string; required?: boolean }[]> {
    return {
      personal: PERSONAL_FIELDS,
      license: LICENSE_FIELDS,
      organization: ORG_FIELDS
    };
  }

  readonly profileCompletion = computed(() => {
    const values = this.formValues();
    const controls = this.form.controls as Record<string, AbstractControl>;
    const map = this.stepFieldMap();

    const personal = calcStepFieldProgress(map.personal, values, controls);
    const license = calcStepFieldProgress(map.license, values, controls);
    const organization = calcOrgFieldProgress(values);

    return calcProfileCompletion([
      { label: 'Personal Info', percent: personal },
      { label: 'License', percent: license },
      { label: 'Assignment', percent: organization }
    ]);
  });

  readonly profileCompletionHint = computed(() => {
    const s = this.profileCompletion().sections;
    if (s.length === 0) return '';
    const parts = s.map(x => `${x.label} ${x.percent}%`).join(' + ');
    const sum = s.reduce((n, x) => n + x.percent, 0);
    return `${parts} = ${sum}% ÷ ${s.length} = ${this.profileCompletion().overall}%`;
  });

  readonly driverStatusLabel = computed(() => {
    const driver = this.loadedDriver();
    const values = this.formValues();
    const controls = this.form.controls as Record<string, AbstractControl>;
    const personalPct = calcStepFieldProgress(PERSONAL_FIELDS, values, controls);
    const licenseComplete = !!(String(values['licenseNumber'] ?? '').trim() && values['licenseExpiryDate']);
    const hasBranch = !!String(values['branchId'] ?? '').trim();
    const hasVehicle = !!(driver?.assignedVehicleId);
    const verification = (driver?.verificationStatus ?? '').toLowerCase();
    const operational = Number(values['status'] ?? driver?.status ?? DriverStatus.Available) as DriverStatus;

    if (operational === DriverStatus.Suspended) return 'Suspended';

    if (verification === 'verified') {
      if (hasVehicle) return 'Assigned';
      if (hasBranch) return 'Available';
      return 'Verified';
    }

    if (verification === 'rejected') return 'Pending Verification';

    if (licenseComplete && hasBranch && personalPct >= 80) return 'Pending Verification';
    if (licenseComplete && hasBranch) return 'Pending Verification';
    if (licenseComplete && !hasBranch) return 'Pending Assignment';
    if (personalPct >= 80 || (this.isEditMode() && licenseComplete)) return 'Pending Verification';
    if (!this.isEditMode() && personalPct < 50) return 'Draft';

    return 'Pending Verification';
  });

  readonly licenseVerificationStatus = computed(() => {
    const values = this.formValues();
    const verification = (this.loadedDriver()?.verificationStatus ?? '').toLowerCase();
    if (verification === 'verified') return 'Verified';
    if (values['licenseNumber'] && values['licenseExpiryDate']) return 'Pending';
    return 'Not Started';
  });

  readonly orgAssignmentStatus = computed(() => {
    const values = this.formValues();
    if (values['branchId']) return 'Assigned';
    if (this.isEditMode()) return 'Pending';
    return 'Not Started';
  });

  readonly backgroundCheckStatus = computed(() => {
    const verification = (this.loadedDriver()?.verificationStatus ?? '').toLowerCase();
    const hasDoc = this.docSlots().some(s => s.type === 'BackgroundCheck' && (s.file || s.previewUrl));
    if (verification === 'verified' && hasDoc) return 'Verified';
    if (hasDoc) return 'Pending';
    return 'Not Started';
  });

  readonly verificationSidebarRows = computed(() => [
    { label: 'Driver Status', status: this.driverStatusLabel() },
    { label: 'License Verification', status: this.licenseVerificationStatus() },
    { label: 'Org Assignment', status: this.orgAssignmentStatus() },
    { label: 'Background Check', status: this.backgroundCheckStatus() }
  ]);

  readonly verificationProgressPercent = computed(() => {
    const rows = this.verificationSidebarRows();
    const weights: Record<string, number> = {
      'VERIFIED': 100, 'ASSIGNED': 100, 'AVAILABLE': 100,
      'PENDING': 50, 'NOT STARTED': 0
    };
    const total = rows.reduce((sum, r) => sum + (weights[r.status.toUpperCase()] ?? 25), 0);
    return Math.round(total / rows.length);
  });

  readonly onboardingChecklist = computed(() => {
    const values = this.formValues();
    const controls = this.form.controls as Record<string, AbstractControl>;
    const personalDone = calcStepFieldProgress(PERSONAL_FIELDS, values, controls) === 100;
    const licenseFieldsDone = !!(values['licenseNumber'] && values['licenseExpiryDate']);
    const licenseDoc = this.docSlots().some(s => s.type === 'DrivingLicense' && (s.file || s.previewUrl));
    const orgDone = !!String(values['branchId'] ?? '').trim();
    const medicalDoc = this.docSlots().some(s => s.type === 'MedicalCertificate' && (s.file || s.previewUrl));
    const bgDoc = this.docSlots().some(s => s.type === 'BackgroundCheck' && (s.file || s.previewUrl));
    const verified = (this.loadedDriver()?.verificationStatus ?? '').toLowerCase() === 'verified';

    return [
      { label: 'Personal Information', done: personalDone },
      { label: 'License Uploaded', done: licenseFieldsDone && licenseDoc },
      { label: 'Organization Assigned', done: orgDone },
      { label: 'Background Check', done: bgDoc },
      { label: 'Medical Certificate', done: medicalDoc },
      { label: 'Driver Verification', done: verified }
    ];
  });

  readonly statusTimeline = computed(() => {
    const d = this.loadedDriver();
    if (!d) return [];

    const items: { label: string; date: string }[] = [];
    const created = formatAbsoluteDateTime(d.createdAt);
    if (created) items.push({ label: 'Driver Created', date: created });

    const values = this.formValues();
    if (values['licenseNumber'] && values['licenseExpiryDate']) {
      const licenseDate = formatAbsoluteDateTime(d.updatedAt ?? d.createdAt);
      if (licenseDate) items.push({ label: 'License Recorded', date: licenseDate });
    }

    if (values['branchId']) {
      const orgDate = formatAbsoluteDateTime(d.updatedAt ?? d.createdAt);
      if (orgDate) items.push({ label: 'Org Assigned', date: orgDate });
    }

    const verification = (d.verificationStatus ?? '').toLowerCase();
    if (verification === 'pending' || verification === 'rejected') {
      items.push({ label: 'Pending Verification', date: formatAbsoluteDateTime(d.updatedAt ?? d.createdAt) ?? '—' });
    } else if (verification === 'verified') {
      items.push({ label: 'Verified', date: formatAbsoluteDateTime(d.updatedAt ?? d.createdAt) ?? '—' });
    }

    return items;
  });

  readonly footerSaveLabel = computed(() => {
    const session = this.lastSavedAt();
    if (session) {
      const relative = formatRelativeTime(session);
      return relative ? `Saved ${relative}` : null;
    }
    const record = this.recordUpdatedAt();
    if (this.isEditMode() && record) {
      const absolute = formatAbsoluteDateTime(record);
      return absolute ? `Last updated on ${absolute}` : null;
    }
    return null;
  });

  readonly progressPercent = computed(() => this.profileCompletion().overall);

  readonly stepCompletion = computed(() => {
    const values = this.formValues();
    const controls = this.form.controls as Record<string, AbstractControl>;
    const map = this.stepFieldMap();

    return this.steps.map(step => {
      const fieldProgress = step.id === 'organization'
        ? calcOrgFieldProgress(values)
        : calcStepFieldProgress(map[step.id], values, controls);
      return {
        id: step.id,
        label: step.label,
        complete: fieldProgress === 100,
        active: step.id === this.currentStep()
      };
    });
  });

  readonly licenseStatus = computed(() => this.licenseVerificationStatus().toUpperCase());

  readonly orgStatus = computed(() => {
    const s = this.orgAssignmentStatus();
    if (s === 'Assigned') return 'ASSIGNED';
    if (s === 'Pending') return 'PENDING';
    return 'NOT STARTED';
  });

  readonly documentsUploadedCount = computed(() => this.docSlots().filter(s => s.file).length);

  constructor() {
    const phoneCtrl = this.form.get('phoneLocal')!;
    phoneCtrl.setValidators([
      requiredTrimmed(),
      phoneLocalValidator(() => String(this.form.get('phoneCountryCode')?.value ?? '+971'))
    ]);

    this.form.get('phoneCountryCode')?.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => phoneCtrl.updateValueAndValidity({ emitEvent: false }));

    this.form.get('nationality')?.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(nationality => {
        const code = phoneCodeForNationality(String(nationality ?? ''));
        if (!code) return;
        const codeCtrl = this.form.get('phoneCountryCode');
        if (codeCtrl?.value !== code) {
          codeCtrl?.setValue(code);
        }
      });

    this.form.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(v => this.formValues.set(v));

    this.formValues.set(this.form.getRawValue());

    this.form.get('phoneLocal')?.valueChanges
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => void this.validatePersonalUniqueness());

    this.form.get('email')?.valueChanges
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => void this.validatePersonalUniqueness());

    this.form.get('licenseNumber')?.valueChanges
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => void this.validateLicenseUniqueness());
  }

  init(id?: number): void {
    this.platformService.getBranches().subscribe(branches => {
      this.branchOptions.set(branches.map(b => ({ value: String(b.id), label: b.name })));
    });
    this.platformService.getDepartments().subscribe(depts => {
      this.departmentOptions.set(depts.map(d => ({ value: String(d.id), label: d.name })));
    });

    if (id) {
      this.isEditMode.set(true);
      this.driverId.set(id);
      this.loading.set(true);
      this.formLoadKey.set(0);
      this.driverService.getById(id).subscribe({
        next: driver => {
          this.applyDriver(driver);
          this.loadDriverDocuments(id);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.toast.error('Failed to load driver');
          void this.router.navigate(['/drivers']);
        }
      });
    } else {
      this.restoreDraft();
    }
  }

  private applyDriver(d: Driver): void {
    this.loadedDriver.set(d);
    const phoneParts = this.splitPhone(d.phone);
    const emergencyPhone = d.emergencyContact ?? '';
    const hasSplitName = !!(d.firstName?.trim() || d.lastName?.trim());
    const nameParts = hasSplitName
      ? { firstName: d.firstName?.trim() ?? '', lastName: d.lastName?.trim() ?? '' }
      : splitDriverFullName(d.fullName);
    const dateOfBirth = toDateInputValue(d.dateOfBirth);
    const licenseExpiryDate = toDateInputValue(d.licenseExpiryDate);

    this.driverCode.set(d.driverCode ?? null);
    this.form.patchValue({
      firstName: nameParts.firstName,
      lastName: nameParts.lastName,
      phoneLocal: phoneParts.local,
      phoneCountryCode: phoneParts.code,
      email: d.email ?? '',
      nationality: d.nationality ?? 'United Arab Emirates',
      dateOfBirth,
      gender: d.gender ?? '',
      address: d.address ?? '',
      emergencyContactName: d.emergencyContactName ?? '',
      emergencyContactPhone: emergencyPhone,
      licenseNumber: d.licenseNumber,
      licenseExpiryDate,
      branchId: d.branchId ? String(d.branchId) : '',
      departmentId: d.departmentId ? String(d.departmentId) : '',
      cnic: d.cnic ?? '',
      status: d.status,
      isActive: d.isActive
    }, { emitEvent: false });

    this.patchDateControl('dateOfBirth', dateOfBirth);
    this.patchDateControl('licenseExpiryDate', licenseExpiryDate);
    this.formValues.set(this.form.getRawValue());
    this.formLoadKey.update(n => n + 1);

    const photo = resolveDriverPhotoUrl(d.photoUrl);
    this.photoPreviewUrl.set(photo);

    this.recordUpdatedAt.set(parseApiDate(d.updatedAt ?? d.createdAt));
  }

  private patchDateControl(name: string, value: string): void {
    const control = this.form.get(name);
    if (!control) return;
    control.setValue(value, { emitEvent: false });
    control.updateValueAndValidity({ emitEvent: false });
  }

  branchSummaryLabel(): { text: string; warning: boolean } {
    const d = this.loadedDriver();
    const formBranch = String(this.form.getRawValue().branchId ?? '').trim();
    const name = d?.branchName
      ?? this.branchOptions().find(b => b.value === formBranch)?.label;
    if (name) return { text: name, warning: false };
    return { text: 'Driver not assigned to any branch', warning: true };
  }

  departmentSummaryLabel(): { text: string; warning: boolean } {
    const d = this.loadedDriver();
    const formDept = String(this.form.getRawValue().departmentId ?? '').trim();
    const name = d?.departmentName
      ?? this.departmentOptions().find(x => x.value === formDept)?.label;
    if (name) return { text: name, warning: false };
    return { text: 'No department assigned', warning: true };
  }

  isVehicleUnassigned(): boolean {
    return this.assignedVehicleLabel() === 'Unassigned';
  }

  private loadDriverDocuments(id: number): void {
    this.driverService.getDocuments(id).subscribe({
      next: docs => {
        this.docSlots.update(slots =>
          slots.map(s => {
            const doc = docs.find(d => d.documentType === s.type);
            if (!doc?.fileUrl) return s;
            return { ...s, previewUrl: resolveUploadUrl(doc.fileUrl) };
          })
        );
      },
      error: () => { /* optional */ }
    });
  }

  resolvedPhotoPreview(): string | null {
    return resolveDriverPhotoUrl(this.photoPreviewUrl());
  }

  formatUpdatedAt(): string | null {
    return formatAbsoluteDateTime(this.recordUpdatedAt());
  }

  assignedVehicleLabel(): string {
    const d = this.loadedDriver();
    if (!d) return 'Unassigned';
    return d.assignedVehicleRegistration || d.assignedVehicleCode || 'Unassigned';
  }

  hireDateLabel(): string {
    const raw = this.loadedDriver()?.hireDate;
    if (!raw) return 'Pending organization assignment';
    const d = parseApiDate(raw);
    if (!d) return 'Pending organization assignment';
    return new Intl.DateTimeFormat(undefined, {
      day: '2-digit',
      month: 'short',
      year: 'numeric'
    }).format(d);
  }

  private splitPhone(phone: string): { code: string; local: string } {
    const trimmed = phone.trim();
    for (const rule of PHONE_COUNTRY_CODES) {
      if (trimmed.startsWith(rule.code)) {
        return { code: rule.code, local: trimmed.slice(rule.code.length).replace(/\D/g, '') };
      }
    }
    if (trimmed.startsWith('+')) {
      const match = trimmed.match(/^(\+\d{1,4})\s*(.*)$/);
      if (match) return { code: match[1], local: match[2].replace(/\D/g, '') };
    }
    return { code: '+971', local: trimmed.replace(/\D/g, '') };
  }

  buildPhone(): string {
    const v = this.form.getRawValue();
    const code = String(v.phoneCountryCode ?? '+971').trim();
    const local = String(v.phoneLocal ?? '').replace(/\D/g, '');
    return `${code} ${local}`.trim();
  }

  async validatePersonalUniqueness(): Promise<boolean> {
    const phoneCtrl = this.form.get('phoneLocal');
    const emailCtrl = this.form.get('email');
    const includePhone = isReadyForUniquenessCheck(phoneCtrl);
    const includeEmail = isReadyForUniquenessCheck(emailCtrl);

    if (!includePhone && !includeEmail) return true;

    try {
      const availability = await firstValueFrom(
        this.driverService.checkAvailability({
          phone: includePhone ? this.buildPhone() : undefined,
          email: includeEmail ? String(emailCtrl!.value).trim() : undefined,
          excludeDriverId: this.driverId() ?? undefined
        })
      );

      if (includePhone) this.setDuplicateError(phoneCtrl!, !availability.phoneAvailable);
      if (includeEmail) this.setDuplicateError(emailCtrl!, !availability.emailAvailable);

      const phoneOk = !includePhone || availability.phoneAvailable;
      const emailOk = !includeEmail || availability.emailAvailable;
      return phoneOk && emailOk;
    } catch {
      return true;
    }
  }

  async validateLicenseUniqueness(): Promise<boolean> {
    const ctrl = this.form.get('licenseNumber');
    if (!ctrl || ctrl.invalid) return true;

    const licenseNumber = String(ctrl.value ?? '').trim();
    if (!licenseNumber) return true;

    try {
      const availability = await firstValueFrom(
        this.driverService.checkAvailability({
          licenseNumber,
          excludeDriverId: this.driverId() ?? undefined
        })
      );
      this.setDuplicateError(ctrl, !availability.licenseAvailable);
      return availability.licenseAvailable;
    } catch {
      return true;
    }
  }

  private setDuplicateError(ctrl: AbstractControl, duplicate: boolean): void {
    const errors = { ...(ctrl.errors ?? {}) };
    if (duplicate) errors['duplicate'] = true;
    else delete errors['duplicate'];
    ctrl.setErrors(Object.keys(errors).length ? errors : null);
  }

  goToStep(step: string): void {
    const target = step as DriverWizardStepId;
    const targetIdx = this.steps.findIndex(s => s.id === target);
    const currentIdx = this.steps.findIndex(s => s.id === this.currentStep());
    if (targetIdx <= currentIdx) {
      this.currentStep.set(target);
      return;
    }

    void this.validateStepsUpTo(targetIdx - 1).then(ok => {
      if (ok) this.currentStep.set(target);
    });
  }

  private async validateStepsUpTo(lastIdx: number): Promise<boolean> {
    const original = this.currentStep();
    for (let i = 0; i <= lastIdx; i++) {
      this.currentStep.set(this.steps[i].id);
      if (!(await this.validateCurrentStep())) {
        this.currentStep.set(original);
        return false;
      }
    }
    return true;
  }

  prevStep(): void {
    const idx = this.steps.findIndex(s => s.id === this.currentStep());
    if (idx > 0) this.currentStep.set(this.steps[idx - 1].id);
  }

  nextStep(): void {
    void this.advanceStep();
  }

  private async advanceStep(): Promise<void> {
    if (!(await this.validateCurrentStep())) return;
    const idx = this.steps.findIndex(s => s.id === this.currentStep());
    if (idx < this.steps.length - 1) this.currentStep.set(this.steps[idx + 1].id);
  }

  async validateCurrentStep(): Promise<boolean> {
    this.validationAttempted.update(n => n + 1);
    const step = this.currentStep();
    const fields: Record<DriverWizardStepId, string[]> = {
      personal: [
        'firstName', 'lastName', 'phoneLocal', 'email', 'dateOfBirth', 'nationality', 'gender',
        'emergencyContactName', 'emergencyContactPhone'
      ],
      license: ['licenseNumber', 'licenseExpiryDate'],
      organization: []
    };
    let valid = true;
    for (const name of fields[step]) {
      const c = this.form.get(name);
      c?.markAsTouched();
      if (c?.invalid) valid = false;
    }

    if (valid && step === 'personal') {
      const unique = await this.validatePersonalUniqueness();
      if (!unique || this.form.get('phoneLocal')?.hasError('duplicate') || this.form.get('email')?.hasError('duplicate')) {
        valid = false;
      }
    }

    if (valid && step === 'license') {
      const unique = await this.validateLicenseUniqueness();
      if (!unique || this.form.get('licenseNumber')?.hasError('duplicate')) {
        valid = false;
      }
    }

    if (!valid) this.toast.error('Please fix validation errors');
    return valid;
  }

  handlePrimaryAction(): void {
    if (this.currentStep() !== 'organization') {
      this.nextStep();
      return;
    }
    void this.submit();
  }

  primaryCtaLabel(): string {
    return this.currentStep() === 'organization'
      ? (this.isEditMode() ? 'Save Changes' : 'Register Driver')
      : 'Next Step';
  }

  pageSubtitle(): string {
    return this.isEditMode()
      ? 'Update driver profile, license, and organization details.'
      : `Onboard a new professional to the ${COMPANY_NAME} fleet network.`;
  }

  onPhotoSelected(file: File | null): void {
    if (!file) return;
    const sizeError = vehicleUploadSizeError(file);
    if (sizeError) {
      this.toast.error(sizeError);
      return;
    }
    this.photoFile.set(file);
    this.photoPreviewUrl.set(URL.createObjectURL(file));
  }

  onPhotoRemoved(): void {
    const url = this.photoPreviewUrl();
    if (url?.startsWith('blob:')) URL.revokeObjectURL(url);
    this.photoFile.set(null);
    this.photoPreviewUrl.set(null);
  }

  onDocSelected(type: DriverDocType, file: File | null): void {
    if (!file) return;
    const sizeError = vehicleUploadSizeError(file);
    if (sizeError) {
      this.toast.error(sizeError);
      return;
    }
    this.docSlots.update(slots =>
      slots.map(s => {
        if (s.type !== type) return s;
        if (s.previewUrl?.startsWith('blob:')) URL.revokeObjectURL(s.previewUrl);
        return { ...s, file, previewUrl: URL.createObjectURL(file) };
      })
    );
  }

  saveDraft(): void {
    this.draftSaving.set(true);
    try {
      const draft: DriverWizardDraft = {
        form: this.form.getRawValue(),
        currentStep: this.currentStep(),
        savedAt: new Date().toISOString()
      };
      localStorage.setItem(DRIVER_WIZARD_DRAFT_KEY, JSON.stringify(draft));
      this.lastSavedAt.set(new Date());
      this.toast.success('Draft saved');
    } finally {
      this.draftSaving.set(false);
    }
  }

  lastSavedFormatted(): string | null {
    const at = this.lastSavedAt();
    if (!at) return null;
    return new Intl.DateTimeFormat(undefined, {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    }).format(at);
  }

  private restoreDraft(): void {
    try {
      const raw = localStorage.getItem(DRIVER_WIZARD_DRAFT_KEY);
      if (!raw) return;
      const draft = JSON.parse(raw) as DriverWizardDraft;
      if (draft.form) {
        this.form.patchValue(draft.form, { emitEvent: false });
        this.formValues.set(this.form.getRawValue());
      }
      if (draft.currentStep) this.currentStep.set(draft.currentStep);
      if (draft.savedAt) this.lastSavedAt.set(new Date(draft.savedAt));
    } catch {
      /* ignore */
    }
  }

  clearDraft(): void {
    localStorage.removeItem(DRIVER_WIZARD_DRAFT_KEY);
  }

  cancel(): void {
    void this.router.navigate(['/drivers']);
  }

  private buildDto(): CreateDriverDto {
    const v = this.form.getRawValue();
    return sanitizeCreateDriverDto({
      firstName: String(v.firstName).trim(),
      lastName: String(v.lastName).trim(),
      phone: this.buildPhone(),
      licenseNumber: String(v.licenseNumber).trim(),
      licenseExpiryDate: String(v.licenseExpiryDate).trim(),
      email: String(v.email).trim(),
      nationality: v.nationality?.trim() || null,
      dateOfBirth: v.dateOfBirth ? String(v.dateOfBirth).trim() : null,
      gender: v.gender?.trim() || null,
      emergencyContactName: v.emergencyContactName?.trim() || null,
      emergencyContact: v.emergencyContactPhone?.trim() || null,
      branchId: v.branchId ? Number(v.branchId) : null,
      departmentId: v.departmentId ? Number(v.departmentId) : null,
      cnic: v.cnic?.trim() || null,
      address: v.address?.trim() || null
    });
  }

  private async validateAllSteps(): Promise<boolean> {
    for (const step of this.steps) {
      this.currentStep.set(step.id);
      if (!(await this.validateCurrentStep())) return false;
    }
    return true;
  }

  async submit(): Promise<void> {
    this.attemptedSubmit.set(true);
    this.validationAttempted.update(n => n + 1);
    this.form.markAllAsTouched();
    if (!(await this.validateAllSteps())) {
      this.toast.warning('Please complete required fields');
      return;
    }

    this.saving.set(true);
    try {
      const dto = this.buildDto();
      let id = this.driverId();

      if (this.isEditMode() && id) {
        const updateDto: UpdateDriverDto = {
          ...dto,
          status: Number(this.form.getRawValue().status) as DriverStatus,
          isActive: !!this.form.getRawValue().isActive
        };
        await firstValueFrom(this.driverService.update({ id, driver: updateDto }));
        this.lastSavedAt.set(new Date());
      } else {
        id = await firstValueFrom(this.driverService.create({ driver: dto }));
        this.driverId.set(id);
        this.clearDraft();
      }

      const photo = this.photoFile();
      if (photo && id) {
        await firstValueFrom(this.driverService.uploadPhoto(id, photo));
      }

      if (id) {
        for (const slot of this.docSlots()) {
          if (slot.file) {
            await firstValueFrom(this.driverService.uploadDocument(id, slot.type, slot.file));
          }
        }
      }

      if (this.isEditMode() && id) {
        const refreshed = await firstValueFrom(this.driverService.getById(id));
        this.applyDriver(refreshed);
        this.loadDriverDocuments(id);
      }

      this.toast.success(this.isEditMode() ? 'Driver updated' : 'Driver registered');
      if (!this.isEditMode()) {
        void this.router.navigate(['/drivers']);
      }
    } catch (err) {
      this.applyConflictFromError(err);
      this.toast.error(apiErrorMessage(err, 'Save failed'));
    } finally {
      this.saving.set(false);
    }
  }

  private applyConflictFromError(err: unknown): void {
    if (!(err instanceof HttpErrorResponse) || err.status !== 409) return;

    const message = apiErrorMessage(err, '').toLowerCase();
    if (message.includes('mobile') || message.includes('phone')) {
      this.setDuplicateError(this.form.get('phoneLocal')!, true);
    }
    if (message.includes('email')) {
      this.setDuplicateError(this.form.get('email')!, true);
    }
    if (message.includes('license')) {
      this.setDuplicateError(this.form.get('licenseNumber')!, true);
    }
  }

  validationErrors(): string[] {
    const errors: string[] = [];
    if (this.form.get('firstName')?.invalid) errors.push('First name is required');
    if (this.form.get('lastName')?.invalid) errors.push('Last name is required');
    if (this.form.get('dateOfBirth')?.hasError('futureDate')) errors.push('Date of birth cannot be in the future');
    if (this.form.get('dateOfBirth')?.hasError('minAge')) errors.push('Driver must be at least 18 years old');
    if (this.form.get('phoneLocal')?.invalid) errors.push('Valid mobile number is required');
    if (this.form.get('email')?.invalid) errors.push('Valid email is required');
    if (this.form.get('emergencyContactName')?.invalid) errors.push('Emergency contact name is required');
    if (this.form.get('emergencyContactPhone')?.invalid) errors.push('Valid emergency contact phone is required');
    if (this.form.get('licenseNumber')?.invalid) errors.push('License number is required');
    if (this.form.get('phoneLocal')?.hasError('duplicate')) errors.push('Mobile number is already registered');
    if (this.form.get('email')?.hasError('duplicate')) errors.push('Email is already registered');
    if (this.form.get('licenseNumber')?.hasError('duplicate')) errors.push('License number is already registered');
    return errors;
  }
}

function isReadyForUniquenessCheck(ctrl: AbstractControl | null | undefined): boolean {
  if (!ctrl) return false;
  if (!String(ctrl.value ?? '').trim()) return false;
  const errs = { ...(ctrl.errors ?? {}) };
  delete errs['duplicate'];
  return Object.keys(errs).length === 0;
}
