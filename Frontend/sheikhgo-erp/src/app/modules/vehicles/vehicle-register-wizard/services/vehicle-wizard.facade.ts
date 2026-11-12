import { Injectable, inject, signal, computed } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { debounceTime, distinctUntilChanged, filter, switchMap, catchError, of, Subject, takeUntil, forkJoin, tap, firstValueFrom } from 'rxjs';
import { VehicleService } from '../../../../core/services/vehicle.service';
import { GpsTrackingService } from '../../../../core/services/gps-tracking.service';
import { PlatformService } from '../../../../core/services/platform.service';
import { LookupService } from '../../../../core/services/lookup.service';
import { CompanyScopeService } from '../../../../core/services/company-scope.service';
import { CompanyScopeController } from '../../../../shared/components/company-scope/company-scope.controller';
import {
  CreateVehicleDto,
  FuelType,
  UpdateVehicleDto,
  Vehicle,
  VehicleDocument,
  VehicleGps,
  VehicleStatus,
  sanitizeCreateVehicleDto,
  normalizeFuelType,
  normalizeVehicleStatus
} from '../../../../core/models/vehicle.model';
import { GpsDevice } from '../../../../core/models/gps-tracking.model';
import { dateInputToIso, toDateInputValue, todayDateInputValue } from '../../../../core/utils/date-input.util';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { vehicleUploadSizeError } from '../../../../core/utils/upload-url.util';
import { UiSelectOption } from '../../../../shared/components/ui/types/ui.types';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import {
  AssignedTrackerInfo,
  DocumentSlotState,
  GpsWizardMode,
  getVinValidationState,
  generateVehicleCode,
  fuelTypeLabel,
  resolveWizardTrackerModel,
  validateVin,
  WIZARD_DOCUMENT_SLOTS,
  WizardStepId,
  WIZARD_STEPS,
  VEHICLE_IMAGE_ANGLES,
  VehicleImageSlotState,
  documentContinueMessage
} from '../models/vehicle-wizard.model';
import {
  buildDefaultDetailCatalogOptions,
  mergeModelsByMake,
  modelOptionsForMake,
  toSelectOptions,
  withSelectOption
} from '../utils/vehicle-catalog.util';
import {
  hydrateVehicleDocumentSlots,
  readVehicleDocumentId,
  readVehicleFileUrl,
  slotHasUploadedDocument,
  unwrapUploadVehicleDocumentResult
} from '../utils/vehicle-document-slots.util';
import {
  descriptiveVehicleTextError,
  descriptiveVehicleTextValidator,
  isDescriptiveVehicleText
} from '../../utils/vehicle-descriptive-text.util';

@Injectable()
export class VehicleWizardFacade {
  private readonly fb = inject(FormBuilder);
  private readonly vehicleService = inject(VehicleService);
  private readonly gpsService = inject(GpsTrackingService);
  private readonly platformService = inject(PlatformService);
  private readonly lookupService = inject(LookupService);
  private readonly companyScopeService = inject(CompanyScopeService);
  private readonly companyScope = inject(CompanyScopeController, { optional: true });
  private readonly router = inject(Router);
  private readonly toast = inject(UiToastService);
  private readonly destroy$ = new Subject<void>();
  private autosaveSubscribed = false;

  readonly steps = WIZARD_STEPS;
  readonly currentStep = signal<WizardStepId>('documents');
  readonly vehicleId = signal<number | null>(null);
  readonly vehicleStatus = signal<VehicleStatus | null>(null);
  readonly isEditMode = signal(false);
  readonly loading = signal(false);
  readonly draftSaving = signal(false);
  readonly publishing = signal(false);
  readonly attemptedSubmit = signal(false);
  readonly lastSavedAt = signal<Date | null>(null);
  readonly gpsAssigned = signal(false);
  readonly gpsDeviceId = signal<number | null>(null);
  readonly assignedTracker = signal<AssignedTrackerInfo | null>(null);
  readonly changingTracker = signal(false);
  readonly unassignedDevices = signal<GpsDevice[]>([]);
  readonly branchOptions = signal<UiSelectOption[]>([]);
  readonly currencyOptions = signal<UiSelectOption[]>([]);
  readonly makeOptions = signal<UiSelectOption[]>(buildDefaultDetailCatalogOptions().makes);
  readonly colorOptions = signal<UiSelectOption[]>(buildDefaultDetailCatalogOptions().colors);
  readonly modelsByMake = signal<Record<string, string[]>>(buildDefaultDetailCatalogOptions().modelsByMake);
  readonly modelOptions = computed(() =>
    modelOptionsForMake(String(this.formValues()['make'] ?? ''), this.modelsByMake())
  );
  readonly uploadedDocuments = signal<VehicleDocument[]>([]);

  readonly documentSlots = signal<DocumentSlotState[]>(
    WIZARD_DOCUMENT_SLOTS.map(s => ({ ...s }))
  );

  readonly vehicleImageSlots = signal<VehicleImageSlotState[]>(
    VEHICLE_IMAGE_ANGLES.map(({ angle, label }) => ({ angle, label }))
  );

  readonly form: FormGroup = this.fb.group({
    name: ['', [requiredTrimmed(), descriptiveVehicleTextValidator(), Validators.maxLength(100)]],
    vehicleCode: [generateVehicleCode()],
    registrationNumber: ['', [requiredTrimmed(), Validators.maxLength(20)]],
    vin: [''],
    year: [String(new Date().getFullYear()), [Validators.required]],
    make: ['', [requiredTrimmed(), descriptiveVehicleTextValidator(), Validators.maxLength(80)]],
    model: ['', [requiredTrimmed(), descriptiveVehicleTextValidator(), Validators.maxLength(80)]],
    color: ['', [requiredTrimmed(), descriptiveVehicleTextValidator(), Validators.maxLength(40)]],
    fuelType: [String(FuelType.Petrol), Validators.required],
    fuelAverage: [null as number | null, [Validators.required, Validators.min(0.1)]],
    engineNo: ['', [requiredTrimmed(), Validators.maxLength(60), Validators.pattern(/^(?=.*\d)[A-Za-z0-9]+$/)]],
    chassisNo: ['', [requiredTrimmed(), Validators.maxLength(60), Validators.pattern(/^(?=.*\d)[A-Za-z0-9]+$/)]],
    purchaseCurrencyCode: ['', Validators.required],
    purchasePrice: [{ value: null as number | null, disabled: true }, [Validators.required, Validators.min(1)]],
    insuranceExpiryDate: [''],
    registrationExpiryDate: [''],
    roadTaxExpiryDate: [''],
    fitnessExpiryDate: [''],
    seatingCapacity: [null as number | null, [Validators.required, Validators.min(1)]],
    branchId: ['', [Validators.required]],
    currentMileage: [0, Validators.min(0)]
  });

  readonly gpsForm: FormGroup = this.fb.group({
    mode: ['new' as GpsWizardMode],
    model: ['teltonika_fmb920'],
    uniqueId: [''],
    simNumber: ['', [Validators.pattern(/^[0-9]*$/)]],
    vendor: ['Teltonika'],
    existingDeviceId: [''],
    deviceName: ['']
  });

  readonly formValues = signal(this.form.getRawValue());

  readonly vinStatus = computed(() => getVinValidationState(this.formValues()?.vin as string | undefined));
  readonly vinValid = computed(() => this.vinStatus() === 'valid');
  readonly previewName = computed(() => String(this.formValues()?.name ?? '').trim() || 'New Vehicle');
  readonly previewPlate = computed(() => String(this.formValues()?.registrationNumber ?? '').trim());
  readonly previewFuelLabel = computed(() => fuelTypeLabel(this.formValues()?.fuelType as FuelType | string | number));
  readonly isDraftVehicle = computed(() => {
    const status = this.vehicleStatus();
    return status == null || status === VehicleStatus.Draft;
  });
  readonly isPublishedVehicle = computed(() => {
    const status = this.vehicleStatus();
    return status != null && status !== VehicleStatus.Draft;
  });
  readonly pageSubtitle = computed(() => {
    if (!this.isEditMode()) {
      return 'Complete all steps to onboard a vehicle into the fleet registry.';
    }
    if (this.isDraftVehicle()) {
      return 'Update this draft and publish when all required details are complete.';
    }
    return 'Update vehicle details and save your changes.';
  });
  readonly summaryValidationErrors = computed(() => {
    if (!this.attemptedSubmit() && this.currentStep() !== 'review') {
      return [];
    }
    return this.validationErrors();
  });

  readonly documentsUploadedCount = computed(() => {
    const docSlots = this.documentSlots().filter(s => slotHasUploadedDocument(s)).length;
    const hasVehicleImage = this.vehicleImageSlots().some(s => slotHasUploadedDocument(s));
    return docSlots + (hasVehicleImage ? 1 : 0);
  });

  readonly primaryVehicleImageUrl = computed(() => {
    const slots = this.vehicleImageSlots();
    const primary = slots.find(s => s.isPrimary && slotHasUploadedDocument(s));
    const fallback = slots.find(s => slotHasUploadedDocument(s));
    const chosen = primary ?? fallback;
    if (!chosen) return null;
    if (chosen.file) return URL.createObjectURL(chosen.file);
    return chosen.fileUrl ?? null;
  });

  readonly vehicleImageHasError = computed(() => {
    if (this.vehicleImageSlots().some(s => slotHasUploadedDocument(s))) return false;
    return this.attemptedSubmit()
      || this.vehicleImageSlots().some(s => s.error === 'At least one vehicle image is required.');
  });

  readonly validationErrors = computed(() => {
    const errors: string[] = [];
    const f = this.formValues();
    if (!f.name?.trim()) errors.push('Vehicle name is required');
    else if (!isDescriptiveVehicleText(f.name)) errors.push(descriptiveVehicleTextError('name'));
    if (!f.registrationNumber?.trim()) errors.push('License plate is required');
    if (getVinValidationState(f.vin) === 'invalid') errors.push('VIN format is invalid');
    if (getVinValidationState(f.vin) === 'incomplete') errors.push('VIN must be 17 characters');
    if (!f.year) errors.push('Year is required');
    if (!f.make?.trim()) errors.push('Make is required');
    else if (!isDescriptiveVehicleText(f.make)) errors.push(descriptiveVehicleTextError('make'));
    if (!f.model?.trim()) errors.push('Model is required');
    else if (!isDescriptiveVehicleText(f.model)) errors.push(descriptiveVehicleTextError('model'));
    if (!f.color?.trim()) errors.push('Color is required');
    else if (!isDescriptiveVehicleText(f.color)) errors.push(descriptiveVehicleTextError('color'));
    if (!f.seatingCapacity || Number(f.seatingCapacity) < 1) errors.push('Seating capacity is required');
    if (!f.fuelAverage || Number(f.fuelAverage) <= 0) errors.push('Fuel economy is required');
    if (!f.engineNo?.trim()) errors.push('Engine number is required');
    if (!f.chassisNo?.trim()) errors.push('Chassis number is required');
    if (!f.purchaseCurrencyCode?.trim()) errors.push('Purchase currency is required');
    if (!f.purchasePrice || Number(f.purchasePrice) <= 0) errors.push('Purchase amount is required');
    if (!f.branchId) errors.push('Branch is required');
    if (!this.vehicleImageSlots().some(s => slotHasUploadedDocument(s))) {
      errors.push('At least one vehicle image is required');
    }
    const slots = this.documentSlots();
    for (const slot of slots.filter(s => s.required)) {
      if (slot.uploading) continue;
      const msg = documentContinueMessage(slot);
      if (msg) errors.push(msg);
    }
    return errors;
  });

  init(editId?: number): void {
    this.syncFormValues();
    this.setupGpsValidationRules();
    this.loadBranches();
    this.loadCurrencies();
    this.loadDetailCatalog();
    this.setupMakeModelRules();
    this.setupPurchaseCurrencyRules();
    this.loadUnassignedDevices();

    if (editId) {
      this.isEditMode.set(true);
      this.vehicleId.set(editId);
      this.loading.set(true);
      // Load vehicle first so company scope / X-Tenant-Id matches before documents hydrate.
      // Parallel forkJoin previously fetched docs under the wrong tenant and left previews empty.
      this.vehicleService.getById(editId).pipe(
        switchMap(vehicle => {
          if (vehicle.companyId) {
            this.companyScope?.syncTo(vehicle.companyId, vehicle.companyName ?? undefined);
          }
          this.prefillVehicle(vehicle);
          return forkJoin({
            documents: this.vehicleService.getDocuments(editId).pipe(catchError(() => of([] as VehicleDocument[]))),
            gps: this.vehicleService.getGps(editId).pipe(catchError(() => of(null as VehicleGps | null)))
          });
        }),
        takeUntil(this.destroy$)
      ).subscribe({
        next: ({ documents, gps }) => {
          this.uploadedDocuments.set(documents);
          this.syncDocumentSlots(documents);
          if (gps?.gpsDeviceId) {
            this.applyAssignedGps(gps);
            // Keep current assignment — do not put the assigned id into Unassigned Device select.
            this.gpsForm.patchValue({ mode: 'skip', existingDeviceId: '' });
            this.changingTracker.set(false);
          }
          this.currentStep.set(this.resolveInitialStep());
          this.syncFormValuesFromForm();
          this.loading.set(false);
          this.setupAutosave();
        },
        error: () => {
          this.loading.set(false);
          this.toast.error('Failed to load vehicle');
        }
      });
    } else {
      this.loading.set(true);
      void this.ensureDraft()
        .then(() => this.setupAutosave())
        .catch(() => this.toast.error('Failed to create vehicle draft. Please refresh and try again.'))
        .finally(() => this.loading.set(false));
    }
  }

  destroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  goToStep(step: string): void {
    const stepId = step as WizardStepId;
    if (this.isEditMode()) {
      this.currentStep.set(stepId);
      return;
    }

    const currentIdx = this.steps.findIndex(s => s.id === this.currentStep());
    const targetIdx = this.steps.findIndex(s => s.id === stepId);
    if (targetIdx <= currentIdx) {
      this.currentStep.set(stepId);
      return;
    }
    if (targetIdx > currentIdx + 1) {
      return;
    }
    if (!this.isCurrentStepValid()) {
      this.markCurrentStepTouched();
      return;
    }
    this.currentStep.set(stepId);
  }

  nextStep(): void {
    if (!this.isCurrentStepValid()) {
      this.markCurrentStepTouched();
      return;
    }
    const idx = this.steps.findIndex(s => s.id === this.currentStep());
    if (idx < this.steps.length - 1) {
      this.currentStep.set(this.steps[idx + 1].id);
    }
  }

  prevStep(): void {
    const idx = this.steps.findIndex(s => s.id === this.currentStep());
    if (idx > 0) {
      this.currentStep.set(this.steps[idx - 1].id);
    }
  }

  isCurrentStepValid(): boolean {
    return this.isStepValid(this.currentStep());
  }

  private isStepValid(step: WizardStepId): boolean {
    if (step === 'details') {
      return this.controlsValid(['name', 'registrationNumber', 'vin', 'year', 'make', 'model', 'color']);
    }
    if (step === 'technical') {
      return this.controlsValid([
        'fuelType',
        'fuelAverage',
        'seatingCapacity',
        'engineNo',
        'chassisNo',
        'purchaseCurrencyCode',
        'purchasePrice',
        'branchId',
        'currentMileage'
      ]);
    }
    if (step === 'gps') {
      // Assigned + not changing: Current Tracker card only — always valid.
      if (this.assignedTracker() && !this.changingTracker()) return true;
      const mode = this.gpsForm.get('mode')?.value as GpsWizardMode;
      if (mode === 'skip') return true;
      if (mode === 'existing') return !!this.gpsForm.get('existingDeviceId')?.valid;
      return this.gpsControlsValid(['uniqueId', 'model', 'vendor', 'simNumber']);
    }
    if (step === 'documents') {
      return this.requiredDocumentSlotsValid();
    }
    return true;
  }

  async saveDraft(): Promise<void> {
    if (this.isEditMode() && this.isPublishedVehicle()) {
      await this.saveChanges({ navigate: false });
      return;
    }
    try {
      await this.persistVehicle(true);
    } catch (err) {
      this.handleSaveError(err);
    }
  }

  async saveChanges(options: { navigate?: boolean } = { navigate: true }): Promise<void> {
    const id = this.vehicleId();
    if (!id) return;

    this.attemptedSubmit.set(true);
    if (this.validationErrors().length) {
      this.form.markAllAsTouched();
      this.gpsForm.markAllAsTouched();
      this.currentStep.set('review');
      this.toast.error('Please resolve validation errors before saving.');
      return;
    }

    this.publishing.set(true);
    try {
      await this.persistVehicle(false);
      await this.syncExpiryDocuments(id);
      this.toast.success('Vehicle updated successfully');
      if (options.navigate !== false) {
        this.router.navigate(this.isEditMode() ? ['/vehicles'] : ['/vehicles', id]);
      }
    } catch (err) {
      this.handleSaveError(err);
    } finally {
      this.publishing.set(false);
    }
  }

  regenerateCode(): void {
    this.form.patchValue({ vehicleCode: generateVehicleCode() });
  }

  async handleGpsStep(): Promise<boolean> {
    const mode = this.gpsForm.get('mode')?.value as GpsWizardMode;
    // Keep current assignment (initial view or "Keep current tracker" after Change Tracker).
    if (mode === 'skip') {
      this.changingTracker.set(false);
      return true;
    }

    const vehicleId = this.vehicleId();
    if (!vehicleId) return false;

    try {
      if (mode === 'existing') {
        const deviceId = Number(this.gpsForm.get('existingDeviceId')?.value);
        if (!Number.isFinite(deviceId) || deviceId <= 0) return false;
        await firstValueFrom(this.vehicleService.assignGps(vehicleId, { gpsDeviceId: deviceId }));
        await this.refreshAssignedGps(vehicleId);
        this.changingTracker.set(false);
        this.gpsForm.patchValue({ mode: 'skip', existingDeviceId: '' });
        this.loadUnassignedDevices();
        return true;
      }

      const g = this.gpsForm.value;
      const catalog = resolveWizardTrackerModel(String(g.model ?? ''));
      const uniqueId = String(g.uniqueId ?? '').trim();
      const name = g.deviceName?.trim() || catalog.label;
      const registered = await firstValueFrom(this.gpsService.registerTracker({
        name,
        uniqueId,
        category: 'car',
        trackerModelId: 0,
        trackerModelKey: catalog.key,
        phone: g.simNumber?.trim() || undefined,
        vendor: g.vendor?.trim() || catalog.vendor,
        supportsEngineCutoff: false,
        vehicleId
      }));

      const deviceId = typeof registered === 'number'
        ? registered
        : Number((registered as { id?: number })?.id);
      if (!Number.isFinite(deviceId) || deviceId <= 0) {
        this.toast.error('Tracker registered but no device id was returned.');
        return false;
      }

      // Register may already link vehicleId; assign-gps is idempotent insurance when not linked.
      if (!this.gpsAssigned() || this.gpsDeviceId() !== deviceId) {
        await firstValueFrom(this.vehicleService.assignGps(vehicleId, { gpsDeviceId: deviceId }));
      }
      await this.refreshAssignedGps(vehicleId);
      this.changingTracker.set(false);
      this.gpsForm.patchValue({ mode: 'skip', existingDeviceId: '' });
      this.loadUnassignedDevices();
      return true;
    } catch (err) {
      this.toast.error(apiErrorMessage(err, 'Failed to assign GPS tracker'));
      return false;
    }
  }

  startChangingTracker(): void {
    this.changingTracker.set(true);
    this.gpsForm.patchValue({ mode: 'existing', existingDeviceId: '' });
    this.applyGpsModeValidators();
  }

  cancelChangingTracker(): void {
    this.changingTracker.set(false);
    this.gpsForm.patchValue({ mode: 'skip', existingDeviceId: '' });
    this.applyGpsModeValidators();
  }

  private applyAssignedGps(gps: VehicleGps): void {
    const id = gps.gpsDeviceId;
    if (!id) {
      this.gpsAssigned.set(false);
      this.gpsDeviceId.set(null);
      this.assignedTracker.set(null);
      return;
    }
    this.gpsAssigned.set(true);
    this.gpsDeviceId.set(id);
    this.assignedTracker.set({
      gpsDeviceId: id,
      deviceName: gps.deviceName,
      brandName: gps.brandName,
      modelName: gps.modelName,
      uniqueId: gps.uniqueId,
      gpsOnline: gps.gpsOnline
    });
  }

  private async refreshAssignedGps(vehicleId: number): Promise<void> {
    try {
      const gps = await firstValueFrom(
        this.vehicleService.getGps(vehicleId).pipe(catchError(() => of(null)))
      );
      if (gps?.gpsDeviceId) {
        this.applyAssignedGps(gps);
      } else {
        this.gpsAssigned.set(!!this.gpsDeviceId());
      }
    } catch {
      this.gpsAssigned.set(!!this.gpsDeviceId());
    }
  }

  async uploadVehicleImage(slotIndex: number, file: File): Promise<void> {
    const vehicleId = this.vehicleId();
    if (!vehicleId) {
      this.toast.error('Vehicle draft is still being created. Please wait a moment and try again.');
      return;
    }

    const sizeError = vehicleUploadSizeError(file);
    const slots = [...this.vehicleImageSlots()];
    if (sizeError) {
      slots[slotIndex] = { ...slots[slotIndex], error: sizeError, uploading: false };
      this.vehicleImageSlots.set(slots);
      return;
    }

    slots[slotIndex] = { ...slots[slotIndex], file, uploading: true, error: undefined };
    this.vehicleImageSlots.set(slots);

    try {
      const rawResult = await firstValueFrom(this.vehicleService.uploadDocument(
        vehicleId,
        file,
        'VehicleImage',
        undefined,
        slots[slotIndex].angle
      ));

      const result = unwrapUploadVehicleDocumentResult(rawResult);
      const updated = [...this.vehicleImageSlots()];
      const wasPrimary = updated[slotIndex].isPrimary;
      const hasPrimary = updated.some(
        s => s.isPrimary && slotHasUploadedDocument(s) && s.angle !== updated[slotIndex].angle
      );
      const documentId = readVehicleDocumentId(result);
      const fileUrl = readVehicleFileUrl(result) ?? updated[slotIndex].fileUrl;
      updated[slotIndex] = {
        ...updated[slotIndex],
        // Keep local file until a remote URL is confirmed so the preview does not flash empty.
        file: fileUrl ? undefined : updated[slotIndex].file,
        fileUrl,
        documentId: documentId ?? updated[slotIndex].documentId,
        uploading: false,
        error: undefined,
        isPrimary: wasPrimary || !hasPrimary
      };
      this.vehicleImageSlots.set(this.stripVehicleImageRequiredErrors(updated));

      // Always rehydrate from the API so edit/clarify previews match persisted rows.
      await this.refreshVehicleImageSlots(vehicleId);
    } catch (err) {
      const updated = [...this.vehicleImageSlots()];
      updated[slotIndex] = {
        ...updated[slotIndex],
        uploading: false,
        error: apiErrorMessage(err, 'Upload failed')
      };
      this.vehicleImageSlots.set(updated);
    }
  }

  setVehicleImageSlotError(slotIndex: number, message: string): void {
    const slots = [...this.vehicleImageSlots()];
    slots[slotIndex] = { ...slots[slotIndex], error: message, uploading: false };
    this.vehicleImageSlots.set(slots);
  }

  async selectPrimaryVehicleImage(slotIndex: number): Promise<void> {
    const vehicleId = this.vehicleId();
    const slots = [...this.vehicleImageSlots()];
    const slot = slots[slotIndex];
    const documentId = readVehicleDocumentId(slot);
    if (!vehicleId || !documentId || !slotHasUploadedDocument(slot)) {
      this.toast.warning('Save the image first, then set it as the display photo.');
      return;
    }

    const previous = slots.map(s => ({ ...s }));
    const optimistic = slots.map((s, i) => ({ ...s, isPrimary: i === slotIndex, error: undefined }));
    this.vehicleImageSlots.set(optimistic);

    try {
      await firstValueFrom(this.vehicleService.setPrimaryVehicleImage(vehicleId, documentId));
      this.toast.success('Display photo updated');
    } catch (err) {
      this.vehicleImageSlots.set(previous);
      this.toast.error(apiErrorMessage(err, 'Failed to set display photo'));
    }
  }

  async uploadDocumentSlot(slotIndex: number, file: File): Promise<void> {
    const vehicleId = this.vehicleId();
    if (!vehicleId) {
      this.toast.error('Vehicle draft is still being created. Please wait a moment and try again.');
      return;
    }

    const sizeError = vehicleUploadSizeError(file);
    const slots = [...this.documentSlots()];
    if (sizeError) {
      slots[slotIndex] = { ...slots[slotIndex], error: sizeError, uploading: false };
      this.documentSlots.set(slots);
      return;
    }

    slots[slotIndex] = { ...slots[slotIndex], file, uploading: true, error: undefined };
    this.documentSlots.set(slots);

    try {
      const rawResult = await firstValueFrom(this.vehicleService.uploadDocument(
        vehicleId,
        file,
        slots[slotIndex].documentType
      ));

      const result = unwrapUploadVehicleDocumentResult(rawResult);
      const updated = [...this.documentSlots()];
      const fileUrl = readVehicleFileUrl(result) ?? updated[slotIndex].fileUrl;

      updated[slotIndex] = {
        ...updated[slotIndex],
        file: fileUrl ? undefined : updated[slotIndex].file,
        fileUrl,
        documentId: readVehicleDocumentId(result) ?? updated[slotIndex].documentId,
        uploading: false,
        error: undefined
      };
      this.documentSlots.set(updated);
      await this.refreshVehicleImageSlots(vehicleId);
    } catch (err) {
      const updated = [...this.documentSlots()];
      updated[slotIndex] = {
        ...updated[slotIndex],
        uploading: false,
        fileUrl: undefined,
        documentId: undefined,
        error: apiErrorMessage(err, 'Upload failed')
      };
      this.documentSlots.set(updated);
    }
  }

  async publish(): Promise<void> {
    const id = this.vehicleId();
    this.attemptedSubmit.set(true);
    if (!id || this.validationErrors().length) {
      this.form.markAllAsTouched();
      this.gpsForm.markAllAsTouched();
      this.currentStep.set('review');
      this.toast.error('Please resolve validation errors before publishing.');
      return;
    }

    this.publishing.set(true);
    try {
      await this.persistVehicle(false);
      await this.syncExpiryDocuments(id);
      await firstValueFrom(this.vehicleService.publish(id));
      this.toast.success('Vehicle published successfully');
      this.router.navigate(['/vehicles']);
    } catch (err) {
      const message = apiErrorMessage(err, 'Failed to publish vehicle');
      if (message.toLowerCase().includes('registration') || message.toLowerCase().includes('license')) {
        this.form.get('registrationNumber')?.setErrors({ conflict: true });
        this.form.get('registrationNumber')?.markAsTouched();
        this.currentStep.set('details');
      }
      this.toast.error(message);
    } finally {
      this.publishing.set(false);
    }
  }

  cancel(): void {
    this.router.navigate(['/vehicles']);
  }

  /**
   * Best-effort delete of the auto-created draft (create mode only) — used when a Super Admin switches
   * company before publishing, so no orphan draft is left in the previous company.
   */
  async discardNewDraft(): Promise<void> {
    const id = this.vehicleId();
    if (!id || this.isEditMode()) return;
    try {
      await firstValueFrom(this.vehicleService.delete(id));
    } catch {
      /* draft stays listed under Drafts; not worth blocking the switch */
    }
    this.vehicleId.set(null);
  }

  /**
   * Super Admin changed company on an existing vehicle: clear org fields that belong to the old
   * tenant, reload company-scoped lookups, and persist CompanyId immediately.
   */
  async applyCompanyChange(companyId: number): Promise<void> {
    if (!this.isEditMode() || !(companyId > 0)) return;
    this.form.patchValue({ branchId: null });
    this.loadBranches();
    this.loadUnassignedDevices();
    try {
      await this.persistVehicle(false);
      const vehicleId = this.vehicleId();
      if (vehicleId) {
        // Rehydrate docs under the new X-Tenant-Id so save validation still sees uploaded files.
        await this.refreshVehicleImageSlots(vehicleId);
      }
      this.toast.success('Vehicle moved to the selected company');
    } catch (err) {
      this.toast.error(apiErrorMessage(err, 'Failed to change vehicle company'));
    }
  }

  primaryCtaLabel(): string {
    const step = this.currentStep();
    if (step === 'review') {
      if (this.isEditMode() && this.isPublishedVehicle()) {
        return 'Save Changes';
      }
      if (this.isEditMode() || this.vehicleId()) {
        return 'Publish Vehicle';
      }
      return this.gpsAssigned() ? 'Publish Vehicle' : 'Create & Assign Tracker';
    }
    if (step === 'gps') return 'Continue';
    return 'Continue';
  }

  saveDraftLabel(): string {
    if (this.isEditMode() && this.isPublishedVehicle()) {
      return this.currentStep() === 'review' ? 'Save' : 'Save Progress';
    }
    return 'Save Draft';
  }

  async handlePrimaryAction(): Promise<void> {
    const step = this.currentStep();
    if (step === 'gps') {
      const ok = await this.handleGpsStep();
      if (ok) this.nextStep();
      return;
    }
    if (step === 'review') {
      if (this.isEditMode() && this.isPublishedVehicle()) {
        await this.saveChanges();
      } else {
        await this.publish();
      }
      return;
    }
    if (!this.isCurrentStepValid()) {
      this.markCurrentStepTouched();
      return;
    }
    this.nextStep();
  }

  private loadBranches(): void {
    this.platformService.getBranches().pipe(
      catchError(err => {
        this.toast.error(apiErrorMessage(err, 'Failed to load branches'));
        return of(null);
      })
    ).subscribe(branches => {
      if (branches == null) {
        this.branchOptions.set([]);
        return;
      }
      const active = branches.filter(b => b.isActive !== false);
      this.branchOptions.set(active.map(b => ({ value: String(b.id), label: b.name })));
      if (active.length === 0) {
        this.toast.info('No branches configured — create one under Platform → Branches.');
      }
    });
  }

  private loadCurrencies(): void {
    this.lookupService.getCurrencies().pipe(catchError(() => of([]))).subscribe(currencies => {
      this.currencyOptions.set(
        currencies.map(c => ({ value: c.code, label: `${c.code} — ${c.name}` }))
      );
    });
  }

  private loadDetailCatalog(): void {
    const defaults = buildDefaultDetailCatalogOptions();
    this.makeOptions.set(defaults.makes);
    this.colorOptions.set(defaults.colors);
    this.modelsByMake.set(defaults.modelsByMake);

    this.vehicleService.getAll(1, 500).pipe(catchError(() => of({ items: [] }))).subscribe(result => {
      const makes = new Set(defaults.makes.map(o => o.value));
      const colors = new Set(defaults.colors.map(o => o.value));
      let modelsByMake = { ...defaults.modelsByMake };

      for (const vehicle of result.items) {
        if (vehicle.make?.trim()) makes.add(vehicle.make.trim());
        if (vehicle.make?.trim() && vehicle.model?.trim()) {
          modelsByMake = mergeModelsByMake(modelsByMake, vehicle.make, vehicle.model);
        }
      }

      this.makeOptions.set(toSelectOptions(makes));
      this.colorOptions.set(toSelectOptions(colors));
      this.modelsByMake.set(modelsByMake);
      this.ensureDetailCatalogIncludesFormValues();
    });
  }

  private ensureDetailCatalogIncludesFormValues(): void {
    const values = this.form.getRawValue() as Record<string, unknown>;
    const make = String(values['make'] ?? '').trim();
    const model = String(values['model'] ?? '').trim();
    const color = String(values['color'] ?? '').trim();

    if (make) this.makeOptions.update(opts => withSelectOption(opts, make));
    if (color) this.colorOptions.update(opts => withSelectOption(opts, color));
    if (make && model) {
      this.modelsByMake.update(map => mergeModelsByMake(map, make, model));
    }
  }

  private setupMakeModelRules(): void {
    const makeControl = this.form.get('make');
    const modelControl = this.form.get('model');
    if (!makeControl || !modelControl) return;

    makeControl.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(make => {
      const selectedMake = String(make ?? '').trim();
      const currentModel = String(modelControl.value ?? '').trim();
      if (!currentModel) return;

      const allowed = modelOptionsForMake(selectedMake, this.modelsByMake()).map(o => o.value);
      if (!allowed.includes(currentModel)) {
        modelControl.reset('', { emitEvent: false });
        this.syncFormValuesFromForm();
      }
    });
  }

  private setupPurchaseCurrencyRules(): void {
    const currencyControl = this.form.get('purchaseCurrencyCode');
    const priceControl = this.form.get('purchasePrice');
    if (!currencyControl || !priceControl) return;

    const syncPriceEnabled = (code: string | null | undefined) => {
      if (code?.trim()) {
        priceControl.enable({ emitEvent: false });
      } else {
        priceControl.reset(null, { emitEvent: false });
        priceControl.disable({ emitEvent: false });
      }
    };

    syncPriceEnabled(currencyControl.value);
    currencyControl.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(syncPriceEnabled);
  }

  private loadUnassignedDevices(): void {
    this.gpsService.getDevices().pipe(catchError(() => of([]))).subscribe(devices => {
      this.unassignedDevices.set(devices.filter(d => !d.vehicleId && d.isActive));
    });
  }

  private prefillVehicle(v: Vehicle): void {
    this.vehicleStatus.set(normalizeVehicleStatus(v.status));
    this.form.patchValue({
      name: v.name,
      vehicleCode: v.vehicleCode || generateVehicleCode(),
      registrationNumber: v.registrationNumber,
      vin: v.vin || '',
      year: v.year ? String(v.year) : '',
      make: v.make || '',
      model: v.model || '',
      color: v.color || '',
      fuelType: String(normalizeFuelType(v.fuelType)),
      fuelAverage: v.fuelAverage,
      engineNo: v.engineNo || '',
      chassisNo: v.chassisNo || '',
      purchaseCurrencyCode: v.purchaseCurrencyCode || '',
      purchasePrice: v.purchasePrice,
      insuranceExpiryDate: toDateInputValue(v.insuranceExpiryDate),
      seatingCapacity: v.seatingCapacity,
      branchId: v.branchId ? String(v.branchId) : '',
      currentMileage: v.currentMileage
    });
    this.syncFormValuesFromForm();
    this.ensureDetailCatalogIncludesFormValues();
  }

  private syncDocumentSlots(documents: VehicleDocument[]): void {
    // Empty payloads usually mean a failed/tenant-mismatched fetch — do not wipe previews.
    if (!documents.length) return;

    const { imageSlots, documentSlots } = hydrateVehicleDocumentSlots(documents, this.documentSlots());
    this.vehicleImageSlots.set(this.stripVehicleImageRequiredErrors(imageSlots));
    this.documentSlots.set(documentSlots);

    const regDoc = documents.find(d =>
      d.documentType?.toLowerCase() === 'registration' && (!!d.fileUrl?.trim() || readVehicleDocumentId(d) != null)
    );
    const roadDoc = documents.find(d =>
      d.documentType?.toLowerCase() === 'roadtax' && (!!d.fileUrl?.trim() || readVehicleDocumentId(d) != null)
    );
    const fitDoc = documents.find(d =>
      d.documentType?.toLowerCase() === 'fitness' && (!!d.fileUrl?.trim() || readVehicleDocumentId(d) != null)
    );
    this.form.patchValue({
      registrationExpiryDate: toDateInputValue(regDoc?.expiryDate),
      roadTaxExpiryDate: toDateInputValue(roadDoc?.expiryDate),
      fitnessExpiryDate: toDateInputValue(fitDoc?.expiryDate)
    });
    this.syncFormValuesFromForm();
  }

  private async ensureDraft(): Promise<void> {
    if (this.vehicleId()) return;
    const dto = this.buildDto();
    const id = await firstValueFrom(this.vehicleService.createDraft(dto));
    if (!id) {
      throw new Error('Failed to create vehicle draft');
    }
    this.vehicleId.set(id);
    this.vehicleStatus.set(VehicleStatus.Draft);
  }

  private setupAutosave(): void {
    if (this.autosaveSubscribed) return;
    this.autosaveSubscribed = true;

    this.form.valueChanges.pipe(
      debounceTime(2000),
      distinctUntilChanged(),
      filter(() => !!this.vehicleId() && this.canAutosaveDraft()),
      tap(() => this.draftSaving.set(true)),
      switchMap(() => this.persistVehicle(false).then(() => true).catch(() => false)),
      takeUntil(this.destroy$)
    ).subscribe(() => this.draftSaving.set(false));
  }

  private async persistVehicle(showToast: boolean): Promise<void> {
    const id = this.vehicleId();
    const dto = this.buildDto();

    try {
      if (!id) {
        const newId = await firstValueFrom(this.vehicleService.createDraft(dto));
        if (newId) {
          this.vehicleId.set(newId);
          this.vehicleStatus.set(VehicleStatus.Draft);
        }
      } else {
        const status = normalizeVehicleStatus(this.vehicleStatus(), VehicleStatus.Draft);
        const companyId = this.companyScopeService.tenantId();
        const updateDto: UpdateVehicleDto = {
          ...dto,
          status,
          ...(companyId != null && companyId > 0 ? { companyId } : {})
        };
        await firstValueFrom(this.vehicleService.update({ id, vehicle: updateDto }));
      }
      this.lastSavedAt.set(new Date());
      if (showToast) {
        const message = this.isPublishedVehicle() ? 'Changes saved' : 'Draft saved';
        this.toast.success(message);
      }
    } catch (err) {
      const message = apiErrorMessage(err, 'Failed to save vehicle');
      if (showToast) this.toast.error(message);
      throw new Error(message);
    }
  }

  private buildDto(): CreateVehicleDto {
    const f = this.form.getRawValue();
    return sanitizeCreateVehicleDto({
      name: f.name,
      registrationNumber: f.registrationNumber,
      vehicleCode: f.vehicleCode,
      vin: f.vin,
      make: f.make,
      model: f.model,
      year: f.year,
      color: f.color,
      seatingCapacity: f.seatingCapacity,
      fuelAverage: f.fuelAverage,
      fuelType: f.fuelType,
      engineNo: f.engineNo,
      chassisNo: f.chassisNo,
      currentMileage: f.currentMileage,
      insuranceExpiryDate: dateInputToIso(f.insuranceExpiryDate),
      purchasePrice: f.purchasePrice,
      purchaseCurrencyCode: f.purchaseCurrencyCode,
      branchId: f.branchId
    });
  }

  private async syncExpiryDocuments(vehicleId: number): Promise<void> {
    const f = this.form.value;
    const pairs: { type: string; date: string }[] = [
      { type: 'Registration', date: f.registrationExpiryDate },
      { type: 'RoadTax', date: f.roadTaxExpiryDate },
      { type: 'Fitness', date: f.fitnessExpiryDate }
    ];

    for (const { type, date } of pairs) {
      if (!date) continue;
      await firstValueFrom(this.vehicleService.addDocument(vehicleId, {
        documentType: type,
        expiryDate: dateInputToIso(date) ?? undefined
      })).catch(() => undefined);
    }
  }

  private handleSaveError(error: unknown): void {
    const message = apiErrorMessage(error, 'Failed to save vehicle');
    if (message.toLowerCase().includes('registration') || message.toLowerCase().includes('license')) {
      this.form.get('registrationNumber')?.setErrors({ conflict: true });
      this.form.get('registrationNumber')?.markAsTouched();
      this.currentStep.set('details');
    }
    this.toast.error(message);
  }

  private canAutosaveDraft(): boolean {
    const registration = String(this.form.getRawValue().registrationNumber ?? '').trim();
    return registration.length > 0;
  }

  private syncFormValues(): void {
    this.syncFormValuesFromForm();
    this.form.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(values => {
      this.formValues.set(values);
    });
  }

  private syncFormValuesFromForm(): void {
    this.formValues.set(this.form.getRawValue());
  }

  private resolveInitialStep(): WizardStepId {
    if (!this.isEditMode()) {
      return 'details';
    }

    for (const step of this.steps) {
      if (!this.isStepValid(step.id)) {
        return step.id;
      }
    }

    return 'review';
  }

  private setupGpsValidationRules(): void {
    this.form.get('vin')?.setValidators([
      Validators.maxLength(17),
      Validators.pattern(/^[A-HJ-NPR-Z0-9]*$/i),
      vinOptionalValidator()
    ]);

    this.gpsForm.get('mode')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.applyGpsModeValidators());

    this.applyGpsModeValidators();
  }

  private applyGpsModeValidators(): void {
    const mode = this.gpsForm.get('mode')?.value as GpsWizardMode;
    const uniqueId = this.gpsForm.get('uniqueId');
    const existingDeviceId = this.gpsForm.get('existingDeviceId');
    const model = this.gpsForm.get('model');
    const vendor = this.gpsForm.get('vendor');

    uniqueId?.clearValidators();
    existingDeviceId?.clearValidators();
    model?.clearValidators();
    vendor?.clearValidators();

    if (mode === 'new') {
      uniqueId?.setValidators([
        Validators.required,
        Validators.pattern(/^\d{15}$/)
      ]);
      model?.setValidators([Validators.required]);
      vendor?.setValidators([Validators.required]);
    } else if (mode === 'existing') {
      existingDeviceId?.setValidators([Validators.required]);
    }

    uniqueId?.updateValueAndValidity({ emitEvent: false });
    existingDeviceId?.updateValueAndValidity({ emitEvent: false });
    model?.updateValueAndValidity({ emitEvent: false });
    vendor?.updateValueAndValidity({ emitEvent: false });
  }

  private controlsValid(keys: string[]): boolean {
    return keys.every(key => this.form.get(key)?.valid);
  }

  private gpsControlsValid(keys: string[]): boolean {
    return keys.every(key => this.gpsForm.get(key)?.valid);
  }

  private markCurrentStepTouched(): void {
    const step = this.currentStep();
    if (step === 'details') {
      this.markTouched(this.form.get('name'));
      this.markTouched(this.form.get('registrationNumber'));
      this.markTouched(this.form.get('vin'));
      this.markTouched(this.form.get('year'));
      this.markTouched(this.form.get('make'));
      this.markTouched(this.form.get('model'));
      this.markTouched(this.form.get('color'));
      return;
    }
    if (step === 'technical') {
      this.markTouched(this.form.get('fuelType'));
      this.markTouched(this.form.get('fuelAverage'));
      this.markTouched(this.form.get('seatingCapacity'));
      this.markTouched(this.form.get('engineNo'));
      this.markTouched(this.form.get('chassisNo'));
      this.markTouched(this.form.get('purchaseCurrencyCode'));
      this.markTouched(this.form.get('purchasePrice'));
      this.markTouched(this.form.get('branchId'));
      return;
    }
    if (step === 'gps') {
      const mode = this.gpsForm.get('mode')?.value as GpsWizardMode;
      if (mode === 'existing') {
        this.markTouched(this.gpsForm.get('existingDeviceId'));
      } else if (mode === 'new') {
        this.markTouched(this.gpsForm.get('uniqueId'));
        this.markTouched(this.gpsForm.get('model'));
        this.markTouched(this.gpsForm.get('simNumber'));
        this.markTouched(this.gpsForm.get('vendor'));
      }
      return;
    }
    if (step === 'documents') {
      const slots = [...this.documentSlots()];
      let changed = false;
      for (let i = 0; i < slots.length; i++) {
        if (slots[i].uploading) continue;
        const msg = documentContinueMessage(slots[i]);
        if (msg && slots[i].error !== msg) {
          slots[i] = { ...slots[i], error: msg };
          changed = true;
        } else if (!msg && slots[i].error) {
          slots[i] = { ...slots[i], error: undefined };
          changed = true;
        }
      }
      if (changed) {
        this.documentSlots.set(slots);
      }

      if (!this.vehicleImageSlots().some(s => slotHasUploadedDocument(s))) {
        const imageSlots = this.vehicleImageSlots().map(s => ({
          ...s,
          error: s.error === 'At least one vehicle image is required.' ? undefined : s.error
        }));
        imageSlots[0] = { ...imageSlots[0], error: 'At least one vehicle image is required.' };
        this.vehicleImageSlots.set(imageSlots);
      }
    }
  }

  private markTouched(control: AbstractControl | null): void {
    control?.markAsTouched();
    control?.updateValueAndValidity({ emitEvent: false });
  }

  private async refreshVehicleImageSlots(vehicleId: number): Promise<void> {
    try {
      const documents = await firstValueFrom(this.vehicleService.getDocuments(vehicleId));
      this.uploadedDocuments.set(documents);
      this.syncDocumentSlots(documents);
    } catch {
      // Keep current slot previews if reload fails (transient scope/network issues).
    }
  }

  private stripVehicleImageRequiredErrors(slots: VehicleImageSlotState[]): VehicleImageSlotState[] {
    if (!slots.some(s => slotHasUploadedDocument(s))) return slots;
    return slots.map(s =>
      s.error === 'At least one vehicle image is required.' ? { ...s, error: undefined } : s
    );
  }

  private requiredDocumentSlotsValid(): boolean {
    const hasVehicleImage = this.vehicleImageSlots().some(slot => slotHasUploadedDocument(slot));
    const docsValid = this.documentSlots().every(slot => {
      if (!slot.required) return true;
      if (slot.uploading) return false;
      return slotHasUploadedDocument(slot);
    });
    return hasVehicleImage && docsValid;
  }
}

function normalizeCompare(value: string): string {
  return value.trim().toLowerCase().replace(/[\s\-]/g, '');
}

function requiredTrimmed(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;
    if (value == null) return { required: true };
    if (typeof value === 'string' && value.trim().length === 0) return { required: true };
    return null;
  };
}

function vinOptionalValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = (control.value as string | null | undefined)?.trim() ?? '';
    if (!value) return null;
    if (value.length < 17) return { vinIncomplete: true };
    if (!validateVin(value)) return { vinInvalid: true };
    return null;
  };
}
