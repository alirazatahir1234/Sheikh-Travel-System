import {
  UserRole,
  AssignedRole,
  parseUserRole
} from '../../core/models/user.model';
import { RoleSummary } from '../../core/models/platform.model';

/** UI primary job role — maps to legacy `Users.Role` + platform RBAC codes. */
export interface PrimaryRoleDefinition {
  id: string;
  label: string;
  platformCode: string;
  legacyRole: UserRole;
  description: string;
  modules: string[];
  approxPermissions: number;
  financialAccess: 'full' | 'restricted' | 'none';
  defaultWorkspaceKey: string;
  defaultHomeRoute: string;
  defaultDashboardKey: string;
  workspaceLabel: string;
  dashboardLabel: string;
  /** Hide unless platform super admin can assign platform-only roles. */
  platformOnly?: boolean;
}

export const PRIMARY_ROLE_CATALOG: PrimaryRoleDefinition[] = [
  {
    id: 'TENANT_ADMIN',
    label: 'Administrator',
    platformCode: 'TENANT_ADMIN',
    legacyRole: UserRole.Admin,
    description: 'Full company administration — users, access control, configuration, and reporting.',
    modules: ['Administration', 'Access Control', 'Organization', 'Analytics', 'Dashboard'],
    approxPermissions: 120,
    financialAccess: 'full',
    defaultWorkspaceKey: 'company',
    defaultHomeRoute: '/dashboard',
    defaultDashboardKey: 'erp.default',
    workspaceLabel: 'Company',
    dashboardLabel: 'ERP Default'
  },
  {
    id: 'TENANT_ADMIN_ALT',
    label: 'Tenant Administrator',
    platformCode: 'TENANT_ADMIN',
    legacyRole: UserRole.Admin,
    description: 'Company-wide tenant administration with the same scope as Administrator.',
    modules: ['Administration', 'Access Control', 'Organization', 'Analytics'],
    approxPermissions: 120,
    financialAccess: 'full',
    defaultWorkspaceKey: 'company',
    defaultHomeRoute: '/dashboard',
    defaultDashboardKey: 'erp.default',
    workspaceLabel: 'Company',
    dashboardLabel: 'ERP Default'
  },
  {
    id: 'FLEET_MANAGER',
    label: 'Fleet Manager',
    platformCode: 'FLEET_MANAGER',
    legacyRole: UserRole.Admin,
    description: 'Fleet operations — vehicles, drivers, GPS, maintenance, and live tracking.',
    modules: ['Fleet', 'GPS', 'Live Tracking', 'Maintenance', 'Drivers', 'Reports'],
    approxPermissions: 85,
    financialAccess: 'restricted',
    defaultWorkspaceKey: 'fleet',
    defaultHomeRoute: '/dashboard',
    defaultDashboardKey: 'erp.fleet',
    workspaceLabel: 'Fleet',
    dashboardLabel: 'Fleet Center'
  },
  {
    id: 'DRIVER_MANAGER',
    label: 'Driver Manager',
    platformCode: 'DRIVER_MANAGER',
    legacyRole: UserRole.Dispatcher,
    description: 'Driver directory, assignments, compliance, and performance.',
    modules: ['Drivers', 'Assignments', 'Compliance', 'Fleet', 'Reports'],
    approxPermissions: 72,
    financialAccess: 'restricted',
    defaultWorkspaceKey: 'drivers',
    defaultHomeRoute: '/drivers',
    defaultDashboardKey: 'erp.fleet',
    workspaceLabel: 'Drivers',
    dashboardLabel: 'Fleet Center'
  },
  {
    id: 'DISPATCHER',
    label: 'Dispatcher',
    platformCode: 'DISPATCHER',
    legacyRole: UserRole.Dispatcher,
    description: 'Bookings, trips, dispatch board, driver assignment, and live GPS.',
    modules: ['Trips', 'Dispatch', 'Driver Assignment', 'GPS', 'Live Tracking', 'Customers'],
    approxPermissions: 82,
    financialAccess: 'none',
    defaultWorkspaceKey: 'trips',
    defaultHomeRoute: '/bookings',
    defaultDashboardKey: 'erp.trips',
    workspaceLabel: 'Operations',
    dashboardLabel: 'Dispatch Center'
  },
  {
    id: 'ACCOUNTANT',
    label: 'Accountant',
    platformCode: 'ACCOUNTANT',
    legacyRole: UserRole.Accountant,
    description: 'Payments, invoices, revenue reports, and finance analytics.',
    modules: ['Finance', 'Payments', 'Reports', 'Analytics'],
    approxPermissions: 64,
    financialAccess: 'full',
    defaultWorkspaceKey: 'finance',
    defaultHomeRoute: '/payments',
    defaultDashboardKey: 'erp.default',
    workspaceLabel: 'Finance',
    dashboardLabel: 'Finance Overview'
  },
  {
    id: 'DRIVER',
    label: 'Driver',
    platformCode: 'DRIVER',
    legacyRole: UserRole.Driver,
    description: 'Field driver — assigned trips, status updates, fuel, and mobile GPS.',
    modules: ['My Trips', 'GPS', 'Fuel'],
    approxPermissions: 28,
    financialAccess: 'none',
    defaultWorkspaceKey: 'driver',
    defaultHomeRoute: '/my-trips',
    defaultDashboardKey: 'mobile.driver',
    workspaceLabel: 'Driver',
    dashboardLabel: 'Driver Home'
  },
  {
    id: 'CUSTOMER_SERVICE',
    label: 'Customer Service',
    platformCode: 'DISPATCHER',
    legacyRole: UserRole.Dispatcher,
    description: 'Customer bookings and trip support — dispatch-lite with customer focus.',
    modules: ['Customers', 'Bookings', 'Trips', 'Notifications'],
    approxPermissions: 55,
    financialAccess: 'none',
    defaultWorkspaceKey: 'trips',
    defaultHomeRoute: '/bookings',
    defaultDashboardKey: 'erp.trips',
    workspaceLabel: 'Operations',
    dashboardLabel: 'Dispatch Center'
  },
  {
    id: 'MAINTENANCE_MANAGER',
    label: 'Maintenance Manager',
    platformCode: 'FLEET_MANAGER',
    legacyRole: UserRole.Admin,
    description: 'Workshop, service history, parts, and fleet maintenance workflows.',
    modules: ['Maintenance', 'Fleet', 'Parts', 'Service History', 'Reports'],
    approxPermissions: 68,
    financialAccess: 'restricted',
    defaultWorkspaceKey: 'fleet',
    defaultHomeRoute: '/fleet-management/maintenance',
    defaultDashboardKey: 'erp.fleet',
    workspaceLabel: 'Fleet',
    dashboardLabel: 'Maintenance Hub'
  },
  {
    id: 'GPS_OPERATOR',
    label: 'GPS Operator',
    platformCode: 'GPS_OPERATOR',
    legacyRole: UserRole.Dispatcher,
    description: 'Live map, GPS alerts, and fleet tracking operations.',
    modules: ['GPS', 'Live Tracking', 'Alerts', 'Fleet Map'],
    approxPermissions: 45,
    financialAccess: 'none',
    defaultWorkspaceKey: 'fleet',
    defaultHomeRoute: '/gps-tracking/live',
    defaultDashboardKey: 'erp.fleet',
    workspaceLabel: 'Fleet',
    dashboardLabel: 'Live Map'
  }
];

const LEGACY_TO_PRIMARY: Record<UserRole, string> = {
  [UserRole.Admin]: 'TENANT_ADMIN',
  [UserRole.Dispatcher]: 'DISPATCHER',
  [UserRole.Driver]: 'DRIVER',
  [UserRole.Accountant]: 'ACCOUNTANT'
};

const PLATFORM_PRIORITY = [
  'FLEET_MANAGER',
  'DRIVER_MANAGER',
  'GPS_OPERATOR',
  'DISPATCHER',
  'ACCOUNTANT',
  'DRIVER',
  'TENANT_ADMIN',
  'SUPER_ADMIN'
];

export function getPrimaryRoleById(id: string | null | undefined): PrimaryRoleDefinition | undefined {
  if (!id) return undefined;
  return PRIMARY_ROLE_CATALOG.find(r => r.id === id);
}

export function inferPrimaryRoleId(
  legacyRole: UserRole,
  assigned: AssignedRole[] | null | undefined
): string {
  const codes = new Set((assigned ?? []).map(a => (a.code || '').toUpperCase()));
  for (const code of PLATFORM_PRIORITY) {
    if (!codes.has(code)) continue;
    const match = PRIMARY_ROLE_CATALOG.find(r => r.platformCode === code);
    if (match) return match.id;
  }
  return LEGACY_TO_PRIMARY[parseUserRole(legacyRole)] ?? 'DISPATCHER';
}

export function resolvePlatformRoleId(
  platformCode: string,
  assignable: RoleSummary[]
): number | null {
  const role = assignable.find(
    r => r.isActive && r.code.toUpperCase() === platformCode.toUpperCase()
  );
  return role?.id ?? null;
}

export function permissionCountForPrimary(
  def: PrimaryRoleDefinition,
  assignable: RoleSummary[]
): number {
  const role = assignable.find(r => r.code.toUpperCase() === def.platformCode.toUpperCase());
  return role?.permissionCount ?? def.approxPermissions;
}

export const SYSTEM_ROLE_CODES = new Set([
  'TENANT_ADMIN',
  'FLEET_MANAGER',
  'DRIVER_MANAGER',
  'DISPATCHER',
  'ACCOUNTANT',
  'DRIVER',
  'SUPER_ADMIN'
]);

export function permissionReadWriteSplit(total: number): { read: number; write: number } {
  const read = Math.max(1, Math.round(total * 0.72));
  return { read, write: Math.max(0, total - read) };
}

export function isSystemRoleCode(code: string): boolean {
  return SYSTEM_ROLE_CODES.has(code.toUpperCase());
}

/** HR / reporting bucket — not the same as Primary Role (access). */
export function defaultEmployeeTypeForPrimary(def: PrimaryRoleDefinition): string {
  switch (def.id) {
    case 'DRIVER':
      return 'Driver';
    case 'TENANT_ADMIN':
    case 'TENANT_ADMIN_ALT':
      return 'Admin';
    case 'FLEET_MANAGER':
    case 'DRIVER_MANAGER':
    case 'MAINTENANCE_MANAGER':
      return 'Manager';
    case 'ACCOUNTANT':
    case 'DISPATCHER':
    case 'CUSTOMER_SERVICE':
    case 'GPS_OPERATOR':
    default:
      return 'Staff';
  }
}

export const EMPLOYEE_TYPE_DESCRIPTIONS: Record<string, string> = {
  Driver: 'Field staff — trips, vehicle, compliance',
  Staff: 'Operations & office (non-supervisor)',
  Admin: 'Company / tenant administration',
  Manager: 'Supervisory — fleet, drivers, or teams'
};
