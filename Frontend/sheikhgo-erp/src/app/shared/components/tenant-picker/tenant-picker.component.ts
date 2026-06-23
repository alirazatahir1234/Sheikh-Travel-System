import { Component, OnInit, OnDestroy } from '@angular/core';
import { Subject, takeUntil } from 'rxjs';
import { PlatformService } from '../../../core/services/platform.service';
import { PlatformTenantContextService } from '../../../core/services/platform-tenant-context.service';
import { Tenant } from '../../../core/models/platform.model';

@Component({
  selector: 'app-tenant-picker',
  templateUrl: './tenant-picker.component.html',
  styleUrls: ['./tenant-picker.component.scss']
})
export class TenantPickerComponent implements OnInit, OnDestroy {
  tenants: Tenant[] = [];
  selectedTenantId: number | null = null;
  loading = true;

  private destroy$ = new Subject<void>();

  constructor(
    private platform: PlatformService,
    private tenantContext: PlatformTenantContextService
  ) {}

  ngOnInit(): void {
    this.tenantContext.tenantId$
      .pipe(takeUntil(this.destroy$))
      .subscribe(id => {
        this.selectedTenantId = id;
      });

    this.loadTenants();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadTenants(): void {
    this.loading = true;
    this.platform.getTenants().subscribe({
      next: (tenants) => {
        this.tenants = tenants.filter(t => t.isActive);
        this.loading = false;

        if (this.selectedTenantId) {
          const tenant = this.tenants.find(t => t.id === this.selectedTenantId);
          if (tenant) {
            this.tenantContext.setTenantDetails(tenant);
          } else if (this.tenants.length > 0) {
            this.selectTenant(this.tenants[0].id);
          }
        } else if (this.tenants.length > 0) {
          this.selectTenant(this.tenants[0].id);
        }
      },
      error: () => {
        this.loading = false;
      }
    });
  }

  selectTenant(tenantId: number | null): void {
    if (tenantId === null) {
      this.tenantContext.clear();
      return;
    }

    const tenant = this.tenants.find(t => t.id === tenantId);
    if (tenant) {
      this.tenantContext.selectTenant(tenant);
    }
  }

  displayFn(tenantId: number | null): string {
    if (!tenantId) return '';
    const tenant = this.tenants.find(t => t.id === tenantId);
    return tenant?.name ?? '';
  }
}
