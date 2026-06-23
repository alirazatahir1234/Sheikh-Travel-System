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

const routes: Routes = [
  { path: '', component: DriverInventoryPageComponent },
  { path: 'new', component: DriverRegisterWizardComponent },
  { path: ':id/edit', component: DriverRegisterWizardComponent },
  { path: ':id/verify', component: DriverVerificationHubComponent },
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
    RouterModule.forChild(routes)
  ]
})
export class DriversModule {}
