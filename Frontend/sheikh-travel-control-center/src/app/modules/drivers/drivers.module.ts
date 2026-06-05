import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { DriverListComponent } from './driver-list/driver-list.component';
import { DriverFormComponent } from './driver-form/driver-form.component';
import { DriverProfileComponent } from './driver-profile/driver-profile.component';

const routes: Routes = [
  { path: '', component: DriverListComponent },
  { path: 'new', component: DriverFormComponent },
  { path: ':id', component: DriverProfileComponent },
  { path: ':id/edit', component: DriverFormComponent }
];

@NgModule({
  declarations: [DriverListComponent, DriverFormComponent, DriverProfileComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class DriversModule {}
