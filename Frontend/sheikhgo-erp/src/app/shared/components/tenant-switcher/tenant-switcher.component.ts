import { Component, OnDestroy, OnInit } from '@angular/core';
import { Subject, takeUntil } from 'rxjs';
import { PlatformService } from '../../../core/services/platform.service';
import { TenantContextService } from '../../../core/services/tenant-context.service';
import { SwitchableTenant } from '../../../core/models/platform.model';

const ALL_VALUE = '__all__';

@Component({
  standalone: false,
  selector: 'app-tenant-switcher',
  templateUrl: './tenant-switcher.component.html',
  styleUrls: ['./tenant-switcher.component.scss']
})
export class TenantSwitcherComponent implements OnInit, OnDestroy {
  readonly allValue = ALL_VALUE;

  visible = false;
  loading = true;
  tenants: SwitchableTenant[] = [];
  /** Selected mat-option value: tenant id, ALL_VALUE, or null (unset). */
  selectedValue: number | string | null = null;
  label = 'Pick a company';

  private destroy$ = new Subject<void>();

  constructor(
    private platform: PlatformService,
    private tenantContext: TenantContextService
  ) {}

  ngOnInit(): void {
    this.visible = this.tenantContext.canUseSwitcher;
    if (!this.visible) return;

    this.syncFromContext();
    this.tenantContext.tenantId$.pipe(takeUntil(this.destroy$)).subscribe(() => this.syncFromContext());
    this.tenantContext.isAllCompanies$.pipe(takeUntil(this.destroy$)).subscribe(() => this.syncFromContext());
    this.tenantContext.tenant$.pipe(takeUntil(this.destroy$)).subscribe(() => this.syncFromContext());

    this.loadTenants();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onSelectionChange(value: number | string | null): void {
    if (value === ALL_VALUE) {
      this.tenantContext.selectAll();
      return;
    }
    if (value == null || value === '') {
      this.tenantContext.clear();
      return;
    }
    const id = typeof value === 'number' ? value : parseInt(String(value), 10);
    if (!Number.isFinite(id)) return;
    const row = this.tenants.find(t => t.id === id);
    if (row) {
      this.tenantContext.selectSwitchable(row);
    } else {
      this.tenantContext.selectTenantById(id);
    }
  }

  optionLabel(tenant: SwitchableTenant): string {
    const base = tenant.name;
    if (!tenant.status || tenant.status === 'Active') return base;
    return `${base} — ${tenant.status}`;
  }

  private loadTenants(): void {
    this.loading = true;
    this.platform.getSwitchableTenants().subscribe({
      next: list => {
        this.tenants = [...(list ?? [])].sort((a, b) => a.name.localeCompare(b.name));
        this.loading = false;
        this.hydrateDetails();
        this.syncFromContext();
      },
      error: () => {
        this.loading = false;
      }
    });
  }

  private hydrateDetails(): void {
    const id = this.tenantContext.currentTenantId;
    if (id == null || this.tenantContext.isAllCompanies) return;
    const row = this.tenants.find(t => t.id === id);
    if (row) this.tenantContext.setTenantDetails({
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
    });
  }

  private syncFromContext(): void {
    if (this.tenantContext.isAllCompanies) {
      this.selectedValue = ALL_VALUE;
      this.label = 'All companies';
      return;
    }
    const id = this.tenantContext.currentTenantId;
    if (id == null) {
      this.selectedValue = null;
      this.label = 'Pick a company';
      return;
    }
    this.selectedValue = id;
    const name = this.tenantContext.currentTenant?.name
      ?? this.tenants.find(t => t.id === id)?.name;
    this.label = name ?? `Company #${id}`;
  }
}
