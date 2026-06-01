import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { FuelLogListComponent } from './fuel-log-list/fuel-log-list.component';
import { FuelLogFormComponent } from './fuel-log-form/fuel-log-form.component';
import { FuelLogAnalyticsComponent } from './fuel-log-analytics/fuel-log-analytics.component';

const routes: Routes = [
  { path: '', component: FuelLogListComponent },
  { path: 'new', component: FuelLogFormComponent },
  { path: ':id/edit', component: FuelLogFormComponent }
];

@NgModule({
  declarations: [FuelLogListComponent, FuelLogFormComponent, FuelLogAnalyticsComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class FuelLogsModule {}
