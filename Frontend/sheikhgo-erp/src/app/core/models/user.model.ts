/**
 * User management models — mirrors Backend.Application.Features.Users.DTOs.
 * Roles must stay in sync with Domain.Enums.UserRole.
 */

export enum UserRole {
  Admin      = 1,
  Dispatcher = 2,
  Driver     = 3,
  Accountant = 4
}

export const UserRoleLabels: Record<UserRole, string> = {
  [UserRole.Admin]:      'Admin',
  [UserRole.Dispatcher]: 'Dispatcher',
  [UserRole.Driver]:     'Driver',
  [UserRole.Accountant]: 'Accountant'
};

export const UserRoleDescriptions: Record<UserRole, string> = {
  [UserRole.Admin]:      'Full system access — manage users, configure rules, view all data',
  [UserRole.Dispatcher]: 'Operations staff — create bookings, assign drivers/vehicles, track trips',
  [UserRole.Driver]:     'Field driver — view assigned trips, update status, log fuel',
  [UserRole.Accountant]: 'Finance team — view payments, reports, revenue data'
};

export type UserLifecycleStatus = 'Pending' | 'Active' | 'Inactive' | 'Suspended' | 'Locked';
export type EmployeeType = 'Driver' | 'Staff' | 'Admin' | 'Manager';

export const USER_LIFECYCLE_STATUSES: UserLifecycleStatus[] = [
  'Pending', 'Active', 'Inactive', 'Suspended', 'Locked'
];

export const EMPLOYEE_TYPES: EmployeeType[] = [
  'Driver', 'Staff', 'Admin', 'Manager'
];

/** API returns enum names as strings (JsonStringEnumConverter); mat-select needs numeric UserRole. */
export function parseUserRole(value: unknown): UserRole {
  if (typeof value === 'number' && UserRoleLabels[value as UserRole]) {
    return value as UserRole;
  }

  if (typeof value === 'string') {
    const trimmed = value.trim();
    const byName = (UserRole as Record<string, number | string>)[trimmed];
    if (typeof byName === 'number') {
      return byName as UserRole;
    }

    const numeric = Number(trimmed);
    if (!Number.isNaN(numeric) && UserRoleLabels[numeric as UserRole]) {
      return numeric as UserRole;
    }
  }

  return UserRole.Dispatcher;
}

export interface User {
  id: number;
  fullName: string;
  email: string;
  phone: string;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
  lastLoginAt?: string | null;
  companyId?: number | null;
  companyName?: string | null;
  branchId?: number | null;
  branchName?: string | null;
  departmentId?: number | null;
  departmentName?: string | null;
  jobTitle?: string | null;
  employeeCode?: string | null;
  employeeType?: EmployeeType | string | null;
  status?: UserLifecycleStatus | string;
  defaultWorkspaceKey?: string | null;
  defaultDashboardKey?: string | null;
  homeRoute?: string | null;
  timeZone?: string | null;
  language?: string | null;
  theme?: string | null;
  avatarUrl?: string | null;
  assignedRoles?: AssignedRole[] | null;
}

export interface AssignedRole {
  roleId: number;
  code: string;
  name: string;
  displayName: string;
  category?: string | null;
  roleType?: string | null;
  branchId?: number | null;
  departmentId?: number | null;
}

export interface SetUserRolesRequest {
  roleIds: number[];
  scopes?: { roleId: number; branchId?: number | null; departmentId?: number | null }[] | null;
}

export interface CreateUserDto {
  fullName: string;
  email: string;
  password: string;
  phone: string;
  role: UserRole;
  platformRoleCode?: string | null;
  branchId?: number | null;
  departmentId?: number | null;
  jobTitle?: string | null;
  employeeCode?: string | null;
  employeeType?: string | null;
  status?: string | null;
  defaultWorkspaceKey?: string | null;
  defaultDashboardKey?: string | null;
  homeRoute?: string | null;
  timeZone?: string | null;
  language?: string | null;
  theme?: string | null;
  avatarUrl?: string | null;
}

export interface UpdateUserDto {
  fullName: string;
  email: string;
  phone: string;
  role: UserRole;
  isActive: boolean;
  branchId?: number | null;
  departmentId?: number | null;
  jobTitle?: string | null;
  employeeCode?: string | null;
  employeeType?: string | null;
  status?: string | null;
  defaultWorkspaceKey?: string | null;
  defaultDashboardKey?: string | null;
  homeRoute?: string | null;
  timeZone?: string | null;
  language?: string | null;
  theme?: string | null;
  avatarUrl?: string | null;
}

export interface CreateUserRequest {
  user: CreateUserDto;
}

export type BulkImportMode = 'CreateOnly' | 'CreateOrUpdate' | 'UpdateOnly';

export interface BulkCreateUsersOptions {
  dryRun?: boolean;
  skipDuplicates?: boolean;
  mode?: BulkImportMode;
}

export interface BulkCreateUsersRequest {
  users: CreateUserDto[];
  options?: BulkCreateUsersOptions | null;
}

export interface BulkCreateUserSuccess {
  row: number;
  email: string;
  userId: number;
  temporaryPassword?: string | null;
  dryRun?: boolean;
}

export interface BulkCreateUserFailure {
  row: number;
  email?: string | null;
  error: string;
}

export interface BulkCreateUserSkipped {
  row: number;
  email: string;
  reason: string;
}

export interface BulkCreateUsersResult {
  succeeded: number;
  failed: number;
  skipped: number;
  dryRun: boolean;
  created: BulkCreateUserSuccess[];
  errors: BulkCreateUserFailure[];
  skippedRows: BulkCreateUserSkipped[];
}

export interface UpdateUserRequest {
  id: number;
  user: UpdateUserDto;
}

export interface UpdateUserStatusRequest {
  id: number;
  isActive: boolean;
  status?: string | null;
}

export interface ResetPasswordResponse {
  temporaryPassword: string;
}

export interface CompanyUserSummary {
  companyId: number;
  totalUsers: number;
  drivers: number;
  managers: number;
  administrators: number;
  staff: number;
  departmentCount: number;
}

export interface UserListFilters {
  tenantId?: number | null;
  branchId?: number | null;
  departmentId?: number | null;
  status?: string | null;
  employeeType?: string | null;
  search?: string | null;
}
