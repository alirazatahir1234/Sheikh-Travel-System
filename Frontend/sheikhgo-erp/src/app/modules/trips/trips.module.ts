import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { GoogleMapsModule } from '@angular/google-maps';
import { DragDropModule } from '@angular/cdk/drag-drop';
import { SharedModule } from '../../shared/shared.module';
import { UiButtonComponent } from '../../shared/components/ui/button/ui-button.component';
import { UiEmptyStateComponent } from '../../shared/components/ui/empty-state/ui-empty-state.component';
import { TripsLayoutComponent } from './trips-layout.component';
import { TripDashboardComponent } from './trip-dashboard/trip-dashboard.component';
import { TripListComponent } from './trip-list/trip-list.component';
import { TripFormComponent } from './trip-form/trip-form.component';
import { TripDetailComponent } from './trip-detail/trip-detail.component';
import { TripCalendarComponent } from './trip-calendar/trip-calendar.component';
import { TripLiveBoardComponent } from './trip-live-board/trip-live-board.component';
import { TripReportsComponent } from './trip-reports/trip-reports.component';
import { TripSmartAssignmentComponent } from './smart-assignment/smart-assignment.component';
import { permissionGuard } from '../../core/guards/permission.guard';

const routes: Routes = [
  {
    path: '',
    component: TripsLayoutComponent,
    children: [
      { path: '', redirectTo: 'list', pathMatch: 'full' },
      { path: 'dashboard', component: TripDashboardComponent },
      { path: 'list', component: TripListComponent },
      { path: 'calendar', component: TripCalendarComponent },
      { path: 'live', component: TripLiveBoardComponent },
      { path: 'reports', component: TripReportsComponent },
      { path: 'smart-assignment', component: TripSmartAssignmentComponent },
      {
        path: 'new',
        component: TripFormComponent,
        canActivate: [permissionGuard],
        data: { permissions: ['Trip.Create'] }
      },
      {
        path: ':id/edit',
        component: TripFormComponent,
        canMatch: [tripIdCanMatch],
        canActivate: [permissionGuard],
        data: { permissions: ['Trip.Update'] }
      },
      { path: ':id', component: TripDetailComponent, canMatch: [tripIdCanMatch] }
    ]
  }
];

function tripIdCanMatch(route: import('@angular/router').Route, segments: import('@angular/router').UrlSegment[]): boolean {
  const id = segments[0]?.path;
  return !!id && /^\d+$/.test(id);
}

@NgModule({
  declarations: [
    TripsLayoutComponent,
    TripDashboardComponent,
    TripListComponent,
    TripFormComponent,
    TripDetailComponent,
    TripCalendarComponent,
    TripLiveBoardComponent,
    TripReportsComponent,
    TripSmartAssignmentComponent
  ],
  imports: [SharedModule, GoogleMapsModule, DragDropModule, RouterModule.forChild(routes), UiButtonComponent, UiEmptyStateComponent]
})
export class TripsModule {}
