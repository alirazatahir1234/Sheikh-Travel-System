import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { DriverCashOutAdminComponent } from './driver-cash-out-admin/driver-cash-out-admin.component';

const routes: Routes = [{ path: '', component: DriverCashOutAdminComponent }];

@NgModule({
  declarations: [DriverCashOutAdminComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class DriverWalletsModule {}
