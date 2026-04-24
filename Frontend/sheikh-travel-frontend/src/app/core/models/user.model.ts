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

export interface User {
  id: number;
  fullName: string;
  email: string;
  phone: string;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
}

export interface CreateUserDto {
  fullName: string;
  email: string;
  password: string;
  phone: string;
  role: UserRole;
}

export interface UpdateUserDto {
  fullName: string;
  email: string;
  phone: string;
  role: UserRole;
  isActive: boolean;
}

export interface CreateUserRequest {
  user: CreateUserDto;
}

export interface UpdateUserRequest {
  id: number;
  user: UpdateUserDto;
}

export interface UpdateUserStatusRequest {
  id: number;
  isActive: boolean;
}

export interface ResetPasswordResponse {
  temporaryPassword: string;
}
