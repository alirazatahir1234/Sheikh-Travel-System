import { Component, inject } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';

export interface GpsNavItem {
  label: string;
  icon: string;
  route: string;
  /** Optional permission — omit means GPS.View (module gate) is enough. */
  permission?: string;
}

@Component({
  standalone: false,
  selector: 'app-gps-tracking-layout',
  templateUrl: './gps-tracking-layout.component.html',
  styleUrls: ['./gps-tracking-layout.component.scss']
})
export class GpsTrackingLayoutComponent {
  private readonly auth = inject(AuthService);

  private readonly allNavItems: GpsNavItem[] = [
    { label: 'Live Map', icon: 'my_location', route: 'live' },
    { label: 'Devices', icon: 'sensors', route: 'devices' },
    { label: 'Alerts', icon: 'notifications_active', route: 'alerts', permission: 'Gps.AlertView' },
    { label: 'Geofences', icon: 'fence', route: 'geofences' },
    { label: 'Trips', icon: 'route', route: 'trips' },
    { label: 'History', icon: 'history', route: 'history' },
    { label: 'Commands', icon: 'power_settings_new', route: 'commands', permission: 'Gps.CommandView' },
    { label: 'Analytics', icon: 'insights', route: 'analytics' }
  ];

  get navItems(): GpsNavItem[] {
    return this.allNavItems.filter(item =>
      !item.permission || this.auth.hasPermission(item.permission)
    );
  }
}
