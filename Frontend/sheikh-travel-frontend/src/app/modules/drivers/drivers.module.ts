import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { DriverListComponent } from './driver-list/driver-list.component';
import { DriverFormComponent } from './driver-form/driver-form.component';

const routes: Routes = [
  { path: '', component: DriverListComponent },
  { path: 'new', component: DriverFormComponent },
  { path: ':id/edit', component: DriverFormComponent }
];

@NgModule({
  declarations: [DriverListComponent, DriverFormComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class DriversModule {}
