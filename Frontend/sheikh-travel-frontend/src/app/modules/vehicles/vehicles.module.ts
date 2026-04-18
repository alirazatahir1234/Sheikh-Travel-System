import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { VehicleListComponent } from './vehicle-list/vehicle-list.component';
import { VehicleFormComponent } from './vehicle-form/vehicle-form.component';

const routes: Routes = [
  { path: '', component: VehicleListComponent },
  { path: 'new', component: VehicleFormComponent },
  { path: ':id/edit', component: VehicleFormComponent }
];

@NgModule({
  declarations: [VehicleListComponent, VehicleFormComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class VehiclesModule {}
