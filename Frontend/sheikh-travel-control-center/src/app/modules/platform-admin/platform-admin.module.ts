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

const routes: Routes = [
  { path: 'tenants', component: TenantListComponent },
  { path: 'tenants/new', component: TenantProvisionComponent },
  { path: 'tenants/:id', component: TenantDetailComponent },
  { path: 'branches', component: BranchListComponent },
  { path: 'branches/new', component: BranchFormComponent },
  { path: 'branches/:id/edit', component: BranchFormComponent },
  { path: 'departments', component: DepartmentListComponent },
  { path: 'roles', component: RoleListComponent },
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
    RoleListComponent
  ],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class PlatformAdminModule {}
