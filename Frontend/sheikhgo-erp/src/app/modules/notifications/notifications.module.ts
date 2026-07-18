import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { NotificationCenterComponent } from './notification-center/notification-center.component';

const routes: Routes = [
  { path: '', component: NotificationCenterComponent }
];

@NgModule({
  declarations: [NotificationCenterComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class NotificationsModule {}
