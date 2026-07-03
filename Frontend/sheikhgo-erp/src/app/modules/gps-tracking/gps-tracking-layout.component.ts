import { Component } from '@angular/core';
import { SharedModule } from '../../shared/shared.module';

export interface GpsNavItem {
  label: string;
  icon: string;
  route: string;
}

@Component({
    selector: 'app-gps-tracking-layout',
    templateUrl: './gps-tracking-layout.component.html',
    styleUrls: ['./gps-tracking-layout.component.scss'],
    standalone: true,
    imports: [SharedModule]
})
export class GpsTrackingLayoutComponent {
  readonly navItems: GpsNavItem[] = [
    { label: 'Live Map', icon: 'my_location', route: 'live' },
    { label: 'Devices', icon: 'sensors', route: 'devices' },
    { label: 'Alerts', icon: 'notifications_active', route: 'alerts' },
    { label: 'Geofences', icon: 'fence', route: 'geofences' },
    { label: 'Trips', icon: 'route', route: 'trips' },
    { label: 'Stops', icon: 'pause_circle', route: 'stops' },
    { label: 'Events', icon: 'notifications_active', route: 'events' },
    { label: 'History', icon: 'history', route: 'history' },
    { label: 'Commands', icon: 'power_settings_new', route: 'commands' }
  ];
}
