import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ScrollingModule } from '@angular/cdk/scrolling';
import { SharedModule } from '../../shared/shared.module';
import { OverlayModule } from '@angular/cdk/overlay';
import { A11yModule } from '@angular/cdk/a11y';
import { UiInputComponent } from '../../shared/components/ui/input/ui-input.component';
import { UiSelectComponent } from '../../shared/components/ui/select/ui-select.component';
import { UiChartComponent } from '../../shared/components/ui/chart/ui-chart.component';
import { UiDataTableComponent } from '../../shared/components/ui/data-table/ui-data-table.component';
import { UiTableCellDirective } from '../../shared/components/ui/data-table/ui-table-cell.directive';
import { CriticalAlertsCardComponent } from '../fleet-management/fleet-dashboard/widgets/critical-alerts-card.component';
import { RecentActivitiesCardComponent } from '../fleet-management/fleet-dashboard/widgets/recent-activities-card.component';
import { GpsTrackingLayoutComponent } from './gps-tracking-layout.component';
import { GpsScopeFilterBarComponent } from './gps-scope-filter-bar/gps-scope-filter-bar.component';
import { LiveMapComponent } from './live-map/live-map.component';
import { FleetKpiStripComponent } from './live-map/fleet-kpi-strip/fleet-kpi-strip.component';
import { NearbyPlacesPanelComponent } from './live-map/nearby-places-panel/nearby-places-panel.component';
import { VehicleDetailPanelComponent } from './live-map/vehicle-detail-panel/vehicle-detail-panel.component';
import { GpsHistoryComponent } from './history/gps-history.component';
import { HistoryMapComponent } from './history/history-map/history-map.component';
import { HistoryPlaybackDockComponent } from './history/history-playback-dock/history-playback-dock.component';
import { HistoryPointDetailsComponent } from './history/history-point-details/history-point-details.component';
import { HistoryTimelinePanelComponent } from './history/history-timeline-panel/history-timeline-panel.component';
import { HistoryVehiclePickerComponent } from './history/history-vehicle-picker/history-vehicle-picker.component';
import { GpsTripsComponent } from './trips/gps-trips.component';
import { TripDetailPageComponent } from './trip-detail/trip-detail-page.component';
import { TripReplayMapComponent } from './shared/trip-replay-map/trip-replay-map.component';
import { TripRouteAnalysisPanelComponent } from './shared/trip-route-analysis-panel/trip-route-analysis-panel.component';
import { GeofenceWorkspaceComponent } from './geofences/geofence-workspace/geofence-workspace.component';
import { GeofenceEditorComponent } from './geofences/geofence-editor/geofence-editor.component';
import { GeofenceVisitsComponent } from './geofences/geofence-visits/geofence-visits.component';
import { GpsAlertsComponent } from './alerts/gps-alerts.component';
import { SosDispatchComponent } from './sos-dispatch/sos-dispatch.component';
import { GpsDevicesComponent } from './devices/gps-devices.component';
import { GpsCommandsComponent } from './commands/gps-commands.component';
import { GpsAnalyticsComponent } from './analytics/gps-analytics.component';
import { AnalyticsReportSchedulesComponent } from './analytics/report-schedules/analytics-report-schedules.component';
import { TrackerRegisterPageComponent } from './tracker-register/tracker-register-page.component';
import { TrackerInstallPageComponent } from './tracker-install/tracker-install-page.component';
import { TrackerDetailsPageComponent } from './tracker-details/tracker-details-page.component';
import { DriverPerformancePageComponent } from './driver-performance/driver-performance-page.component';
import { ClientTrackingLinksPageComponent } from './client-tracking-links/client-tracking-links-page.component';
import { FatigueEventsPageComponent } from './fatigue-events/fatigue-events-page.component';
import { AccidentRiskPageComponent } from './accident-risk/accident-risk-page.component';
import { GpsTripModePageComponent } from './trip-mode/gps-trip-mode-page.component';
import { permissionGuard } from '../../core/guards/permission.guard';

const routes: Routes = [
  {
    path: '',
    component: GpsTrackingLayoutComponent,
    children: [
      { path: '', redirectTo: 'live', pathMatch: 'full' },
      { path: 'live', component: LiveMapComponent },
      { path: 'history', component: GpsHistoryComponent },
      { path: 'trips', component: GpsTripsComponent },
      {
        path: 'trip-mode',
        component: GpsTripModePageComponent,
        canActivate: [permissionGuard],
        data: { permissions: ['Gps.DeviceManage'] }
      },
      { path: 'trips/:tripKey', component: TripDetailPageComponent },
      { path: 'geofences', component: GeofenceWorkspaceComponent },
      { path: 'geofences/visits', component: GeofenceVisitsComponent },
      {
        path: 'geofences/new',
        component: GeofenceEditorComponent,
        canActivate: [permissionGuard],
        data: { permissions: ['Gps.GeofenceManage'] }
      },
      {
        path: 'geofences/:id/edit',
        component: GeofenceEditorComponent,
        canActivate: [permissionGuard],
        data: { permissions: ['Gps.GeofenceManage'] }
      },
      {
        path: 'alerts',
        component: GpsAlertsComponent,
        canActivate: [permissionGuard],
        data: { permissions: ['Gps.AlertView'] }
      },
      {
        path: 'sos',
        component: SosDispatchComponent,
        canActivate: [permissionGuard],
        data: { permissions: ['Gps.AlertView'] }
      },
      {
        path: 'fatigue-events',
        component: FatigueEventsPageComponent,
        canActivate: [permissionGuard],
        data: { permissions: ['Gps.AlertView'] }
      },
      {
        path: 'devices',
        component: GpsDevicesComponent,
        canActivate: [permissionGuard],
        data: { permissions: ['Gps.DeviceManage'] }
      },
      {
        path: 'devices/register',
        component: TrackerRegisterPageComponent,
        canActivate: [permissionGuard],
        data: { permissions: ['Gps.DeviceManage'] }
      },
      {
        path: 'devices/:id/install',
        component: TrackerInstallPageComponent,
        canActivate: [permissionGuard],
        data: { permissions: ['Gps.DeviceManage'] }
      },
      {
        path: 'devices/:id/transfer',
        component: TrackerInstallPageComponent,
        canActivate: [permissionGuard],
        data: { permissions: ['Gps.DeviceManage'] }
      },
      {
        path: 'devices/:id/edit',
        component: TrackerRegisterPageComponent,
        canActivate: [permissionGuard],
        data: { permissions: ['Gps.DeviceManage'] }
      },
      {
        path: 'devices/:id',
        component: TrackerDetailsPageComponent,
        canActivate: [permissionGuard],
        data: { permissions: ['Gps.DeviceManage'] }
      },
      {
        path: 'commands',
        component: GpsCommandsComponent,
        canActivate: [permissionGuard],
        data: { roles: ['SUPER_ADMIN'], permissions: ['Gps.CommandView'] }
      },
      { path: 'analytics', component: GpsAnalyticsComponent },
      { path: 'driver-performance', component: DriverPerformancePageComponent },
      {
        path: 'accident-risk',
        component: AccidentRiskPageComponent,
        canActivate: [permissionGuard],
        data: { permissions: ['Gps.AlertView'] }
      },
      { path: 'client-tracking-links', component: ClientTrackingLinksPageComponent }
    ]
  }
];

@NgModule({
  declarations: [
    GpsTrackingLayoutComponent,
    GpsScopeFilterBarComponent,
    LiveMapComponent,
    FleetKpiStripComponent,
    NearbyPlacesPanelComponent,
    VehicleDetailPanelComponent,
    GpsHistoryComponent,
    GpsTripsComponent,
    TripDetailPageComponent,
    TripReplayMapComponent,
    TripRouteAnalysisPanelComponent,
    GpsAlertsComponent,
    SosDispatchComponent,
    GpsDevicesComponent,
    GpsCommandsComponent,
    GpsAnalyticsComponent,
    TrackerRegisterPageComponent,
    TrackerInstallPageComponent,
    TrackerDetailsPageComponent,
    DriverPerformancePageComponent,
    AccidentRiskPageComponent,
    ClientTrackingLinksPageComponent
  ],
  imports: [
    SharedModule, UiInputComponent, UiSelectComponent, UiChartComponent, ScrollingModule,
    UiDataTableComponent, UiTableCellDirective,
    CriticalAlertsCardComponent, RecentActivitiesCardComponent,
    AnalyticsReportSchedulesComponent,
    FatigueEventsPageComponent,
    // History workspace: overlay + focus trap for its pickers, and its five panels.
    OverlayModule, A11yModule,
    HistoryVehiclePickerComponent, HistoryTimelinePanelComponent, HistoryMapComponent,
    HistoryPlaybackDockComponent, HistoryPointDetailsComponent,
    RouterModule.forChild(routes)
  ]
})
export class GpsTrackingModule {}
