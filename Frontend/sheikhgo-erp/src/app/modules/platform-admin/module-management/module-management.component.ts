import { Component, OnInit, OnDestroy } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, of } from 'rxjs';
import { catchError, filter, switchMap, takeUntil, tap } from 'rxjs/operators';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { PlatformTenantContextService } from '../../../core/services/platform-tenant-context.service';
import { PlatformService } from '../../../core/services/platform.service';
import { AuthService } from '../../../core/services/auth.service';
import {
  Tenant,
  ModuleStatus,
  LicenseLimit,
  TenantModuleOverview,
  MODULE_ICONS
} from '../../../core/models/platform.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

interface ReloadRequest {
  tenantId: number;
  soft: boolean;
}

@Component({
  standalone: false,
  selector: 'app-module-management',
  templateUrl: './module-management.component.html',
  styleUrls: ['./module-management.component.scss']
})
export class ModuleManagementComponent implements OnInit, OnDestroy {
  loading = false;
  saving = false;
  loadError = false;
  selectedTenant: Tenant | null = null;
  overview: TenantModuleOverview | null = null;
  filter: 'all' | 'installed' | 'comingSoon' = 'all';

  modules: ModuleStatus[] = [];
  private originalEnabled = new Set<string>();

  private destroy$ = new Subject<void>();
  private reload$ = new Subject<ReloadRequest>();

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private toast: UiToastService,
    private tenantContext: PlatformTenantContextService,
    private platform: PlatformService,
    private auth: AuthService
  ) {}

  get canManage(): boolean {
    return this.auth.hasPermission('Platform.Tenants.Manage');
  }

  get selectedTenantId(): number | null {
    return this.selectedTenant?.id ?? this.tenantContext.currentTenantId ?? null;
  }

  ngOnInit(): void {
    const tenantIdParam = this.route.snapshot.queryParamMap.get('tenantId');
    if (tenantIdParam) {
      const id = parseInt(tenantIdParam, 10);
      if (Number.isFinite(id)) {
        this.tenantContext.selectTenantById(id);
      }
    }

    this.tenantContext.tenant$
      .pipe(takeUntil(this.destroy$))
      .subscribe(tenant => {
        this.selectedTenant = tenant;
      });

    this.tenantContext.tenantId$
      .pipe(
        takeUntil(this.destroy$),
        tap(id => {
          if (!id) {
            this.clearState();
            this.loading = false;
            this.loadError = false;
          }
        }),
        filter((id): id is number => id != null && id > 0),
        switchMap(id => this.fetchOverview(id, false))
      )
      .subscribe(overview => this.applyOverview(overview));

    this.reload$
      .pipe(
        takeUntil(this.destroy$),
        switchMap(req => this.fetchOverview(req.tenantId, req.soft))
      )
      .subscribe(overview => this.applyOverview(overview));
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get filteredModules(): ModuleStatus[] {
    if (this.filter === 'installed') {
      return this.modules.filter(m => m.isEnabled || m.isInstalled);
    }
    if (this.filter === 'comingSoon') {
      return this.modules.filter(m => this.isComingSoon(m));
    }
    return this.modules;
  }

  get categories(): string[] {
    const set = new Set(this.filteredModules.map(m => m.category || 'General'));
    return [...set].sort((a, b) => a.localeCompare(b));
  }

  modulesInCategory(category: string): ModuleStatus[] {
    return this.filteredModules
      .filter(m => (m.category || 'General') === category)
      .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0) || a.code.localeCompare(b.code));
  }

  filterCount(kind: 'all' | 'installed' | 'comingSoon'): number {
    if (kind === 'all') return this.modules.length;
    if (kind === 'installed') {
      return this.modules.filter(m => m.isEnabled || m.isInstalled).length;
    }
    return this.modules.filter(m => this.isComingSoon(m)).length;
  }

  /** Catalog enableability (ignores RBAC) — used for dirty-state and dependency walks. */
  isEnableable(module: ModuleStatus): boolean {
    if (module.canToggle === false) return false;
    if (module.canToggle === true) return true;
    const status = (module.status || 'Active').replace(/\s+/g, '');
    return status === 'Active';
  }

  /** Whether the current user may flip the toggle in the UI. */
  canToggle(module: ModuleStatus): boolean {
    return this.canManage && this.isEnableable(module);
  }

  isComingSoon(module: ModuleStatus): boolean {
    return (module.status || '').replace(/\s+/g, '') === 'ComingSoon';
  }

  isBundledActive(module: ModuleStatus): boolean {
    if (this.isEnableable(module) || this.isComingSoon(module)) return false;
    const status = (module.status || 'Active').replace(/\s+/g, '');
    return status === 'Active' && !!module.dependencies?.length;
  }

  includedInLabel(module: ModuleStatus): string | null {
    const parentCode = module.dependencies?.[0];
    if (!parentCode) return null;
    const parent = this.modules.find(m => m.code.toLowerCase() === parentCode.toLowerCase());
    const label = parent?.displayName || parent?.name || parentCode;
    return `Included in ${label}`;
  }

  readonlyActionLabel(module: ModuleStatus): string {
    if (!this.canManage && this.isEnableable(module)) {
      return module.isEnabled ? 'Enabled' : 'Disabled';
    }
    if (this.isComingSoon(module)) return this.formatStatus(module.status);
    const included = this.includedInLabel(module);
    if (included) return included;
    return this.formatStatus(module.status);
  }

  formatStatus(status?: string | null): string {
    if (!status) return 'Active';
    if (status === 'ComingSoon') return 'Coming Soon';
    return status;
  }

  moduleIcon(module: ModuleStatus): string {
    return module.icon || MODULE_ICONS[module.code] || 'extension';
  }

  trackByCode(_index: number, module: ModuleStatus): string {
    return module.code;
  }

  private fetchOverview(tenantId: number, soft: boolean) {
    this.loading = true;
    this.loadError = false;
    if (!soft) {
      this.overview = null;
      this.modules = [];
      this.originalEnabled = new Set();
    }
    return this.platform.getTenantModuleOverview(tenantId).pipe(
      catchError(err => {
        this.toast.error(apiErrorMessage(err, 'Failed to load module overview.'));
        this.loadError = true;
        if (!soft) {
          this.overview = null;
          this.modules = [];
        }
        return of(null);
      })
    );
  }

  private applyOverview(overview: TenantModuleOverview | null): void {
    this.loading = false;
    if (!overview) {
      if (!this.overview) {
        this.modules = [];
        this.originalEnabled = new Set();
      }
      return;
    }

    this.overview = overview;
    this.modules = overview.modules.map(m => ({ ...m }));
    this.originalEnabled = new Set(
      overview.modules.filter(m => m.isEnabled && this.isEnableable(m)).map(m => m.code)
    );
    this.loadError = false;
  }

  private clearState(): void {
    this.overview = null;
    this.modules = [];
    this.originalEnabled = new Set();
  }

  toggleModule(module: ModuleStatus, enabled: boolean): void {
    if (!this.canToggle(module) || this.saving) return;

    if (enabled) {
      const before = new Set(this.modules.filter(m => m.isEnabled).map(m => m.code.toLowerCase()));
      this.enableWithDependencies(module.code);
      const autoEnabled = this.modules.filter(
        m =>
          m.isEnabled &&
          !before.has(m.code.toLowerCase()) &&
          m.code.toLowerCase() !== module.code.toLowerCase()
      );
      if (autoEnabled.length) {
        this.toast.info(
          `Also enabled required module(s): ${autoEnabled.map(m => m.displayName || m.name).join(', ')}`
        );
      }
      if (module.isLicensed === false) {
        this.toast.warning(
          `${module.displayName || module.name} is not in the current plan license — enable only if intentionally entitled.`
        );
      }
    } else {
      const before = new Set(this.modules.filter(m => m.isEnabled).map(m => m.code.toLowerCase()));
      this.disableWithDependents(module.code);
      const autoDisabled = this.modules.filter(
        m =>
          !m.isEnabled &&
          before.has(m.code.toLowerCase()) &&
          m.code.toLowerCase() !== module.code.toLowerCase()
      );
      if (autoDisabled.length) {
        this.toast.info(
          `Also disabled dependent module(s): ${autoDisabled.map(m => m.displayName || m.name).join(', ')}`
        );
      }
    }
  }

  /** Enable a module and its enableable transitive dependencies (matches API expand). */
  private enableWithDependencies(code: string): void {
    const byCode = new Map(this.modules.map(m => [m.code.toLowerCase(), m]));
    const visited = new Set<string>();

    const enable = (raw: string) => {
      const key = raw.toLowerCase();
      if (visited.has(key)) return;
      visited.add(key);
      const mod = byCode.get(key);
      if (!mod || !this.isEnableable(mod)) return;
      mod.isEnabled = true;
      mod.isInstalled = true;
      for (const dep of mod.dependencies ?? []) {
        enable(dep);
      }
    };

    enable(code);
  }

  /** Disable a module and any enableable modules that depend on it (transitively). */
  private disableWithDependents(code: string): void {
    const target = code.toLowerCase();
    const toDisable = new Set<string>([target]);
    let changed = true;
    while (changed) {
      changed = false;
      for (const mod of this.modules) {
        if (!this.isEnableable(mod)) continue;
        const key = mod.code.toLowerCase();
        if (toDisable.has(key)) continue;
        const deps = (mod.dependencies ?? []).map(d => d.toLowerCase());
        if (deps.some(d => toDisable.has(d))) {
          toDisable.add(key);
          changed = true;
        }
      }
    }

    for (const mod of this.modules) {
      if (toDisable.has(mod.code.toLowerCase()) && this.isEnableable(mod)) {
        mod.isEnabled = false;
        mod.isInstalled = false;
      }
    }
  }

  get hasChanges(): boolean {
    const current = new Set(
      this.modules.filter(m => m.isEnabled && this.isEnableable(m)).map(m => m.code)
    );
    if (current.size !== this.originalEnabled.size) return true;
    for (const code of current) {
      if (!this.originalEnabled.has(code)) return true;
    }
    return false;
  }

  saveModules(): void {
    if (!this.overview || this.saving || !this.hasChanges || !this.canManage) return;

    const tenantId = this.overview.tenantId;
    const enabledCodes = this.modules
      .filter(m => m.isEnabled && this.isEnableable(m))
      .map(m => m.code);

    this.saving = true;
    this.platform.setTenantModules(tenantId, enabledCodes).subscribe({
      next: () => {
        this.saving = false;
        this.toast.success('Modules updated.');
        if (this.selectedTenantId === tenantId) {
          this.reload$.next({ tenantId, soft: true });
        }
      },
      error: err => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Failed to update modules.'));
      }
    });
  }

  resetModules(): void {
    if (!this.overview || this.saving || !this.canManage) return;
    this.modules = this.overview.modules.map(m => ({ ...m }));
  }

  refresh(): void {
    const id = this.selectedTenantId ?? this.overview?.tenantId ?? null;
    if (id) this.reload$.next({ tenantId: id, soft: !!this.overview });
  }

  usagePercent(limit: LicenseLimit): number {
    if (!limit.limit || limit.limit <= 0) return 0;
    return Math.min(100, Math.round((limit.used / limit.limit) * 100));
  }

  usageClass(limit: LicenseLimit): string {
    const pct = this.usagePercent(limit);
    if (!limit.limit) return 'usage-unlimited';
    if (pct >= 90) return 'usage-critical';
    if (pct >= 75) return 'usage-warning';
    return 'usage-healthy';
  }

  usageBarWidth(limit: LicenseLimit): number {
    if (!limit.limit || limit.limit <= 0) return 100;
    return this.usagePercent(limit);
  }

  goBack(): void {
    void this.router.navigate(['/platform']);
  }
}
