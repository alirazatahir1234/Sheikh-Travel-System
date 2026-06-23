export interface MaintenanceNavItem {
  id: string;
  label: string;
  route: string;
  exact?: boolean;
}

export const MAINTENANCE_SUB_NAV: MaintenanceNavItem[] = [
  { id: 'dashboard', label: 'Dashboard', route: '/fleet/maintenance', exact: true },
  { id: 'requests', label: 'Service Requests', route: '/fleet/maintenance/requests' },
  { id: 'work-orders', label: 'Work Orders', route: '/fleet/maintenance/work-orders' },
  { id: 'schedules', label: 'Service Scheduler', route: '/fleet/maintenance/schedules' },
  { id: 'history', label: 'Service History', route: '/fleet/maintenance/history' },
  { id: 'parts', label: 'Spare Parts', route: '/fleet/maintenance/parts' },
  { id: 'workshops', label: 'Workshops & Vendors', route: '/fleet/maintenance/workshops' },
  { id: 'reports', label: 'Reports', route: '/fleet/maintenance/reports' }
];
