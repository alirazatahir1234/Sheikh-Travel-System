import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { FuelLogListComponent } from './fuel-log-list/fuel-log-list.component';
import { FuelLogFormComponent } from './fuel-log-form/fuel-log-form.component';

const routes: Routes = [
  { path: '', component: FuelLogListComponent },
  { path: 'new', component: FuelLogFormComponent }
];

@NgModule({
  declarations: [FuelLogListComponent, FuelLogFormComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class FuelLogsModule {}
