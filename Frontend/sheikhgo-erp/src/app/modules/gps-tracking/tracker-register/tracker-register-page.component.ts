import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { filter, forkJoin, of, switchMap } from 'rxjs';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { TrackerCatalogService } from '../../../core/services/tracker-catalog.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { DriverService } from '../../../core/services/driver.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { Vehicle, VehicleStatus } from '../../../core/models/vehicle.model';
import { DriverListItem } from '../../../core/models/driver.model';
import { TrackerDetail, TrackerRegisteredResult } from '../../../core/models/gps-tracking.model';
import { TrackerBrand, TrackerModel } from '../../../core/models/tracker-catalog.model';
import { UiSelectOption } from '../../../shared/components/ui/types/ui.types';
import {
  countrySelectOptions,
  CURRENT_STATUSES,
  DEFAULT_TRACKER_COUNTRY,
  getTrackerCountry,
  RELAY_OUTPUTS,
  simProviderOptions,
  SIM_PACKAGES,
  TRACKER_CATEGORIES,
} from './tracker-country.config';
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
  private readonly vehiclesSvc = inject(VehicleService);
  private readonly driversSvc = inject(DriverService);
  private readonly toast = inject(UiToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly countryOptions = countrySelectOptions();
  readonly categoryOptions = TRACKER_CATEGORIES;
  readonly relayOutputs = RELAY_OUTPUTS;
  readonly simPackages = SIM_PACKAGES;
  readonly currentStatuses = CURRENT_STATUSES;
  readonly minDate = todayIsoDate();

  brands: TrackerBrand[] = [];
  models: TrackerModel[] = [];
  vehicles: Vehicle[] = [];
  drivers: DriverListItem[] = [];
  existingImeis = new Set<string>();
  loading = false;
  saving = false;
  isEdit = false;
  trackerId: number | null = null;
  registrationSuccess: TrackerRegisteredResult | null = null;
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
    vehicleId: [''],
    driverId: [''],
    supportsEngineCutoff: [false],
    relayOutput: ['output1'],
    installationDate: [todayIsoDate()],
    installedBy: [''],
    installationNotes: [''],
    serialNumber: [''],
    simProvider: [''],
    simPackage: [''],
    monthlySimCost: [null as number | null],
    warrantyStart: [todayIsoDate(), notPastDateValidator()],
    warrantyEnd: [''],
    purchaseDate: [todayIsoDate(), notPastDateValidator()],
    purchasePrice: [null as number | null],
    vendor: [''],
    currentStatus: ['Installed'],
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
      vehicles: this.vehiclesSvc.getAll(1, 500),
      drivers: this.driversSvc.getAll(1, 500),
      devices: this.gps.getDevices(),
      tracker: this.trackerId ? this.gps.getTracker(this.trackerId) : of(null)
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: ({ brands, vehicles, drivers, devices, tracker }) => {
        this.brands = brands;
        this.vehicles = vehicles.items.filter(
          v => v.status !== VehicleStatus.Draft && v.name.trim().toLowerCase() !== 'draft vehicle'
        );
        this.drivers = drivers.items;
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
        this.form.patchValue({ supportsEngineCutoff: model.supportsEngineCutOff }, { emitEvent: false });
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

  get showRelayOutput(): boolean {
    return !!this.form.get('supportsEngineCutoff')?.value;
  }

  get canSubmit(): boolean {
    if (this.saving || this.registrationSuccess) return false;
    if (!this.form.get('name')?.valid || !this.form.get('category')?.valid) return false;
    if (!this.form.get('trackerBrandId')?.valid || !this.form.get('trackerModelId')?.valid) return false;
    if (!this.imeiIsValid || this.imeiIsDuplicate) return false;
    if (this.form.get('phoneLocal')?.invalid) return false;
    if (this.form.get('warrantyStart')?.invalid || this.form.get('purchaseDate')?.invalid) return false;
    return true;
  }

  get vehicleOptions(): UiSelectOption[] {
    return this.vehicles.map(v => ({
      value: String(v.id),
      label: `${v.name} - ${v.registrationNumber}${v.vehicleCode ? ` · ${v.vehicleCode}` : ''}`
    }));
  }

  get driverOptions(): UiSelectOption[] {
    return this.drivers.map(d => ({
      value: String(d.id),
      label: d.fullName
    }));
  }

  get pageTitle(): string {
    if (this.registrationSuccess) return 'Tracker Registered';
    return this.isEdit ? 'Edit Tracker' : 'Register Tracker';
  }

  phonePlaceholder(): string {
    return getTrackerCountry(this.form.get('countryCode')?.value as string)?.localPlaceholder ?? 'Local number';
  }

  cancel(): void {
    void this.router.navigate(['/gps-tracking/devices']);
  }

  submit(): void {
    if (!this.canSubmit) return;
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
      vehicleId: (v.vehicleId as string) ? Number(v.vehicleId) : undefined,
      driverId: (v.driverId as string) ? Number(v.driverId) : undefined,
      supportsEngineCutoff: !!(v.supportsEngineCutoff),
      relayOutput: v.supportsEngineCutoff ? (v.relayOutput as string) : undefined,
      installationDate: (v.installationDate as string) || undefined,
      installedBy: (v.installedBy as string) || undefined,
      installationNotes: (v.installationNotes as string) || undefined,
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
      currentStatus: (v.currentStatus as string) || 'Installed',
    };

    if (this.isEdit && this.trackerId) {
      this.gps.updateTracker(this.trackerId, { ...payload, isActive: !!(v.isActive) }).pipe(
        takeUntilDestroyed(this.destroyRef)
      ).subscribe({
        next: () => {
          this.saving = false;
          this.toast.success('Tracker updated');
          void this.router.navigate(['/gps-tracking/devices']);
        },
        error: err => {
          this.saving = false;
          this.toast.error(err?.error?.message ?? 'Update failed');
        }
      });
      return;
    }

    this.gps.registerTracker(payload).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: result => {
        this.saving = false;
        this.registrationSuccess = result;
      },
      error: err => {
        this.saving = false;
        this.toast.error(err?.error?.message ?? 'Registration failed');
      }
    });
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
      supportsEngineCutoff: match?.supportsEngineCutOff ?? false
    }, { emitEvent: false });
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
      vehicleId: t.vehicleId ? String(t.vehicleId) : '',
      driverId: t.driverId ? String(t.driverId) : '',
      supportsEngineCutoff: t.supportsEngineCutoff,
      relayOutput: t.relayOutput ?? 'output1',
      installationDate: t.installationDate?.slice(0, 10) ?? todayIsoDate(),
      installedBy: t.installedBy ?? '',
      installationNotes: t.installationNotes ?? '',
      serialNumber: t.serialNumber ?? '',
      simProvider: t.simProvider ?? '',
      simPackage: t.simPackage ?? '',
      monthlySimCost: t.monthlySimCost ?? null,
      warrantyStart: t.warrantyStart?.slice(0, 10) ?? todayIsoDate(),
      warrantyEnd: t.warrantyEnd?.slice(0, 10) ?? '',
      purchaseDate: t.purchaseDate?.slice(0, 10) ?? todayIsoDate(),
      purchasePrice: t.purchasePrice ?? null,
      vendor: t.vendor ?? '',
      currentStatus: t.currentStatus ?? 'Installed',
      isActive: t.isActive,
    });
    this.form.get('uniqueId')?.disable();
    this.existingImeis.delete(t.uniqueId);
  }
}
