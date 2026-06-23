import { Injectable, signal, computed } from '@angular/core';
import { COMPANY_NAME, APP_LOGO_PATH } from '../../../core/constants/app-brand';
import {
  Tenant,
  Branch,
  Department,
  User,
  OrganizationNode,
  BranchCapacity,
  AuditLog,
  UserPreview,
  StructuralLineage,
  PendingChange,
  TabItem,
} from '../models/organization.models';

@Injectable({
  providedIn: 'root'
})
export class MockDataService {
  
  readonly tenant = signal<Tenant>({
    id: 1,
    name: COMPANY_NAME,
    code: 'ST-001',
    logo: APP_LOGO_PATH,
    isActive: true,
  });

  readonly branches = signal<Branch[]>([
    {
      id: 1,
      branchCode: 'HQ-004',
      name: 'Dubai Main Hub',
      status: 'Active',
      parentBranchId: null,
      tenantId: 1,
      city: 'Dubai',
      country: 'UAE',
      userCount: 120,
      vehicleCount: 45,
      driverCount: 310,
      departmentCount: 4,
      complianceScore: 98,
      isGpsEnabled: true,
      createdAt: '2024-01-15T10:00:00Z',
      updatedAt: '2024-06-01T14:30:00Z',
    },
    {
      id: 2,
      branchCode: 'HQ-003',
      name: 'Lahore Office',
      status: 'Active',
      parentBranchId: null,
      tenantId: 1,
      city: 'Lahore',
      country: 'Pakistan',
      userCount: 45,
      vehicleCount: 20,
      driverCount: 85,
      departmentCount: 2,
      complianceScore: 92,
      isGpsEnabled: true,
      createdAt: '2024-02-10T09:00:00Z',
      updatedAt: '2024-05-28T11:15:00Z',
    },
  ]);

  readonly departments = signal<Department[]>([
    {
      id: 1,
      name: 'Logistics & Fleet',
      branchId: 1,
      tenantId: 1,
      departmentHeadUserId: 1,
      departmentHeadName: 'Ahmed Khan',
      staffCount: 8,
      vehicleCount: 12,
      isActive: true,
    },
    {
      id: 2,
      name: 'Finance Dept',
      branchId: 1,
      tenantId: 1,
      departmentHeadUserId: 2,
      departmentHeadName: 'Sara Ali',
      staffCount: 4,
      vehicleCount: 2,
      isActive: true,
    },
    {
      id: 3,
      name: 'Operations',
      branchId: 1,
      tenantId: 1,
      departmentHeadUserId: 3,
      departmentHeadName: 'Mohammed Hassan',
      staffCount: 15,
      vehicleCount: 8,
      isActive: true,
    },
    {
      id: 4,
      name: 'Customer Service',
      branchId: 1,
      tenantId: 1,
      departmentHeadUserId: 4,
      departmentHeadName: 'Fatima Zahra',
      staffCount: 10,
      vehicleCount: 0,
      isActive: true,
    },
    {
      id: 5,
      name: 'QA Compliance',
      branchId: null,
      tenantId: 1,
      departmentHeadUserId: null,
      staffCount: 3,
      vehicleCount: 0,
      isActive: true,
    },
    {
      id: 6,
      name: 'Human Resources (Global)',
      branchId: null,
      tenantId: 1,
      departmentHeadUserId: null,
      staffCount: 5,
      vehicleCount: 0,
      isActive: true,
    },
  ]);

  readonly users = signal<User[]>([
    {
      id: 1,
      fullName: 'Ahmed Khan',
      email: 'ahmed.khan@sheikhtravel.com',
      role: 'Branch Manager',
      departmentId: 1,
      branchId: 1,
      isActive: true,
      lastLoginAt: '2024-06-08T09:30:00Z',
    },
    {
      id: 2,
      fullName: 'Fatima S.',
      email: 'fatima.s@sheikhtravel.com',
      role: 'Fleet Coordinator',
      departmentId: 1,
      branchId: 1,
      isActive: true,
      lastLoginAt: '2024-06-08T08:15:00Z',
    },
    {
      id: 3,
      fullName: 'Omar Farooq',
      email: 'omar.farooq@sheikhtravel.com',
      role: 'Driver Supervisor',
      departmentId: 1,
      branchId: 1,
      isActive: true,
      lastLoginAt: '2024-06-07T16:45:00Z',
    },
    {
      id: 4,
      fullName: 'Aisha Begum',
      email: 'aisha.begum@sheikhtravel.com',
      role: 'Finance Manager',
      departmentId: 2,
      branchId: 1,
      isActive: true,
      lastLoginAt: '2024-06-08T10:00:00Z',
    },
    {
      id: 5,
      fullName: 'Hassan Ali',
      email: 'hassan.ali@sheikhtravel.com',
      role: 'Operations Lead',
      departmentId: 3,
      branchId: 1,
      isActive: true,
      lastLoginAt: '2024-06-08T07:30:00Z',
    },
  ]);

  readonly selectedBranch = signal<Branch | null>(this.branches()[0]);

  readonly branchCapacity = computed<BranchCapacity | null>(() => {
    const branch = this.selectedBranch();
    if (!branch) return null;
    
    return {
      branchId: branch.id,
      staffUtilization: {
        current: 120,
        max: 150,
        percentage: 80,
        label: 'Staff Utilization',
      },
      fleetAllocation: {
        current: 45,
        max: 60,
        percentage: 75,
        label: 'Fleet Allocation',
      },
      departmentSlots: {
        current: 4,
        max: 10,
        percentage: 40,
        label: 'Department Slots',
      },
      lastUpdated: '5m ago',
    };
  });

  readonly structuralLineage = computed<StructuralLineage | null>(() => {
    const branch = this.selectedBranch();
    if (!branch) return null;
    
    return {
      tenant: {
        id: 1,
        name: COMPANY_NAME,
        type: 'TENANT',
        icon: 'building',
      },
      branch: {
        id: branch.id,
        name: branch.name,
        type: 'BRANCH',
        icon: 'map-pin',
      },
      children: {
        departmentCount: branch.departmentCount,
        userCount: branch.userCount,
      },
    };
  });

  readonly auditLogs = signal<AuditLog[]>([
    {
      id: 1,
      timestamp: '2024-06-08T14:20:00Z',
      action: 'Department Added',
      description: 'Ahmed Khan added Fleet Logistics department to Dubai Hub.',
      userId: 1,
      userName: 'Ahmed Khan',
      entityType: 'department',
      entityId: 1,
      severity: 'info',
    },
    {
      id: 2,
      timestamp: '2024-06-08T12:30:00Z',
      action: 'Policy Updated',
      description: 'Branch HQ-004 security policy updated by System Admin.',
      userId: 0,
      userName: 'System Admin',
      entityType: 'branch',
      entityId: 1,
      severity: 'warning',
    },
    {
      id: 3,
      timestamp: '2024-06-07T16:15:00Z',
      action: 'Bulk Import',
      description: 'Bulk user import completed: 15 new drivers assigned.',
      userId: 1,
      userName: 'Ahmed Khan',
      entityType: 'user',
      entityId: 0,
      severity: 'info',
    },
  ]);

  readonly userPreview = computed<UserPreview | null>(() => {
    const branch = this.selectedBranch();
    if (!branch) return null;
    
    const branchUsers = this.users().filter(u => u.branchId === branch.id);
    return {
      users: branchUsers.slice(0, 5),
      totalCount: branch.userCount,
      branchId: branch.id,
    };
  });

  readonly pendingChanges = signal<PendingChange[]>([
    {
      id: '1',
      type: 'update',
      entityType: 'branch',
      entityName: 'Dubai Main Hub',
      timestamp: '2024-06-08T14:25:30Z',
    },
    {
      id: '2',
      type: 'create',
      entityType: 'department',
      entityName: 'IT Support',
      timestamp: '2024-06-08T14:24:15Z',
    },
    {
      id: '3',
      type: 'update',
      entityType: 'user',
      entityName: 'Ahmed Khan',
      timestamp: '2024-06-08T14:23:00Z',
    },
  ]);

  readonly lastAutoSaved = signal<string>('14:25:30');

  readonly tabs = signal<TabItem[]>([
    { id: 'overview', label: 'Overview' },
    { id: 'departments', label: 'Departments', count: 4 },
    { id: 'users', label: 'Users', count: 120 },
    { id: 'assets', label: 'Assets' },
    { id: 'audit-logs', label: 'Audit Logs' },
  ]);

  readonly activeTab = signal<string>('overview');

  readonly organizationTree = computed<OrganizationNode[]>(() => {
    const tenant = this.tenant();
    const branches = this.branches();
    const departments = this.departments();

    const tenantNode: OrganizationNode = {
      id: `tenant-${tenant.id}`,
      type: 'tenant',
      name: `${tenant.name} (Global)`,
      code: tenant.code,
      parentId: null,
      data: tenant,
      isExpanded: true,
      isSelected: false,
      children: branches
        .filter(b => b.parentBranchId === null)
        .map(branch => this.buildBranchNode(branch, departments)),
    };

    return [tenantNode];
  });

  readonly unassignedDepartments = computed<Department[]>(() => {
    return this.departments().filter(d => d.branchId === null);
  });

  private buildBranchNode(branch: Branch, allDepartments: Department[]): OrganizationNode {
    const branchDepartments = allDepartments.filter(d => d.branchId === branch.id);
    
    return {
      id: `branch-${branch.id}`,
      type: 'branch',
      name: branch.name,
      code: branch.branchCode,
      parentId: `tenant-${branch.tenantId}`,
      data: branch,
      stats: {
        departmentCount: branch.departmentCount,
        userCount: branch.userCount,
        vehicleCount: branch.vehicleCount,
        driverCount: branch.driverCount,
      },
      isExpanded: branch.id === 1,
      isSelected: branch.id === this.selectedBranch()?.id,
      children: branchDepartments.map(dept => ({
        id: `dept-${dept.id}`,
        type: 'department' as const,
        name: dept.name,
        parentId: `branch-${branch.id}`,
        data: dept,
        stats: {
          userCount: dept.staffCount,
          vehicleCount: dept.vehicleCount,
        },
        isExpanded: false,
        isSelected: false,
        children: [],
      })),
    };
  }

  selectBranch(branchId: number): void {
    const branch = this.branches().find(b => b.id === branchId);
    this.selectedBranch.set(branch ?? null);
  }

  setActiveTab(tabId: string): void {
    this.activeTab.set(tabId);
  }

  saveConfiguration(): void {
    console.log('Saving configuration...');
    this.pendingChanges.set([]);
  }

  discardChanges(): void {
    console.log('Discarding changes...');
    this.pendingChanges.set([]);
  }
}
