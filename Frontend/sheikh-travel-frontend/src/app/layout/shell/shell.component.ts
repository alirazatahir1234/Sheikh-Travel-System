import { Component } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';

interface NavItem {
  label: string;
  icon: string;
  route: string;
}

@Component({
  selector: 'app-shell',
  templateUrl: './shell.component.html',
  styleUrls: ['./shell.component.scss']
})
export class ShellComponent {
  navItems: NavItem[] = [
    { label: 'Dashboard',     icon: 'dashboard',         route: '/dashboard' },
    { label: 'Vehicles',      icon: 'directions_bus',    route: '/vehicles' },
    { label: 'Drivers',       icon: 'person',            route: '/drivers' },
    { label: 'Routes',        icon: 'route',             route: '/routes' },
    { label: 'Bookings',      icon: 'book_online',       route: '/bookings' },
    { label: 'Payments',      icon: 'payments',          route: '/payments' },
    { label: 'Reports',       icon: 'bar_chart',         route: '/reports' },
    { label: 'Tracking',      icon: 'location_on',       route: '/tracking' },
  ];

  currentUser$: AuthService['currentUser$'];

  constructor(private auth: AuthService) {
    this.currentUser$ = auth.currentUser$;
  }

  trackByRoute(_index: number, item: NavItem): string {
    return item.route;
  }

  logout(): void {
    this.auth.logout();
  }
}
