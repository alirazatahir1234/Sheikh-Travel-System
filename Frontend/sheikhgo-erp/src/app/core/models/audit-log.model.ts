export interface AuditLog {
  id: number;
  action: string;
  entityName: string;
  entityId?: number | null;
  oldValues?: string | null;
  newValues?: string | null;
  userId?: number | null;
  userName?: string | null;
  ipAddress?: string | null;
  createdAt: string;
}

export const AuditActions = [
  'Create',
  'Update',
  'Delete',
  'Login',
  'Logout',
  'PasswordReset',
  'StatusChange'
] as const;

export const AuditEntities = [
  'Booking',
  'Vehicle',
  'Driver',
  'Customer',
  'Route',
  'User',
  'FuelLog',
  'Maintenance',
  'Payment'
] as const;
