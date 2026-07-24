import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormControl, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { PlatformService } from '../../../core/services/platform.service';
import { LookupService } from '../../../core/services/lookup.service';
import { UserService } from '../../../core/services/user.service';
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
  CompanyFeature,
  CompanyLicense,
  RoleSummary,
  MenuCatalog,
  applyPlanDefaults,
  tenantDisplayCode,
  tenantPlanMeta,
  AuditEventListItem
} from '../../../core/models/platform.model';
import { CompanyUserSummary } from '../../../core/models/user.model';

@Component({
  standalone: false,
  selector: 'app-tenant-detail',
  templateUrl: './tenant-detail.component.html',
  styleUrls: ['./tenant-detail.component.scss']
})
export class TenantDetailComponent implements OnInit {
  loading = true;
  saving = false;
  resettingPassword = false;
  featuresLoading = false;
  tenantId?: number;
  tenant?: TenantDetail;
  modules: TenantModuleDefinition[] = [];
  features: CompanyFeature[] = [];
  license: CompanyLicense | null = null;
  userSummary: CompanyUserSummary | null = null;
  companyRoles: RoleSummary[] = [];
  permissionCatalogCount = 0;
  permissionCategories: string[] = [];
  menuModuleCount = 0;
  menuItemCount = 0;
  menuTopLabels: string[] = [];
  recentActivity: AuditEventListItem[] = [];
  adminInfo?: TenantAdminInfo | null;

  readonly planTiers = TENANT_PLAN_TIERS;
  readonly planDefinitions = PLAN_DEFINITIONS;
  readonly moduleIcons = MODULE_ICONS;
  countries: string[] = [];
  currencies: string[] = [];
  timezones: string[] = [];
  countrySearch = '';
  currencySearch = '';
  hideAdminPassword = true;
  readonly tenantDisplayCode = tenantDisplayCode;
  readonly tenantPlanMeta = tenantPlanMeta;

  form;

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private platform: PlatformService,
    private users: UserService,
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
      features: this.platform.getCompanyFeatures(this.tenantId).pipe(
        catchError(() => of([] as CompanyFeature[]))
      ),
      license: this.platform.getCompanyLicense(this.tenantId).pipe(
        catchError(() => of(null as CompanyLicense | null))
      ),
      userSummary: this.users.getCompanySummary(this.tenantId).pipe(
        catchError(() => of(null as CompanyUserSummary | null))
      ),
      roles: this.platform.getRolesForTenant(this.tenantId).pipe(
        catchError(() => of([] as RoleSummary[]))
      ),
      permissions: this.platform.getPermissions().pipe(
        catchError(() => of([]))
      ),
      menuCatalog: this.platform.getMenuCatalog().pipe(
        catchError(() => of(null as MenuCatalog | null))
      ),
      recentActivity: this.platform.getRecentAuditEvents(this.tenantId, null, 20).pipe(
        catchError(() => of([] as AuditEventListItem[]))
      ),
      countries: this.lookup.getCountryNames(),
      currencies: this.lookup.getCurrencyCodes(),
      timezones: this.lookup.getTimezoneIds()
    }).subscribe({
      next: ({ tenant, modules, features, license, userSummary, roles, permissions, menuCatalog, recentActivity, countries, currencies, timezones }) => {
        this.countries = countries;
        this.currencies = currencies;
        this.timezones = timezones;
        this.features = features ?? [];
        this.license = license;
        this.userSummary = userSummary;
        this.companyRoles = roles ?? [];
        this.recentActivity = recentActivity ?? [];
        this.permissionCatalogCount = permissions?.length ?? 0;
        this.permissionCategories = [...new Set(
          (permissions ?? [])
            .map(p => p.category)
            .filter((c): c is string => !!c && c.trim().length > 0)
        )].sort((a, b) => a.localeCompare(b));
        const menuModules = menuCatalog?.modules ?? [];
        this.menuModuleCount = menuModules.length;
        this.menuItemCount = menuModules.reduce((sum, m) => sum + (m.items?.length ?? 0), 0);
        this.menuTopLabels = menuModules
          .slice(0, 4)
          .map(m => m.displayName || m.name)
          .filter(Boolean);
        this.initForm(tenant, modules);
      },
      error: () => {
        this.loading = false;
        this.toast.error('Failed to load company.');
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

  get featureGroups(): { category: string; features: CompanyFeature[] }[] {
    const map = new Map<string, CompanyFeature[]>();
    for (const f of this.features) {
      const category = f.category || f.moduleKey || 'General';
      const list = map.get(category) ?? [];
      list.push(f);
      map.set(category, list);
    }
    return [...map.entries()]
      .map(([category, features]) => ({
        category,
        features: [...features].sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0))
      }))
      .sort((a, b) => a.category.localeCompare(b.category));
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
    return this.planDefinitions[planName] ?? this.planDefinitions['Enterprise'];
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

  get primaryColorPreview(): string {
    return this.form.controls.primaryColor.value?.trim() || '#007A57';
  }

  selectPlan(planName: string): void {
    this.form.controls.subscriptionPlan.setValue(planName);
    this.form.markAsDirty();
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
