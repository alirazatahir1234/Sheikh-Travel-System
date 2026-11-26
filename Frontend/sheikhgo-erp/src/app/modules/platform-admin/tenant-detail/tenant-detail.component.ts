import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormControl, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from '../../../core/services/auth.service';
import { PlatformService } from '../../../core/services/platform.service';
import { TenantContextService } from '../../../core/services/tenant-context.service';
import { LookupService } from '../../../core/services/lookup.service';
import { UserService } from '../../../core/services/user.service';
import { apiErrorMessage } from '../../../core/utils/api-error.util';
import {
  DEFAULT_CURRENCY,
  ALWAYS_ON_MODULE_CODES,
  DEFAULT_TENANT_MODULE_CODES,
  MODULE_ICONS,
  PREMIUM_ALERT_MODULE_CODES,
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
  formatCurrency,
  planYearlyAmount,
  tenantDisplayCode,
  tenantPlanMeta,
  AuditEventListItem
} from '../../../core/models/platform.model';
import { CompanyUserSummary } from '../../../core/models/user.model';
import { DEFAULT_TIME_ZONE } from '../../../core/constants/time-zone.constants';

const OPTIONAL_HTTP_URL = /^(https?:\/\/)?([\w-]+\.)+[\w-]{2,}(\/\S*)?$/i;

function optionalHttpUrlValidator() {
  return (control: { value: unknown }) => {
    const v = String(control.value ?? '').trim();
    if (!v) return null;
    return OPTIONAL_HTTP_URL.test(v) ? null : { url: true };
  };
}

function atLeastOneModuleValidator() {
  return (control: { value: unknown }) => {
    const codes = control.value;
    return Array.isArray(codes) && codes.length > 0 ? null : { required: true };
  };
}

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
  activityError = false;
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
  /** Configuration | Domains | Limits — Domains empty until a real domains API exists. */
  activeTab: 'configuration' | 'domains' | 'limits' = 'configuration';
  /** Invoice/subscription rows when overview loads; else license presence drives KPI. */
  subscriptionCount = 0;

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
    private users: UserService,
    private lookup: LookupService,
    private toast: UiToastService,
    private auth: AuthService,
    private tenantContext: TenantContextService
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(200)]],
      subscriptionPlan: ['Enterprise', Validators.required],
      isActive: [true],
      moduleCodes: new FormControl<string[]>([...DEFAULT_TENANT_MODULE_CODES], atLeastOneModuleValidator()),
      maxUsers: [null as number | null, Validators.min(0)],
      maxVehicles: [null as number | null, Validators.min(0)],
      maxDrivers: [null as number | null, Validators.min(0)],
      maxBranches: [null as number | null, Validators.min(0)],
      // Licensed devices: drives the yearly price (per device) and caps tracker registration.
      maxGpsDevices: [1 as number | null, Validators.min(1)],
      website: ['', optionalHttpUrlValidator()],
      supportEmail: ['', Validators.email],
      country: ['United Arab Emirates'],
      currencyCode: [DEFAULT_CURRENCY],
      timeZone: [DEFAULT_TIME_ZONE]
    });
  }

  /** Route opens with View; mutations require Manage. */
  get canManage(): boolean {
    return this.auth.hasPermission('Platform.Tenants.Manage');
  }

  /** True opt-in TenantFeatures only (GPS add-ons etc.) — never module-legacy duplicates. */
  private static readonly GPS_ADDON_KEYS = new Set([
    'geofencing',
    'speed-alerts',
    'route-playback'
  ]);

  private isRealAddOn(f: CompanyFeature): boolean {
    const key = (f.featureKey || '').toLowerCase();
    if (TenantDetailComponent.GPS_ADDON_KEYS.has(key)) return true;
    if (f.isAddOn === true) return true;
    return false;
  }

  get addonFeatures(): CompanyFeature[] {
    return this.features
      .filter(f => this.isRealAddOn(f))
      .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0)
        || (a.displayName || a.name || '').localeCompare(b.displayName || b.name || ''));
  }

  get enabledFeatureCount(): number {
    return this.addonFeatures.filter(f => f.isEnabled).length;
  }

  get kpiEmployees(): number | null {
    if (!this.userSummary && !this.tenant) return null;
    return this.userSummary?.totalUsers ?? this.tenant?.activeUserCount ?? 0;
  }

  get kpiDepartments(): number | null {
    if (!this.tenant && !this.userSummary) return null;
    return this.tenant?.departmentCount ?? this.userSummary?.departmentCount ?? 0;
  }

  get kpiUsers(): number | null {
    if (!this.userSummary && !this.tenant) return null;
    return this.userSummary?.totalUsers ?? this.tenant?.activeUserCount ?? 0;
  }

  get kpiDrivers(): number | null {
    if (!this.tenant && !this.userSummary) return null;
    return this.tenant?.activeDriverCount ?? this.userSummary?.drivers ?? 0;
  }

  get kpiVehicles(): number | null {
    if (!this.tenant) return null;
    return this.tenant.activeVehicleCount ?? 0;
  }

  get kpiModules(): number | null {
    if (!this.tenant) return null;
    const selected = new Set(
      (this.form.controls.moduleCodes.value ?? this.tenant.moduleCodes ?? []).map(c => c.toUpperCase())
    );
    return this.visibleModules.filter(m => this.isCoreModule(m.code) || selected.has(m.code.toUpperCase())).length;
  }

  /** Enableable, visible, released modules — excludes legacy / feature-governed / ComingSoon codes. */
  get visibleModules(): TenantModuleDefinition[] {
    return this.modules.filter(m =>
      m.visible !== false
      && m.isEnableable !== false
      && (m.status ?? 'Active').toLowerCase() === 'active');
  }

  moduleIcon(m: TenantModuleDefinition): string {
    return m.icon || this.moduleIcons[m.code] || 'extension';
  }

  isAlertChild(code: string): boolean {
    return (code || '').toUpperCase().startsWith('ALERT_');
  }

  isPremiumModule(code: string): boolean {
    return (PREMIUM_ALERT_MODULE_CODES as readonly string[]).includes((code || '').toUpperCase());
  }

  isModuleLocked(code: string): boolean {
    return this.isCoreModule(code) || (this.isAlertChild(code) && !this.isModuleSelected('ALERTS'));
  }

  get kpiFeatures(): number | null {
    if (this.loading) return null;
    return this.enabledFeatureCount;
  }

  get kpiSubscriptions(): number | null {
    if (this.loading) return null;
    if (this.subscriptionCount > 0) return this.subscriptionCount;
    return this.license ? 1 : 0;
  }

  /** No dedicated integrations API — show 0 until one exists (never invent). */
  get kpiIntegrations(): number | null {
    if (this.loading) return null;
    return 0;
  }

  /** UI label: Starter plan shown as Basic to match configuration mock. */
  planUiLabel(planName: string): string {
    return planName === 'Starter' ? 'Basic' : planName;
  }

  setTab(tab: 'configuration' | 'domains' | 'limits'): void {
    this.activeTab = tab;
  }

  isActivityFailed(e: AuditEventListItem): boolean {
    return e.success === false
      || /fail|error|denied/i.test(e.eventKey || '')
      || /fail|error/i.test(e.severity || '');
  }

  copyLicenseKey(): void {
    const key = this.license?.subscriptionCode?.trim();
    if (!key) {
      this.toast.warning('No license key available for this company.');
      return;
    }
    void navigator.clipboard.writeText(key).then(
      () => this.toast.success('License key copied.'),
      () => this.toast.error('Could not copy license key.')
    );
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (!idParam) {
      void this.router.navigate(['/platform/tenants']);
      return;
    }

    this.tenantId = Number(idParam);
    if (!Number.isFinite(this.tenantId) || this.tenantId <= 0) {
      this.toast.error('Invalid company id.');
      void this.router.navigate(['/platform/tenants']);
      return;
    }

    this.tenantContext.selectTenantById(this.tenantId);

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
        catchError(() => {
          this.activityError = true;
          return of([] as AuditEventListItem[]);
        })
      ),
      subscriptionOverview: this.platform.getSubscriptionOverview(this.tenantId).pipe(
        catchError(() => of(null))
      ),
      countries: this.lookup.getCountryNames(),
      currencies: this.lookup.getCurrencyCodes(),
      timezones: this.lookup.getTimezoneIds()
    }).subscribe({
      next: ({ tenant, modules, features, license, userSummary, roles, permissions, menuCatalog, recentActivity, subscriptionOverview, countries, currencies, timezones }) => {
        this.countries = countries;
        this.currencies = currencies;
        this.timezones = timezones;
        this.features = features ?? [];
        this.license = license;
        this.userSummary = userSummary;
        this.companyRoles = roles ?? [];
        this.recentActivity = recentActivity ?? [];
        const invoices = subscriptionOverview?.invoices?.length ?? 0;
        this.subscriptionCount = invoices > 0 ? invoices : (subscriptionOverview?.subscription ? 1 : 0);
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
      moduleCodes: (() => {
        const codes = tenant.moduleCodes?.length ? [...tenant.moduleCodes] : [...DEFAULT_TENANT_MODULE_CODES];
        return this.withAlwaysOn(codes);
      })(),
      maxUsers: tenant.maxUsers ?? null,
      maxVehicles: tenant.maxVehicles ?? null,
      maxDrivers: tenant.maxDrivers ?? null,
      maxBranches: tenant.maxBranches ?? null,
      maxGpsDevices: tenant.maxGpsDevices ?? null,
      website: tenant.website ?? '',
      supportEmail: tenant.supportEmail ?? '',
      country: tenant.country ?? 'United Arab Emirates',
      currencyCode: tenant.currencyCode ?? DEFAULT_CURRENCY,
      timeZone: tenant.timeZone ?? DEFAULT_TIME_ZONE
    });
    if (this.canManage) {
      this.form.enable({ emitEvent: false });
    } else {
      this.form.disable({ emitEvent: false });
    }
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

  isCoreModule(code: string): boolean {
    return (ALWAYS_ON_MODULE_CODES as readonly string[]).includes((code || '').toUpperCase());
  }

  private withAlwaysOn(codes: string[]): string[] {
    const next = [...codes];
    for (const c of ALWAYS_ON_MODULE_CODES) {
      if (!next.some(x => x.toUpperCase() === c)) next.push(c);
    }
    return next;
  }

  toggleModule(code: string): void {
    if (!this.canManage || this.isModuleLocked(code)) return;
    const control = this.form.controls.moduleCodes;
    const current = control.value ?? [];
    let next = current.includes(code)
      ? current.filter(c => c !== code)
      : [...current, code];
    // Children depend on ALERTS; the backend re-adds the parent if any child stays selected.
    if (code.toUpperCase() === 'ALERTS' && current.includes(code)) {
      next = next.filter(c => !this.isAlertChild(c));
    }
    control.setValue(this.withAlwaysOn(next));
    control.markAsDirty();
  }

  isModuleSelected(code: string): boolean {
    if (this.isCoreModule(code)) return true;
    return (this.form.controls.moduleCodes.value ?? []).includes(code);
  }

  get featureGroups(): { category: string; features: CompanyFeature[] }[] {
    const map = new Map<string, CompanyFeature[]>();
    for (const f of this.addonFeatures) {
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

  /** Category row is Enabled when at least one real feature in the group is on. */
  isFeatureCategoryEnabled(group: { features: CompanyFeature[] }): boolean {
    return group.features.some(f => f.isEnabled);
  }

  isAddonEnabled(f: CompanyFeature): boolean {
    return !!f.isEnabled;
  }

  featureCategoryIcon(category: string): string {
    const key = (category || '').trim().toLowerCase();
    const map: Record<string, string> = {
      administration: 'admin_panel_settings',
      admin: 'admin_panel_settings',
      'gps add-ons': 'extension',
      addons: 'extension',
      'add-ons': 'extension',
      bookings: 'event_note',
      booking: 'event_note',
      crm: 'handshake',
      customers: 'people',
      customer: 'people',
      drivers: 'badge',
      driver: 'badge',
      financial: 'payments',
      finance: 'payments',
      fleet: 'local_shipping',
      gps: 'my_location',
      tracking: 'my_location',
      maintenance: 'build',
      reports: 'assessment',
      analytics: 'insights',
      travel: 'flight_takeoff',
      trips: 'map',
      trip: 'map',
      vehicles: 'directions_car',
      vehicle: 'directions_car',
      fuel: 'local_gas_station',
      ai: 'smart_toy',
      whatsapp: 'chat',
      website: 'language',
      security: 'security',
      platform: 'settings_applications',
      general: 'extension'
    };
    return map[key] || 'extension';
  }

  submit(): void {
    if (!this.canManage) {
      this.toast.error('You do not have permission to update companies.');
      return;
    }
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
        website: v.website?.trim() || null,
        supportEmail: v.supportEmail?.trim() || null,
        country: v.country || null,
        currencyCode: v.currencyCode?.trim()?.toUpperCase() || null,
        timeZone: v.timeZone || null
      })
    }).subscribe({
      next: () => {
        this.saving = false;
        this.toast.success('Company updated.');
        this.reset();
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

  selectPlan(planName: string): void {
    if (!this.canManage) return;
    this.form.controls.subscriptionPlan.setValue(planName);
    this.form.markAsDirty();
  }

  /** Live monthly price for the plan + licensed vehicles currently in the form. */
  get yearlyPriceLabel(): string {
    const plan = this.form.controls.subscriptionPlan.value;
    const devices = this.form.controls.maxGpsDevices.value;
    const def = applyPlanDefaults(plan ?? 'Enterprise');
    const count = Math.max(1, Math.floor(devices ?? 1));
    const monthly = def.monthlyPricePerVehicleUsd * count + def.platformFeeMonthlyUsd;
    const yearly = planYearlyAmount(plan, count);
    return `${formatCurrency(def.monthlyPricePerVehicleUsd, 'USD')}/veh × ${count}`
      + (def.platformFeeMonthlyUsd ? ` + ${formatCurrency(def.platformFeeMonthlyUsd, 'USD')} fee` : '')
      + ` = ${formatCurrency(monthly, 'USD')}/mo`
      + ` · annual ${formatCurrency(yearly, 'USD')} (15% off)`;
  }

  applyPlan(planName: string): void {
    if (!this.canManage) return;
    const def = applyPlanDefaults(planName);
    const premiumOptIns = (this.form.controls.moduleCodes.value ?? []).filter(c => this.isPremiumModule(c));
    this.form.patchValue({
      maxUsers: def.quotas.maxUsers,
      maxVehicles: def.quotas.maxVehicles,
      maxDrivers: def.quotas.maxDrivers,
      maxBranches: def.quotas.maxBranches,
      // Keep licensed devices — they are what the company pays for; the plan only changes the per-device price.
      maxGpsDevices: this.form.controls.maxGpsDevices.value ?? def.quotas.maxGpsDevices,
      moduleCodes: this.withAlwaysOn([...new Set([...def.moduleCodes, ...premiumOptIns])])
    });
    this.form.markAsDirty();
    this.toast.success(`${planName} plan defaults applied.`);
  }

  resetAdminPassword(): void {
    if (!this.canManage) {
      this.toast.error('You do not have permission to reset admin passwords.');
      return;
    }
    const password = prompt(
      `Enter new password for ${this.adminInfo?.email ?? 'admin'} (min 8 characters):`
    );
    if (!password) return;
    if (password.length < 8) {
      this.toast.error('Password must be at least 8 characters.');
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
