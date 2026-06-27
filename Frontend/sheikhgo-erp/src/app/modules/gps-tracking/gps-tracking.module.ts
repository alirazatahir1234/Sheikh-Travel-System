import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { UiInputComponent } from '../../shared/components/ui/input/ui-input.component';
import { UiSelectComponent } from '../../shared/components/ui/select/ui-select.component';
import { GpsTrackingLayoutComponent } from './gps-tracking-layout.component';
import { LiveMapComponent } from './live-map/live-map.component';
import { GpsHistoryComponent } from './history/gps-history.component';
import { GpsTripsComponent } from './trips/gps-trips.component';
import { GpsGeofencesComponent } from './geofences/gps-geofences.component';
import { GpsAlertsComponent } from './alerts/gps-alerts.component';
import { GpsDevicesComponent } from './devices/gps-devices.component';
import { GpsCommandsComponent } from './commands/gps-commands.component';

const routes: Routes = [
  {
    path: '',
    component: GpsTrackingLayoutComponent,
    children: [
      { path: '', redirectTo: 'live', pathMatch: 'full' },
      { path: 'live', component: LiveMapComponent },
      { path: 'history', component: GpsHistoryComponent },
      { path: 'trips', component: GpsTripsComponent },
      { path: 'geofences', component: GpsGeofencesComponent },
      { path: 'alerts', component: GpsAlertsComponent },
      { path: 'devices', component: GpsDevicesComponent },
      { path: 'commands', component: GpsCommandsComponent }
    ]
  }
];

@NgModule({
  declarations: [
    GpsTrackingLayoutComponent,
    LiveMapComponent,
    GpsHistoryComponent,
    GpsTripsComponent,
    GpsGeofencesComponent,
    GpsAlertsComponent,
    GpsDevicesComponent,
    GpsCommandsComponent
  ],
  imports: [SharedModule, UiInputComponent, UiSelectComponent, RouterModule.forChild(routes)]
})
export class GpsTrackingModule {}
