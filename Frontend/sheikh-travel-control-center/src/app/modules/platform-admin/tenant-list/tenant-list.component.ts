import { Component, OnInit, ViewChild } from '@angular/core';
import { Router } from '@angular/router';
import { MatPaginator } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableDataSource } from '@angular/material/table';
import { forkJoin } from 'rxjs';
import { PlatformService } from '../../../core/services/platform.service';
import {
  Tenant,
  TenantHealthStatus,
  TenantManagementStats,
  TenantModuleDefinition,
  TENANT_PLAN_TIERS,
  TENANT_TYPES,
  formatModuleBadge,
  tenantDisplayCode,
  tenantHealthStatus,
  tenantModuleList,
  tenantPlanMeta,
  tenantUsageLabel,
  tenantUsagePercent,
  TenantPlanMeta
} from '../../../core/models/platform.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

interface TenantFilters {
  search: string;
  status: string;
  country: string;
  tenantType: string;
  module: string;
  plan: string;
}

@Component({
  selector: 'app-tenant-list',
  templateUrl: './tenant-list.component.html',
  styleUrls: ['./tenant-list.component.scss']
})
export class TenantListComponent implements OnInit {
  @ViewChild(MatPaginator) paginator?: MatPaginator;

  loading = true;
  tenants: Tenant[] = [];
  modules: TenantModuleDefinition[] = [];
  selectedIds = new Set<number>();
  selectedRow: Tenant | null = null;
  stats: TenantManagementStats = {
    activeTenants: 0,
    activeUsers: 0,
    activeVehicles: 0,
    expiringPlans: 0,
    monthlyRevenue: 0,
    tenantsAddedThisMonth: 0
  };

  filters: TenantFilters = {
    search: '',
    status: 'all',
    country: 'all',
    tenantType: 'all',
    module: 'all',
    plan: 'all'
  };

  dataSource = new MatTableDataSource<Tenant>([]);
  readonly pageSize = 10;
  readonly displayedColumns = ['select', 'tenant', 'type', 'country', 'modules', 'plan', 'usage', 'health', 'actions'];
  readonly planOptions = TENANT_PLAN_TIERS;
  readonly tenantTypeOptions = TENANT_TYPES;
  readonly tenantDisplayCode = tenantDisplayCode;
  readonly tenantPlanMeta = tenantPlanMeta;
  readonly tenantModuleList = tenantModuleList;
  readonly formatModuleBadge = formatModuleBadge;
  readonly tenantUsageLabel = tenantUsageLabel;
  readonly tenantUsagePercent = tenantUsagePercent;
  readonly tenantHealthStatus = tenantHealthStatus;
  readonly Math = Math;

  constructor(
    private platform: PlatformService,
    private router: Router,
    private snackBar: MatSnackBar
  ) {
    this.dataSource.filterPredicate = (tenant, raw) => this.matchesFilters(tenant, raw);
  }

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading = true;
    forkJoin({
      tenants: this.platform.getTenants(),
      stats: this.platform.getTenantManagementStats(),
      modules: this.platform.getModules()
    }).subscribe({
      next: ({ tenants, stats, modules }) => {
        this.tenants = tenants;
        this.stats = stats;
        this.modules = modules;
        this.dataSource.data = tenants;
        this.applyFilters();
        setTimeout(() => { if (this.paginator) this.dataSource.paginator = this.paginator; });
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.snackBar.open('Failed to load tenants.', 'Close', { duration: 3000 });
      }
    });
  }

  applyFilters(): void {
    this.dataSource.filter = JSON.stringify(this.filters);
    this.selectedIds.clear();
  }

  private matchesFilters(tenant: Tenant, raw: string): boolean {
    let f: TenantFilters;
    try { f = JSON.parse(raw) as TenantFilters; }
    catch { return true; }

    const q = f.search.trim().toLowerCase();
    if (q) {
      const haystack = [
        tenant.name,
        tenantDisplayCode(tenant),
        tenant.slug,
        tenant.country ?? '',
        tenant.tenantType ?? '',
        tenant.location ?? ''
      ].join(' ').toLowerCase();
      if (!haystack.includes(q)) return false;
    }

    if (f.status === 'active' && !tenant.isActive) return false;
    if (f.status === 'inactive' && tenant.isActive) return false;

    if (f.country !== 'all' && (tenant.country ?? '') !== f.country) return false;

    if (f.tenantType !== 'all' && (tenant.tenantType ?? '') !== f.tenantType) return false;

    if (f.plan !== 'all') {
      const plan = (tenant.subscriptionPlan ?? '').toLowerCase();
      if (!plan.includes(f.plan.toLowerCase())) return false;
    }

    if (f.module !== 'all') {
      const codes = tenantModuleList(tenant).map(c => c.toUpperCase());
      if (!codes.includes(f.module.toUpperCase())) return false;
    }

    return true;
  }

  countryOptions(): string[] {
    return [...new Set(this.tenants.map(t => t.country).filter((c): c is string => !!c?.trim()))].sort();
  }

  tenantGrowthPercent(): string {
    if (!this.stats.activeTenants || !this.stats.tenantsAddedThisMonth) return '+0% this month';
    const pct = Math.round((this.stats.tenantsAddedThisMonth / this.stats.activeTenants) * 100);
    return `+${pct}% this month`;
  }

  revenueTrendLabel(): string {
    return '+5% vs target';
  }

  initials(name: string): string {
    return name.split(/\s+/).slice(0, 2).map(w => w[0]?.toUpperCase() ?? '').join('') || '?';
  }

  avatarColor(name: string): string {
    const palette = ['#166534', '#1d4ed8', '#7c3aed', '#b45309', '#be123c', '#0f766e'];
    let hash = 0;
    for (let i = 0; i < name.length; i++) hash = name.charCodeAt(i) + ((hash << 5) - hash);
    return palette[Math.abs(hash) % palette.length];
  }

  healthClass(health: TenantHealthStatus): string {
    switch (health) {
      case 'Healthy': return 'health-healthy';
      case 'Trial Ending': return 'health-trial';
      case 'Expired': return 'health-expired';
      default: return 'health-suspended';
    }
  }

  openAddTenant(): void {
    void this.router.navigate(['/platform/tenants/new']);
  }

  openTenant(tenant: Tenant): void {
    void this.router.navigate(['/platform/tenants', tenant.id]);
  }

  setActionRow(tenant: Tenant): void {
    this.selectedRow = tenant;
  }

  toggleActive(tenant: Tenant): void {
    this.platform.updateTenant(tenant.id, {
      name: tenant.name,
      subscriptionPlan: tenant.subscriptionPlan ?? undefined,
      isActive: !tenant.isActive
    }).subscribe({
      next: () => {
        tenant.isActive = !tenant.isActive;
        this.dataSource.data = [...this.tenants];
        this.applyFilters();
        this.snackBar.open(tenant.isActive ? 'Tenant activated.' : 'Tenant suspended.', 'Close', { duration: 2000 });
      },
      error: (err) => this.snackBar.open(apiErrorMessage(err, 'Update failed.'), 'Close', { duration: 4000 })
    });
  }

  manageUsers(): void {
    void this.router.navigate(['/users']);
  }

  openBilling(tenant: Tenant): void {
    void this.router.navigate(['/platform/tenants', tenant.id], { queryParams: { tab: 'subscription' } });
  }

  openAuditLogs(): void {
    void this.router.navigate(['/audit-logs']);
  }

  isSelected(tenant: Tenant): boolean {
    return this.selectedIds.has(tenant.id);
  }

  isAllSelected(): boolean {
    const rows = this.dataSource.filteredData;
    return rows.length > 0 && rows.every(t => this.selectedIds.has(t.id));
  }

  isSomeSelected(): boolean {
    return this.selectedIds.size > 0 && !this.isAllSelected();
  }

  toggleRow(tenant: Tenant, checked: boolean): void {
    if (checked) this.selectedIds.add(tenant.id);
    else this.selectedIds.delete(tenant.id);
  }

  toggleAll(checked: boolean): void {
    this.selectedIds.clear();
    if (checked) this.dataSource.filteredData.forEach(t => this.selectedIds.add(t.id));
  }

  bulkActivate(): void {
    this.bulkSetActive(true);
  }

  bulkSuspend(): void {
    this.bulkSetActive(false);
  }

  private bulkSetActive(active: boolean): void {
    const targets = this.tenants.filter(t => this.selectedIds.has(t.id) && t.isActive !== active);
    if (!targets.length) {
      this.snackBar.open('No tenants selected for this action.', 'Close', { duration: 2500 });
      return;
    }

    let done = 0;
    let failed = 0;
    targets.forEach(tenant => {
      this.platform.updateTenant(tenant.id, {
        name: tenant.name,
        subscriptionPlan: tenant.subscriptionPlan ?? undefined,
        isActive: active
      }).subscribe({
        next: () => {
          tenant.isActive = active;
          done++;
          if (done + failed === targets.length) this.finishBulk(done, failed, active);
        },
        error: () => {
          failed++;
          if (done + failed === targets.length) this.finishBulk(done, failed, active);
        }
      });
    });
  }

  private finishBulk(done: number, failed: number, active: boolean): void {
    this.dataSource.data = [...this.tenants];
    this.applyFilters();
    const action = active ? 'activated' : 'suspended';
    if (failed) {
      this.snackBar.open(`${done} tenant(s) ${action}, ${failed} failed.`, 'Close', { duration: 3500 });
    } else {
      this.snackBar.open(`${done} tenant(s) ${action}.`, 'Close', { duration: 2500 });
    }
  }

  exportCsv(): void {
    const rows = this.dataSource.filteredData;
    if (!rows.length) {
      this.snackBar.open('No tenants to export.', 'Close', { duration: 2500 });
      return;
    }
    const header = ['Code', 'Name', 'Type', 'Country', 'Modules', 'Plan', 'Usage', 'Health'];
    const lines = rows.map(t => [
      tenantDisplayCode(t),
      t.name,
      t.tenantType ?? '',
      t.country ?? '',
      tenantModuleList(t).join('; '),
      t.subscriptionPlan ?? '',
      tenantUsageLabel(t),
      tenantHealthStatus(t)
    ].map(v => `"${String(v).replace(/"/g, '""')}"`).join(','));
    const csv = [header.join(','), ...lines].join('\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'tenants.csv';
    a.click();
    URL.revokeObjectURL(url);
  }

  openOrganizationDesigner(): void {
    this.snackBar.open('Organization Designer will be available in a future release.', 'Close', { duration: 3500 });
  }

  openAccessControl(): void {
    void this.router.navigate(['/platform/roles']);
  }

  openModuleManagement(): void {
    this.snackBar.open('Module provisioning will be available in a future release.', 'Close', { duration: 3500 });
  }

  openSubscriptionManagement(): void {
    this.snackBar.open('Subscription review will be available in a future release.', 'Close', { duration: 3500 });
  }

  planFor(tenant: Tenant): TenantPlanMeta {
    return tenantPlanMeta(tenant.subscriptionPlan);
  }

  formatRevenue(value: number): string {
    if (value >= 1000) return `AED ${Math.round(value / 1000)}k`;
    return `AED ${value}`;
  }
}
