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
    <div class="min-h-screen bg-surface-muted pb-20">
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
      <div class="p-6">
        <!-- Tenant Selector Row -->
        <div class="mb-6 flex items-center justify-between">
          <div class="w-72">
            <app-tenant-selector
              [tenants]="orgService.tenants()"
              [selectedTenantId]="selectedTenantId()"
              [loading]="tenantsLoading()"
              (tenantSelected)="onTenantSelected($event)"
            />
          </div>
          @if (orgService.loading()) {
            <div class="flex items-center gap-2 text-sm text-text-muted">
              <svg class="w-4 h-4 animate-spin" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
              </svg>
              Loading organization data...
            </div>
          }
        </div>

        @if (orgService.error(); as error) {
          <div class="mb-6 p-4 bg-red-50 border border-red-200 rounded-lg text-red-700 text-sm">
            {{ error }}
          </div>
        }

        @if (viewMode() === 'diagram') {
          <!-- Diagram View (Full Width) -->
          <app-diagram-view
            [nodes]="orgService.organizationTree()"
            [selectedNodeId]="selectedNodeId()"
            (nodeSelected)="onNodeSelected($event)"
          />
        } @else {
          <div class="grid grid-cols-12 gap-6">
            <!-- Left Column: Organization Tree -->
            <div class="col-span-12 lg:col-span-3">
              <div class="sticky top-24">
                <app-organization-tree
                  [nodes]="orgService.organizationTree()"
                  [unassignedDepartments]="orgService.unassignedDepartments()"
                  [selectedNodeId]="selectedNodeId()"
                  (nodeSelected)="onNodeSelected($event)"
                  (departmentSelected)="onDepartmentSelected($event)"
                />
              </div>
            </div>

          <!-- Center Column: Branch Details -->
          <div class="col-span-12 lg:col-span-6">
            @if (orgService.selectedBranch(); as branch) {
              <!-- Branch Header with Tabs -->
              <app-branch-header
                [branch]="branch"
                [tabs]="orgService.tabs()"
                [activeTab]="orgService.activeTab()"
                [health]="orgService.selectedBranchHealth()"
                [manager]="orgService.selectedBranchManager()"
                (edit)="onEditBranch()"
                (duplicate)="onDuplicateBranch()"
                (delete)="onDeleteBranch()"
                (tabChange)="orgService.setActiveTab($event)"
              />

              <!-- Tab Content -->
              <div class="bg-white rounded-b-lg border border-t-0 border-border p-6">
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
                  @case ('audit-logs') {
                    <div class="text-center py-12 text-text-muted">
                      <svg class="w-12 h-12 mx-auto mb-4 text-text-soft" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                      </svg>
                      <p>Audit Logs tab content - coming soon</p>
                    </div>
                  }
                }
              </div>
            } @else {
              <!-- Overview Dashboard - shown when no branch is selected -->
              <app-overview-dashboard
                [tenantName]="orgService.tenant()?.name ?? null"
                [stats]="orgService.tenantOverviewStats()"
                [recentActivity]="orgService.recentActivity()"
                (addBranch)="onAddBranch()"
                (addDepartment)="onAddDepartment()"
                (importData)="onImportData()"
              />
            }
          </div>

          <!-- Right Column: Structural Lineage & Logs -->
          <div class="col-span-12 lg:col-span-3 space-y-6">
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
  protected readonly orgService = inject(OrganizationHierarchyService);

  readonly viewMode = signal<ViewMode>('tree');
  readonly tenantsLoading = signal(true);

  readonly selectedNodeId = computed(() => {
    const branchId = this.orgService.selectedBranchId();
    return branchId ? `branch-${branchId}` : null;
  });

  readonly selectedTenantId = computed(() => {
    const tenant = this.orgService.tenant();
    return tenant?.id ?? null;
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
    this.orgService.loadTenants();
    this.tenantsLoading.set(false);

    const tenantIdParam = this.route.snapshot.queryParamMap.get('tenantId');
    if (tenantIdParam) {
      const id = parseInt(tenantIdParam, 10);
      if (Number.isFinite(id)) {
        this.orgService.selectTenantById(id);
      }
    }
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
    if (node.type === 'branch') {
      const branchId = parseInt(node.id.replace('branch-', ''), 10);
      this.orgService.selectBranch(branchId);
    } else if (node.type === 'department') {
      const deptId = parseInt(node.id.replace('dept-', ''), 10);
      this.orgService.selectDepartment(deptId);
    }
  }

  onDepartmentSelected(department: Department): void {
    this.orgService.selectDepartment(department.id);
  }

  onEditBranch(): void {
    const branch = this.orgService.selectedBranch();
    if (branch) {
      this.router.navigate(['/platform/branches', branch.id, 'edit']);
    }
  }

  onDuplicateBranch(): void {
    console.log('Duplicate branch');
  }

  onDeleteBranch(): void {
    console.log('Delete branch');
  }

  onAddUser(): void {
    console.log('Add user');
  }

  onViewAllUsers(): void {
    this.orgService.setActiveTab('users');
  }

  onUserAction(event: { action: string; user: unknown }): void {
    console.log('User action:', event);
  }

  onRefreshLogs(): void {
    const tenant = this.orgService.tenant();
    if (tenant) {
      this.orgService.loadData(tenant.id);
    }
  }

  onViewFullAuditTrail(): void {
    this.orgService.setActiveTab('audit-logs');
  }

  onCommitChanges(): void {
    this.orgService.saveConfiguration();
  }

  onSaveConfiguration(): void {
    this.orgService.saveConfiguration();
  }

  onDiscardChanges(): void {
    this.orgService.discardChanges();
  }

  onAddBranch(): void {
    const tenant = this.orgService.tenant();
    if (tenant) {
      this.router.navigate(['/platform/branches/new'], { queryParams: { tenantId: tenant.id } });
    }
  }

  onAddDepartment(): void {
    console.log('Add department');
  }

  onEditDepartment(dept: Department): void {
    console.log('Edit department:', dept);
  }

  onEditUser(user: unknown): void {
    console.log('Edit user:', user);
  }

  onAddAsset(): void {
    console.log('Add asset');
  }

  onAssetCategorySelected(categoryId: string): void {
    console.log('Asset category selected:', categoryId);
  }

  onImportData(): void {
    console.log('Import data');
  }
}
