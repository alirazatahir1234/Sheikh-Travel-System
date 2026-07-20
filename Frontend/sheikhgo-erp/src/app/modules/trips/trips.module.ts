import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { UiButtonComponent } from '../../shared/components/ui/button/ui-button.component';
import { TripsLayoutComponent } from './trips-layout.component';
import { TripDashboardComponent } from './trip-dashboard/trip-dashboard.component';
import { TripListComponent } from './trip-list/trip-list.component';
import { TripFormComponent } from './trip-form/trip-form.component';
import { TripDetailComponent } from './trip-detail/trip-detail.component';
import { TripCalendarComponent } from './trip-calendar/trip-calendar.component';
import { TripLiveBoardComponent } from './trip-live-board/trip-live-board.component';
import { TripReportsComponent } from './trip-reports/trip-reports.component';

const routes: Routes = [
  {
    path: '',
    component: TripsLayoutComponent,
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', component: TripDashboardComponent },
      { path: 'list', component: TripListComponent },
      { path: 'calendar', component: TripCalendarComponent },
      { path: 'live', component: TripLiveBoardComponent },
      { path: 'reports', component: TripReportsComponent },
      { path: 'new', component: TripFormComponent },
      { path: ':id/edit', component: TripFormComponent, canMatch: [tripIdCanMatch] },
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
    TripReportsComponent
  ],
  imports: [SharedModule, RouterModule.forChild(routes), UiButtonComponent]
})
export class TripsModule {}
