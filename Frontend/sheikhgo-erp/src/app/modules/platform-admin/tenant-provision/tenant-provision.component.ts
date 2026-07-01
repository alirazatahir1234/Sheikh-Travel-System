import { Component, ElementRef, OnInit } from '@angular/core';
import { FormBuilder, FormControl, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { forkJoin } from 'rxjs';
import { PlatformService } from '../../../core/services/platform.service';
import { LookupService } from '../../../core/services/lookup.service';
import { apiErrorMessage } from '../../../core/utils/api-error.util';
import {
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

type SectionKey = 'profile' | 'plan' | 'admin' | 'branding' | 'security' | 'organization' | 'billing';

@Component({
  selector: 'app-tenant-provision',
  templateUrl: './tenant-provision.component.html',
  styleUrls: ['./tenant-provision.component.scss']
})
export class TenantProvisionComponent implements OnInit {
  saving = false;
  loadingModules = true;
  modules: TenantModuleDefinition[] = [];

  readonly planTiers = TENANT_PLAN_TIERS;
  readonly planDefinitions = PLAN_DEFINITIONS;
  readonly tenantTypes = TENANT_TYPES;
  readonly industryTypes = INDUSTRY_TYPES;
  readonly storageModels = STORAGE_MODELS;
  readonly gpsProviders = GPS_PROVIDERS;
  readonly moduleIcons = MODULE_ICONS;
  readonly tenantPlanMeta = tenantPlanMeta;
  countries: string[] = [];
  currencies: string[] = [];
  timezones: string[] = [];
  countrySearch = '';
  currencySearch = '';

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
        headOfficeName: ['Head Office'],
        defaultBranchName: ['Main Operations Center'],
        defaultDepartments: ['Operations,Finance,Fleet,HR'],
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
      if (plan) this.applyPlan(plan);
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
      modules: this.platform.getModules(),
      countries: this.lookup.getCountryNames(),
      currencies: this.lookup.getCurrencyCodes(),
      timezones: this.lookup.getTimezoneIds()
    }).subscribe({
      next: ({ modules, countries, currencies, timezones }) => {
        this.modules = modules;
        this.countries = countries;
        this.currencies = currencies;
        this.timezones = timezones;
        this.loadingModules = false;
      },
      error: () => {
        this.loadingModules = false;
        this.toast.error('Failed to load catalog.');
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

  get activeModuleCount(): number {
    return (this.planGroup.get('moduleCodes')?.value as string[] ?? []).length;
  }

  get summaryPlanName(): string {
    return this.planGroup.get('planName')?.value ?? 'Enterprise';
  }

  get summaryUserQuota(): string | number {
    const value = this.planGroup.get('maxUsers')?.value;
    return value === null || value === undefined ? 'Unlimited' : value;
  }

  get primaryColorPreview(): string {
    return this.brandingGroup.get('primaryColor')?.value || '#007A57';
  }

  get filteredCountries(): string[] {
    const query = this.countrySearch.trim().toLowerCase();
    if (!query) return this.countries;
    return this.countries.filter(c => c.toLowerCase().includes(query));
  }

  get filteredCurrencies(): string[] {
    const query = this.currencySearch.trim().toLowerCase();
    if (!query) return this.currencies;
    return this.currencies.filter(c => c.toLowerCase().includes(query));
  }

  clearCountrySearch(): void {
    this.countrySearch = '';
  }

  clearCurrencySearch(): void {
    this.currencySearch = '';
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
    const def = applyPlanDefaults(planName);
    this.planGroup.patchValue({
      maxUsers: def.quotas.maxUsers,
      maxVehicles: def.quotas.maxVehicles,
      maxDrivers: def.quotas.maxDrivers,
      maxBranches: def.quotas.maxBranches,
      maxGpsDevices: def.quotas.maxGpsDevices,
      moduleCodes: [...def.moduleCodes]
    });
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
        this.toast.success('Tenant provisioned successfully.');
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
