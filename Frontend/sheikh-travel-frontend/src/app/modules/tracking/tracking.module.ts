import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { TrackingComponent } from './tracking.component';

const routes: Routes = [{ path: '', component: TrackingComponent }];

@NgModule({
  declarations: [TrackingComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class TrackingModule {}
