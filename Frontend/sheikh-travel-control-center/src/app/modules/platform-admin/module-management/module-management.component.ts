import { Component, OnInit, OnDestroy } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subject, takeUntil } from 'rxjs';
import { PlatformTenantContextService } from '../../../core/services/platform-tenant-context.service';
import { PlatformService } from '../../../core/services/platform.service';
import {
  Tenant,
  ModuleStatus,
  LicenseLimit,
  TenantModuleOverview,
  MODULE_ICONS
} from '../../../core/models/platform.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
  selector: 'app-module-management',
  templateUrl: './module-management.component.html',
  styleUrls: ['./module-management.component.scss']
})
export class ModuleManagementComponent implements OnInit, OnDestroy {
  loading = false;
  saving = false;
  selectedTenant: Tenant | null = null;
  overview: TenantModuleOverview | null = null;

  modules: ModuleStatus[] = [];
  private originalEnabled = new Set<string>();

  private destroy$ = new Subject<void>();

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private snackBar: MatSnackBar,
    private tenantContext: PlatformTenantContextService,
    private platform: PlatformService
  ) {}

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
      .pipe(takeUntil(this.destroy$))
      .subscribe(id => {
        if (id) {
          this.loadOverview(id);
        } else {
          this.overview = null;
          this.modules = [];
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadOverview(tenantId: number): void {
    this.loading = true;
    this.platform.getTenantModuleOverview(tenantId).subscribe({
      next: (overview) => {
        this.overview = overview;
        this.modules = overview.modules.map(m => ({ ...m }));
        this.originalEnabled = new Set(overview.modules.filter(m => m.isEnabled).map(m => m.code));
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.snackBar.open(apiErrorMessage(err, 'Failed to load module overview.'), 'Close', { duration: 4000 });
      }
    });
  }

  toggleModule(module: ModuleStatus, enabled: boolean): void {
    module.isEnabled = enabled;
  }

  get hasChanges(): boolean {
    const current = new Set(this.modules.filter(m => m.isEnabled).map(m => m.code));
    if (current.size !== this.originalEnabled.size) return true;
    for (const code of current) {
      if (!this.originalEnabled.has(code)) return true;
    }
    return false;
  }

  saveModules(): void {
    if (!this.overview) return;

    const tenantId = this.overview.tenantId;
    const enabledCodes = this.modules.filter(m => m.isEnabled).map(m => m.code);

    this.saving = true;
    this.platform.setTenantModules(tenantId, enabledCodes).subscribe({
      next: () => {
        this.saving = false;
        this.snackBar.open('Modules updated.', 'Close', { duration: 2500 });
        this.loadOverview(tenantId);
      },
      error: (err) => {
        this.saving = false;
        this.snackBar.open(apiErrorMessage(err, 'Failed to update modules.'), 'Close', { duration: 4000 });
      }
    });
  }

  resetModules(): void {
    this.modules = (this.overview?.modules ?? []).map(m => ({ ...m }));
  }

  refresh(): void {
    if (this.overview) this.loadOverview(this.overview.tenantId);
  }

  getModuleIcon(code: string): string {
    return MODULE_ICONS[code] ?? 'extension';
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

  goBack(): void {
    this.router.navigate(['/platform/tenants']);
  }
}
