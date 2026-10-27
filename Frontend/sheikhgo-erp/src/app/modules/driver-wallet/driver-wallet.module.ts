import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { DriverWalletPageComponent } from './driver-wallet-page/driver-wallet-page.component';

const routes: Routes = [{ path: '', component: DriverWalletPageComponent }];

@NgModule({
  declarations: [DriverWalletPageComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class DriverWalletModule {}
