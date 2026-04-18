import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { RouteListComponent } from './route-list/route-list.component';
import { RouteFormComponent } from './route-form/route-form.component';

const routes: Routes = [
  { path: '', component: RouteListComponent },
  { path: 'new', component: RouteFormComponent },
  { path: ':id/edit', component: RouteFormComponent }
];

@NgModule({
  declarations: [RouteListComponent, RouteFormComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class RoutesModule {}
