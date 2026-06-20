import { Injectable, computed, inject, signal, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormBuilder, FormGroup, ValidationErrors, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
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
  splitDriverFullName
} from '../../../../core/models/driver.model';
import { dateInputToIso, toDateInputValue, todayDateInputValue } from '../../../../core/utils/date-input.util';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { vehicleUploadSizeError } from '../../../../core/utils/upload-url.util';
import { UiSelectOption } from '../../../../shared/components/ui/types/ui.types';
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
  dateOfBirthValidator,
  maxDateOfBirthInputValue,
  minDateOfBirthInputValue,
  phoneLocalValidator
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
  { name: 'nationality' },
  { name: 'phoneLocal' },
  { name: 'email' }
];

const LICENSE_FIELDS = [
  { name: 'licenseNumber' },
  { name: 'licenseExpiryDate' }
];

const ORG_FIELDS = [
  { name: 'branchId', required: false }
];

@Injectable()
export class DriverWizardFacade {
  private readonly fb = inject(FormBuilder);
  private readonly driverService = inject(DriverService);
  private readonly platformService = inject(PlatformService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  readonly steps = DRIVER_WIZARD_STEPS;
  readonly phoneCountryCodes = PHONE_COUNTRY_CODES;
  readonly maxDateOfBirth = maxDateOfBirthInputValue();
  readonly minDateOfBirth = minDateOfBirthInputValue();
  readonly currentStep = signal<DriverWizardStepId>('personal');
  readonly driverId = signal<number | null>(null);
  readonly driverCode = signal<string | null>(null);
  readonly isEditMode = signal(false);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly draftSaving = signal(false);
  readonly attemptedSubmit = signal(false);
  readonly lastSavedAt = signal<Date | null>(null);
  readonly photoPreviewUrl = signal<string | null>(null);
  readonly photoFile = signal<File | null>(null);
  readonly branchOptions = signal<UiSelectOption[]>([]);
  readonly departmentOptions = signal<UiSelectOption[]>([]);
  readonly minLicenseExpiry = todayDateInputValue();
  readonly previewCode = signal(previewDriverCode());
  readonly formValues = signal<Record<string, unknown>>({});

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
    emergencyContactName: ['', Validators.maxLength(100)],
    emergencyContactPhone: ['', Validators.maxLength(20)],
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

  readonly previewDriverCode = computed(() => this.driverCode() ?? this.previewCode());

  readonly driverStatusLabel = computed(() =>
    this.isEditMode() ? 'Available' : 'Draft'
  );

  readonly progressPercent = computed(() => {
    const stepIdx = this.steps.findIndex(s => s.id === this.currentStep());
    const values = this.formValues();
    const controls = this.form.controls as Record<string, AbstractControl>;

    const stepFieldMap: Record<DriverWizardStepId, { name: string; required?: boolean }[]> = {
      personal: PERSONAL_FIELDS,
      license: LICENSE_FIELDS,
      organization: ORG_FIELDS
    };

    const stepWeight = 100 / this.steps.length;
    let total = 0;

    for (let i = 0; i < this.steps.length; i++) {
      const stepId = this.steps[i].id;
      const progress = calcStepFieldProgress(stepFieldMap[stepId], values, controls);
      if (i < stepIdx) total += stepWeight;
      else if (i === stepIdx) total += (progress / 100) * stepWeight;
    }

    return Math.min(100, Math.round(total));
  });

  readonly stepCompletion = computed(() => {
    const currentIdx = this.steps.findIndex(s => s.id === this.currentStep());
    const values = this.formValues();
    const controls = this.form.controls as Record<string, AbstractControl>;

    const stepFieldMap: Record<DriverWizardStepId, { name: string; required?: boolean }[]> = {
      personal: PERSONAL_FIELDS,
      license: LICENSE_FIELDS,
      organization: ORG_FIELDS
    };

    return this.steps.map((step, i) => {
      const fieldProgress = calcStepFieldProgress(stepFieldMap[step.id], values, controls);
      return {
        id: step.id,
        label: step.label,
        complete: i < currentIdx || (i === currentIdx && fieldProgress === 100),
        active: step.id === this.currentStep()
      };
    });
  });

  readonly licenseStatus = computed(() => {
    const stepIdx = this.steps.findIndex(s => s.id === 'license');
    const currentIdx = this.steps.findIndex(s => s.id === this.currentStep());
    if (currentIdx < stepIdx) return 'NOT STARTED';

    const v = this.formValues();
    if (v['licenseNumber'] && v['licenseExpiryDate']) return 'IN PROGRESS';
    return 'NOT STARTED';
  });

  readonly orgStatus = computed(() => {
    const stepIdx = this.steps.findIndex(s => s.id === 'organization');
    const currentIdx = this.steps.findIndex(s => s.id === this.currentStep());
    if (currentIdx < stepIdx) return 'NOT STARTED';
    return this.formValues()['branchId'] ? 'ASSIGNED' : 'UNASSIGNED';
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
      this.driverService.getById(id).subscribe({
        next: driver => {
          this.applyDriver(driver);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.snackBar.open('Failed to load driver', 'Close', { duration: 3000 });
          void this.router.navigate(['/drivers']);
        }
      });
    } else {
      this.restoreDraft();
    }
  }

  private applyDriver(d: Driver): void {
    const phoneParts = this.splitPhone(d.phone);
    const emergencyPhone = d.emergencyContact ?? '';
    const hasSplitName = !!(d.firstName?.trim() || d.lastName?.trim());
    const nameParts = hasSplitName
      ? { firstName: d.firstName?.trim() ?? '', lastName: d.lastName?.trim() ?? '' }
      : splitDriverFullName(d.fullName);
    this.driverCode.set(d.driverCode ?? null);
    this.form.patchValue({
      firstName: nameParts.firstName,
      lastName: nameParts.lastName,
      phoneLocal: phoneParts.local,
      phoneCountryCode: phoneParts.code,
      email: d.email ?? '',
      nationality: d.nationality ?? 'United Arab Emirates',
      dateOfBirth: toDateInputValue(d.dateOfBirth),
      gender: d.gender ?? 'Male',
      address: d.address ?? '',
      emergencyContactName: d.emergencyContactName ?? '',
      emergencyContactPhone: emergencyPhone,
      licenseNumber: d.licenseNumber,
      licenseExpiryDate: toDateInputValue(d.licenseExpiryDate),
      branchId: d.branchId ? String(d.branchId) : '',
      departmentId: d.departmentId ? String(d.departmentId) : '',
      cnic: d.cnic ?? '',
      status: d.status,
      isActive: d.isActive
    });
    if (d.photoUrl) this.photoPreviewUrl.set(d.photoUrl);
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
    this.currentStep.set(step as DriverWizardStepId);
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
    const step = this.currentStep();
    const fields: Record<DriverWizardStepId, string[]> = {
      personal: ['firstName', 'lastName', 'phoneLocal', 'email', 'dateOfBirth', 'nationality', 'gender'],
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

    if (!valid) this.snackBar.open('Please fix validation errors', 'Close', { duration: 2500 });
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
      : 'Onboard a new professional to the Sheikh Travel fleet network.';
  }

  onPhotoSelected(file: File | null): void {
    if (!file) return;
    const sizeError = vehicleUploadSizeError(file);
    if (sizeError) {
      this.snackBar.open(sizeError, 'Close', { duration: 3000 });
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
      this.snackBar.open(sizeError, 'Close', { duration: 3000 });
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
      this.snackBar.open('Draft saved', 'Close', { duration: 2000 });
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
      if (draft.form) this.form.patchValue(draft.form);
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
    return {
      firstName: String(v.firstName).trim(),
      lastName: String(v.lastName).trim(),
      phone: this.buildPhone(),
      licenseNumber: String(v.licenseNumber).trim(),
      licenseExpiryDate: dateInputToIso(v.licenseExpiryDate)!,
      email: String(v.email).trim(),
      nationality: v.nationality?.trim() || null,
      dateOfBirth: dateInputToIso(v.dateOfBirth),
      gender: v.gender?.trim() || null,
      emergencyContactName: v.emergencyContactName?.trim() || null,
      emergencyContact: v.emergencyContactPhone?.trim() || null,
      branchId: v.branchId ? Number(v.branchId) : null,
      departmentId: v.departmentId ? Number(v.departmentId) : null,
      cnic: v.cnic?.trim() || null,
      address: v.address?.trim() || null
    };
  }

  async submit(): Promise<void> {
    this.attemptedSubmit.set(true);
    this.form.markAllAsTouched();
    await this.validatePersonalUniqueness();
    await this.validateLicenseUniqueness();
    if (this.form.invalid) {
      this.snackBar.open('Please complete required fields', 'Close', { duration: 3000 });
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

      this.snackBar.open(this.isEditMode() ? 'Driver updated' : 'Driver registered', 'Close', { duration: 2500 });
      void this.router.navigate(['/drivers']);
    } catch (err) {
      this.applyConflictFromError(err);
      this.snackBar.open(apiErrorMessage(err, 'Save failed'), 'Close', { duration: 4000 });
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
