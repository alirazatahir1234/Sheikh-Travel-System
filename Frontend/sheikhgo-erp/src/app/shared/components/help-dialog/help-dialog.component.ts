import { Component } from '@angular/core';
import { MatDialogRef } from '@angular/material/dialog';
import { APP_PRODUCT_NAME } from '../../../core/constants/app-brand';

export type HelpAccent = 'blue' | 'purple' | 'teal' | 'amber' | 'red' | 'yellow' | 'sky' | 'slate';

export interface HelpCard {
  id: string;
  categoryId: string;
  title: string;
  description: string;
  icon: string;
  accent: HelpAccent;
  checklist: string[];
}

export interface HelpCategory {
  id: string;
  label: string;
  icon: string;
  /** When true, show every card (Getting Started). */
  showAll?: boolean;
  heading: string;
  subtitle: string;
}

@Component({
  standalone: false,
  selector: 'app-help-dialog',
  templateUrl: './help-dialog.component.html',
  styleUrls: ['./help-dialog.component.scss']
})
export class HelpDialogComponent {
  readonly appProductName = APP_PRODUCT_NAME;
  readonly supportEmail = 'support@sheikhtravel.com';
  readonly supportPhone = '+92 300 1234567';
  readonly supportHours = 'Mon - Fri, 9:00 AM - 6:00 PM (Pakistan Time)';

  readonly categories: HelpCategory[] = [
    {
      id: 'getting-started',
      label: 'Getting Started',
      icon: 'menu_book',
      showAll: true,
      heading: 'Getting Started',
      subtitle: `Welcome to ${APP_PRODUCT_NAME}! Here are some quick guides to help you get started.`
    },
    {
      id: 'bookings',
      label: 'Bookings',
      icon: 'confirmation_number',
      heading: 'Bookings',
      subtitle: 'Create and manage bookings for your customers.'
    },
    {
      id: 'trips',
      label: 'Trips',
      icon: 'alt_route',
      heading: 'Trips',
      subtitle: 'Plan routes, waypoints, and trip assignments.'
    },
    {
      id: 'fleet',
      label: 'Fleet Management',
      icon: 'directions_bus',
      heading: 'Fleet Management',
      subtitle: 'Manage vehicles and fleet operations.'
    },
    {
      id: 'gps',
      label: 'GPS Tracking',
      icon: 'gps_fixed',
      heading: 'GPS Tracking',
      subtitle: 'Monitor vehicles in real time with live telemetry.'
    },
    {
      id: 'drivers',
      label: 'Drivers',
      icon: 'badge',
      heading: 'Drivers',
      subtitle: 'Add, assign, and monitor your drivers.'
    },
    {
      id: 'maintenance',
      label: 'Maintenance',
      icon: 'build',
      heading: 'Maintenance',
      subtitle: 'Keep your fleet in top condition.'
    },
    {
      id: 'fuel',
      label: 'Fuel Management',
      icon: 'local_gas_station',
      heading: 'Fuel Management',
      subtitle: 'Track and analyze your fuel usage.'
    },
    {
      id: 'geofences',
      label: 'Geofences & Alerts',
      icon: 'fence',
      heading: 'Geofences & Alerts',
      subtitle: 'Define zones and stay on top of notifications.'
    },
    {
      id: 'reports',
      label: 'Reports & Analytics',
      icon: 'insights',
      heading: 'Reports & Analytics',
      subtitle: 'Get insights and export your data.'
    },
    {
      id: 'admin',
      label: 'Users & Administration',
      icon: 'admin_panel_settings',
      heading: 'Users & Administration',
      subtitle: 'Manage users, access, and system settings.'
    }
  ];

  readonly cards: HelpCard[] = [
    {
      id: 'booking',
      categoryId: 'bookings',
      title: 'Creating a Booking',
      description: 'Learn how to create and manage bookings for your customers.',
      icon: 'confirmation_number',
      accent: 'blue',
      checklist: [
        'Navigate to Bookings → New Booking',
        'Select a customer or create a new one',
        'Choose pickup/dropoff locations and dates',
        'Assign a vehicle and driver',
        'Review and confirm the booking'
      ]
    },
    {
      id: 'trips',
      categoryId: 'trips',
      title: 'Managing Trips',
      description: 'Create and manage trips with routes, waypoints and assignments.',
      icon: 'alt_route',
      accent: 'purple',
      checklist: [
        'Navigate to Trips → New Trip',
        'Select customer and route',
        'Add pickup, destination and waypoints',
        'Set date, time and expected arrival',
        'Assign driver and vehicle',
        'Monitor trip progress from Tracking'
      ]
    },
    {
      id: 'fleet',
      categoryId: 'fleet',
      title: 'Fleet Management',
      description: 'Manage your vehicles and fleet operations.',
      icon: 'directions_bus',
      accent: 'teal',
      checklist: [
        'Add and manage vehicles',
        'Maintain vehicle information',
        'Assign vehicles to drivers and trips',
        'Track vehicle status and availability',
        'View real-time locations on the map'
      ]
    },
    {
      id: 'gps',
      categoryId: 'gps',
      title: 'GPS Tracking',
      description: 'Monitor vehicles in real time with live telemetry.',
      icon: 'gps_fixed',
      accent: 'sky',
      checklist: [
        'Open Fleet Tracking',
        'View vehicles on the live map',
        'Monitor moving, idle, parked and offline status',
        'View telemetry and current location',
        'Use nearby search to find useful locations',
        'Send supported commands to GPS devices'
      ]
    },
    {
      id: 'drivers',
      categoryId: 'drivers',
      title: 'Managing Drivers',
      description: 'Add, assign and monitor your drivers.',
      icon: 'badge',
      accent: 'amber',
      checklist: [
        'Add drivers with license and contact details',
        'View driver profiles and trip history',
        'Track driver availability and status',
        'Monitor license expiration alerts'
      ]
    },
    {
      id: 'maintenance',
      categoryId: 'maintenance',
      title: 'Maintenance',
      description: 'Keep your fleet in top condition.',
      icon: 'build',
      accent: 'red',
      checklist: [
        'Create and manage maintenance records',
        'Track upcoming and overdue services',
        'Record repair and maintenance costs',
        'Review vehicle maintenance history'
      ]
    },
    {
      id: 'fuel',
      categoryId: 'fuel',
      title: 'Fuel Management',
      description: 'Track and analyze your fuel usage.',
      icon: 'local_gas_station',
      accent: 'yellow',
      checklist: [
        'Record fuel transactions',
        'Track fuel quantity and cost',
        'Monitor fuel consumption',
        'Review fuel history by vehicle'
      ]
    },
    {
      id: 'geofences',
      categoryId: 'geofences',
      title: 'Geofences & Alerts',
      description: 'Set geographic zones and review notifications.',
      icon: 'fence',
      accent: 'teal',
      checklist: [
        'Open Geofencing under GPS Tracking',
        'Create and edit geofence zones on the map',
        'Review breach and location alerts',
        'Use Notification Center for in-app alerts'
      ]
    },
    {
      id: 'reports',
      categoryId: 'reports',
      title: 'Reports & Analytics',
      description: 'Get insights and export your data.',
      icon: 'insights',
      accent: 'blue',
      checklist: [
        'Access reports for trips, vehicles and drivers',
        'Monitor fleet KPIs on the dashboard',
        'Export reports to Excel or PDF',
        'Review system activity and audit logs'
      ]
    },
    {
      id: 'admin',
      categoryId: 'admin',
      title: 'Users & Administration',
      description: 'Manage access, users, and configuration.',
      icon: 'admin_panel_settings',
      accent: 'slate',
      checklist: [
        'Manage users under Users',
        'Configure roles and access control',
        'Review security and audit centers (Admin)',
        'Update organization settings in Settings'
      ]
    }
  ];

  activeCategoryId = 'getting-started';
  searchQuery = '';

  constructor(private dialogRef: MatDialogRef<HelpDialogComponent>) {}

  get activeCategory(): HelpCategory {
    return this.categories.find(c => c.id === this.activeCategoryId) ?? this.categories[0];
  }

  get showWelcomeBanner(): boolean {
    return this.activeCategoryId === 'getting-started' && !this.searchQuery.trim();
  }

  get filteredCards(): HelpCard[] {
    const q = this.searchQuery.trim().toLowerCase();
    const category = this.activeCategory;

    let list = this.cards;
    if (!category.showAll) {
      list = list.filter(c => c.categoryId === category.id);
    }

    if (!q) return list;

    return this.cards.filter(card => {
      const hay = [
        card.title,
        card.description,
        ...card.checklist
      ].join(' ').toLowerCase();
      return hay.includes(q);
    });
  }

  selectCategory(id: string): void {
    this.activeCategoryId = id;
    this.searchQuery = '';
  }

  onSearchInput(value: string): void {
    this.searchQuery = value;
  }

  onCardClick(card: HelpCard): void {
    this.activeCategoryId = card.categoryId;
    this.searchQuery = '';
  }

  contactSupport(): void {
    window.location.href = `mailto:${this.supportEmail}`;
  }

  close(): void {
    this.dialogRef.close();
  }

  trackByCategoryId(_i: number, c: HelpCategory): string {
    return c.id;
  }

  trackByCardId(_i: number, c: HelpCard): string {
    return c.id;
  }
}
