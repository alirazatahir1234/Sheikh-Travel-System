import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ScrollingModule } from '@angular/cdk/scrolling';
import { SharedModule } from '../../shared/shared.module';
import { UiInputComponent } from '../../shared/components/ui/input/ui-input.component';
import { UiSelectComponent } from '../../shared/components/ui/select/ui-select.component';
import { UiChartComponent } from '../../shared/components/ui/chart/ui-chart.component';
import { UiDataTableComponent } from '../../shared/components/ui/data-table/ui-data-table.component';
import { UiTableCellDirective } from '../../shared/components/ui/data-table/ui-table-cell.directive';
import { QuickActionsCardComponent } from '../fleet-management/fleet-dashboard/widgets/quick-actions-card.component';
import { CriticalAlertsCardComponent } from '../fleet-management/fleet-dashboard/widgets/critical-alerts-card.component';
import { RecentActivitiesCardComponent } from '../fleet-management/fleet-dashboard/widgets/recent-activities-card.component';
import { ActiveAssignmentsTableComponent } from '../fleet-management/fleet-dashboard/widgets/active-assignments-table.component';
import { GpsTrackingLayoutComponent } from './gps-tracking-layout.component';
import { LiveMapComponent } from './live-map/live-map.component';
import { GpsHistoryComponent } from './history/gps-history.component';
import { GpsTripsComponent } from './trips/gps-trips.component';
import { TripDetailPageComponent } from './trip-detail/trip-detail-page.component';
import { TripReplayMapComponent } from './shared/trip-replay-map/trip-replay-map.component';
import { TripRouteAnalysisPanelComponent } from './shared/trip-route-analysis-panel/trip-route-analysis-panel.component';
import { GpsGeofencesComponent } from './geofences/gps-geofences.component';
import { GpsAlertsComponent } from './alerts/gps-alerts.component';
import { GpsDevicesComponent } from './devices/gps-devices.component';
import { GpsCommandsComponent } from './commands/gps-commands.component';
import { GpsAnalyticsComponent } from './analytics/gps-analytics.component';
import { AnalyticsReportSchedulesComponent } from './analytics/report-schedules/analytics-report-schedules.component';
import { TrackerRegisterPageComponent } from './tracker-register/tracker-register-page.component';
import { TrackerInstallPageComponent } from './tracker-install/tracker-install-page.component';
import { TrackerDetailsPageComponent } from './tracker-details/tracker-details-page.component';

const routes: Routes = [
  {
    path: '',
    component: GpsTrackingLayoutComponent,
    children: [
      { path: '', redirectTo: 'live', pathMatch: 'full' },
      { path: 'live', component: LiveMapComponent },
      { path: 'history', component: GpsHistoryComponent },
      { path: 'trips', component: GpsTripsComponent },
      { path: 'trips/:tripKey', component: TripDetailPageComponent },
      { path: 'geofences', component: GpsGeofencesComponent },
      { path: 'alerts', component: GpsAlertsComponent },
      { path: 'devices', component: GpsDevicesComponent },
      { path: 'devices/register', component: TrackerRegisterPageComponent },
      { path: 'devices/:id/install', component: TrackerInstallPageComponent },
      { path: 'devices/:id/transfer', component: TrackerInstallPageComponent },
      { path: 'devices/:id/edit', component: TrackerRegisterPageComponent },
      { path: 'devices/:id', component: TrackerDetailsPageComponent },
      { path: 'commands', component: GpsCommandsComponent },
      { path: 'analytics', component: GpsAnalyticsComponent }
    ]
  }
];

@NgModule({
  declarations: [
    GpsTrackingLayoutComponent,
    LiveMapComponent,
    GpsHistoryComponent,
    GpsTripsComponent,
    TripDetailPageComponent,
    TripReplayMapComponent,
    TripRouteAnalysisPanelComponent,
    GpsGeofencesComponent,
    GpsAlertsComponent,
    GpsDevicesComponent,
    GpsCommandsComponent,
    GpsAnalyticsComponent,
    TrackerRegisterPageComponent,
    TrackerInstallPageComponent,
    TrackerDetailsPageComponent
  ],
  imports: [
    SharedModule, UiInputComponent, UiSelectComponent, UiChartComponent, ScrollingModule,
    UiDataTableComponent, UiTableCellDirective,
    QuickActionsCardComponent, CriticalAlertsCardComponent, RecentActivitiesCardComponent, ActiveAssignmentsTableComponent,
    AnalyticsReportSchedulesComponent,
    RouterModule.forChild(routes)
  ]
})
export class GpsTrackingModule {}
