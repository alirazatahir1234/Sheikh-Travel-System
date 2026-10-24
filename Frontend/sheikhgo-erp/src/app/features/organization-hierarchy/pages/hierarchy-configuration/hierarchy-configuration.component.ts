import {
  Component,
  ChangeDetectionStrategy,
  inject,
  signal,
  computed,
  OnInit,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute } from '@angular/router';

import { OrganizationHierarchyService } from '../../services/organization-hierarchy.service';
import { ViewMode, OrganizationNode, Department } from '../../models/organization.models';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { AuthService } from '../../../../core/services/auth.service';
import { PlatformTenantContextService } from '../../../../core/services/platform-tenant-context.service';

import { TopNavbarComponent, BreadcrumbItem } from '../../components/top-navbar/top-navbar.component';
import { OrganizationTreeComponent } from '../../components/organization-tree/organization-tree.component';
import { BranchHeaderComponent } from '../../components/branch-header/branch-header.component';
import { StatisticsCardsComponent } from '../../components/statistics-cards/statistics-cards.component';
import { BranchCapacityCardComponent } from '../../components/branch-capacity-card/branch-capacity-card.component';
import { StructuralLineageComponent } from '../../components/structural-lineage/structural-lineage.component';
import { OperationalLogsComponent } from '../../components/operational-logs/operational-logs.component';
import { UserPreviewComponent } from '../../components/user-preview/user-preview.component';
import { FooterActionBarComponent } from '../../components/footer-action-bar/footer-action-bar.component';
import { TenantSelectorComponent } from '../../components/tenant-selector/tenant-selector.component';
import { OverviewDashboardComponent } from '../../components/overview-dashboard/overview-dashboard.component';
import { DepartmentsTabComponent } from '../../components/tab-content/departments-tab.component';
import { UsersTabComponent } from '../../components/tab-content/users-tab.component';
import { AssetsTabComponent, AssetCategory } from '../../components/tab-content/assets-tab.component';
import { DiagramViewComponent } from '../../components/diagram-view/diagram-view.component';

@Component({
  selector: 'app-hierarchy-configuration',
  standalone: true,
  imports: [
    CommonModule,
    TopNavbarComponent,
    OrganizationTreeComponent,
    BranchHeaderComponent,
    StatisticsCardsComponent,
    BranchCapacityCardComponent,
    StructuralLineageComponent,
    OperationalLogsComponent,
    UserPreviewComponent,
    FooterActionBarComponent,
    TenantSelectorComponent,
    OverviewDashboardComponent,
    DepartmentsTabComponent,
    UsersTabComponent,
    AssetsTabComponent,
    DiagramViewComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen bg-surface-muted pb-24">
      <!-- Top Navbar -->
      <app-top-navbar
        [viewMode]="viewMode()"
        [breadcrumb]="breadcrumb()"
        (back)="goBack()"
        (viewModeChange)="viewMode.set($event)"
        (commitChanges)="onCommitChanges()"
        (breadcrumbClick)="onBreadcrumbClick($event)"
      />

      <!-- Main Content -->
      <div class="px-4 py-4 md:px-6 md:py-6">
        <!-- Tenant Selector Row -->
        <div class="mb-5 flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
          <div class="w-full sm:w-72 max-w-full">
            <app-tenant-selector
              [tenants]="orgService.tenants()"
              [selectedTenantId]="selectedTenantId()"
              [loading]="tenantsLoading()"
              (tenantSelected)="onTenantSelected($event)"
            />
          </div>
          @if (orgService.loading()) {
            <div class="flex items-center gap-2 text-sm text-text-muted" role="status" aria-live="polite">
              <svg class="w-4 h-4 animate-spin text-primary-600" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
              </svg>
              Loading organization data…
            </div>
          }
        </div>

        @if (orgService.error(); as error) {
          <div class="mb-5 flex items-start gap-3 p-4 bg-red-50 border border-red-200 rounded-lg text-red-700 text-sm" role="alert">
            <svg class="w-5 h-5 flex-shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
            <span>{{ error }}</span>
          </div>
        }

        @if (!tenantsLoading() && !orgService.loading() && !orgService.tenant() && !selectedTenantId()) {
          <div class="mb-5 px-6 py-10 bg-white border border-dashed border-border rounded-lg text-center">
            <div class="mx-auto mb-3 w-12 h-12 rounded-lg bg-primary-50 flex items-center justify-center">
              <svg class="w-6 h-6 text-primary-600" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
              </svg>
            </div>
            <p class="text-sm font-semibold text-text">Select a company to design its hierarchy</p>
            <p class="text-sm text-text-muted mt-1 max-w-md mx-auto">
              Use the company picker above to load branches and departments.
            </p>
          </div>
        }

        @if (orgService.loading() && !orgService.tenant()) {
          <div class="grid grid-cols-12 gap-4 md:gap-6" aria-hidden="true">
            <div class="col-span-12 lg:col-span-3">
              <div class="bg-white rounded-lg border border-border p-4 space-y-3">
                <div class="h-4 w-32 bg-surface-alt rounded animate-pulse"></div>
                <div class="h-9 w-full bg-surface-alt rounded-lg animate-pulse"></div>
                <div class="h-9 w-full bg-surface-alt rounded-lg animate-pulse"></div>
                <div class="space-y-2 pt-2">
                  <div class="h-10 w-full bg-surface-alt rounded-lg animate-pulse"></div>
                  <div class="h-10 w-full bg-surface-alt rounded-lg animate-pulse"></div>
                  <div class="h-10 w-5/6 bg-surface-alt rounded-lg animate-pulse"></div>
                </div>
              </div>
            </div>
            <div class="col-span-12 lg:col-span-6">
              <div class="bg-white rounded-lg border border-border p-6 space-y-4">
                <div class="h-6 w-48 bg-surface-alt rounded animate-pulse"></div>
                <div class="grid grid-cols-2 md:grid-cols-3 gap-3">
                  <div class="h-20 bg-surface-alt rounded-lg animate-pulse"></div>
                  <div class="h-20 bg-surface-alt rounded-lg animate-pulse"></div>
                  <div class="h-20 bg-surface-alt rounded-lg animate-pulse"></div>
                </div>
                <div class="h-24 w-full bg-surface-alt rounded-lg animate-pulse"></div>
              </div>
            </div>
            <div class="col-span-12 lg:col-span-3 space-y-4">
              <div class="bg-white rounded-lg border border-border p-4 space-y-3">
                <div class="h-3 w-28 bg-surface-alt rounded animate-pulse"></div>
                <div class="h-10 w-full bg-surface-alt rounded-lg animate-pulse"></div>
                <div class="h-10 w-full bg-surface-alt rounded-lg animate-pulse"></div>
              </div>
              <div class="bg-white rounded-lg border border-border p-4 space-y-3">
                <div class="h-3 w-32 bg-surface-alt rounded animate-pulse"></div>
                <div class="h-16 w-full bg-surface-alt rounded-lg animate-pulse"></div>
              </div>
            </div>
          </div>
        } @else if (viewMode() === 'diagram') {
          <!-- Diagram View (Full Width) -->
          <app-diagram-view
            [nodes]="orgService.organizationTree()"
            [selectedNodeId]="selectedNodeId()"
            (nodeSelected)="onNodeSelected($event)"
          />
        } @else if (orgService.tenant() || selectedTenantId()) {
          <div class="grid grid-cols-12 gap-4 md:gap-6">
            <!-- Left Column: Organization Tree -->
            <div class="col-span-12 lg:col-span-3">
              <div class="lg:sticky lg:top-24">
                <app-organization-tree
                  [nodes]="orgService.organizationTree()"
                  [unassignedDepartments]="orgService.unassignedDepartments()"
                  [selectedNodeId]="selectedNodeId()"
                  [canManageBranches]="canManageBranches()"
                  [canManageDepartments]="canManageDepartments()"
                  (nodeSelected)="onNodeSelected($event)"
                  (departmentSelected)="onDepartmentSelected($event)"
                  (addBranch)="onAddBranch()"
                  (addDepartment)="onAddDepartment()"
                  (exportHierarchy)="onExportHierarchy()"
                  (bulkMove)="onBulkComingSoon('Move')"
                  (bulkArchive)="onBulkComingSoon('Archive')"
                  (bulkExport)="onExportHierarchy()"
                  (bulkAssignManager)="onBulkComingSoon('Assign Manager')"
                  (contextMenuAction)="onContextMenuAction($event)"
                />
              </div>
            </div>

            <!-- Center Column: Branch Details -->
            <div class="col-span-12 lg:col-span-6 min-w-0">
              @if (orgService.selectedBranch(); as branch) {
                <!-- Branch Header with Tabs -->
                <app-branch-header
                  [branch]="branch"
                  [tabs]="orgService.tabs()"
                  [activeTab]="orgService.activeTab()"
                  [health]="orgService.selectedBranchHealth()"
                  [manager]="orgService.selectedBranchManager()"
                  [canManageBranches]="canManageBranches()"
                  (edit)="onEditBranch()"
                  (duplicate)="onDuplicateBranch()"
                  (delete)="onDeleteBranch()"
                  (tabChange)="orgService.setActiveTab($event)"
                />

                <!-- Tab Content -->
                <div class="bg-white rounded-b-lg border border-t-0 border-border p-4 md:p-6">
                  @switch (orgService.activeTab()) {
                    @case ('overview') {
                      <!-- Statistics Cards -->
                      <app-statistics-cards [branch]="branch" />

                      <!-- Branch Capacity -->
                      @if (orgService.branchCapacity(); as capacity) {
                        <div class="mt-6">
                          <app-branch-capacity-card [capacity]="capacity" />
                        </div>
                      }

                      <!-- User Preview -->
                      @if (orgService.userPreview(); as preview) {
                        <div class="mt-6">
                          <app-user-preview
                            [preview]="preview"
                            (addUser)="onAddUser()"
                            (viewAllUsers)="onViewAllUsers()"
                            (userActionTriggered)="onUserAction($event)"
                          />
                        </div>
                      }
                    }
                    @case ('departments') {
                      <app-departments-tab
                        [departments]="branchDepartments()"
                        [canManageDepartments]="canManageDepartments()"
                        (addDepartment)="onAddDepartment()"
                        (editDepartment)="onEditDepartment($event)"
                        (departmentSelected)="onDepartmentSelected($event)"
                      />
                    }
                    @case ('users') {
                      <app-users-tab
                        [users]="orgService.userPreview()?.users ?? []"
                        [totalCount]="branch.userCount"
                        [isPlaceholder]="true"
                        (addUser)="onAddUser()"
                        (editUser)="onEditUser($event)"
                        (viewAllUsers)="onViewAllUsers()"
                      />
                    }
                    @case ('assets') {
                      <app-assets-tab
                        [categories]="assetCategories()"
                        (addAsset)="onAddAsset()"
                        (categorySelected)="onAssetCategorySelected($event)"
                      />
                    }
                  }
                </div>
              } @else {
                <!-- Overview Dashboard - shown when no branch is selected -->
                <app-overview-dashboard
                  [tenantName]="orgService.tenant()?.name ?? null"
                  [stats]="orgService.tenantOverviewStats()"
                  [recentActivity]="orgService.recentActivity()"
                  [canManageBranches]="canManageBranches()"
                  [canManageDepartments]="canManageDepartments()"
                  (addBranch)="onAddBranch()"
                  (addDepartment)="onAddDepartment()"
                  (importData)="onImportData()"
                />
              }
            </div>

            <!-- Right Column: Structural Lineage & Logs -->
            <div class="col-span-12 lg:col-span-3 space-y-4 md:space-y-6">
              @if (orgService.structuralLineage(); as lineage) {
                <app-structural-lineage [lineage]="lineage" />
              }

              <app-operational-logs
                [logs]="orgService.recentActivity()"
                (refresh)="onRefreshLogs()"
                (viewFullAuditTrail)="onViewFullAuditTrail()"
              />
            </div>
          </div>
        }
      </div>

      <!-- Footer Action Bar -->
      <app-footer-action-bar
        [pendingChanges]="orgService.pendingChanges()"
        [lastAutoSaved]="orgService.lastAutoSaved()"
        (save)="onSaveConfiguration()"
        (discard)="onDiscardChanges()"
      />
    </div>
  `,
})
export class HierarchyConfigurationComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly toast = inject(UiToastService);
  private readonly auth = inject(AuthService);
  private readonly tenantContext = inject(PlatformTenantContextService);
  protected readonly orgService = inject(OrganizationHierarchyService);

  readonly viewMode = signal<ViewMode>('tree');
  readonly tenantsLoading = signal(true);

  /** Route allows Tenants.View OR manage; write actions need manage perms. */
  readonly canManageBranches = signal(false);
  readonly canManageDepartments = signal(false);

  readonly selectedNodeId = computed(() => {
    const deptId = this.orgService.selectedDepartmentId();
    if (deptId) return `dept-${deptId}`;
    const branchId = this.orgService.selectedBranchId();
    return branchId ? `branch-${branchId}` : null;
  });

  readonly selectedTenantId = computed(() => {
    return this.orgService.tenant()?.id ?? this.tenantContext.currentTenantId;
  });

  readonly branchDepartments = computed(() => {
    const branch = this.orgService.selectedBranch();
    if (!branch) return [];
    return this.orgService.departments().filter(d => d.branchId === branch.id);
  });

  readonly assetCategories = computed<AssetCategory[]>(() => {
    const branch = this.orgService.selectedBranch();
    if (!branch) return [];
    return [
      { id: 'vehicles', label: 'Vehicles', icon: 'vehicle', count: branch.vehicleCount, isPlaceholder: true },
      { id: 'drivers', label: 'Drivers', icon: 'driver', count: branch.driverCount, isPlaceholder: true },
      { id: 'gps', label: 'GPS Devices', icon: 'gps', count: 0, isPlaceholder: true },
    ];
  });

  readonly breadcrumb = computed<BreadcrumbItem[]>(() => {
    const items: BreadcrumbItem[] = [];
    const tenant = this.orgService.tenant();
    const branch = this.orgService.selectedBranch();

    items.push({ label: 'Organization Hierarchy', type: 'platform' });

    if (tenant) {
      items.push({ label: tenant.name, type: 'tenant', id: tenant.id });
    }

    if (branch) {
      items.push({ label: branch.name, type: 'branch', id: branch.id });
    }

    return items;
  });

  ngOnInit(): void {
    this.canManageBranches.set(this.auth.hasPermission('Platform.Branches.Manage'));
    this.canManageDepartments.set(this.auth.hasPermission('Platform.Departments.Manage'));

    this.orgService.loadTenants().subscribe({
      next: () => this.tenantsLoading.set(false),
      error: () => this.tenantsLoading.set(false),
    });

    const tenantIdParam = this.route.snapshot.queryParamMap.get('tenantId');
    if (tenantIdParam) {
      const id = parseInt(tenantIdParam, 10);
      if (Number.isFinite(id)) {
        this.orgService.selectTenantById(id);
      }
    }
  }

  private requireBranchManage(): boolean {
    if (this.canManageBranches()) return true;
    this.toast.warning('You need Platform.Branches.Manage to change branches.');
    return false;
  }

  private requireDepartmentManage(): boolean {
    if (this.canManageDepartments()) return true;
    this.toast.warning('You need Platform.Departments.Manage to change departments.');
    return false;
  }

  private validateDepartmentName(name: string): string | null {
    const trimmed = name.trim();
    if (!trimmed) {
      this.toast.warning('Department name is required.');
      return null;
    }
    if (trimmed.length > 100) {
      this.toast.warning('Department name must be 100 characters or fewer.');
      return null;
    }
    return trimmed;
  }

  goBack(): void {
    this.router.navigate(['/platform/tenants']);
  }

  onBreadcrumbClick(item: BreadcrumbItem): void {
    switch (item.type) {
      case 'platform':
        this.orgService.clearSelection();
        break;
      case 'tenant':
        this.orgService.clearSelection();
        break;
      case 'branch':
        break;
    }
  }

  onTenantSelected(tenantId: number): void {
    this.orgService.selectTenantById(tenantId);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tenantId },
      queryParamsHandling: 'merge',
    });
  }

  onNodeSelected(node: OrganizationNode): void {
    if (node.type === 'tenant') {
      this.orgService.clearSelection();
      return;
    }
    if (node.type === 'branch') {
      const branchId = parseInt(node.id.replace('branch-', ''), 10);
      if (Number.isFinite(branchId)) {
        this.orgService.selectBranch(branchId);
      }
    } else if (node.type === 'department') {
      const deptId = parseInt(node.id.replace('dept-', ''), 10);
      if (Number.isFinite(deptId)) {
        this.orgService.selectDepartment(deptId);
      }
    }
  }

  onDepartmentSelected(department: Department): void {
    this.orgService.selectDepartment(department.id);
  }

  onEditBranch(): void {
    if (!this.requireBranchManage()) return;
    const branch = this.orgService.selectedBranch();
    if (branch) {
      this.router.navigate(['/platform/branches', branch.id, 'edit']);
    }
  }

  onDuplicateBranch(): void {
    if (!this.requireBranchManage()) return;
    this.toast.info('Branch duplication is coming soon.');
  }

  onDeleteBranch(): void {
    if (!this.requireBranchManage()) return;
    const branch = this.orgService.selectedBranch();
    if (!branch) return;
    if (!confirm(`Delete branch "${branch.name}"? Departments under this branch will become unassigned.`)) {
      return;
    }

    this.orgService.deleteBranch(branch.id).subscribe({
      next: () => this.toast.success('Branch deleted.'),
      error: (err) => this.toast.error(apiErrorMessage(err, 'Failed to delete branch.')),
    });
  }

  onAddUser(): void {
    this.toast.info('User management from hierarchy is coming soon. Use Access Control for now.');
  }

  onViewAllUsers(): void {
    this.orgService.setActiveTab('users');
  }

  onUserAction(_event: { action: string; user: unknown }): void {
    this.toast.info('User actions from hierarchy are coming soon.');
  }

  onRefreshLogs(): void {
    const tenant = this.orgService.tenant();
    if (tenant) {
      this.orgService.loadData(tenant.id);
    }
  }

  onViewFullAuditTrail(): void {
    this.toast.info('Full audit trail is coming soon.');
  }

  onCommitChanges(): void {
    if (!this.orgService.saveConfiguration()) {
      this.toast.info('No pending hierarchy changes to commit.');
      return;
    }
    this.toast.success('Configuration saved.');
  }

  onSaveConfiguration(): void {
    if (!this.orgService.saveConfiguration()) {
      this.toast.info('No pending hierarchy changes to save.');
      return;
    }
    this.toast.success('Configuration saved.');
  }

  onDiscardChanges(): void {
    if (!this.orgService.discardChanges()) {
      this.toast.info('No pending hierarchy changes to discard.');
      return;
    }
    this.toast.success('Pending changes discarded.');
  }

  onAddBranch(): void {
    if (!this.requireBranchManage()) return;
    const tenant = this.orgService.tenant();
    if (!tenant) {
      this.toast.warning('Select a tenant first.');
      return;
    }
    this.router.navigate(['/platform/branches/new'], { queryParams: { tenantId: tenant.id } });
  }

  onAddDepartment(): void {
    if (!this.requireDepartmentManage()) return;
    const tenant = this.orgService.tenant();
    if (!tenant) {
      this.toast.warning('Select a tenant first.');
      return;
    }

    const name = window.prompt('Department name:');
    if (name === null) return;
    const trimmed = this.validateDepartmentName(name);
    if (!trimmed) return;

    const branchId = this.orgService.selectedBranch()?.id ?? null;
    this.orgService.createDepartment(trimmed, branchId).subscribe({
      next: () => {
        this.toast.success('Department created.');
        if (branchId) {
          this.orgService.setActiveTab('departments');
        }
      },
      error: (err) => this.toast.error(apiErrorMessage(err, 'Failed to create department.')),
    });
  }

  onEditDepartment(dept: Department): void {
    if (!this.requireDepartmentManage()) return;
    const renamed = window.prompt('Rename department:', dept.name);
    if (renamed === null) return;
    const trimmed = this.validateDepartmentName(renamed);
    if (!trimmed) return;
    if (trimmed === dept.name) return;

    this.orgService.updateDepartment(dept.id, trimmed, dept.branchId, dept.isActive).subscribe({
      next: () => this.toast.success('Department updated.'),
      error: (err) => this.toast.error(apiErrorMessage(err, 'Failed to update department.')),
    });
  }

  onEditUser(_user: unknown): void {
    this.toast.info('User editing from hierarchy is coming soon.');
  }

  onAddAsset(): void {
    this.toast.info('Asset management from hierarchy is coming soon.');
  }

  onAssetCategorySelected(_categoryId: string): void {
    this.toast.info('Asset details from hierarchy are coming soon.');
  }

  onImportData(): void {
    this.toast.info('Hierarchy import is coming soon.');
  }

  onAddTeam(): void {
    this.toast.info('Teams are coming soon.');
  }

  onBulkComingSoon(action: string): void {
    this.toast.info(`${action} for multiple nodes is coming soon.`);
  }

  onExportHierarchy(): void {
    const payload = this.orgService.getHierarchyExportPayload();
    const tenant = this.orgService.tenant();
    if (!payload || !tenant) {
      this.toast.warning('Select a tenant with organization data to export.');
      return;
    }

    const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${tenant.name.replace(/\s+/g, '-').toLowerCase()}-hierarchy.json`;
    a.click();
    URL.revokeObjectURL(url);
    this.toast.success('Hierarchy exported.');
  }

  onContextMenuAction(event: { action: string; node: OrganizationNode }): void {
    const { action, node } = event;

    if (node.type === 'branch') {
      const branchId = parseInt(node.id.replace('branch-', ''), 10);
      if (!Number.isFinite(branchId)) return;

      switch (action) {
        case 'edit':
          this.orgService.selectBranch(branchId);
          this.onEditBranch();
          break;
        case 'add-department':
          this.orgService.selectBranch(branchId);
          this.onAddDepartment();
          break;
        case 'delete':
          this.orgService.selectBranch(branchId);
          this.onDeleteBranch();
          break;
        case 'assign-manager':
        case 'move':
        case 'archive':
          this.toast.info('This action is coming soon.');
          break;
        default:
          break;
      }
      return;
    }

    if (node.type === 'department') {
      const deptId = parseInt(node.id.replace('dept-', ''), 10);
      if (!Number.isFinite(deptId)) return;
      const dept = this.orgService.departments().find(d => d.id === deptId);
      if (!dept) return;

      switch (action) {
        case 'rename':
          this.onEditDepartment(dept);
          break;
        case 'delete':
          if (!this.requireDepartmentManage()) return;
          if (!confirm(`Delete department "${dept.name}"?`)) return;
          this.orgService.deleteDepartment(dept.id).subscribe({
            next: () => this.toast.success('Department deleted.'),
            error: (err) => this.toast.error(apiErrorMessage(err, 'Failed to delete department.')),
          });
          break;
        case 'assign-users':
        case 'move':
          this.toast.info('This action is coming soon.');
          break;
        default:
          break;
      }
    }
  }
}
