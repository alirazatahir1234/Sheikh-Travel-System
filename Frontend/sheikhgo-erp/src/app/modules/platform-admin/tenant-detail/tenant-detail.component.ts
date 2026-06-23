import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormControl, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { forkJoin } from 'rxjs';
import { PlatformService } from '../../../core/services/platform.service';
import { LookupService } from '../../../core/services/lookup.service';
import { apiErrorMessage } from '../../../core/utils/api-error.util';
import {
  DEFAULT_CURRENCY,
  DEFAULT_TENANT_MODULE_CODES,
  MODULE_ICONS,
  PLAN_DEFINITIONS,
  TENANT_PLAN_TIERS,
  TenantAdminInfo,
  TenantDetail,
  TenantModuleDefinition,
  applyPlanDefaults,
  tenantDisplayCode,
  tenantPlanMeta
} from '../../../core/models/platform.model';

@Component({
  selector: 'app-tenant-detail',
  templateUrl: './tenant-detail.component.html',
  styleUrls: ['./tenant-detail.component.scss']
})
export class TenantDetailComponent implements OnInit {
  loading = true;
  saving = false;
  resettingPassword = false;
  tenantId?: number;
  tenant?: TenantDetail;
  modules: TenantModuleDefinition[] = [];
  adminInfo?: TenantAdminInfo | null;

  readonly planTiers = TENANT_PLAN_TIERS;
  readonly planDefinitions = PLAN_DEFINITIONS;
  readonly moduleIcons = MODULE_ICONS;
  countries: string[] = [];
  currencies: string[] = [];
  timezones: string[] = [];
  countrySearch = '';
  currencySearch = '';
  readonly tenantDisplayCode = tenantDisplayCode;
  readonly tenantPlanMeta = tenantPlanMeta;

  form;

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private platform: PlatformService,
    private lookup: LookupService,
    private toast: UiToastService,
    private dialog: MatDialog
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(200)]],
      subscriptionPlan: ['Enterprise', Validators.required],
      isActive: [true],
      moduleCodes: new FormControl<string[]>([...DEFAULT_TENANT_MODULE_CODES], Validators.required),
      maxUsers: [null as number | null, Validators.min(0)],
      maxVehicles: [null as number | null, Validators.min(0)],
      maxDrivers: [null as number | null, Validators.min(0)],
      maxBranches: [null as number | null, Validators.min(0)],
      maxGpsDevices: [null as number | null, Validators.min(0)],
      logoUrl: [''],
      primaryColor: ['#1d4ed8'],
      website: [''],
      supportEmail: ['', Validators.email],
      country: ['United Arab Emirates'],
      currencyCode: [DEFAULT_CURRENCY],
      timeZone: ['Asia/Dubai']
    });
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (!idParam) {
      void this.router.navigate(['/platform/tenants']);
      return;
    }

    this.tenantId = Number(idParam);
    forkJoin({
      tenant: this.platform.getTenantById(this.tenantId),
      modules: this.platform.getModules(),
      countries: this.lookup.getCountryNames(),
      currencies: this.lookup.getCurrencyCodes(),
      timezones: this.lookup.getTimezoneIds()
    }).subscribe({
      next: ({ tenant, modules, countries, currencies, timezones }) => {
        this.countries = countries;
        this.currencies = currencies;
        this.timezones = timezones;
        this.initForm(tenant, modules);
      },
      error: () => {
        this.loading = false;
        this.toast.error('Failed to load tenant.');
        void this.router.navigate(['/platform/tenants']);
      }
    });
  }

  private initForm(tenant: TenantDetail, modules: TenantModuleDefinition[]): void {
    this.tenant = tenant;
    this.modules = modules;
    this.adminInfo = tenant.adminInfo;
    this.form.patchValue({
      name: tenant.name,
      subscriptionPlan: tenant.subscriptionPlan ?? 'Enterprise',
      isActive: tenant.isActive,
      moduleCodes: tenant.moduleCodes?.length ? [...tenant.moduleCodes] : [...DEFAULT_TENANT_MODULE_CODES],
      maxUsers: tenant.maxUsers ?? null,
      maxVehicles: tenant.maxVehicles ?? null,
      maxDrivers: tenant.maxDrivers ?? null,
      maxBranches: tenant.maxBranches ?? null,
      maxGpsDevices: tenant.maxGpsDevices ?? null,
      logoUrl: tenant.logoUrl ?? '',
      primaryColor: tenant.primaryColor ?? '#1d4ed8',
      website: tenant.website ?? '',
      supportEmail: tenant.supportEmail ?? '',
      country: tenant.country ?? 'United Arab Emirates',
      currencyCode: tenant.currencyCode ?? DEFAULT_CURRENCY,
      timeZone: tenant.timeZone ?? 'Asia/Dubai'
    });
    this.loading = false;
  }

  reset(): void {
    if (!this.tenantId) return;
    this.loading = true;
    this.platform.getTenantById(this.tenantId).subscribe({
      next: tenant => {
        this.initForm(tenant, this.modules);
        this.loading = false;
      },
      error: () => { this.loading = false; }
    });
  }

  toggleModule(code: string): void {
    const control = this.form.controls.moduleCodes;
    const current = control.value ?? [];
    const next = current.includes(code)
      ? current.filter(c => c !== code)
      : [...current, code];
    control.setValue(next);
    control.markAsDirty();
  }

  isModuleSelected(code: string): boolean {
    return (this.form.controls.moduleCodes.value ?? []).includes(code);
  }

  submit(): void {
    if (this.form.invalid || !this.tenantId) {
      this.form.markAllAsTouched();
      this.toast.error('Please fix validation errors before saving.');
      return;
    }
    if (this.saving) return;

    const v = this.form.getRawValue();
    this.saving = true;

    forkJoin({
      tenant: this.platform.updateTenant(this.tenantId, {
        name: v.name!.trim(),
        subscriptionPlan: v.subscriptionPlan,
        isActive: !!v.isActive,
        moduleCodes: v.moduleCodes ?? [],
        maxUsers: v.maxUsers,
        maxVehicles: v.maxVehicles,
        maxDrivers: v.maxDrivers,
        maxBranches: v.maxBranches,
        maxGpsDevices: v.maxGpsDevices
      }),
      branding: this.platform.updateTenantBranding(this.tenantId, {
        logoUrl: v.logoUrl?.trim() || null,
        primaryColor: v.primaryColor?.trim() || null,
        website: v.website?.trim() || null,
        supportEmail: v.supportEmail?.trim() || null,
        country: v.country || null,
        currencyCode: v.currencyCode?.trim()?.toUpperCase() || null,
        timeZone: v.timeZone || null
      })
    }).subscribe({
      next: () => {
        this.saving = false;
        this.toast.success('Tenant updated.');
        void this.router.navigate(['/platform/tenants']);
      },
      error: (err: unknown) => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Save failed.'));
      }
    });
  }

  planDef(planName: string) {
    return this.planDefinitions[planName] ?? null;
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

  applyPlan(planName: string): void {
    const def = applyPlanDefaults(planName);
    this.form.patchValue({
      maxUsers: def.quotas.maxUsers,
      maxVehicles: def.quotas.maxVehicles,
      maxDrivers: def.quotas.maxDrivers,
      maxBranches: def.quotas.maxBranches,
      maxGpsDevices: def.quotas.maxGpsDevices,
      moduleCodes: [...def.moduleCodes]
    });
    this.form.markAsDirty();
    this.toast.success(`${planName} plan defaults applied.`);
  }

  resetAdminPassword(): void {
    const password = prompt(
      `Enter new password for ${this.adminInfo?.email ?? 'admin'} (min 8 characters):`
    );
    if (!password) return;
    if (password.length < 8) {
      this.toast.success('Password must be at least 8 characters.');
      return;
    }
    if (!this.tenantId) return;

    this.resettingPassword = true;
    this.platform.resetTenantAdminPassword(this.tenantId, password).subscribe({
      next: () => {
        this.resettingPassword = false;
        this.toast.success('Admin password reset successfully.');
      },
      error: (err: unknown) => {
        this.resettingPassword = false;
        this.toast.error(apiErrorMessage(err, 'Password reset failed.'));
      }
    });
  }

  cancel(): void {
    void this.router.navigate(['/platform/tenants']);
  }
}
