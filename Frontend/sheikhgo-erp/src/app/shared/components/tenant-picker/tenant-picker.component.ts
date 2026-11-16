import { Component, OnInit, OnDestroy } from '@angular/core';
import { Subject, takeUntil } from 'rxjs';
import { PlatformService } from '../../../core/services/platform.service';
import { PlatformTenantContextService } from '../../../core/services/platform-tenant-context.service';
import { SwitchableTenant, Tenant } from '../../../core/models/platform.model';

@Component({
  standalone: false,
  selector: 'app-tenant-picker',
  templateUrl: './tenant-picker.component.html',
  styleUrls: ['./tenant-picker.component.scss']
})
export class TenantPickerComponent implements OnInit, OnDestroy {
  tenants: SwitchableTenant[] = [];
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
    this.platform.getSwitchableTenants().subscribe({
      next: (tenants) => {
        // SG-11: include suspended/expired/deactivated so renew is not a one-way door.
        this.tenants = [...(tenants ?? [])].sort((a, b) => a.name.localeCompare(b.name));
        this.loading = false;

        if (this.selectedTenantId) {
          const tenant = this.tenants.find(t => t.id === this.selectedTenantId);
          if (tenant) {
            this.tenantContext.setTenantDetails(this.toTenant(tenant));
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
      this.tenantContext.selectTenant(this.toTenant(tenant));
    }
  }

  displayFn(tenantId: number | null): string {
    if (!tenantId) return '';
    const tenant = this.tenants.find(t => t.id === tenantId);
    return tenant?.name ?? '';
  }

  optionLabel(tenant: SwitchableTenant): string {
    const base = `${tenant.name} (${tenant.slug || tenant.code || tenant.id})`;
    if (!tenant.status || tenant.status === 'Active') return base;
    return `${base} — ${tenant.status}`;
  }

  private toTenant(row: SwitchableTenant): Tenant {
    return {
      id: row.id,
      name: row.name,
      slug: row.slug,
      code: row.code,
      isActive: row.isActive,
      status: row.status,
      subscriptionStatus: row.subscriptionStatus,
      branchCount: 0,
      departmentCount: 0,
      roleCount: 0,
      createdAt: '',
      activeUserCount: 0,
      activeVehicleCount: 0
    };
  }
}
