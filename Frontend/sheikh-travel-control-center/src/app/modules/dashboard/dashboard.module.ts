import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { DashboardContainerComponent } from './components/dashboard-container/dashboard-container.component';

const routes: Routes = [{ path: '', component: DashboardContainerComponent }];

@NgModule({
  imports: [DashboardContainerComponent, RouterModule.forChild(routes)]
})
export class DashboardModule {}
