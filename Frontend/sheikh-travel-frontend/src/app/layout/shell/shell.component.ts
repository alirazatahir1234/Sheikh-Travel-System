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
    { label: 'Dashboard', icon: 'dashboard',      route: '/dashboard' },
    { label: 'Bookings',  icon: 'confirmation_number', route: '/bookings' },
    { label: 'Vehicles',  icon: 'directions_bus', route: '/vehicles' },
    { label: 'Drivers',   icon: 'badge',          route: '/drivers' },
    { label: 'Customers', icon: 'group',          route: '/customers' },
    { label: 'Routes',    icon: 'alt_route',      route: '/routes' },
    { label: 'Tracking',  icon: 'my_location',    route: '/tracking' },
    { label: 'Payments',  icon: 'account_balance_wallet', route: '/payments' },
    { label: 'Reports',   icon: 'insights',       route: '/reports' },
    { label: 'Allowance Rules', icon: 'rule',     route: '/driver-allowance-rules' },
    { label: 'Users',     icon: 'manage_accounts', route: '/users' },
  ];

  currentUser$: AuthService['currentUser$'];

  constructor(private auth: AuthService) {
    this.currentUser$ = auth.currentUser$;
  }

  trackByRoute(_i: number, item: NavItem): string { return item.route; }

  logout(): void { this.auth.logout(); }

  initials(fullName?: string | null): string {
    if (!fullName) return '?';
    return fullName.split(' ').map(p => p[0]).slice(0, 2).join('').toUpperCase();
  }
}
