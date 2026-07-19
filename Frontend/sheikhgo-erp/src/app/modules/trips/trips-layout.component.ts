import { Component } from '@angular/core';

interface TripNavItem {
  label: string;
  icon: string;
  route: string;
}

@Component({
  standalone: false,
  selector: 'app-trips-layout',
  templateUrl: './trips-layout.component.html',
  styleUrls: ['./trips-layout.component.scss']
})
export class TripsLayoutComponent {
  readonly navItems: TripNavItem[] = [
    { label: 'Dashboard', icon: 'dashboard', route: 'dashboard' },
    { label: 'Trip List', icon: 'list_alt', route: 'list' },
    { label: 'Calendar', icon: 'calendar_month', route: 'calendar' },
    { label: 'Dispatch Board', icon: 'view_kanban', route: 'live' },
    { label: 'Reports', icon: 'insights', route: 'reports' },
    { label: 'New Trip', icon: 'add_circle', route: 'new' }
  ];
}
