import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { driverOnlyGuard } from '../../core/guards/driver-workspace.guard';
import { MyTripsComponent } from './my-trips/my-trips.component';
import { LogFuelComponent } from './log-fuel/log-fuel.component';

const routes: Routes = [
  { path: '', component: MyTripsComponent, canActivate: [driverOnlyGuard] },
  { path: 'fuel', component: LogFuelComponent, canActivate: [driverOnlyGuard] }
];

@NgModule({
  declarations: [MyTripsComponent, LogFuelComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class DriverWorkspaceModule {}
