import { Routes } from '@angular/router';

export const GPS_TRACKING_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./gps-tracking-layout.component').then(m => m.GpsTrackingLayoutComponent),
    children: [
      { path: '', redirectTo: 'live', pathMatch: 'full' },
      {
        path: 'live',
        loadComponent: () => import('./live-map/live-map.page').then(m => m.LiveMapPageComponent)
      },
      {
        path: 'history',
        loadComponent: () => import('./history/gps-history.component').then(m => m.GpsHistoryComponent)
      },
      {
        path: 'trips',
        loadComponent: () => import('./trips/gps-trips.component').then(m => m.GpsTripsComponent)
      },
      {
        path: 'stops',
        loadComponent: () => import('./stops/gps-stops.component').then(m => m.GpsStopsComponent)
      },
      {
        path: 'events',
        loadComponent: () => import('./events/gps-events.component').then(m => m.GpsEventsComponent)
      },
      {
        path: 'geofences',
        loadComponent: () =>
          import('./geofences/gps-geofences.component').then(m => m.GpsGeofencesComponent)
      },
      {
        path: 'alerts',
        loadComponent: () => import('./alerts/gps-alerts.component').then(m => m.GpsAlertsComponent)
      },
      {
        path: 'devices',
        loadComponent: () => import('./devices/gps-devices.component').then(m => m.GpsDevicesComponent)
      },
      {
        path: 'devices/register',
        loadComponent: () =>
          import('./tracker-register/tracker-register-page.component').then(m => m.TrackerRegisterPageComponent)
      },
      {
        path: 'devices/:id/install',
        loadComponent: () =>
          import('./tracker-install/tracker-install-page.component').then(m => m.TrackerInstallPageComponent)
      },
      {
        path: 'devices/:id/transfer',
        loadComponent: () =>
          import('./tracker-install/tracker-install-page.component').then(m => m.TrackerInstallPageComponent)
      },
      {
        path: 'devices/:id/edit',
        loadComponent: () =>
          import('./tracker-register/tracker-register-page.component').then(m => m.TrackerRegisterPageComponent)
      },
      {
        path: 'devices/:id',
        loadComponent: () =>
          import('./tracker-details/tracker-details-page.component').then(m => m.TrackerDetailsPageComponent)
      },
      {
        path: 'commands',
        loadComponent: () => import('./commands/gps-commands.component').then(m => m.GpsCommandsComponent)
      }
    ]
  }
];
