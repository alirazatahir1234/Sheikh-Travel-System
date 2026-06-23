/**
 * Organization Hierarchy - Data Models
 * Strongly typed interfaces for the Organization Hierarchy Configuration feature
 */

export interface Tenant {
  id: number;
  name: string;
  code: string;
  logo?: string;
  isActive: boolean;
}

export interface Branch {
  id: number;
  branchCode: string;
  name: string;
  status: BranchStatus;
  parentBranchId: number | null;
  tenantId: number;
  city?: string;
  country?: string;
  userCount: number;
  vehicleCount: number;
  driverCount: number;
  departmentCount: number;
  complianceScore: number;
  isGpsEnabled: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface Department {
  id: number;
  name: string;
  branchId: number | null;
  tenantId: number;
  departmentHeadUserId: number | null;
  departmentHeadName?: string;
  staffCount: number;
  vehicleCount: number;
  isActive: boolean;
}

export interface User {
  id: number;
  fullName: string;
  email: string;
  avatarUrl?: string;
  role: string;
  departmentId: number | null;
  branchId: number | null;
  isActive: boolean;
  lastLoginAt?: string;
}

export interface Vehicle {
  id: number;
  registrationNumber: string;
  model: string;
  type: string;
  branchId: number;
  status: VehicleStatus;
}

export interface Driver {
  id: number;
  fullName: string;
  licenseNumber: string;
  branchId: number;
  status: DriverStatus;
}

export interface OrganizationNode {
  id: string;
  type: 'tenant' | 'branch' | 'department';
  name: string;
  code?: string;
  parentId: string | null;
  children: OrganizationNode[];
  data: Tenant | Branch | Department;
  stats?: NodeStats;
  isExpanded: boolean;
  isSelected: boolean;
}

export interface NodeStats {
  departmentCount?: number;
  userCount?: number;
  vehicleCount?: number;
  driverCount?: number;
}

export interface BranchCapacity {
  branchId: number;
  staffUtilization: CapacityMetric;
  fleetAllocation: CapacityMetric;
  departmentSlots: CapacityMetric;
  lastUpdated: string;
}

export interface CapacityMetric {
  current: number;
  max: number;
  percentage: number;
  label: string;
}

export interface AuditLog {
  id: number;
  timestamp: string;
  action: string;
  description: string;
  userId: number;
  userName: string;
  entityType: string;
  entityId: number;
  severity: 'info' | 'warning' | 'error';
}

export interface UserPreview {
  users: User[];
  totalCount: number;
  branchId: number;
}

export interface StructuralLineage {
  tenant: LineageNode;
  branch: LineageNode;
  children: LineageChildSummary;
}

export interface LineageNode {
  id: number;
  name: string;
  type: string;
  icon: string;
}

export interface LineageChildSummary {
  departmentCount: number;
  userCount: number;
}

export interface PendingChange {
  id: string;
  type: 'create' | 'update' | 'delete';
  entityType: 'branch' | 'department' | 'user';
  entityName: string;
  timestamp: string;
}

export type BranchStatus = 'Active' | 'Inactive' | 'Maintenance' | 'Closed';

export type VehicleStatus = 'Available' | 'InUse' | 'Maintenance' | 'Retired';

export type DriverStatus = 'Active' | 'OnLeave' | 'Suspended' | 'Inactive';

export type ViewMode = 'tree' | 'diagram';

export type TabType = 'overview' | 'departments' | 'users' | 'assets' | 'audit-logs';

export interface TabItem {
  id: TabType;
  label: string;
  count?: number;
}
