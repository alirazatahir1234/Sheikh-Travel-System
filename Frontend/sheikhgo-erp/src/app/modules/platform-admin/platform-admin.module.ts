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
