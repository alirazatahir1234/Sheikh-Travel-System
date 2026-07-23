import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { PlatformTenantContextService } from '../../../core/services/platform-tenant-context.service';
import { PlatformService } from '../../../core/services/platform.service';
import { CompanyFeature, Tenant } from '../../../core/models/platform.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
  standalone: false,
  selector: 'app-feature-management',
  templateUrl: './feature-management.component.html',
  styleUrls: ['./feature-management.component.scss']
})
export class FeatureManagementComponent implements OnInit, OnDestroy {
  loading = false;
  saving = false;
  selectedTenant: Tenant | null = null;
  features: CompanyFeature[] = [];
  filter: 'all' | 'enabled' | 'comingSoon' = 'all';
  private originalEnabled = new Set<string>();
  private destroy$ = new Subject<void>();

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private toast: UiToastService,
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
          this.loadFeatures(id);
        } else {
          this.features = [];
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get filteredFeatures(): CompanyFeature[] {
    if (this.filter === 'enabled') {
      return this.features.filter(f => f.isEnabled);
    }
    if (this.filter === 'comingSoon') {
      return this.features.filter(f => !this.canToggle(f));
    }
    return this.features;
  }

  get categories(): string[] {
    const set = new Set(
      this.filteredFeatures.map(f => f.category || 'General')
    );
    return [...set].sort((a, b) => a.localeCompare(b));
  }

  featuresInCategory(category: string): CompanyFeature[] {
    return this.filteredFeatures.filter(f => (f.category || 'General') === category);
  }

  canToggle(feature: CompanyFeature): boolean {
    if (feature.canToggle === false) return false;
    if (feature.canToggle === true) return true;
    const status = (feature.status || 'Active').replace(/\s+/g, '');
    return (status === 'Active' || status === 'Beta') && feature.isModuleInstalled !== false;
  }

  formatStatus(status?: string | null): string {
    if (!status) return 'Active';
    if (status === 'ComingSoon') return 'Coming Soon';
    return status;
  }

  featureIcon(feature: CompanyFeature): string {
    return feature.icon || 'tune';
  }

  private loadFeatures(tenantId: number): void {
    this.loading = true;
    this.platform.getCompanyFeatures(tenantId).subscribe({
      next: (features) => {
        this.features = features.map(f => ({ ...f }));
        this.originalEnabled = new Set(
          features.filter(f => f.isEnabled && this.canToggle(f)).map(f => f.featureKey)
        );
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load features.'));
      }
    });
  }

  toggleFeature(feature: CompanyFeature, enabled: boolean): void {
    if (!this.canToggle(feature)) return;
    feature.isEnabled = enabled;
  }

  get hasChanges(): boolean {
    const current = new Set(
      this.features.filter(f => f.isEnabled && this.canToggle(f)).map(f => f.featureKey)
    );
    if (current.size !== this.originalEnabled.size) return true;
    for (const key of current) {
      if (!this.originalEnabled.has(key)) return true;
    }
    return false;
  }

  saveFeatures(): void {
    if (!this.selectedTenant) return;
    const tenantId = this.selectedTenant.id;
    const enabledKeys = this.features
      .filter(f => f.isEnabled && this.canToggle(f))
      .map(f => f.featureKey);

    this.saving = true;
    this.platform.setCompanyFeatures(tenantId, enabledKeys).subscribe({
      next: () => {
        this.saving = false;
        this.toast.success('Features updated.');
        this.loadFeatures(tenantId);
      },
      error: (err) => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Failed to update features.'));
      }
    });
  }

  resetFeatures(): void {
    if (!this.selectedTenant) return;
    this.loadFeatures(this.selectedTenant.id);
  }

  refresh(): void {
    if (this.selectedTenant) this.loadFeatures(this.selectedTenant.id);
  }

  goBack(): void {
    void this.router.navigate(['/platform']);
  }
}
