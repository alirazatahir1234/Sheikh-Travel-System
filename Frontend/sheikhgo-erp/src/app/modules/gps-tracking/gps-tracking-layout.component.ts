import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Subscription, filter } from 'rxjs';
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
export class GpsTrackingLayoutComponent implements OnInit, OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private sub: Subscription | null = null;

  activeChild = 'live';

  private readonly allNavItems: GpsNavItem[] = [
    { label: 'Live Map', icon: 'my_location', route: 'live' },
    { label: 'Devices', icon: 'sensors', route: 'devices' },
    { label: 'Alerts', icon: 'notifications_active', route: 'alerts', permission: 'Gps.AlertView' },
    { label: 'Geofences', icon: 'fence', route: 'geofences' },
    { label: 'Trips', icon: 'route', route: 'trips' },
    { label: 'Trip Mode', icon: 'settings', route: 'trip-mode', permission: 'Gps.DeviceManage' },
    { label: 'History', icon: 'history', route: 'history' },
    { label: 'Commands', icon: 'power_settings_new', route: 'commands', permission: 'Gps.CommandView' },
    // Keep scoring next to Analytics so it is not buried at the far end of the scrollable tab strip.
    { label: 'Analytics', icon: 'insights', route: 'analytics' },
    { label: 'Driver Performance', icon: 'speed', route: 'driver-performance' },
    { label: 'Accident Risk', icon: 'health_and_safety', route: 'accident-risk', permission: 'Gps.AlertView' },
    { label: 'Client Links', icon: 'link', route: 'client-tracking-links' }
  ];

  get navItems(): GpsNavItem[] {
    return this.allNavItems.filter(item =>
      !item.permission || this.auth.hasPermission(item.permission)
    );
  }

  get isAlertsRoute(): boolean {
    return this.activeChild === 'alerts';
  }

  ngOnInit(): void {
    this.syncChild(this.router.url);
    this.sub = this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd)
    ).subscribe(e => this.syncChild(e.urlAfterRedirects));
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  openManageAlertRules(): void {
    void this.router.navigate(['/gps-tracking/alerts'], {
      queryParams: { tab: 'rules' },
      queryParamsHandling: 'merge'
    });
  }

  private syncChild(url: string): void {
    const match = /\/gps-tracking\/([^/?#]+)/.exec(url);
    this.activeChild = match?.[1] ?? 'live';
  }
}
