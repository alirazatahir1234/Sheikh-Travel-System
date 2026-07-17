import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { ReportsComponent } from './reports.component';
import { FleetReportsPageComponent } from './fleet/fleet-reports-page.component';

const routes: Routes = [{ path: '', component: ReportsComponent }];

@NgModule({
  declarations: [ReportsComponent],
  imports: [SharedModule, FleetReportsPageComponent, RouterModule.forChild(routes)]
})
export class ReportsModule {}
