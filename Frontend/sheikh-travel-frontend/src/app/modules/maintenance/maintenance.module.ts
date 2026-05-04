import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { MaintenanceListComponent } from './maintenance-list/maintenance-list.component';
import { MaintenanceFormComponent } from './maintenance-form/maintenance-form.component';

const routes: Routes = [
  { path: '', component: MaintenanceListComponent },
  { path: 'new', component: MaintenanceFormComponent },
  { path: ':id/edit', component: MaintenanceFormComponent }
];

@NgModule({
  declarations: [MaintenanceListComponent, MaintenanceFormComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class MaintenanceModule {}
