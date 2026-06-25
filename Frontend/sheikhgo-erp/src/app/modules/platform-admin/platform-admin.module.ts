import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { TenantListComponent } from './tenant-list/tenant-list.component';
import { TenantProvisionComponent } from './tenant-provision/tenant-provision.component';
import { TenantDetailComponent } from './tenant-detail/tenant-detail.component';
import { BranchListComponent } from './branch-list/branch-list.component';
import { BranchFormComponent } from './branch-form/branch-form.component';
import { DepartmentListComponent } from './department-list/department-list.component';
import { RoleListComponent } from './role-list/role-list.component';
import { AccessControlComponent } from './access-control/access-control.component';
import { ModuleManagementComponent } from './module-management/module-management.component';
import { SubscriptionManagementComponent } from './subscription-management/subscription-management.component';
import { UiButtonComponent } from '../../shared/components/ui/button/ui-button.component';
import { UiPageHeaderComponent } from '../../shared/components/ui/page-header/ui-page-header.component';

const routes: Routes = [
  { path: 'tenants', component: TenantListComponent },
  { path: 'tenants/new', component: TenantProvisionComponent },
  { path: 'tenants/:id', component: TenantDetailComponent },
  { path: 'branches', component: BranchListComponent },
  { path: 'branches/new', component: BranchFormComponent },
  { path: 'branches/:id/edit', component: BranchFormComponent },
  { path: 'departments', component: DepartmentListComponent },
  { path: 'roles', component: RoleListComponent },
  {
    path: 'organization-designer',
    loadComponent: () =>
      import('../../features/organization-hierarchy/pages/hierarchy-configuration/hierarchy-configuration.component').then(
        m => m.HierarchyConfigurationComponent
      ),
    title: 'Organization Hierarchy'
  },
  { path: 'hierarchy-config', redirectTo: 'organization-designer', pathMatch: 'full' },
  { path: 'access-control', component: AccessControlComponent },
  { path: 'module-management', component: ModuleManagementComponent },
  { path: 'subscription-management', component: SubscriptionManagementComponent },
  { path: '', redirectTo: 'branches', pathMatch: 'full' }
];

@NgModule({
  declarations: [
    TenantListComponent,
    TenantProvisionComponent,
    TenantDetailComponent,
    BranchListComponent,
    BranchFormComponent,
    DepartmentListComponent,
    RoleListComponent,
    AccessControlComponent,
    ModuleManagementComponent,
    SubscriptionManagementComponent
  ],
  imports: [SharedModule, RouterModule.forChild(routes), UiButtonComponent, UiPageHeaderComponent]
})
export class PlatformAdminModule {}
