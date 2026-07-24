import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { permissionGuard } from '../../core/guards/permission.guard';
import { TenantListComponent } from './tenant-list/tenant-list.component';
import { TenantProvisionComponent } from './tenant-provision/tenant-provision.component';
import { TenantDetailComponent } from './tenant-detail/tenant-detail.component';
import { BranchListComponent } from './branch-list/branch-list.component';
import { BranchFormComponent } from './branch-form/branch-form.component';
import { DepartmentListComponent } from './department-list/department-list.component';
import { AccessControlComponent } from './access-control/access-control.component';
import { ModuleManagementComponent } from './module-management/module-management.component';
import { FeatureManagementComponent } from './feature-management/feature-management.component';
import { MenuManagementComponent } from './menu-management/menu-management.component';
import { WorkspaceManagementComponent } from './workspace-management/workspace-management.component';
import { DashboardManagementComponent } from './dashboard-management/dashboard-management.component';
import { SecurityCenterComponent } from './security-center/security-center.component';
import { AuditCenterComponent } from './audit-center/audit-center.component';
import { GpsControlCenterComponent } from './gps-control-center/gps-control-center.component';
import { PermissionCoverageComponent } from './permission-coverage/permission-coverage.component';
import { SubscriptionManagementComponent } from './subscription-management/subscription-management.component';
import { MigrationManagerComponent } from './migration-manager/migration-manager.component';
import { DatabaseResetComponent } from './database-reset/database-reset.component';
import { ResetDatabaseDialogComponent } from './database-reset/reset-database-dialog.component';
import { PlatformHubComponent } from './platform-hub/platform-hub.component';
import { UiButtonComponent } from '../../shared/components/ui/button/ui-button.component';
import { UiPageHeaderComponent } from '../../shared/components/ui/page-header/ui-page-header.component';
import { UiDataTableComponent } from '../../shared/components/ui/data-table/ui-data-table.component';
import { UiTableCellDirective } from '../../shared/components/ui/data-table/ui-table-cell.directive';
import { UiSelectComponent } from '../../shared/components/ui/select/ui-select.component';

const routes: Routes = [
  {
    path: '',
    component: PlatformHubComponent,
    pathMatch: 'full'
  },
  {
    path: 'tenants',
    component: TenantListComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Platform.Tenants.View'] }
  },
  {
    path: 'tenants/new',
    component: TenantProvisionComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Platform.Tenants.Manage'] }
  },
  {
    path: 'tenants/:id',
    component: TenantDetailComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Platform.Tenants.View'] }
  },
  { path: 'companies', redirectTo: 'tenants', pathMatch: 'full' },
  { path: 'companies/new', redirectTo: 'tenants/new', pathMatch: 'full' },
  { path: 'companies/:id', redirectTo: 'tenants/:id' },
  {
    path: 'branches',
    component: BranchListComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Platform.Branches.Manage'] }
  },
  {
    path: 'branches/new',
    component: BranchFormComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Platform.Branches.Manage'] }
  },
  {
    path: 'branches/:id/edit',
    component: BranchFormComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Platform.Branches.Manage'] }
  },
  {
    path: 'departments',
    component: DepartmentListComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Platform.Departments.Manage'] }
  },
  {
    path: 'roles',
    component: AccessControlComponent,
    canActivate: [permissionGuard],
    data: {
      permissions: ['Platform.Roles.View', 'Platform.Users.View'],
      defaultTab: 'roles'
    }
  },
  {
    path: 'organization-designer',
    canActivate: [permissionGuard],
    data: {
      permissions: [
        'Platform.Branches.Manage',
        'Platform.Departments.Manage',
        'Platform.Tenants.View'
      ]
    },
    loadComponent: () =>
      import('../../features/organization-hierarchy/pages/hierarchy-configuration/hierarchy-configuration.component').then(
        m => m.HierarchyConfigurationComponent
      ),
    title: 'Organization Hierarchy'
  },
  { path: 'hierarchy-config', redirectTo: 'organization-designer', pathMatch: 'full' },
  {
    path: 'access-control',
    component: AccessControlComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Platform.Roles.View', 'Platform.Users.View'] }
  },
  {
    path: 'module-management',
    component: ModuleManagementComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Platform.Tenants.View', 'Platform.Tenants.Manage'] }
  },
  {
    path: 'feature-management',
    component: FeatureManagementComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Platform.Tenants.View', 'Platform.Tenants.Manage'] }
  },
  {
    path: 'menu-management',
    component: MenuManagementComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Platform.Menus.Manage'] }
  },
  {
    path: 'workspace-management',
    component: WorkspaceManagementComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Platform.Workspaces.Manage'] }
  },
  {
    path: 'dashboard-management',
    component: DashboardManagementComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Platform.Dashboards.View', 'Platform.Dashboards.Manage'] }
  },
  {
    path: 'security-center',
    component: SecurityCenterComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Platform.Security.View', 'Platform.Security.Manage'] }
  },
  {
    path: 'permission-coverage',
    component: PermissionCoverageComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Platform.Security.Manage'] }
  },
  {
    path: 'audit-center',
    component: AuditCenterComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Platform.Audit.View', 'Platform.AuditLogs.View', 'Platform.Audit.Manage'] }
  },
  {
    path: 'gps-control-center',
    component: GpsControlCenterComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Platform.Gps.Control.View', 'Platform.Gps.Manufacturers.Manage', 'Platform.Gps.Models.Manage'] }
  },
  {
    path: 'subscription-management',
    component: SubscriptionManagementComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Platform.Tenants.View', 'Platform.Tenants.Manage'] }
  },
  {
    path: 'migrations',
    component: MigrationManagerComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Platform.Migrations.View', 'Platform.Migrations.Manage'] }
  },
  {
    path: 'maintenance',
    component: DatabaseResetComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Platform.System.Reset'] }
  }
];

@NgModule({
  declarations: [
    PlatformHubComponent,
    TenantListComponent,
    TenantProvisionComponent,
    TenantDetailComponent,
    BranchListComponent,
    BranchFormComponent,
    DepartmentListComponent,
    AccessControlComponent,
    ModuleManagementComponent,
    FeatureManagementComponent,
    MenuManagementComponent,
    WorkspaceManagementComponent,
    DashboardManagementComponent,
    SecurityCenterComponent,
    AuditCenterComponent,
    GpsControlCenterComponent,
    PermissionCoverageComponent,
    SubscriptionManagementComponent,
    MigrationManagerComponent,
    DatabaseResetComponent,
    ResetDatabaseDialogComponent
  ],
  imports: [
    SharedModule,
    RouterModule.forChild(routes),
    UiButtonComponent,
    UiPageHeaderComponent,
    UiDataTableComponent,
    UiTableCellDirective,
    UiSelectComponent
  ]
})
export class PlatformAdminModule {}
