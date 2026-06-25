import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { UserListComponent } from './user-list/user-list.component';
import { UserFormComponent } from './user-form/user-form.component';
import { UiButtonComponent } from '../../shared/components/ui/button/ui-button.component';
import { UiPageHeaderComponent } from '../../shared/components/ui/page-header/ui-page-header.component';

const routes: Routes = [
  { path: '', component: UserListComponent },
  { path: 'new', component: UserFormComponent },
  { path: ':id/edit', component: UserFormComponent }
];

@NgModule({
  declarations: [UserListComponent, UserFormComponent],
  imports: [SharedModule, RouterModule.forChild(routes), UiButtonComponent, UiPageHeaderComponent]
})
export class UsersModule {}
