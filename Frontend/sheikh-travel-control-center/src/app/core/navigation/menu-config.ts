import { NavGroup, NavItem, ResolvedMenu } from './nav-models';
import { TenantType } from './tenant-type';

const dashboardGroup: NavGroup = {
  id: 'dashboard',
  label: 'Dashboard',
  icon: 'dashboard',
  collapsible: false,
  items: [
    { id: 'dashboard', label: 'Dashboard', icon: 'dashboard', route: '/dashboard', moduleKey: 'dashboard' }
  ]
};

const operationsGroup: NavGroup = {
  id: 'operations',
  label: 'Operations',
  icon: 'settings',
  collapsible: true,
  items: [
    { id: 'bookings', label: 'Bookings', icon: 'confirmation_number', route: '/bookings', moduleKey: 'bookings' },
    { id: 'trips', label: 'Trips', icon: 'route', route: '/bookings', moduleKey: 'bookings' },
    { id: 'routes', label: 'Routes', icon: 'alt_route', route: '/routes', moduleKey: 'routes' },
    { id: 'dispatch-board', label: 'Dispatch Board', icon: 'view_kanban', route: '/bookings', moduleKey: 'bookings' }
  ]
};

const fleetGroup: NavGroup = {
  id: 'fleet',
  label: 'Fleet',
  icon: 'local_shipping',
  collapsible: true,
  items: [
    { id: 'vehicles', label: 'Vehicles', icon: 'directions_bus', route: '/vehicles', moduleKey: 'vehicles' },
    { id: 'drivers', label: 'Drivers', icon: 'badge', route: '/drivers', moduleKey: 'drivers' },
    { id: 'gps-tracking', label: 'GPS Tracking', icon: 'my_location', route: '/gps-tracking', moduleKey: 'gps-tracking' },
    { id: 'fuel-logs', label: 'Fuel Logs', icon: 'local_gas_station', route: '/fuel-logs', moduleKey: 'fuel-logs' },
    { id: 'maintenance', label: 'Maintenance', icon: 'build', route: '/maintenance', moduleKey: 'maintenance' }
  ]
};

const customersGroup: NavGroup = {
  id: 'customers',
  label: 'Customers',
  icon: 'groups',
  collapsible: true,
  items: [
    { id: 'customers', label: 'Customers', icon: 'group', route: '/customers', moduleKey: 'customers' },
    { id: 'corporate-accounts', label: 'Corporate Accounts', icon: 'business', route: '/customers', moduleKey: 'customers' },
    { id: 'passengers', label: 'Passengers', icon: 'person_outline', route: '/customers', moduleKey: 'customers' },
    { id: 'vendors', label: 'Vendors', icon: 'storefront', route: '/customers', moduleKey: 'customers' }
  ]
};

const financeGroup: NavGroup = {
  id: 'finance',
  label: 'Finance',
  icon: 'account_balance_wallet',
  collapsible: true,
  items: [
    { id: 'payments', label: 'Payments', icon: 'payments', route: '/payments', moduleKey: 'payments' },
    { id: 'invoices', label: 'Invoices', icon: 'receipt_long', route: '/payments', moduleKey: 'payments' },
    { id: 'wallets', label: 'Wallets', icon: 'account_balance', route: '/payments', moduleKey: 'payments' },
    { id: 'expenses', label: 'Expenses', icon: 'money_off', route: '/fuel-logs', moduleKey: 'fuel-logs' }
  ]
};

const analyticsGroup: NavGroup = {
  id: 'analytics',
  label: 'Analytics',
  icon: 'bar_chart',
  collapsible: true,
  items: [
    { id: 'reports', label: 'Reports', icon: 'insights', route: '/reports', moduleKey: 'reports' },
    { id: 'audit-logs', label: 'Audit Logs', icon: 'history', route: '/audit-logs', adminOnly: true, moduleKey: 'audit-logs' },
    { id: 'performance-analytics', label: 'Performance Analytics', icon: 'speed', route: '/reports', moduleKey: 'reports' }
  ]
};

const organizationGroup: NavGroup = {
  id: 'organization',
  label: 'Organization',
  icon: 'corporate_fare',
  collapsible: true,
  items: [
    { id: 'tenants', label: 'Tenants', icon: 'business', route: '/platform/tenants', adminOnly: true, moduleKey: 'organization' },
    { id: 'branches', label: 'Branches', icon: 'account_tree', route: '/platform/branches', adminOnly: true, moduleKey: 'organization' },
    { id: 'departments', label: 'Departments', icon: 'domain', route: '/platform/departments', adminOnly: true, moduleKey: 'organization' }
  ]
};

const accessControlGroup: NavGroup = {
  id: 'access_control',
  label: 'Access Control',
  icon: 'admin_panel_settings',
  collapsible: true,
  items: [
    { id: 'users', label: 'Users', icon: 'manage_accounts', route: '/users', adminOnly: true, moduleKey: 'access_control' },
    { id: 'roles', label: 'Roles', icon: 'security', route: '/platform/roles', adminOnly: true, moduleKey: 'access_control' },
    { id: 'allowance-rules', label: 'Allowance Rules', icon: 'rule', route: '/driver-allowance-rules', adminOnly: true, moduleKey: 'driver-allowance-rules' }
  ]
};

const administrationGroup: NavGroup = {
  id: 'administration',
  label: 'Administration',
  icon: 'admin_panel_settings',
  collapsible: true,
  items: [
    { id: 'users', label: 'Users', icon: 'manage_accounts', route: '/users', adminOnly: true, moduleKey: 'users' },
    { id: 'roles-permissions', label: 'Roles & Permissions', icon: 'security', route: '/users', adminOnly: true, moduleKey: 'users' },
    { id: 'tenant-settings', label: 'Tenant Settings', icon: 'tune', route: '/profile', queryParams: { tab: 'settings' }, adminOnly: true },
    { id: 'allowance-rules', label: 'Allowance Rules', icon: 'rule', route: '/driver-allowance-rules', adminOnly: true, moduleKey: 'driver-allowance-rules' },
    { id: 'system-configuration', label: 'System Configuration', icon: 'settings_suggest', route: '/users', adminOnly: true, moduleKey: 'users' }
  ]
};

const allGroups: NavGroup[] = [
  dashboardGroup,
  operationsGroup,
  fleetGroup,
  customersGroup,
  financeGroup,
  analyticsGroup,
  organizationGroup,
  accessControlGroup,
  administrationGroup
];

const driverItems: NavItem[] = [
  { id: 'my-trips', label: 'My Trips', icon: 'route', route: '/my-trips' },
  { id: 'log-fuel', label: 'Log Fuel', icon: 'local_gas_station', route: '/my-trips/fuel' },
  { id: 'navigation', label: 'Navigation', icon: 'navigation', route: '/my-trips' },
  { id: 'documents', label: 'Documents', icon: 'description', route: '/profile' },
  { id: 'wallet', label: 'Wallet', icon: 'account_balance_wallet', route: '/profile' },
  { id: 'profile', label: 'Profile', icon: 'person_outline', route: '/profile' }
];

const tenantGroupIds: Record<TenantType, string[]> = {
  [TenantType.TravelAgency]: ['dashboard', 'operations', 'customers', 'finance', 'analytics', 'organization', 'access_control'],
  [TenantType.FleetOperator]: ['dashboard', 'fleet', 'analytics', 'organization', 'access_control'],
  [TenantType.CorporateCustomer]: ['dashboard', 'operations', 'customers', 'finance', 'analytics', 'organization', 'access_control'],
  [TenantType.Driver]: []
};

/** Per-tenant item allow-lists; omitted tenants see all items in their groups. */
const tenantItemIds: Partial<Record<TenantType, Set<string>>> = {
  [TenantType.CorporateCustomer]: new Set([
    'dashboard',
    'bookings',
    'corporate-accounts',
    'passengers',
    'invoices',
    'reports',
    'tenant-settings'
  ]),
  [TenantType.FleetOperator]: new Set([
    'dashboard',
    'vehicles',
    'drivers',
    'gps-tracking',
    'maintenance',
    'reports',
    'tenant-settings'
  ])
};

function moduleKeyMatches(item: NavItem, enabledModules: string[]): boolean {
  if (!item.moduleKey) return true;
  return enabledModules.includes(item.moduleKey);
}

function filterItems(
  items: NavItem[],
  isAdmin: boolean,
  itemFilter: Set<string> | undefined,
  enabledModules: string[]
): NavItem[] {
  return items.filter(item => {
    if (item.adminOnly && !isAdmin) return false;
    if (itemFilter && !itemFilter.has(item.id)) return false;
    if (enabledModules.length && !moduleKeyMatches(item, enabledModules)) return false;
    return true;
  });
}

export function resolveMenu(options: {
  tenantType: TenantType;
  roles: string[];
  enabledModules?: string[];
}): ResolvedMenu {
  const { tenantType, roles, enabledModules = [] } = options;

  if (tenantType === TenantType.Driver) {
    return { groups: [], standaloneItems: driverItems, isDriverLayout: true };
  }

  const isAdmin = roles.includes('Admin');
  const groupIds = tenantGroupIds[tenantType] ?? tenantGroupIds[TenantType.TravelAgency];
  const itemFilter = tenantItemIds[tenantType];

  const groups: NavGroup[] = [];
  for (const group of allGroups) {
    if (!groupIds.includes(group.id)) continue;

    const items = filterItems(group.items, isAdmin, itemFilter, enabledModules);
    if (!items.length) continue;

    groups.push({ ...group, items });
  }

  return { groups, standaloneItems: [], isDriverLayout: false };
}

export function defaultExpandedGroupIds(menu: ResolvedMenu): Set<string> {
  return new Set(menu.groups.filter(g => g.collapsible).map(g => g.id));
}

export function groupContainingRoute(menu: ResolvedMenu, url: string): string | null {
  const path = url.split('?')[0];
  for (const group of menu.groups) {
    for (const item of group.items) {
      if (path === item.route || path.startsWith(item.route + '/')) {
        return group.id;
      }
    }
  }
  for (const item of menu.standaloneItems) {
    if (path === item.route || path.startsWith(item.route + '/')) {
      return null;
    }
  }
  return null;
}
