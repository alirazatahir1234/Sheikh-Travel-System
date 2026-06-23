import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { ProfilePageComponent } from './profile-page/profile-page.component';

const routes: Routes = [
  { path: '', component: ProfilePageComponent }
];

@NgModule({
  declarations: [ProfilePageComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class ProfileModule {}
