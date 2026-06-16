import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { FleetUiModule } from '../../shared/fleet-ui';
import { UiModule } from '../../shared/components/ui/ui.module';
import { VehicleInventoryPageComponent } from './vehicle-inventory-page/vehicle-inventory-page.component';
import { VehicleRegisterWizardComponent } from './vehicle-register-wizard/vehicle-register-wizard.component';
import { VehicleProfileComponent } from './vehicle-profile/vehicle-profile.component';
import { VehicleDetailsDrawerComponent } from './vehicle-details-drawer/vehicle-details-drawer.component';

const routes: Routes = [
  { path: '', component: VehicleInventoryPageComponent },
  { path: 'new', component: VehicleRegisterWizardComponent },
  { path: ':id', component: VehicleProfileComponent },
  { path: ':id/edit', component: VehicleRegisterWizardComponent }
];

@NgModule({
  declarations: [VehicleProfileComponent],
  imports: [
    SharedModule,
    FleetUiModule,
    UiModule,
    VehicleInventoryPageComponent,
    VehicleRegisterWizardComponent,
    VehicleDetailsDrawerComponent,
    RouterModule.forChild(routes)
  ]
})
export class VehiclesModule {}
