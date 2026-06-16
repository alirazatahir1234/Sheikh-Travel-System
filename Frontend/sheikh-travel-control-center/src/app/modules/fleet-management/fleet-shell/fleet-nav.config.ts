export interface FleetNavLink {
  id: string;
  label: string;
  icon: string;
  /** Omitted for items that are visual placeholders pending their own screens. */
  route?: string;
  /** Match the route exactly rather than by prefix. */
  exact?: boolean;
}

export interface FleetNavGroup {
  id: string;
  label: string;
  items: FleetNavLink[];
}

export const FLEET_NAV_GROUPS: FleetNavGroup[] = [
  {
    id: 'main',
    label: 'Fleet Management',
    items: [
      { id: 'dashboard', label: 'Dashboard', icon: 'dashboard', route: '/fleet/dashboard' },
      { id: 'vehicles', label: 'Vehicles', icon: 'local_shipping', route: '/vehicles' },
      { id: 'drivers', label: 'Drivers', icon: 'person', route: '/drivers' },
      { id: 'assignments', label: 'Assignments', icon: 'assignment', route: '/fleet/assignments' }
    ]
  },
  {
    id: 'operations',
    label: 'Operations',
    items: [
      { id: 'live-tracking', label: 'Live Tracking', icon: 'my_location', route: '/gps-tracking/live' },
      { id: 'trips', label: 'Trips', icon: 'route', route: '/gps-tracking/trips' },
      { id: 'maintenance', label: 'Maintenance', icon: 'build', route: '/maintenance' },
      { id: 'fuel', label: 'Fuel', icon: 'local_gas_station', route: '/fuel-logs' },
      { id: 'inspections', label: 'Inspections', icon: 'fact_check', route: '/fleet/inspections' },
      { id: 'compliance', label: 'Compliance', icon: 'verified_user', route: '/fleet/compliance' }
    ]
  },
  {
    id: 'resources',
    label: 'Resources',
    items: [
      { id: 'documents', label: 'Documents', icon: 'description', route: '/fleet/compliance' },
      { id: 'expenses', label: 'Expenses', icon: 'payments', route: '/reports' },
      { id: 'reports', label: 'Reports', icon: 'bar_chart', route: '/reports' }
    ]
  },
  {
    id: 'analytics',
    label: 'Analytics',
    items: [
      { id: 'fleet-analytics', label: 'Fleet Analytics', icon: 'analytics', route: '/reports' },
      { id: 'driver-performance', label: 'Driver Performance', icon: 'speed', route: '/reports' }
    ]
  },
  {
    id: 'administration',
    label: 'Administration',
    items: [
      { id: 'tracker-config', label: 'Tracker Configuration', icon: 'settings_input_antenna', route: '/gps-tracking/devices' },
      { id: 'geofencing', label: 'Geofencing', icon: 'fence', route: '/gps-tracking/geofences' },
      { id: 'settings', label: 'Settings', icon: 'settings', route: '/settings' }
    ]
  }
];

export const FLEET_NAV_FOOTER: FleetNavLink[] = [
  { id: 'support', label: 'Support', icon: 'help', route: '/settings' },
  { id: 'settings', label: 'Settings', icon: 'settings', route: '/settings' }
];
