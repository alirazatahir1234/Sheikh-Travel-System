import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { FleetUiModule } from '../../shared/fleet-ui';
import { UiModule } from '../../shared/components/ui/ui.module';
import { VehicleInventoryPageComponent } from './vehicle-inventory-page/vehicle-inventory-page.component';
import { VehicleRegisterWizardComponent } from './vehicle-register-wizard/vehicle-register-wizard.component';
import { VehicleProfileComponent } from './vehicle-profile/vehicle-profile.component';
import { VehicleDetailsDrawerComponent } from './vehicle-details-drawer/vehicle-details-drawer.component';
import { permissionGuard } from '../../core/guards/permission.guard';

const routes: Routes = [
  { path: '', component: VehicleInventoryPageComponent },
  {
    path: 'new',
    component: VehicleRegisterWizardComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Vehicle.Create'] }
  },
  { path: ':id', component: VehicleProfileComponent },
  {
    path: ':id/edit',
    component: VehicleRegisterWizardComponent,
    canActivate: [permissionGuard],
    data: { permissions: ['Vehicle.Update'] }
  }
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
