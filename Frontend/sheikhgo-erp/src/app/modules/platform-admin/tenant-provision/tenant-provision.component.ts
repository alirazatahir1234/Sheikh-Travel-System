import { Component, ElementRef, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormControl, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { PlatformService } from '../../../core/services/platform.service';
import { LookupService } from '../../../core/services/lookup.service';
import { apiErrorMessage } from '../../../core/utils/api-error.util';
import {
  BRANCH_CURRENCIES,
  DEFAULT_CURRENCY,
  DEFAULT_TENANT_MODULE_CODES,
  GPS_PROVIDERS,
  INDUSTRY_TYPES,
  MODULE_ICONS,
  PLAN_DEFINITIONS,
  ProvisionTenantRequest,
  STORAGE_MODELS,
  TENANT_PLAN_TIERS,
  TENANT_TYPES,
  TenantModuleDefinition,
  applyPlanDefaults,
  tenantPlanMeta
} from '../../../core/models/platform.model';
import { blockNonOrgNameKey, orgNameListValidator, orgNameValidator } from './org-name.validator';
import { parseOptionalPositiveInt } from '../../../core/utils/integer-input.util';
import { UiSelectOption } from '../../../shared/components/ui/types/ui.types';

type SectionKey = 'profile' | 'plan' | 'admin' | 'branding' | 'security' | 'organization' | 'billing';

const FALLBACK_COUNTRIES = [
  'United Arab Emirates',
  'Saudi Arabia',
  'Pakistan',
  'Qatar',
  'Oman',
  'Bahrain',
  'Kuwait',
  'United States',
  'United Kingdom'
];

const FALLBACK_TIMEZONES = [
  'Asia/Dubai',
  'Asia/Riyadh',
  'Asia/Karachi',
  'Asia/Qatar',
  'Asia/Muscat',
  'Asia/Bahrain',
  'Asia/Kuwait',
  'UTC',
  'Europe/London',
  'America/New_York'
];

@Component({
  standalone: false,
  selector: 'app-tenant-provision',
  templateUrl: './tenant-provision.component.html',
  styleUrls: ['./tenant-provision.component.scss']
})
export class TenantProvisionComponent implements OnInit, OnDestroy {
  saving = false;
  loadingModules = true;
  lookupLoading = true;
  modules: TenantModuleDefinition[] = [];
  hideAdminPassword = true;
  logoDragOver = false;
  logoPreviewUrl: string | null = null;

  readonly planTiers = TENANT_PLAN_TIERS;
  readonly planDefinitions = PLAN_DEFINITIONS;
  readonly tenantTypes = TENANT_TYPES;
  readonly industryTypes = INDUSTRY_TYPES;
  readonly storageModels = STORAGE_MODELS;
  readonly gpsProviders = GPS_PROVIDERS;
  readonly moduleIcons = MODULE_ICONS;
  readonly tenantPlanMeta = tenantPlanMeta;
  readonly tenantTypeOptions: UiSelectOption[] = TENANT_TYPES.map(value => ({ value, label: value }));
  readonly industryTypeOptions: UiSelectOption[] = INDUSTRY_TYPES.map(value => ({ value, label: value }));
  readonly storageModelOptions: UiSelectOption[] = STORAGE_MODELS.map(m => ({ value: m.value, label: m.label }));
  readonly gpsProviderOptions: UiSelectOption[] = [
    { value: '', label: 'None' },
    ...GPS_PROVIDERS.map(value => ({ value, label: value }))
  ];
  countries: string[] = [...FALLBACK_COUNTRIES];
  currencies: string[] = [...BRANCH_CURRENCIES];
  timezones: string[] = [...FALLBACK_TIMEZONES];
  countryOptions: UiSelectOption[] = FALLBACK_COUNTRIES.map(value => ({ value, label: value }));
  currencyOptions: UiSelectOption[] = BRANCH_CURRENCIES.map(value => ({ value, label: value }));
  timezoneOptions: UiSelectOption[] = FALLBACK_TIMEZONES.map(value => ({ value, label: value }));

  form: FormGroup;

  private readonly sectionOrder: { key: SectionKey; id: string }[] = [
    { key: 'profile', id: 'section-profile' },
    { key: 'plan', id: 'section-plan' },
    { key: 'admin', id: 'section-admin' },
    { key: 'branding', id: 'section-branding' },
    { key: 'security', id: 'section-security' },
    { key: 'organization', id: 'section-organization' },
    { key: 'billing', id: 'section-billing' }
  ];

  constructor(
    private fb: FormBuilder,
    private platform: PlatformService,
    private lookup: LookupService,
    private toast: UiToastService,
    private router: Router,
    private el: ElementRef<HTMLElement>
  ) {
    this.form = this.fb.group({
      profile: this.fb.group({
        name: ['', [Validators.required, Validators.maxLength(200)]],
        code: [''],
        tenantType: ['Travel Agency'],
        industryType: ['Logistics & Transport'],
        slug: ['', [Validators.required, Validators.pattern(/^[a-z0-9-]+$/)]],
        storageModel: ['SharedDatabase']
      }),
      plan: this.fb.group({
        planName: ['Enterprise', Validators.required],
        moduleCodes: new FormControl<string[]>([...DEFAULT_TENANT_MODULE_CODES], Validators.required),
        maxUsers: [null as number | null, [Validators.min(0)]],
        maxVehicles: [null as number | null, [Validators.min(0)]],
        maxDrivers: [null as number | null, [Validators.min(0)]],
        maxBranches: [null as number | null, [Validators.min(0)]],
        maxGpsDevices: [null as number | null, [Validators.min(0)]]
      }),
      admin: this.fb.group({
        adminFullName: ['', Validators.required],
        adminEmail: ['', [Validators.required, Validators.email]],
        adminMobile: [''],
        adminPassword: ['', [Validators.required, Validators.minLength(8)]]
      }),
      branding: this.fb.group({
        country: ['United Arab Emirates'],
        currencyCode: [DEFAULT_CURRENCY],
        timeZone: ['Asia/Dubai'],
        primaryColor: ['#007A57'],
        website: [''],
        supportEmail: ['', Validators.email],
        logoUrl: ['']
      }),
      security: this.fb.group({
        isMfaRequired: [false],
        enforcePasswordExpiry: [true],
        passwordExpiryDays: [90, [Validators.min(1)]],
        enforceSessionTimeout: [true],
        sessionTimeoutMinutes: [30, [Validators.min(1)]],
        isGdprEnabled: [true],
        isAuditLoggingEnabled: [true],
        isVatEnabled: [false]
      }),
      organization: this.fb.group({
        headOfficeName: ['Head Office', orgNameValidator()],
        defaultBranchName: ['Main Operations Center', orgNameValidator()],
        defaultDepartments: ['Operations,Finance,Fleet,HR', orgNameListValidator()],
        generateOrganizationStructure: [true]
      }),
      billing: this.fb.group({
        billingContactName: [''],
        companyTRN: [''],
        billingEmail: ['', Validators.email],
        billingAddress: [''],
        gpsProviderName: ['']
      })
    });

    this.applyPlan(this.planGroup.get('planName')?.value ?? 'Enterprise');

    this.planGroup.get('planName')?.valueChanges.subscribe(plan => {
      if (plan) this.applyPlan(String(plan));
    });

    this.profileGroup.get('name')?.valueChanges.subscribe(name => {
      const slugCtrl = this.profileGroup.get('slug');
      if (!slugCtrl || slugCtrl.dirty) return;
      const slug = (name ?? '')
        .trim()
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, '-')
        .replace(/^-+|-+$/g, '');
      slugCtrl.setValue(slug, { emitEvent: false });
    });
  }

  ngOnInit(): void {
    forkJoin({
      modules: this.platform.getModules().pipe(catchError(() => of([] as TenantModuleDefinition[]))),
      countries: this.lookup.getCountryNames().pipe(catchError(() => of([...FALLBACK_COUNTRIES]))),
      currencies: this.lookup.getCurrencyCodes().pipe(catchError(() => of([...BRANCH_CURRENCIES]))),
      timezones: this.lookup.getTimezoneIds().pipe(catchError(() => of([...FALLBACK_TIMEZONES])))
    }).subscribe({
      next: ({ modules, countries, currencies, timezones }) => {
        this.modules = modules;
        this.countries = countries?.length ? countries : [...FALLBACK_COUNTRIES];
        this.currencies = currencies?.length ? currencies : [...BRANCH_CURRENCIES];
        this.timezones = timezones?.length ? timezones : [...FALLBACK_TIMEZONES];
        this.refreshBrandingOptions();
        this.loadingModules = false;
        this.lookupLoading = false;
        this.syncBrandingSelectValues();
      },
      error: () => {
        this.loadingModules = false;
        this.lookupLoading = false;
        this.refreshBrandingOptions();
        this.toast.error('Failed to load catalog. Using default country/currency/timezone lists.');
        this.syncBrandingSelectValues();
      }
    });
  }

  get profileGroup(): FormGroup { return this.form.get('profile') as FormGroup; }
  get planGroup(): FormGroup { return this.form.get('plan') as FormGroup; }
  get adminGroup(): FormGroup { return this.form.get('admin') as FormGroup; }
  get brandingGroup(): FormGroup { return this.form.get('branding') as FormGroup; }
  get securityGroup(): FormGroup { return this.form.get('security') as FormGroup; }
  get organizationGroup(): FormGroup { return this.form.get('organization') as FormGroup; }
  get billingGroup(): FormGroup { return this.form.get('billing') as FormGroup; }

  blockOrgNameKey(event: KeyboardEvent): void {
    blockNonOrgNameKey(event);
  }

  get activeModuleCount(): number {
    return (this.planGroup.get('moduleCodes')?.value as string[] ?? []).length;
  }

  get summaryPlanName(): string {
    return this.planGroup.get('planName')?.value ?? 'Enterprise';
  }

  formatQuota(value: number | null | undefined): string {
    return value == null ? 'Unlimited' : String(value);
  }

  onPlanChange(planName: string): void {
    const next = (planName || 'Enterprise').trim();
    const ctrl = this.planGroup.get('planName');
    if (ctrl && ctrl.value !== next) {
      ctrl.setValue(next);
    } else {
      this.applyPlan(next);
    }
  }

  get summaryUserQuota(): string | number {
    const value = this.planGroup.get('maxUsers')?.value;
    return value === null || value === undefined ? 'Unlimited' : value;
  }

  get primaryColorPreview(): string {
    return this.sanitizeHexColor(this.brandingGroup.get('primaryColor')?.value) || '#007A57';
  }

  onBrandColorPicked(value: string): void {
    const hex = this.sanitizeHexColor(value);
    if (!hex) return;
    this.brandingGroup.get('primaryColor')?.setValue(hex);
    this.brandingGroup.get('primaryColor')?.markAsDirty();
  }

  private refreshBrandingOptions(): void {
    this.countryOptions = this.toSelectOptions(this.countries);
    this.currencyOptions = this.toSelectOptions(this.currencies);
    this.timezoneOptions = this.toSelectOptions(this.timezones);
  }

  private toSelectOptions(values: string[]): UiSelectOption[] {
    return values.map(value => ({ value, label: value }));
  }

  private sanitizeHexColor(value: unknown): string | null {
    const raw = String(value ?? '').trim();
    const match = raw.match(/^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$/);
    if (!match) return null;
    if (match[1].length === 3) {
      const [r, g, b] = match[1].split('');
      return `#${r}${r}${g}${g}${b}${b}`.toLowerCase();
    }
    return `#${match[1]}`.toLowerCase();
  }

  private syncBrandingSelectValues(): void {
    const country = this.pickExistingOrDefault(
      this.brandingGroup.get('country')?.value,
      this.countries,
      'United Arab Emirates'
    );
    const currencyCode = this.pickExistingOrDefault(
      this.brandingGroup.get('currencyCode')?.value,
      this.currencies,
      DEFAULT_CURRENCY
    );
    const timeZone = this.pickExistingOrDefault(
      this.brandingGroup.get('timeZone')?.value,
      this.timezones,
      'Asia/Dubai'
    );

    this.brandingGroup.patchValue({ country, currencyCode, timeZone }, { emitEvent: false });
  }

  private pickExistingOrDefault(current: string | null | undefined, options: string[], fallback: string): string {
    if (current && options.includes(current)) return current;
    if (options.includes(fallback)) return fallback;
    return options[0] ?? fallback;
  }

  generatePassword(): void {
    const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%';
    let value = '';
    for (let i = 0; i < 12; i++) {
      value += chars.charAt(Math.floor(Math.random() * chars.length));
    }
    this.adminGroup.get('adminPassword')?.setValue(value);
    this.adminGroup.get('adminPassword')?.markAsDirty();
    this.hideAdminPassword = false;
  }

  onLogoDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.logoDragOver = true;
  }

  onLogoDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.logoDragOver = false;
    const file = event.dataTransfer?.files?.[0];
    if (file) this.applyLogoFile(file);
  }

  onLogoFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) this.applyLogoFile(file);
    input.value = '';
  }

  ngOnDestroy(): void {
    this.revokeLogoPreview();
  }

  private applyLogoFile(file: File): void {
    if (!file.type.startsWith('image/')) {
      this.toast.warning('Please select an image file.');
      return;
    }
    this.revokeLogoPreview();
    this.logoPreviewUrl = URL.createObjectURL(file);
    this.toast.info('Logo preview ready. Paste a hosted logo URL to include it in provisioning.');
  }

  private revokeLogoPreview(): void {
    if (this.logoPreviewUrl) {
      URL.revokeObjectURL(this.logoPreviewUrl);
      this.logoPreviewUrl = null;
    }
  }

  get checklistItems(): { label: string; done: boolean }[] {
    return [
      { label: 'Tenant profile defined', done: this.profileGroup.valid },
      { label: 'Plan & modules selected', done: this.planGroup.valid && this.activeModuleCount > 0 },
      { label: 'Initial administrator setup', done: this.adminGroup.valid },
      { label: 'Localization & branding', done: this.brandingGroup.valid },
      { label: 'Security policies configured', done: this.securityGroup.valid },
      { label: 'Organization structure ready', done: this.organizationGroup.valid },
      { label: 'Billing & GPS (optional)', done: this.billingGroup.valid }
    ];
  }

  get checklistCompleteCount(): number {
    return this.checklistItems.filter(i => i.done).length;
  }

  applyPlan(planName: string): void {
    const resolved = PLAN_DEFINITIONS[planName] ? planName : 'Enterprise';
    const def = applyPlanDefaults(resolved);
    const planCtrl = this.planGroup.get('planName');
    if (planCtrl && planCtrl.value !== resolved) {
      planCtrl.setValue(resolved, { emitEvent: false });
    }
    this.planGroup.patchValue({
      maxUsers: parseOptionalPositiveInt(def.quotas.maxUsers),
      maxVehicles: parseOptionalPositiveInt(def.quotas.maxVehicles),
      maxDrivers: parseOptionalPositiveInt(def.quotas.maxDrivers),
      maxBranches: parseOptionalPositiveInt(def.quotas.maxBranches),
      maxGpsDevices: parseOptionalPositiveInt(def.quotas.maxGpsDevices),
      moduleCodes: [...def.moduleCodes]
    }, { emitEvent: false });
    this.planGroup.markAsDirty();
  }

  moduleIcon(code: string): string {
    return this.moduleIcons[code] ?? 'extension';
  }

  isModuleSelected(code: string): boolean {
    const selected = this.planGroup.get('moduleCodes')?.value as string[] | null;
    return selected?.includes(code) ?? false;
  }

  toggleModule(code: string): void {
    const ctrl = this.planGroup.get('moduleCodes');
    const current = [...(ctrl?.value as string[] ?? [])];
    const next = current.includes(code)
      ? current.filter(c => c !== code)
      : [...new Set([...current, code])];
    ctrl?.setValue(next);
    ctrl?.markAsTouched();
  }

  validateConfiguration(): boolean {
    this.form.markAllAsTouched();
    if (this.form.valid) {
      this.toast.success('Configuration is valid and ready to provision.');
      return true;
    }
    this.scrollToFirstInvalid();
    this.toast.warning('Please complete all required fields.');
    return false;
  }

  submit(): void {
    if (this.saving) return;
    if (!this.validateConfiguration()) return;

    const payload = this.buildPayload();
    if (!payload) return;

    this.saving = true;
    this.platform.provisionTenant(payload).subscribe({
      next: () => {
        this.saving = false;
        this.toast.success('Company provisioned successfully.');
        void this.router.navigate(['/platform/tenants']);
      },
      error: (err: unknown) => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Provisioning failed.'));
      }
    });
  }

  cancel(): void {
    void this.router.navigate(['/platform/tenants']);
  }

  private scrollToFirstInvalid(): void {
    for (const section of this.sectionOrder) {
      const group = this.form.get(section.key);
      if (group && group.invalid) {
        const el = this.el.nativeElement.querySelector(`#${section.id}`);
        el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
        return;
      }
    }
  }

  private buildPayload(): ProvisionTenantRequest | null {
    if (this.form.invalid) return null;

    const p = this.profileGroup.getRawValue();
    const pl = this.planGroup.getRawValue();
    const a = this.adminGroup.getRawValue();
    const b = this.brandingGroup.getRawValue();
    const s = this.securityGroup.getRawValue();
    const o = this.organizationGroup.getRawValue();
    const bill = this.billingGroup.getRawValue();

    return {
      name: p.name!.trim(),
      slug: p.slug!.trim().toLowerCase(),
      code: p.code?.trim() || undefined,
      tenantType: p.tenantType?.trim() || undefined,
      industryType: p.industryType?.trim() || undefined,
      storageModel: p.storageModel || 'SharedDatabase',
      planName: pl.planName ?? 'Enterprise',
      maxUsers: pl.maxUsers ?? undefined,
      maxVehicles: pl.maxVehicles ?? undefined,
      maxDrivers: pl.maxDrivers ?? undefined,
      maxBranches: pl.maxBranches ?? undefined,
      maxGpsDevices: pl.maxGpsDevices ?? undefined,
      moduleCodes: pl.moduleCodes ?? undefined,
      adminFullName: a.adminFullName!.trim(),
      adminEmail: a.adminEmail!.trim(),
      adminPassword: a.adminPassword!,
      adminMobile: a.adminMobile?.trim() || undefined,
      country: b.country?.trim() || undefined,
      timeZone: b.timeZone?.trim() || undefined,
      currencyCode: b.currencyCode?.trim() || undefined,
      primaryColor: b.primaryColor?.trim() || undefined,
      logoUrl: b.logoUrl?.trim() || undefined,
      website: b.website?.trim() || undefined,
      supportEmail: b.supportEmail?.trim() || undefined,
      isMfaRequired: s.isMfaRequired ?? false,
      passwordExpiryDays: s.enforcePasswordExpiry ? (s.passwordExpiryDays ?? 90) : 0,
      sessionTimeoutMinutes: s.enforceSessionTimeout ? (s.sessionTimeoutMinutes ?? 30) : 0,
      isGdprEnabled: s.isGdprEnabled ?? true,
      isAuditLoggingEnabled: s.isAuditLoggingEnabled ?? true,
      isVatEnabled: s.isVatEnabled ?? false,
      generateOrganizationStructure: o.generateOrganizationStructure ?? true,
      defaultBranchName: o.defaultBranchName?.trim() || undefined,
      headOfficeName: o.headOfficeName?.trim() || undefined,
      defaultDepartments: o.defaultDepartments?.trim() || undefined,
      billingContactName: bill.billingContactName?.trim() || undefined,
      billingEmail: bill.billingEmail?.trim() || undefined,
      billingAddress: bill.billingAddress?.trim() || undefined,
      companyTRN: bill.companyTRN?.trim() || undefined,
      gpsProviderName: bill.gpsProviderName?.trim() || undefined
    };
  }
}
