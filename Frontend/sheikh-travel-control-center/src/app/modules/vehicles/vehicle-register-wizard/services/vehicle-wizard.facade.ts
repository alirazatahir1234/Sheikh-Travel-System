import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { AbstractControl, FormBuilder, FormGroup, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { debounceTime, distinctUntilChanged, filter, switchMap, catchError, of, Subject, takeUntil, forkJoin, tap, firstValueFrom } from 'rxjs';
import { VehicleService } from '../../../../core/services/vehicle.service';
import { GpsTrackingService } from '../../../../core/services/gps-tracking.service';
import { PlatformService } from '../../../../core/services/platform.service';
import {
  CreateVehicleDto,
  FuelType,
  UpdateVehicleDto,
  Vehicle,
  VehicleDocument,
  VehicleStatus,
  UploadVehicleDocumentResult,
  sanitizeCreateVehicleDto,
  normalizeFuelType,
  normalizeVehicleStatus
} from '../../../../core/models/vehicle.model';
import { GpsDevice } from '../../../../core/models/gps-tracking.model';
import { dateInputToIso, toDateInputValue } from '../../../../core/utils/date-input.util';
import { UiSelectOption } from '../../../../shared/components/ui/types/ui.types';
import {
  DocumentSlotState,
  GpsWizardMode,
  getVinValidationState,
  generateVehicleCode,
  fuelTypeLabel,
  validateVin,
  WIZARD_DOCUMENT_SLOTS,
  WizardStepId,
  WIZARD_STEPS,
  VEHICLE_IMAGE_ANGLES,
  VehicleImageSlotState,
  parseVehicleImageAngle,
  isPrimaryVehicleImage
} from '../models/vehicle-wizard.model';

@Injectable()
export class VehicleWizardFacade {
  private readonly fb = inject(FormBuilder);
  private readonly vehicleService = inject(VehicleService);
  private readonly gpsService = inject(GpsTrackingService);
  private readonly platformService = inject(PlatformService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroy$ = new Subject<void>();
  private autosaveSubscribed = false;

  readonly steps = WIZARD_STEPS;
  readonly currentStep = signal<WizardStepId>('details');
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
  readonly unassignedDevices = signal<GpsDevice[]>([]);
  readonly branchOptions = signal<UiSelectOption[]>([]);
  readonly uploadedDocuments = signal<VehicleDocument[]>([]);

  readonly documentSlots = signal<DocumentSlotState[]>(
    WIZARD_DOCUMENT_SLOTS.map(s => ({ ...s }))
  );

  readonly vehicleImageSlots = signal<VehicleImageSlotState[]>(
    VEHICLE_IMAGE_ANGLES.map(({ angle, label }) => ({ angle, label }))
  );

  readonly form: FormGroup = this.fb.group({
    name: ['', [requiredTrimmed(), Validators.maxLength(100)]],
    vehicleCode: [generateVehicleCode()],
    registrationNumber: ['', [requiredTrimmed(), Validators.maxLength(20)]],
    vin: [''],
    year: [String(new Date().getFullYear()), [Validators.required]],
    make: ['', [requiredTrimmed(), Validators.maxLength(80)]],
    model: ['', [requiredTrimmed(), Validators.maxLength(80)]],
    color: ['', [requiredTrimmed(), Validators.maxLength(40)]],
    fuelType: [String(FuelType.Petrol), Validators.required],
    fuelAverage: [null as number | null, [Validators.required, Validators.min(0.1)]],
    engineNo: ['', [requiredTrimmed(), Validators.maxLength(60)]],
    chassisNo: ['', [requiredTrimmed(), Validators.maxLength(60)]],
    purchasePrice: [null as number | null, [Validators.required, Validators.min(1)]],
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
    model: ['Teltonika FMB920'],
    uniqueId: [''],
    simNumber: [''],
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
    const docSlots = this.documentSlots().filter(s => !!s.fileUrl).length;
    const hasVehicleImage = this.vehicleImageSlots().some(s => !!s.fileUrl);
    return docSlots + (hasVehicleImage ? 1 : 0);
  });

  readonly primaryVehicleImageUrl = computed(() => {
    const slots = this.vehicleImageSlots();
    const primary = slots.find(s => s.isPrimary && s.fileUrl);
    const fallback = slots.find(s => s.fileUrl);
    const chosen = primary ?? fallback;
    if (!chosen) return null;
    if (chosen.file) return URL.createObjectURL(chosen.file);
    return chosen.fileUrl ?? null;
  });

  readonly vehicleImageHasError = computed(() =>
    this.vehicleImageSlots().some(s => !!s.error)
  );

  readonly validationErrors = computed(() => {
    const errors: string[] = [];
    const f = this.formValues();
    if (!f.name?.trim()) errors.push('Vehicle name is required');
    if (!f.registrationNumber?.trim()) errors.push('License plate is required');
    if (getVinValidationState(f.vin) === 'invalid') errors.push('VIN format is invalid');
    if (getVinValidationState(f.vin) === 'incomplete') errors.push('VIN must be 17 characters');
    if (!f.year) errors.push('Year is required');
    if (!f.make?.trim()) errors.push('Make is required');
    if (!f.model?.trim()) errors.push('Model is required');
    if (!f.color?.trim()) errors.push('Color is required');
    if (!f.seatingCapacity || Number(f.seatingCapacity) < 1) errors.push('Seating capacity is required');
    if (!f.fuelAverage || Number(f.fuelAverage) <= 0) errors.push('Fuel economy is required');
    if (!f.engineNo?.trim()) errors.push('Engine number is required');
    if (!f.chassisNo?.trim()) errors.push('Chassis number is required');
    if (!f.purchasePrice || Number(f.purchasePrice) <= 0) errors.push('Purchase price is required');
    if (!f.branchId) errors.push('Branch is required');
    if (!this.vehicleImageSlots().some(s => !!s.fileUrl)) {
      errors.push('At least one vehicle image is required');
    }
    const slots = this.documentSlots();
    for (const slot of slots.filter(s => s.required)) {
      if (!slot.fileUrl) {
        errors.push(`${slot.label} is required`);
      }
    }
    return errors;
  });

  init(editId?: number): void {
    this.syncFormValues();
    this.setupGpsValidationRules();
    this.loadBranches();
    this.loadUnassignedDevices();

    if (editId) {
      this.isEditMode.set(true);
      this.vehicleId.set(editId);
      this.loading.set(true);
      forkJoin({
        vehicle: this.vehicleService.getById(editId),
        documents: this.vehicleService.getDocuments(editId).pipe(catchError(() => of([]))),
        gps: this.vehicleService.getGps(editId).pipe(catchError(() => of(null)))
      }).subscribe({
        next: ({ vehicle, documents, gps }) => {
          this.prefillVehicle(vehicle);
          this.uploadedDocuments.set(documents);
          this.syncDocumentSlots(documents);
          if (gps?.gpsDeviceId) {
            this.gpsAssigned.set(true);
            this.gpsDeviceId.set(gps.gpsDeviceId);
            this.gpsForm.patchValue({ mode: 'existing', existingDeviceId: String(gps.gpsDeviceId) });
          }
          this.currentStep.set(this.resolveInitialStep());
          this.syncFormValuesFromForm();
          this.loading.set(false);
          this.setupAutosave();
        },
        error: () => {
          this.loading.set(false);
          this.snackBar.open('Failed to load vehicle', 'Close', { duration: 3000 });
        }
      });
    } else {
      this.ensureDraft().then(() => this.setupAutosave());
    }
  }

  destroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  goToStep(step: WizardStepId): void {
    if (this.isEditMode()) {
      this.currentStep.set(step);
      return;
    }

    const currentIdx = this.steps.findIndex(s => s.id === this.currentStep());
    const targetIdx = this.steps.findIndex(s => s.id === step);
    if (targetIdx <= currentIdx) {
      this.currentStep.set(step);
      return;
    }
    if (targetIdx > currentIdx + 1) {
      return;
    }
    if (!this.isCurrentStepValid()) {
      this.markCurrentStepTouched();
      return;
    }
    this.currentStep.set(step);
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
        'purchasePrice',
        'branchId',
        'currentMileage'
      ]);
    }
    if (step === 'gps') {
      const mode = this.gpsForm.get('mode')?.value as GpsWizardMode;
      if (mode === 'skip') return true;
      if (mode === 'existing') return !!this.gpsForm.get('existingDeviceId')?.valid;
      return this.gpsControlsValid(['uniqueId', 'model', 'vendor']);
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
      this.snackBar.open('Please resolve validation errors before saving.', 'Close', { duration: 3500 });
      return;
    }

    this.publishing.set(true);
    try {
      await this.persistVehicle(false);
      await this.syncExpiryDocuments(id);
      this.snackBar.open('Vehicle updated successfully', 'Close', { duration: 3000 });
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
    if (mode === 'skip' || this.gpsAssigned()) return true;

    const vehicleId = this.vehicleId();
    if (!vehicleId) return false;

    try {
      if (mode === 'existing') {
        const deviceId = Number(this.gpsForm.get('existingDeviceId')?.value);
        await firstValueFrom(this.vehicleService.assignGps(vehicleId, { gpsDeviceId: deviceId }));
        this.gpsAssigned.set(true);
        this.gpsDeviceId.set(deviceId);
        return true;
      }

      const g = this.gpsForm.value;
      const name = g.deviceName?.trim() || `Tracker ${g.uniqueId}`;
      const deviceId = await firstValueFrom(this.gpsService.createDevice({
        uniqueId: g.uniqueId.trim(),
        name,
        model: g.model,
        simNumber: g.simNumber?.trim() || undefined,
        vendor: g.vendor,
        supportsEngineCutoff: false,
        isActive: true
      }));

      if (deviceId) {
        await firstValueFrom(this.vehicleService.assignGps(vehicleId, { gpsDeviceId: deviceId }));
        this.gpsAssigned.set(true);
        this.gpsDeviceId.set(deviceId);
      }
      return true;
    } catch {
      this.snackBar.open('Failed to assign GPS tracker', 'Close', { duration: 4000 });
      return false;
    }
  }

  async uploadVehicleImage(slotIndex: number, file: File): Promise<void> {
    const vehicleId = this.vehicleId();
    if (!vehicleId) return;

    const slots = [...this.vehicleImageSlots()];
    slots[slotIndex] = { ...slots[slotIndex], file, uploading: true, error: undefined };
    this.vehicleImageSlots.set(slots);

    try {
      const result = await firstValueFrom(this.vehicleService.uploadDocument(
        vehicleId,
        file,
        'VehicleImage',
        undefined,
        slots[slotIndex].angle
      ));

      const updated = [...this.vehicleImageSlots()];
      const wasPrimary = updated[slotIndex].isPrimary;
      const hasPrimary = updated.some(s => s.isPrimary && !!s.fileUrl && s.angle !== updated[slotIndex].angle);
      const documentId = this.extractDocumentId(result);
      updated[slotIndex] = {
        ...updated[slotIndex],
        file: undefined,
        fileUrl: this.readFileUrl(result) ?? updated[slotIndex].fileUrl,
        documentId: documentId ?? updated[slotIndex].documentId,
        uploading: false,
        error: undefined,
        isPrimary: wasPrimary || !hasPrimary
      };
      this.vehicleImageSlots.set(updated);

      if (!documentId) {
        await this.refreshVehicleImageSlots(vehicleId);
      }
    } catch {
      const updated = [...this.vehicleImageSlots()];
      updated[slotIndex] = { ...updated[slotIndex], uploading: false, error: 'Upload failed' };
      this.vehicleImageSlots.set(updated);
    }
  }

  async selectPrimaryVehicleImage(slotIndex: number): Promise<void> {
    const vehicleId = this.vehicleId();
    const slots = [...this.vehicleImageSlots()];
    const slot = slots[slotIndex];
    const documentId = this.readDocumentId(slot);
    if (!vehicleId || !documentId || !slot?.fileUrl) {
      this.snackBar.open('Save the image first, then set it as the display photo.', 'Close', { duration: 3500 });
      return;
    }

    const previous = slots.map(s => ({ ...s }));
    const optimistic = slots.map((s, i) => ({ ...s, isPrimary: i === slotIndex, error: undefined }));
    this.vehicleImageSlots.set(optimistic);

    try {
      await firstValueFrom(this.vehicleService.setPrimaryVehicleImage(vehicleId, documentId));
      this.snackBar.open('Display photo updated', 'Close', { duration: 2500 });
    } catch (err) {
      this.vehicleImageSlots.set(previous);
      this.snackBar.open(this.extractErrorMessage(err, 'Failed to set display photo'), 'Close', { duration: 4000 });
    }
  }

  async uploadDocumentSlot(slotIndex: number, file: File): Promise<void> {
    const vehicleId = this.vehicleId();
    if (!vehicleId) return;

    const slots = [...this.documentSlots()];
    slots[slotIndex] = { ...slots[slotIndex], file, uploading: true, error: undefined };
    this.documentSlots.set(slots);

    try {
      const result = await firstValueFrom(this.vehicleService.uploadDocument(
        vehicleId,
        file,
        slots[slotIndex].documentType
      ));

      const updated = [...this.documentSlots()];
      updated[slotIndex] = {
        ...updated[slotIndex],
        file: undefined,
        fileUrl: this.readFileUrl(result) ?? updated[slotIndex].fileUrl,
        documentId: this.extractDocumentId(result) ?? updated[slotIndex].documentId,
        uploading: false,
        error: undefined
      };
      this.documentSlots.set(updated);
    } catch {
      const updated = [...this.documentSlots()];
      updated[slotIndex] = { ...updated[slotIndex], uploading: false, error: 'Upload failed' };
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
      this.snackBar.open('Please resolve validation errors before publishing.', 'Close', { duration: 3500 });
      return;
    }

    this.publishing.set(true);
    try {
      await this.persistVehicle(false);
      await this.syncExpiryDocuments(id);
      await firstValueFrom(this.vehicleService.publish(id));
      this.snackBar.open('Vehicle published successfully', 'Close', { duration: 3000 });
      this.router.navigate(['/vehicles']);
    } catch (err) {
      const message = this.extractErrorMessage(err, 'Failed to publish vehicle');
      if (message.toLowerCase().includes('registration') || message.toLowerCase().includes('license')) {
        this.form.get('registrationNumber')?.setErrors({ conflict: true });
        this.form.get('registrationNumber')?.markAsTouched();
        this.currentStep.set('details');
      }
      this.snackBar.open(message, 'Close', { duration: 4500 });
    } finally {
      this.publishing.set(false);
    }
  }

  cancel(): void {
    this.router.navigate(['/vehicles']);
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
    this.platformService.getBranches().pipe(catchError(() => of([]))).subscribe(branches => {
      this.branchOptions.set(branches.map(b => ({ value: String(b.id), label: b.name })));
    });
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
      purchasePrice: v.purchasePrice,
      insuranceExpiryDate: toDateInputValue(v.insuranceExpiryDate),
      seatingCapacity: v.seatingCapacity,
      branchId: v.branchId ? String(v.branchId) : '',
      currentMileage: v.currentMileage
    });
    this.syncFormValuesFromForm();
  }

  private syncDocumentSlots(documents: VehicleDocument[]): void {
    const imageDocs = documents.filter(d => d.documentType === 'VehicleImage');
    const slots: VehicleImageSlotState[] = VEHICLE_IMAGE_ANGLES.map(({ angle, label }) => ({ angle, label }));
    const assignedDocIds = new Set<number>();

    for (const slot of slots) {
      const doc = imageDocs.find(d => {
        const id = this.readDocumentId(d);
        return id != null
          && !assignedDocIds.has(id)
          && parseVehicleImageAngle(d.notes) === slot.angle;
      });
      if (!doc) continue;
      const id = this.readDocumentId(doc)!;
      assignedDocIds.add(id);
      slot.fileUrl = doc.fileUrl;
      slot.documentId = id;
      slot.isPrimary = isPrimaryVehicleImage(doc.notes);
    }

    const unassigned = imageDocs.filter(d => {
      const id = this.readDocumentId(d);
      return id != null && !assignedDocIds.has(id);
    });
    const openSlots = slots.filter(s => !s.fileUrl);
    for (let i = 0; i < unassigned.length && i < openSlots.length; i++) {
      const doc = unassigned[i];
      const slot = openSlots[i];
      const id = this.readDocumentId(doc)!;
      assignedDocIds.add(id);
      slot.fileUrl = doc.fileUrl;
      slot.documentId = id;
      slot.isPrimary = isPrimaryVehicleImage(doc.notes);
    }

    if (!slots.some(s => s.isPrimary)) {
      const first = slots.find(s => s.fileUrl);
      if (first) first.isPrimary = true;
    }

    this.vehicleImageSlots.set(slots);

    const docSlots = this.documentSlots().map(slot => {
      const doc = documents.find(d => d.documentType === slot.documentType);
      return doc ? { ...slot, fileUrl: doc.fileUrl, documentId: this.readDocumentId(doc) } : slot;
    });
    this.documentSlots.set(docSlots);

    const regDoc = documents.find(d => d.documentType === 'Registration');
    const roadDoc = documents.find(d => d.documentType === 'RoadTax');
    const fitDoc = documents.find(d => d.documentType === 'Fitness');
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
    try {
      const id = await firstValueFrom(this.vehicleService.createDraft(dto));
      if (id) this.vehicleId.set(id);
    } catch {
      this.snackBar.open('Failed to create draft', 'Close', { duration: 3000 });
    }
  }

  private setupAutosave(): void {
    if (this.autosaveSubscribed) return;
    this.autosaveSubscribed = true;

    this.form.valueChanges.pipe(
      debounceTime(2000),
      distinctUntilChanged(),
      filter(() => !!this.vehicleId()),
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
        const updateDto: UpdateVehicleDto = { ...dto, status };
        await firstValueFrom(this.vehicleService.update({ id, vehicle: updateDto }));
      }
      this.lastSavedAt.set(new Date());
      if (showToast) {
        const message = this.isPublishedVehicle() ? 'Changes saved' : 'Draft saved';
        this.snackBar.open(message, 'Close', { duration: 2000 });
      }
    } catch (err) {
      const message = this.extractErrorMessage(err, 'Failed to save vehicle');
      if (showToast) this.snackBar.open(message, 'Close', { duration: 3000 });
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
      const existing = this.uploadedDocuments().find(d => d.documentType === type);
      if (!existing) {
        await firstValueFrom(this.vehicleService.addDocument(vehicleId, {
          documentType: type,
          expiryDate: dateInputToIso(date) ?? undefined
        })).catch(() => undefined);
      }
    }
  }

  private handleSaveError(error: unknown): void {
    const message = this.extractErrorMessage(error, 'Failed to save vehicle');
    if (message.toLowerCase().includes('registration') || message.toLowerCase().includes('license')) {
      this.form.get('registrationNumber')?.setErrors({ conflict: true });
      this.form.get('registrationNumber')?.markAsTouched();
      this.currentStep.set('details');
    }
    this.snackBar.open(message, 'Close', { duration: 4500 });
  }

  private extractErrorMessage(error: unknown, fallback: string): string {
    if (error instanceof Error && error.message && error.message !== fallback) {
      return error.message;
    }

    if (error instanceof HttpErrorResponse) {
      const payload = error.error as {
        message?: string;
        title?: string;
        errors?: Record<string, string[] | string>;
      } | string | undefined;

      if (typeof payload === 'string' && payload.trim()) return payload;
      if (payload && typeof payload === 'object') {
        if (payload.message) return payload.message;
        if (payload.errors) {
          const preferred = Object.entries(payload.errors).find(([key]) => !key.startsWith('command'));
          const first = preferred ? preferred[1] : Object.values(payload.errors)[0];
          if (Array.isArray(first) && first.length) return String(first[0]);
          if (first) return String(first);
        }
        if (payload.title && payload.title !== 'One or more validation errors occurred.') {
          return payload.title;
        }
      }
    }

    return fallback;
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
      uniqueId?.setValidators([Validators.required, Validators.minLength(5), Validators.maxLength(100)]);
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
      this.markTouched(this.form.get('branchId'));
      this.markTouched(this.form.get('currentMileage'));
      this.markTouched(this.form.get('purchasePrice'));
      return;
    }
    if (step === 'gps') {
      const mode = this.gpsForm.get('mode')?.value as GpsWizardMode;
      if (mode === 'existing') {
        this.markTouched(this.gpsForm.get('existingDeviceId'));
      } else if (mode === 'new') {
        this.markTouched(this.gpsForm.get('uniqueId'));
        this.markTouched(this.gpsForm.get('model'));
        this.markTouched(this.gpsForm.get('vendor'));
      }
      return;
    }
    if (step === 'documents') {
      const slots = [...this.documentSlots()];
      let changed = false;
      for (let i = 0; i < slots.length; i++) {
        if (slots[i].required && !slots[i].fileUrl && !slots[i].uploading) {
          slots[i] = { ...slots[i], error: `${slots[i].label} is required.` };
          changed = true;
        }
      }
      if (changed) {
        this.documentSlots.set(slots);
      }

      if (!this.vehicleImageSlots().some(s => !!s.fileUrl)) {
        const imageSlots = this.vehicleImageSlots().map(s => ({
          ...s,
          error: 'At least one vehicle image is required.'
        }));
        this.vehicleImageSlots.set(imageSlots);
      }
    }
  }

  private markTouched(control: AbstractControl | null): void {
    control?.markAsTouched();
    control?.updateValueAndValidity({ emitEvent: false });
  }

  private async refreshVehicleImageSlots(vehicleId: number): Promise<void> {
    const documents = await firstValueFrom(this.vehicleService.getDocuments(vehicleId).pipe(catchError(() => of([]))));
    this.uploadedDocuments.set(documents);
    this.syncDocumentSlots(documents);
  }

  private readDocumentId(value: VehicleImageSlotState | VehicleDocument | UploadVehicleDocumentResult | Record<string, unknown> | null | undefined): number | undefined {
    if (!value || typeof value !== 'object') return undefined;
    const rec = value as Record<string, unknown>;
    const raw = rec['documentId'] ?? rec['DocumentId'] ?? rec['id'] ?? rec['Id'];
    const num = Number(raw);
    return Number.isFinite(num) && num > 0 ? Math.trunc(num) : undefined;
  }

  private extractDocumentId(value: unknown): number | undefined {
    return this.readDocumentId(value as Record<string, unknown>);
  }

  private readFileUrl(value: unknown): string | undefined {
    if (!value || typeof value !== 'object') return undefined;
    const rec = value as Record<string, unknown>;
    const raw = rec['fileUrl'] ?? rec['FileUrl'];
    return typeof raw === 'string' && raw.trim() ? raw : undefined;
  }

  private requiredDocumentSlotsValid(): boolean {
    const hasVehicleImage = this.vehicleImageSlots().some(slot => !!slot.fileUrl);
    const docsValid = this.documentSlots().every(slot => !slot.required || !!slot.fileUrl);
    return hasVehicleImage && docsValid;
  }
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
