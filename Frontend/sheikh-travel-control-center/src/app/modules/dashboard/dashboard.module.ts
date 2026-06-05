import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { DashboardComponent } from './dashboard.component';
import { DashboardAnalyticsComponent } from './dashboard-analytics/dashboard-analytics.component';

const routes: Routes = [{ path: '', component: DashboardComponent }];

@NgModule({
  declarations: [DashboardComponent, DashboardAnalyticsComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class DashboardModule {}
