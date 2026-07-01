import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { filter, finalize, forkJoin, of, switchMap } from 'rxjs';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { TrackerCatalogService } from '../../../core/services/tracker-catalog.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { TrackerDetail, TrackerRegisteredResult } from '../../../core/models/gps-tracking.model';
import { TrackerBrand, TrackerModel } from '../../../core/models/tracker-catalog.model';
import { UiSelectOption } from '../../../shared/components/ui/types/ui.types';
import {
  countrySelectOptions,
  DEFAULT_TRACKER_COUNTRY,
  getTrackerCountry,
  simProviderOptions,
  SIM_PACKAGES,
  TRACKER_CATEGORIES,
} from './tracker-country.config';
import {
  buildRelayOutputOptions,
  RELAY_OUTPUT_HINT,
  RELAY_PURPOSE_ENGINE,
  relayOutputLabel,
  resolveDefaultRelayOutput,
} from '../utils/relay-immobilizer.util';
import {
  buildInternationalPhone,
  IMEI_PATTERN,
  notPastDateValidator,
  phoneLocalValidator,
  todayIsoDate
} from './tracker-register.validators';

@Component({
  selector: 'app-tracker-register-page',
  templateUrl: './tracker-register-page.component.html',
  styleUrls: ['./tracker-register-page.component.scss']
})
export class TrackerRegisterPageComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly gps = inject(GpsTrackingService);
  private readonly catalog = inject(TrackerCatalogService);
  private readonly toast = inject(UiToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly countryOptions = countrySelectOptions();
  readonly categoryOptions = TRACKER_CATEGORIES;
  readonly relayOutputHint = RELAY_OUTPUT_HINT;
  readonly relayOutputLabel = relayOutputLabel;
  readonly simPackages = SIM_PACKAGES;
  readonly minDate = todayIsoDate();

  brands: TrackerBrand[] = [];
  models: TrackerModel[] = [];
  existingImeis = new Set<string>();
  loading = false;
  saving = false;
  isEdit = false;
  trackerId: number | null = null;
  registrationSuccess: TrackerRegisteredResult | null = null;
  relayManualOverride = false;
  private patchingBrand = false;

  form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    uniqueId: ['', [Validators.required, Validators.pattern(IMEI_PATTERN)]],
    category: ['car', Validators.required],
    countryCode: [DEFAULT_TRACKER_COUNTRY, Validators.required],
    phoneLocal: [''],
    trackerBrandId: ['', Validators.required],
    trackerModelId: ['', Validators.required],
    contact: [''],
    disabled: [false],
    supportsEngineCutoff: [false],
    relayOutput: [''],
    serialNumber: [''],
    simProvider: [''],
    simPackage: [''],
    monthlySimCost: [null as number | null],
    warrantyStart: [todayIsoDate(), notPastDateValidator()],
    warrantyEnd: [''],
    purchaseDate: [todayIsoDate(), notPastDateValidator()],
    purchasePrice: [null as number | null],
    vendor: [''],
    isActive: [true],
  });

  ngOnInit(): void {
    const phoneCtrl = this.form.get('phoneLocal')!;
    phoneCtrl.setValidators([
      phoneLocalValidator(() => String(this.form.get('countryCode')?.value ?? DEFAULT_TRACKER_COUNTRY))
    ]);

    const idParam = this.route.snapshot.paramMap.get('id');
    this.isEdit = this.route.snapshot.url.some(s => s.path === 'edit');
    this.trackerId = idParam ? Number(idParam) : null;

    this.loading = true;
    forkJoin({
      brands: this.catalog.getBrands(),
      devices: this.gps.getDevices(),
      tracker: this.trackerId ? this.gps.getTracker(this.trackerId) : of(null)
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: ({ brands, devices, tracker }) => {
        this.brands = brands;
        this.existingImeis = new Set(devices.map(d => d.uniqueId));

        if (tracker) {
          this.patchFromTracker(tracker);
        } else {
          this.setDefaultBrandAndModel();
        }

        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.toast.error('Failed to load registration data');
      }
    });

    this.form.get('trackerBrandId')!.valueChanges.pipe(
      filter((id): id is string => !!id && !this.patchingBrand),
      switchMap(id => this.catalog.getModels(Number(id))),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: models => this.applyModels(models),
      error: () => this.toast.error('Failed to load tracker models')
    });

    this.form.get('trackerModelId')?.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((modelId: string | null) => {
      const model = this.models.find(m => m.id === Number(modelId));
      if (model) {
        this.applyModelImmobilizerDefaults(model);
      }
    });

    this.form.get('supportsEngineCutoff')?.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(enabled => {
      const relayCtrl = this.form.get('relayOutput')!;
      if (enabled) {
        relayCtrl.enable({ emitEvent: false });
        if (!relayCtrl.value) {
          relayCtrl.setValue(resolveDefaultRelayOutput(this.selectedModel), { emitEvent: false });
        }
      } else {
        relayCtrl.disable({ emitEvent: false });
        relayCtrl.setValue('', { emitEvent: false });
      }
    });

    this.form.get('relayOutput')?.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(value => {
      if (!value) return;
      const recommended = resolveDefaultRelayOutput(this.selectedModel);
      if (value !== recommended) {
        this.relayManualOverride = true;
      }
    });

    this.form.get('countryCode')?.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => {
      this.form.patchValue({ phoneLocal: '', simProvider: '' }, { emitEvent: false });
      phoneCtrl.updateValueAndValidity();
    });

    this.form.get('uniqueId')?.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((raw: string | null) => {
      const digits = String(raw ?? '').replace(/\D/g, '').slice(0, 15);
      if (digits !== raw) this.form.get('uniqueId')?.setValue(digits, { emitEvent: false });
    });

    if (!this.form.get('supportsEngineCutoff')?.value) {
      this.form.get('relayOutput')?.disable({ emitEvent: false });
    }
  }

  get brandOptions(): UiSelectOption[] {
    return this.brands.map(b => ({ value: String(b.id), label: b.name }));
  }

  get modelOptions(): UiSelectOption[] {
    return this.models.map(m => ({ value: String(m.id), label: m.name }));
  }

  get selectedModel(): TrackerModel | undefined {
    const id = Number(this.form.get('trackerModelId')?.value);
    return this.models.find(m => m.id === id);
  }

  get selectedBrand(): TrackerBrand | undefined {
    const id = Number(this.form.get('trackerBrandId')?.value);
    return this.brands.find(b => b.id === id);
  }

  get protocolLabel(): string {
    return this.selectedModel?.protocolLabel ?? '—';
  }

  get simProviderOpts(): UiSelectOption[] {
    return simProviderOptions(this.form.get('countryCode')?.value as string);
  }

  get imeiValue(): string {
    return String(this.form.get('uniqueId')?.value ?? '');
  }

  get imeiIsValid(): boolean {
    return IMEI_PATTERN.test(this.imeiValue);
  }

  get imeiIsDuplicate(): boolean {
    if (this.isEdit || !this.imeiIsValid) return false;
    return this.existingImeis.has(this.imeiValue);
  }

  get imeiError(): string {
    if (this.isEdit) return '';
    const value = this.imeiValue;
    if (!value) return '';
    if (this.imeiIsDuplicate) return 'IMEI already registered';
    if (!this.imeiIsValid) {
      return value.length < 15
        ? `IMEI must be 15 digits (${value.length} entered)`
        : 'IMEI must be exactly 15 digits';
    }
    return '';
  }

  get submitBlockedReason(): string | null {
    if (this.saving || this.registrationSuccess || this.canSubmit) return null;

    if (!this.isEdit) {
      if (!this.imeiValue) return 'Enter the 15-digit IMEI from the tracker label.';
      if (!this.imeiIsValid) return this.imeiError || 'IMEI must be exactly 15 digits.';
      if (this.imeiIsDuplicate) return 'This IMEI is already registered.';
    }

    if (this.form.get('phoneLocal')?.invalid) {
      return 'Phone number format is invalid for the selected country.';
    }
    if (this.form.get('warrantyStart')?.invalid) {
      return 'Warranty start cannot be in the past.';
    }
    if (this.form.get('purchaseDate')?.invalid) {
      return 'Purchase date cannot be in the past.';
    }
    if (this.form.get('supportsEngineCutoff')?.value && !this.form.getRawValue().relayOutput) {
      return 'Select an engine cutoff output when remote cutoff is enabled.';
    }
    if (!this.form.get('name')?.valid) return 'Tracker name is required.';
    if (!this.form.get('trackerBrandId')?.valid || !this.form.get('trackerModelId')?.valid) {
      return 'Select a tracker brand and model.';
    }

    return 'Complete all required fields to continue.';
  }

  get modelSupportsImmobilizer(): boolean {
    return !!this.selectedModel?.supportsEngineCutOff;
  }

  get defaultRelayForModel(): string {
    return resolveDefaultRelayOutput(this.selectedModel);
  }

  get relayOutputOptions(): UiSelectOption[] {
    return buildRelayOutputOptions(this.defaultRelayForModel);
  }

  get showRelayOutput(): boolean {
    return this.modelSupportsImmobilizer && !!this.form.get('supportsEngineCutoff')?.value;
  }

  get canSubmit(): boolean {
    if (this.saving || this.registrationSuccess) return false;
    if (!this.form.get('name')?.valid || !this.form.get('category')?.valid) return false;
    if (!this.form.get('trackerBrandId')?.valid || !this.form.get('trackerModelId')?.valid) return false;
    if (!this.isEdit && (!this.imeiIsValid || this.imeiIsDuplicate)) return false;
    if (this.form.get('phoneLocal')?.invalid) return false;
    if (this.form.get('warrantyStart')?.invalid || this.form.get('purchaseDate')?.invalid) return false;
    if (this.form.get('supportsEngineCutoff')?.value && !this.form.getRawValue().relayOutput) return false;
    return true;
  }

  get pageTitle(): string {
    if (this.registrationSuccess) return 'Tracker Registered';
    return this.isEdit ? 'Edit Tracker Inventory' : 'Register Tracker';
  }

  phonePlaceholder(): string {
    return getTrackerCountry(this.form.get('countryCode')?.value as string)?.localPlaceholder ?? 'Local number';
  }

  cancel(): void {
    void this.router.navigate(['/gps-tracking/devices']);
  }

  goToInstall(): void {
    if (!this.registrationSuccess?.id) return;
    void this.router.navigate(['/gps-tracking/devices', this.registrationSuccess.id, 'install']);
  }

  submit(): void {
    if (!this.canSubmit || this.saving) return;
    this.saving = true;
    const v = this.form.getRawValue();
    const phone = buildInternationalPhone(v.countryCode as string, v.phoneLocal as string);
    const model = this.selectedModel!;

    const payload = {
      name: v.name as string,
      uniqueId: v.uniqueId as string,
      category: v.category as string,
      trackerModelId: Number(v.trackerModelId),
      trackerModelKey: model.catalogKey,
      phone,
      contact: (v.contact as string) || undefined,
      disabled: !!(v.disabled),
      supportsEngineCutoff: !!(v.supportsEngineCutoff),
      relayOutput: v.supportsEngineCutoff ? (v.relayOutput as string) : undefined,
      relayPurpose: v.supportsEngineCutoff ? RELAY_PURPOSE_ENGINE : undefined,
      serialNumber: (v.serialNumber as string) || undefined,
      countryCode: v.countryCode as string,
      simProvider: (v.simProvider as string) || undefined,
      simPackage: (v.simPackage as string) || undefined,
      monthlySimCost: v.monthlySimCost != null ? Number(v.monthlySimCost) : undefined,
      warrantyStart: (v.warrantyStart as string) || undefined,
      warrantyEnd: (v.warrantyEnd as string) || undefined,
      purchaseDate: (v.purchaseDate as string) || undefined,
      purchasePrice: v.purchasePrice != null ? Number(v.purchasePrice) : undefined,
      vendor: (v.vendor as string) || undefined,
      currentStatus: 'Available',
    };

    if (this.isEdit && this.trackerId) {
      this.gps.updateTracker(this.trackerId, { ...payload, isActive: !!(v.isActive) }).pipe(
        finalize(() => { this.saving = false; })
      ).subscribe({
        next: () => {
          this.toast.success('Tracker inventory updated');
          void this.router.navigate(['/gps-tracking/devices']);
        },
        error: err => this.toast.error(this.extractError(err, 'Update failed'))
      });
      return;
    }

    this.gps.registerTracker(payload).pipe(
      finalize(() => { this.saving = false; })
    ).subscribe({
      next: result => {
        this.registrationSuccess = result;
        this.toast.success('Tracker registered successfully');
      },
      error: err => this.toast.error(this.extractError(err, 'Registration failed'))
    });
  }

  private extractError(err: { error?: { message?: string; Message?: string } }, fallback: string): string {
    return err?.error?.message ?? err?.error?.Message ?? fallback;
  }

  private setDefaultBrandAndModel(): void {
    const teltonika = this.brands.find(b => b.name === 'Teltonika') ?? this.brands[0];
    if (!teltonika) return;

    this.patchingBrand = true;
    this.form.patchValue({ trackerBrandId: String(teltonika.id) }, { emitEvent: false });
    this.patchingBrand = false;

    this.catalog.getModels(teltonika.id).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(models => {
      this.applyModels(models, models.find(m => m.name === 'FMB920')?.id);
    });
  }

  private applyModels(models: TrackerModel[], selectModelId?: number): void {
    this.models = models;
    const current = selectModelId ?? Number(this.form.get('trackerModelId')?.value);
    const match = models.find(m => m.id === current) ?? models[0];
    this.form.patchValue({
      trackerModelId: match ? String(match.id) : '',
    }, { emitEvent: false });
    if (match) {
      this.applyModelImmobilizerDefaults(match);
    }
  }

  private applyModelImmobilizerDefaults(model: TrackerModel): void {
    const relayCtrl = this.form.get('relayOutput')!;

    if (!model.supportsEngineCutOff) {
      this.relayManualOverride = false;
      this.form.patchValue({ supportsEngineCutoff: false, relayOutput: '' }, { emitEvent: false });
      relayCtrl.disable({ emitEvent: false });
      return;
    }

    if (this.isEdit) {
      const cutoffEnabled = !!this.form.get('supportsEngineCutoff')?.value;
      if (cutoffEnabled) {
        const defaultRelay = resolveDefaultRelayOutput(model);
        if (!relayCtrl.value) {
          relayCtrl.setValue(defaultRelay, { emitEvent: false });
        }
        relayCtrl.enable({ emitEvent: false });
      } else {
        relayCtrl.setValue('', { emitEvent: false });
        relayCtrl.disable({ emitEvent: false });
      }
      return;
    }

    this.relayManualOverride = false;
    const autoEnable = this.shouldAutoEnableCutoff(model);
    this.form.patchValue({
      supportsEngineCutoff: autoEnable,
      relayOutput: autoEnable ? resolveDefaultRelayOutput(model) : '',
    }, { emitEvent: false });

    if (autoEnable) {
      relayCtrl.enable({ emitEvent: false });
    } else {
      relayCtrl.disable({ emitEvent: false });
    }
  }

  /** Teltonika fleet trackers commonly ship with relay wiring — pre-enable for that brand only. */
  private shouldAutoEnableCutoff(model: TrackerModel): boolean {
    const brand = this.brands.find(b => b.id === model.trackerBrandId);
    return brand?.name?.toLowerCase() === 'teltonika';
  }

  private patchFromTracker(t: TrackerDetail): void {
    let phoneLocal = '';
    if (t.phone && t.countryCode && t.phone.startsWith(t.countryCode)) {
      phoneLocal = t.phone.slice(t.countryCode.length);
    }

    const brandId = t.trackerBrandId
      ?? this.brands.find(b => b.name === t.trackerBrandName)?.id
      ?? this.brands.find(b => b.name === 'Teltonika')?.id;

    const modelId = t.trackerModelId;

    if (brandId) {
      this.patchingBrand = true;
      this.form.patchValue({ trackerBrandId: String(brandId) }, { emitEvent: false });
      this.patchingBrand = false;

      this.catalog.getModels(brandId).pipe(
        takeUntilDestroyed(this.destroyRef)
      ).subscribe(models => {
        const resolvedModelId = modelId
          ?? models.find(m => m.catalogKey === t.trackerModelKey)?.id
          ?? models.find(m => m.name === t.modelName)?.id;
        this.applyModels(models, resolvedModelId);

        const match = models.find(m => m.id === resolvedModelId);
        const defaultRelay = resolveDefaultRelayOutput(match);
        if (t.relayOutput && t.relayOutput !== defaultRelay) {
          this.relayManualOverride = true;
        }

        const relayCtrl = this.form.get('relayOutput')!;
        if (t.supportsEngineCutoff) {
          relayCtrl.setValue(t.relayOutput ?? defaultRelay, { emitEvent: false });
          relayCtrl.enable({ emitEvent: false });
        } else {
          relayCtrl.setValue('', { emitEvent: false });
          relayCtrl.disable({ emitEvent: false });
        }
      });
    }

    this.form.patchValue({
      name: t.name,
      uniqueId: t.uniqueId,
      category: t.category ?? 'car',
      countryCode: t.countryCode ?? DEFAULT_TRACKER_COUNTRY,
      phoneLocal,
      contact: t.contact ?? '',
      disabled: t.disabled ?? false,
      supportsEngineCutoff: t.supportsEngineCutoff,
      serialNumber: t.serialNumber ?? '',
      simProvider: t.simProvider ?? '',
      simPackage: t.simPackage ?? '',
      monthlySimCost: t.monthlySimCost ?? null,
      warrantyStart: t.warrantyStart?.slice(0, 10) ?? todayIsoDate(),
      warrantyEnd: t.warrantyEnd?.slice(0, 10) ?? '',
      purchaseDate: t.purchaseDate?.slice(0, 10) ?? todayIsoDate(),
      purchasePrice: t.purchasePrice ?? null,
      vendor: t.vendor ?? '',
      isActive: t.isActive,
    });
    this.form.get('uniqueId')?.disable();
    this.existingImeis.delete(t.uniqueId);
  }
}
