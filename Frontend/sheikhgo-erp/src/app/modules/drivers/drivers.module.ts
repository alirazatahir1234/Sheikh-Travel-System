import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { FleetUiModule } from '../../shared/fleet-ui';
import { DriverProfileComponent } from './driver-profile/driver-profile.component';
import { DriverInventoryPageComponent } from './driver-inventory-page/driver-inventory-page.component';
import { DriverRegisterWizardComponent } from './driver-register-wizard/driver-register-wizard.component';
import { DriverDetailsDrawerComponent } from './driver-details-drawer/driver-details-drawer.component';
import { DriverVerificationHubComponent } from './driver-verification-hub/driver-verification-hub.component';
import { DriverTrackingRedirectComponent } from './driver-tracking-redirect/driver-tracking-redirect.component';
import { DriverClientRatingsPageComponent } from './driver-client-ratings/driver-client-ratings-page.component';
import { permissionGuard } from '../../core/guards/permission.guard';

const routes: Routes = [
  { path: '', component: DriverInventoryPageComponent },
  { path: 'client-ratings', component: DriverClientRatingsPageComponent },
  {
    path: 'new',
    component: DriverRegisterWizardComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Driver.Create'] }
  },
  {
    path: ':id/edit',
    component: DriverRegisterWizardComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Driver.Update'] }
  },
  {
    path: ':id/verify',
    component: DriverVerificationHubComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Driver.Update'] }
  },
  { path: ':id/tracking', component: DriverTrackingRedirectComponent },
  { path: ':id', component: DriverProfileComponent }
];

@NgModule({
  declarations: [DriverProfileComponent],
  imports: [
    SharedModule,
    FleetUiModule,
    DriverInventoryPageComponent,
    DriverRegisterWizardComponent,
    DriverDetailsDrawerComponent,
    DriverVerificationHubComponent,
    DriverClientRatingsPageComponent,
    RouterModule.forChild(routes)
  ]
})
export class DriversModule {}
