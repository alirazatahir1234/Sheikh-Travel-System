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
    { id: 'dispatch-board', label: 'Dispatch Board', icon: 'view_kanban', route: '/trips/live', moduleKey: 'trips' },
    { id: 'trip-smart-assignment', label: 'Smart Assignment', icon: 'auto_awesome', route: '/trips/smart-assignment', moduleKey: 'trips' }
  ]
};

/** Travel / agency trip + route catalog — mirrors PlatformModules.travel-fleet-operations. */
const travelFleetOperationsGroup: NavGroup = {
  id: 'travel-fleet-operations',
  label: 'Fleet Operations',
  icon: 'route',
  collapsible: true,
  items: [
    { id: 'bookings', label: 'Bookings', icon: 'confirmation_number', route: '/bookings', moduleKey: 'bookings' },
    { id: 'trips', label: 'Trips', icon: 'route', route: '/trips', moduleKey: 'trips' },
    { id: 'routes', label: 'Routes', icon: 'alt_route', route: '/routes', moduleKey: 'routes' },
    { id: 'ev-fleet', label: 'Electric fleet', icon: 'ev_station', route: '/fleet/ev', moduleKey: 'vehicles' }
  ]
};

const fleetGroup: NavGroup = {
  id: 'fleet',
  label: 'Fleet Management',
  icon: 'local_shipping',
  collapsible: true,
  items: [
    { id: 'fleet-dashboard', label: 'Dashboard', icon: 'dashboard', route: '/fleet/dashboard', moduleKey: 'vehicles' },
    { id: 'vehicles', label: 'Vehicles', icon: 'directions_bus', route: '/vehicles', moduleKey: 'vehicles' },
    { id: 'drivers', label: 'Drivers', icon: 'badge', route: '/drivers', moduleKey: 'drivers' },
    {
      id: 'driver-client-ratings',
      label: 'Client ratings',
      icon: 'star_rate',
      route: '/drivers/client-ratings',
      moduleKey: 'drivers',
    },
    { id: 'assignments', label: 'Assignments', icon: 'event_available', route: '/fleet/assignments', moduleKey: 'vehicles' },
    { id: 'vehicle-rentals', label: 'Vehicle Rentals', icon: 'car_rental', route: '/rental', moduleKey: 'rental' },
    { id: 'rental-rates', label: 'Rental Rates', icon: 'payments', route: '/rental/rates', moduleKey: 'rental' },
    { id: 'rental-availability', label: 'Rental Availability', icon: 'event_available', route: '/rental/availability', moduleKey: 'rental' }
  ]
};

const rentalGroup: NavGroup = {
  id: 'rental',
  label: 'Vehicle Rental',
  icon: 'car_rental',
  collapsible: true,
  items: [
    { id: 'rentals', label: 'Rentals', icon: 'car_rental', route: '/rental', moduleKey: 'rental' },
    { id: 'rental-rates-nav', label: 'Rates', icon: 'payments', route: '/rental/rates', moduleKey: 'rental' },
    { id: 'rental-availability-nav', label: 'Availability', icon: 'event_available', route: '/rental/availability', moduleKey: 'rental' }
  ]
};

/** TravelNexus — travel agency / visa / Umrah ERP (module TRAVEL_AGENCY, legacy key travel-agency).
 *  Mirrors PlatformModules.travel-nexus; later phases add their items here and in their migration. */
const travelNexusGroup: NavGroup = {
  id: 'travel-nexus',
  label: 'TravelNexus',
  icon: 'flight_takeoff',
  collapsible: true,
  items: [
    { id: 'travel-enquiries', label: 'CRM & Enquiries', icon: 'contact_phone', route: '/travel/enquiries', moduleKey: 'travel-agency' },
    { id: 'travel-customers', label: 'Customers', icon: 'groups', route: '/travel/customers', moduleKey: 'travel-agency' },
    { id: 'travel-visa-cases', label: 'Visa Management', icon: 'assignment_ind', route: '/travel/visa-cases', moduleKey: 'travel-agency' },
    { id: 'travel-checklists', label: 'Document Checklists', icon: 'checklist', route: '/travel/checklists', moduleKey: 'travel-agency' },
    { id: 'travel-groups', label: 'Umrah Groups', icon: 'groups', route: '/travel/groups', moduleKey: 'travel-agency' },
    { id: 'travel-packages', label: 'Packages', icon: 'inventory_2', route: '/travel/packages', moduleKey: 'travel-agency' },
    { id: 'travel-settings', label: 'Settings', icon: 'tune', route: '/travel/settings', moduleKey: 'travel-agency' }
  ]
};

const fleetOperationsGroup: NavGroup = {
  id: 'fleet-operations',
  label: 'Operations',
  icon: 'settings',
  collapsible: true,
  items: [
    { id: 'trip-smart-assignment', label: 'Smart Assignment', icon: 'auto_awesome', route: '/trips/smart-assignment', moduleKey: 'trips' },
    { id: 'live-tracking', label: 'Live Tracking', icon: 'my_location', route: '/gps-tracking/live', moduleKey: 'gps-tracking' },
    { id: 'gps-trips', label: 'Trips', icon: 'route', route: '/gps-tracking/trips', moduleKey: 'gps-tracking' },
    { id: 'maintenance', label: 'Maintenance', icon: 'build', route: '/fleet/maintenance', moduleKey: 'maintenance' },
    { id: 'predictive-maintenance', label: 'Predictive Maintenance', icon: 'auto_fix_high', route: '/fleet/maintenance/predictive', moduleKey: 'maintenance' },
    { id: 'service-records', label: 'Service Records', icon: 'history', route: '/maintenance/service-records', moduleKey: 'maintenance' },
    { id: 'fuel-logs', label: 'Fuel', icon: 'local_gas_station', route: '/fuel-logs', moduleKey: 'fuel-logs' },
    { id: 'fuel-fraud-events', label: 'Fuel fraud', icon: 'gpp_bad', route: '/fuel-logs/fraud-events', moduleKey: 'fuel-logs' },
    { id: 'inspections', label: 'Inspections', icon: 'fact_check', route: '/fleet/inspections', moduleKey: 'vehicles' },
    { id: 'compliance', label: 'Compliance', icon: 'verified_user', route: '/fleet/compliance', moduleKey: 'vehicles' },
    { id: 'fleet-documents', label: 'Document tracker', icon: 'description', route: '/fleet/documents', moduleKey: 'vehicles' },
    { id: 'ev-fleet', label: 'Electric fleet', icon: 'ev_station', route: '/fleet/ev', moduleKey: 'vehicles' },
    { id: 'fleet-anomalies', label: 'Driving anomalies', icon: 'crisis_alert', route: '/fleet/anomalies', moduleKey: 'gps-tracking' },
    { id: 'fatigue-events', label: 'Fatigue events', icon: 'bedtime', route: '/gps-tracking/fatigue-events', moduleKey: 'gps-tracking' },
    { id: 'sos-dispatch', label: 'SOS dispatch', icon: 'sos', route: '/gps-tracking/sos', moduleKey: 'gps-tracking' },
    { id: 'gps-tracking', label: 'Fleet Tracking', icon: 'gps_fixed', route: '/gps-tracking/live', moduleKey: 'gps-tracking' }
  ]
};

const fleetResourcesGroup: NavGroup = {
  id: 'fleet-resources',
  label: 'Resources',
  icon: 'folder',
  collapsible: true,
  items: [
    { id: 'fleet-documents', label: 'Documents', icon: 'description', route: '/fleet/compliance', moduleKey: 'vehicles' },
    { id: 'fleet-expenses', label: 'Expenses', icon: 'money_off', route: '/fuel-logs', moduleKey: 'fuel-logs' },
    { id: 'fleet-reports', label: 'Reports', icon: 'insights', route: '/reports', moduleKey: 'reports' }
  ]
};

const fleetAdminGroup: NavGroup = {
  id: 'fleet-admin',
  label: 'Administration',
  icon: 'admin_panel_settings',
  collapsible: true,
  items: [
    { id: 'tracker-config', label: 'Tracker Configuration', icon: 'settings_input_antenna', route: '/gps-tracking/devices', moduleKey: 'gps-tracking' },
    { id: 'geofencing', label: 'Geofencing', icon: 'fence', route: '/gps-tracking/geofences', moduleKey: 'gps-tracking' },
    { id: 'notifications', label: 'Notification Center', icon: 'notifications', route: '/notifications', moduleKey: 'dashboard' },
    { id: 'ai-center', label: 'SheikhGo AI Assistant', icon: 'smart_toy', route: '/ai', moduleKey: 'ai' },
    { id: 'mcp-console', label: 'MCP Console', icon: 'hub', route: '/mcp', moduleKey: 'ai' },
    { id: 'fleet-settings', label: 'Settings', icon: 'tune', route: '/settings', moduleKey: 'platform' }
  ]
};

const whatsappGroup: NavGroup = {
  id: 'whatsapp',
  label: 'WhatsApp',
  icon: 'chat',
  collapsible: true,
  items: [
    { id: 'whatsapp-inbox', label: 'Inbox', icon: 'chat', route: '/whatsapp', moduleKey: 'whatsapp' },
    { id: 'whatsapp-accounts', label: 'Accounts', icon: 'phonelink_setup', route: '/whatsapp/accounts', moduleKey: 'whatsapp' },
    { id: 'whatsapp-templates', label: 'Templates', icon: 'description', route: '/whatsapp/templates', moduleKey: 'whatsapp' },
    { id: 'whatsapp-automations', label: 'Automations', icon: 'auto_awesome', route: '/whatsapp/automations', moduleKey: 'whatsapp' },
    { id: 'whatsapp-webhook-logs', label: 'Webhooks', icon: 'receipt_long', route: '/whatsapp/webhook-logs', moduleKey: 'whatsapp' }
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
    { id: 'invoices', label: 'Invoices', icon: 'receipt_long', route: '/invoices', moduleKey: 'payments' },
    { id: 'settlements', label: 'Settlements', icon: 'account_balance_wallet', route: '/settlements', moduleKey: 'payments' },
    { id: 'driver-wallets', label: 'Driver Wallets', icon: 'account_balance', route: '/driver-wallets', moduleKey: 'payments' },
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
    { id: 'fleet-analytics', label: 'Fleet Analytics', icon: 'analytics', route: '/gps-tracking/analytics', moduleKey: 'gps-tracking' },
    { id: 'driver-performance', label: 'Driver Performance', icon: 'speed', route: '/gps-tracking/driver-performance', moduleKey: 'gps-tracking' },
    { id: 'client-tracking-links', label: 'Client Tracking Links', icon: 'link', route: '/gps-tracking/client-tracking-links', moduleKey: 'gps-tracking' },
    { id: 'predictive-maintenance', label: 'Predictive Maintenance', icon: 'auto_fix_high', route: '/fleet/maintenance/predictive', moduleKey: 'maintenance' },
    { id: 'audit-logs', label: 'Audit Center', icon: 'history', route: '/platform/audit-center', adminOnly: true, moduleKey: 'audit-logs' },
    { id: 'notifications', label: 'Notification Center', icon: 'notifications', route: '/notifications', moduleKey: 'dashboard' },
    { id: 'ai-center', label: 'SheikhGo AI Assistant', icon: 'smart_toy', route: '/ai', moduleKey: 'ai' },
    { id: 'mcp-console', label: 'MCP Console', icon: 'hub', route: '/mcp', moduleKey: 'ai' }
  ]
};

const organizationGroup: NavGroup = {
  id: 'organization',
  label: 'Organization',
  icon: 'corporate_fare',
  collapsible: true,
  items: [
    { id: 'tenants', label: 'Companies', icon: 'business', route: '/platform/tenants', adminOnly: true, moduleKey: 'organization' },
    { id: 'organization-designer', label: 'Hierarchy', icon: 'account_tree', route: '/platform/organization-designer', adminOnly: true, moduleKey: 'organization' },
    { id: 'branches', label: 'Branches', icon: 'location_city', route: '/platform/branches', adminOnly: true, moduleKey: 'organization' },
    { id: 'departments', label: 'Departments', icon: 'domain', route: '/platform/departments', adminOnly: true, moduleKey: 'organization' }
  ]
};

const identityGroup: NavGroup = {
  id: 'identity',
  label: 'Identity',
  icon: 'admin_panel_settings',
  collapsible: true,
  items: [
    { id: 'users', label: 'Users', icon: 'manage_accounts', route: '/users', adminOnly: true, moduleKey: 'access_control' },
    { id: 'roles', label: 'Roles', icon: 'security', route: '/platform/access-control', queryParams: { tab: 'roles' }, adminOnly: true, moduleKey: 'access_control' },
    { id: 'permissions', label: 'Access Control', icon: 'verified_user', route: '/platform/access-control', adminOnly: true, moduleKey: 'access_control' },
    { id: 'access-policies', label: 'Access Policies', icon: 'policy', route: '/platform/security-center', adminOnly: true, moduleKey: 'access_control' }
  ]
};

const platformGroup: NavGroup = {
  id: 'platform',
  label: 'Platform',
  icon: 'settings_applications',
  collapsible: true,
  items: [
    { id: 'platform-home', label: 'Platform Home', icon: 'settings_applications', route: '/platform', adminOnly: true, moduleKey: 'platform' },
    { id: 'modules', label: 'Modules', icon: 'extension', route: '/platform/module-management', adminOnly: true, moduleKey: 'platform' },
    { id: 'menus', label: 'Menus', icon: 'menu', route: '/platform/menu-management', adminOnly: true, moduleKey: 'platform' },
    { id: 'workspaces', label: 'Workspaces', icon: 'workspaces', route: '/platform/workspace-management', adminOnly: true, moduleKey: 'platform' },
    { id: 'dashboards', label: 'Dashboards', icon: 'dashboard_customize', route: '/platform/dashboard-management', adminOnly: true, moduleKey: 'platform' },
    { id: 'security-center', label: 'Security Center', icon: 'security', route: '/platform/security-center', adminOnly: true, moduleKey: 'platform' },
    { id: 'audit-logs-platform', label: 'Audit Center', icon: 'history', route: '/platform/audit-center', adminOnly: true, moduleKey: 'audit-logs' },
    { id: 'gps-control-center', label: 'GPS Control Center', icon: 'gps_fixed', route: '/platform/gps-control-center', adminOnly: true, moduleKey: 'platform' },
    { id: 'plans', label: 'Plans', icon: 'subscriptions', route: '/platform/subscription-management', adminOnly: true, moduleKey: 'platform' },
    { id: 'billing', label: 'Billing', icon: 'payments', route: '/platform/subscription-management', queryParams: { tab: 'billing' }, adminOnly: true, moduleKey: 'platform' },
    { id: 'subscription-revenue', label: 'Subscription revenue', icon: 'monitoring', route: '/platform/subscription-revenue', adminOnly: true, moduleKey: 'platform' },
    { id: 'migration-manager', label: 'Migration Manager', icon: 'storage', route: '/platform/migrations', adminOnly: true, moduleKey: 'platform' },
    { id: 'database-reset', label: 'Database Reset', icon: 'build_circle', route: '/platform/maintenance', adminOnly: true, moduleKey: 'platform' },
    { id: 'settings', label: 'Settings', icon: 'tune', route: '/settings', adminOnly: true, moduleKey: 'platform' }
  ]
};

const accessControlGroup: NavGroup = {
  id: 'access_control',
  label: 'Access Control',
  icon: 'admin_panel_settings',
  collapsible: true,
  items: [
    { id: 'users', label: 'Users', icon: 'manage_accounts', route: '/users', adminOnly: true, moduleKey: 'access_control' },
    { id: 'roles', label: 'Roles', icon: 'security', route: '/platform/access-control', queryParams: { tab: 'roles' }, adminOnly: true, moduleKey: 'access_control' },
    { id: 'permissions', label: 'Permissions', icon: 'verified_user', route: '/platform/access-control', queryParams: { tab: 'permissions' }, adminOnly: true, moduleKey: 'access_control' },
    { id: 'allowance-rules', label: 'Allowance Rules', icon: 'rule', route: '/driver-allowance-rules', adminOnly: true, moduleKey: 'driver-allowance-rules' }
  ]
};

const administrationGroup: NavGroup = {
  id: 'administration',
  label: 'Administration',
  icon: 'admin_panel_settings',
  collapsible: true,
  items: [
    // Platform owns Settings + Database Reset. Keep operational / AI tools here.
    { id: 'notifications', label: 'Notification Center', icon: 'notifications', route: '/notifications', moduleKey: 'dashboard' },
    { id: 'ai-center', label: 'SheikhGo AI Assistant', icon: 'smart_toy', route: '/ai', moduleKey: 'ai' },
    { id: 'mcp-console', label: 'MCP Console', icon: 'hub', route: '/mcp', moduleKey: 'ai' }
  ]
};

const websiteGroup: NavGroup = {
  id: 'website',
  label: 'Website',
  icon: 'language',
  collapsible: true,
  items: [
    { id: 'website-dashboard', label: 'Dashboard', icon: 'dashboard', route: '/website', moduleKey: 'website' },
    { id: 'website-home', label: 'Home Page', icon: 'home', route: '/website/home', moduleKey: 'website' },
    { id: 'website-features', label: 'Features', icon: 'auto_awesome', route: '/website/features', moduleKey: 'website' },
    { id: 'website-pages', label: 'Pages', icon: 'article', route: '/website/pages', moduleKey: 'website' },
    { id: 'website-contact-requests', label: 'Contact Requests', icon: 'mail', route: '/website/contact-requests', moduleKey: 'website' },
    { id: 'website-demo-requests', label: 'Demo Requests', icon: 'event', route: '/website/demo-requests', moduleKey: 'website' },
    { id: 'website-media', label: 'Media', icon: 'photo_library', route: '/website/media', moduleKey: 'website' },
    { id: 'website-legal', label: 'Legal', icon: 'gavel', route: '/website/legal', moduleKey: 'website' },
    { id: 'website-settings', label: 'Settings', icon: 'tune', route: '/website/settings', moduleKey: 'website' }
  ]
};

const allGroups: NavGroup[] = [
  dashboardGroup,
  operationsGroup,
  travelFleetOperationsGroup,
  fleetGroup,
  rentalGroup,
  travelNexusGroup,
  fleetOperationsGroup,
  fleetResourcesGroup,
  fleetAdminGroup,
  customersGroup,
  financeGroup,
  analyticsGroup,
  websiteGroup,
  whatsappGroup,
  organizationGroup,
  identityGroup,
  platformGroup,
  accessControlGroup,
  administrationGroup
];

const driverItems: NavItem[] = [
  { id: 'my-trips', label: 'My Trips', icon: 'route', route: '/my-trips' },
  { id: 'log-fuel', label: 'Log Fuel', icon: 'local_gas_station', route: '/my-trips/fuel' },
  { id: 'navigation', label: 'Navigation', icon: 'navigation', route: '/my-trips' },
  { id: 'documents', label: 'Documents', icon: 'description', route: '/profile' },
  { id: 'wallet', label: 'Wallet', icon: 'account_balance_wallet', route: '/wallet' },
  { id: 'profile', label: 'Profile', icon: 'person_outline', route: '/profile' }
];

const tenantGroupIds: Record<TenantType, string[]> = {
  [TenantType.TravelAgency]: ['dashboard', 'operations', 'travel-nexus', 'travel-fleet-operations', 'rental', 'customers', 'finance', 'analytics', 'website', 'whatsapp', 'organization', 'identity', 'platform', 'access_control', 'administration'],
  [TenantType.FleetOperator]: ['dashboard', 'fleet', 'rental', 'fleet-operations', 'fleet-resources', 'fleet-admin', 'analytics', 'whatsapp', 'administration'],
  [TenantType.CorporateCustomer]: ['dashboard', 'travel-fleet-operations', 'customers', 'finance', 'analytics'],
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
    'reports'
  ]),
  [TenantType.FleetOperator]: new Set([
    'dashboard',
    'fleet-dashboard',
    'vehicles',
    'drivers',
    'driver-client-ratings',
    'assignments',
    'vehicle-rentals',
    'rental-rates',
    'rental-availability',
    'rentals',
    'rental-rates-nav',
    'rental-availability-nav',
    'live-tracking',
    'gps-trips',
    'gps-tracking',
    'maintenance',
    'fuel-logs',
    'inspections',
    'compliance',
    'fleet-documents',
    'fleet-expenses',
    'fleet-reports',
    'geofencing',
    'notifications',
    'whatsapp-inbox',
    'whatsapp-accounts',
    'whatsapp-templates',
    'whatsapp-automations',
    'whatsapp-webhook-logs',
    'ai-center',
    'mcp-console',
    'fleet-analytics',
    'driver-performance',
    'trip-smart-assignment',
    'ev-fleet',
    'reports'
  ])
};

/**
 * Strict role-domain route allowlist for ERP navigation and route guards.
 * Ensures every role only sees and accesses routes within its operational domain.
 */
export function isRouteAllowedForPrimaryRole(urlOrRoute: string, primaryRoleCode: string): boolean {
  const path = (urlOrRoute || '').split('?')[0].trim();
  if (!path || path === '/') return true;

  const role = (primaryRoleCode || '').trim().toUpperCase().replace(/[\s-]/g, '_');
  if (!role || role === 'SUPER_ADMIN' || role === 'SUPERADMIN') {
    return true;
  }

  // SuperAdmin-only routes (never accessible to tenant roles, including TENANT_ADMIN / OWNER)
  if (
    path.startsWith('/gps-tracking/commands') ||
    path.startsWith('/platform/tenants') ||
    path.startsWith('/platform/companies') ||
    path.startsWith('/platform/module-management') ||
    path.startsWith('/platform/feature-management') ||
    path.startsWith('/platform/subscription') ||
    path.startsWith('/platform/gps-control-center') ||
    path.startsWith('/platform/migrations') ||
    path.startsWith('/platform/maintenance')
  ) {
    return false;
  }

  switch (role) {
    case 'GPS_OPERATOR':
      return (
        path === '/dashboard' ||
        path === '/tracking' ||
        path.startsWith('/gps-tracking') ||
        path.startsWith('/notifications') ||
        path.startsWith('/profile')
      );

    case 'FLEET_MANAGER':
      // Fleet Manager sees vehicles, maintenance, fuel, fleet trips/assignments, live map, reports —
      // NEVER tracker hardware provisioning (/gps-tracking/devices, /gps-tracking/trip-mode),
      // platform admin, bookings, or finance settlements.
      if (
        path.startsWith('/gps-tracking/devices') ||
        path.startsWith('/gps-tracking/trip-mode') ||
        path.startsWith('/platform') ||
        path.startsWith('/users') ||
        path.startsWith('/settings') ||
        path.startsWith('/website') ||
        path.startsWith('/bookings') ||
        path.startsWith('/customers') ||
        path.startsWith('/payments') ||
        path.startsWith('/settlements') ||
        path.startsWith('/driver-wallets')
      ) {
        return false;
      }
      return true;

    case 'DRIVER_MANAGER':
      return (
        path === '/dashboard' ||
        path.startsWith('/drivers') ||
        path.startsWith('/driver-allowance-rules') ||
        path.startsWith('/fleet/compliance') ||
        path.startsWith('/fleet/documents') ||
        path.startsWith('/fleet/inspections') ||
        path.startsWith('/fleet/assignments') ||
        path === '/gps-tracking' ||
        path.startsWith('/gps-tracking/live') ||
        path.startsWith('/gps-tracking/driver-performance') ||
        path.startsWith('/gps-tracking/alerts') ||
        path.startsWith('/gps-tracking/fatigue-events') ||
        path.startsWith('/reports') ||
        path.startsWith('/notifications') ||
        path.startsWith('/profile')
      );

    case 'DISPATCHER':
      return (
        path === '/dashboard' ||
        path.startsWith('/bookings') ||
        path.startsWith('/trips') ||
        path.startsWith('/routes') ||
        path.startsWith('/customers') ||
        path === '/gps-tracking' ||
        path.startsWith('/gps-tracking/live') ||
        path.startsWith('/gps-tracking/trips') ||
        path.startsWith('/gps-tracking/alerts') ||
        path.startsWith('/gps-tracking/sos') ||
        path.startsWith('/gps-tracking/client-tracking-links') ||
        path.startsWith('/notifications') ||
        path.startsWith('/ai') ||
        path.startsWith('/mcp') ||
        path.startsWith('/profile')
      );

    case 'ACCOUNTANT':
      return (
        path === '/dashboard' ||
        path.startsWith('/payments') ||
        path.startsWith('/invoices') ||
        path.startsWith('/settlements') ||
        path.startsWith('/driver-wallets') ||
        path.startsWith('/fuel-logs') ||
        path.startsWith('/reports') ||
        path.startsWith('/maintenance') ||
        path.startsWith('/fleet/maintenance') ||
        path.startsWith('/notifications') ||
        path.startsWith('/profile')
      );

    case 'OWNER':
      if (
        path.startsWith('/gps-tracking/devices') ||
        path.startsWith('/gps-tracking/trip-mode') ||
        path.startsWith('/platform') ||
        path.startsWith('/users') ||
        path.startsWith('/settings') ||
        path.startsWith('/website')
      ) {
        return false;
      }
      return true;

    case 'DRIVER':
    case 'FIELD_DRIVER':
      return (
        path.startsWith('/my-trips') ||
        path.startsWith('/wallet') ||
        path.startsWith('/profile')
      );

    default:
      return true;
  }
}

function moduleKeyMatches(item: NavItem, enabledModules: string[]): boolean {
  if (!item.moduleKey) return true;
  return enabledModules.includes(item.moduleKey);
}

function filterItems(
  items: NavItem[],
  isAdmin: boolean,
  primaryRole: string,
  itemFilter: Set<string> | undefined,
  enabledModules: string[]
): NavItem[] {
  return items.filter(item => {
    if (item.adminOnly && !isAdmin) return false;
    if (itemFilter && !itemFilter.has(item.id)) return false;
    if (enabledModules.length && !moduleKeyMatches(item, enabledModules)) return false;
    if (primaryRole && !isRouteAllowedForPrimaryRole(item.route, primaryRole)) return false;
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

  const isAdmin = roles.some(r => {
    const code = r.toUpperCase();
    return code === 'ADMIN'
      || code === 'SUPER_ADMIN'
      || code === 'TENANT_ADMIN'
      || code === 'SUPERADMIN';
  });
  const primaryRole = (roles[0] || '').toUpperCase();
  const groupIds = tenantGroupIds[tenantType] ?? tenantGroupIds[TenantType.TravelAgency];
  const itemFilter = tenantItemIds[tenantType];

  const groups: NavGroup[] = [];
  for (const group of allGroups) {
    if (!groupIds.includes(group.id)) continue;

    const items = filterItems(group.items, isAdmin, primaryRole, itemFilter, enabledModules);
    if (!items.length) continue;

    groups.push({ ...group, items });
  }

  return { groups, standaloneItems: [], isDriverLayout: false };
}

export function defaultExpandedGroupIds(_menu: ResolvedMenu): Set<string> {
  // Sub-menus start collapsed; the shell expands only the active route's group.
  return new Set();
}

export function groupContainingRoute(menu: ResolvedMenu, url: string): string | null {
  const path = url.split('?')[0];
  // Prefer the longest matching leaf route so parent hubs like Fleet Tracking
  // (`/gps-tracking`) do not steal Analytics pages (`/gps-tracking/driver-performance`).
  let best: { groupId: string; routeLen: number } | null = null;
  for (const group of menu.groups) {
    for (const item of group.items) {
      if (!item.route) continue;
      const matches = path === item.route || path.startsWith(item.route + '/');
      if (!matches) continue;
      if (!best || item.route.length > best.routeLen) {
        best = { groupId: group.id, routeLen: item.route.length };
      }
    }
  }
  if (best) return best.groupId;

  for (const item of menu.standaloneItems) {
    if (path === item.route || path.startsWith(item.route + '/')) {
      return null;
    }
  }
  return null;
}
