import { Injectable, inject, signal, computed, effect } from '@angular/core';
import { Observable, forkJoin, of, catchError, tap, map } from 'rxjs';
import { PlatformService } from '../../../core/services/platform.service';
import { PlatformTenantContextService } from '../../../core/services/platform-tenant-context.service';
import {
  OrganizationTree as BackendOrgTree,
  OrganizationBranch as BackendBranch,
  OrganizationDepartment as BackendDepartment,
  Tenant as BackendTenant,
  TenantDetail,
} from '../../../core/models/platform.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';
import {
  Tenant,
  Branch,
  Department,
  OrganizationNode,
  BranchCapacity,
  AuditLog,
  UserPreview,
  User,
  StructuralLineage,
  PendingChange,
  TabItem,
  BranchStatus,
} from '../models/organization.models';

export type BranchHealth = 'Healthy' | 'NoManager' | 'NoDepartments' | 'Inactive';

export interface TenantOverviewStats {
  branchCount: number;
  departmentCount: number;
  userCount: number;
  vehicleCount: number;
  driverCount: number;
  gpsDeviceCount: number;
  isPlaceholder: {
    driverCount: boolean;
    gpsDeviceCount: boolean;
  };
}

@Injectable({
  providedIn: 'root'
})
export class OrganizationHierarchyService {
  private readonly platform = inject(PlatformService);
  private readonly tenantContext = inject(PlatformTenantContextService);

  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);

  private readonly rawOrgTree = signal<BackendOrgTree | null>(null);
  private readonly rawTenantDetail = signal<TenantDetail | null>(null);
  private readonly tenantsCache = signal<BackendTenant[]>([]);

  readonly selectedBranchId = signal<number | null>(null);
  readonly selectedDepartmentId = signal<number | null>(null);
  readonly activeTab = signal<string>('overview');
  readonly pendingChanges = signal<PendingChange[]>([]);
  readonly lastAutoSaved = signal<string>('--:--:--');

  readonly tenant = computed<Tenant | null>(() => {
    const detail = this.rawTenantDetail();
    if (!detail) return null;
    return {
      id: detail.id,
      name: detail.name,
      code: detail.code ?? `T-${detail.id}`,
      logo: undefined,
      isActive: detail.isActive,
    };
  });

  readonly tenantOverviewStats = computed<TenantOverviewStats | null>(() => {
    const detail = this.rawTenantDetail();
    const tree = this.rawOrgTree();
    if (!detail) return null;

    const totalUsers = tree?.branches.reduce((sum, b) =>
      sum + b.departments.reduce((ds, d) => ds + d.staffCount, 0), 0) ?? 0;

    return {
      branchCount: detail.branchCount,
      departmentCount: detail.departmentCount,
      userCount: totalUsers || detail.activeUserCount,
      vehicleCount: detail.activeVehicleCount,
      driverCount: detail.maxDrivers ?? 0,
      gpsDeviceCount: detail.maxGpsDevices ?? 0,
      isPlaceholder: {
        driverCount: true,
        gpsDeviceCount: true,
      },
    };
  });

  readonly branches = computed<Branch[]>(() => {
    const tree = this.rawOrgTree();
    if (!tree) return [];

    return tree.branches.map(b => this.mapBackendBranch(b, tree.tenantId));
  });

  readonly departments = computed<Department[]>(() => {
    const tree = this.rawOrgTree();
    if (!tree) return [];

    const allDepts: Department[] = [];
    for (const branch of tree.branches) {
      for (const dept of branch.departments) {
        allDepts.push(this.mapBackendDepartment(dept, tree.tenantId));
      }
    }
    for (const dept of tree.unassignedDepartments) {
      allDepts.push(this.mapBackendDepartment(dept, tree.tenantId));
    }
    return allDepts;
  });

  readonly unassignedDepartments = computed<Department[]>(() => {
    const tree = this.rawOrgTree();
    if (!tree) return [];
    return tree.unassignedDepartments.map(d => this.mapBackendDepartment(d, tree.tenantId));
  });

  readonly selectedBranch = computed<Branch | null>(() => {
    const id = this.selectedBranchId();
    if (!id) return null;
    return this.branches().find(b => b.id === id) ?? null;
  });

  readonly selectedDepartment = computed<Department | null>(() => {
    const id = this.selectedDepartmentId();
    if (!id) return null;
    return this.departments().find(d => d.id === id) ?? null;
  });

  readonly organizationTree = computed<OrganizationNode[]>(() => {
    const tenant = this.tenant();
    const tree = this.rawOrgTree();
    if (!tenant || !tree) return [];

    const tenantNode: OrganizationNode = {
      id: `tenant-${tenant.id}`,
      type: 'tenant',
      name: `${tenant.name} (Global)`,
      code: tenant.code,
      parentId: null,
      data: tenant,
      isExpanded: true,
      isSelected: false,
      children: this.buildBranchNodes(tree.branches, tree.tenantId),
    };

    return [tenantNode];
  });

  readonly branchCapacity = computed<BranchCapacity | null>(() => {
    const branch = this.selectedBranch();
    const detail = this.rawTenantDetail();
    if (!branch || !detail) return null;

    const maxUsers = detail.maxUsers ?? 200;
    const maxVehicles = detail.maxVehicles ?? 100;
    const maxDepts = detail.maxBranches ? detail.maxBranches * 5 : 20;

    return {
      branchId: branch.id,
      staffUtilization: {
        current: branch.userCount,
        max: Math.ceil(maxUsers / Math.max(detail.branchCount, 1)),
        percentage: Math.min(100, Math.round((branch.userCount / (maxUsers / Math.max(detail.branchCount, 1))) * 100)),
        label: 'Staff Utilization',
      },
      fleetAllocation: {
        current: branch.vehicleCount,
        max: Math.ceil(maxVehicles / Math.max(detail.branchCount, 1)),
        percentage: Math.min(100, Math.round((branch.vehicleCount / (maxVehicles / Math.max(detail.branchCount, 1))) * 100)),
        label: 'Fleet Allocation',
      },
      departmentSlots: {
        current: branch.departmentCount,
        max: maxDepts,
        percentage: Math.min(100, Math.round((branch.departmentCount / maxDepts) * 100)),
        label: 'Department Slots',
      },
      lastUpdated: 'just now',
    };
  });

  readonly selectedBranchHealth = computed<BranchHealth | null>(() => {
    const branch = this.selectedBranch();
    if (!branch) return null;
    return this.getBranchHealth(branch);
  });

  readonly selectedBranchManager = computed<{ id: number; name: string; avatarUrl?: string; isPlaceholder: boolean } | null>(() => {
    const branch = this.selectedBranch();
    if (!branch) return null;

    return {
      id: 0,
      name: 'Branch Manager',
      avatarUrl: undefined,
      isPlaceholder: true,
    };
  });

  readonly structuralLineage = computed<StructuralLineage | null>(() => {
    const branch = this.selectedBranch();
    const tenant = this.tenant();
    if (!branch || !tenant) return null;

    return {
      tenant: {
        id: tenant.id,
        name: tenant.name,
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

  readonly recentActivity = signal<AuditLog[]>([
    {
      id: 1,
      timestamp: new Date(Date.now() - 10 * 60000).toISOString(),
      action: 'Branch Created',
      description: 'System Admin created new branch configuration.',
      userId: 0,
      userName: 'System Admin',
      entityType: 'branch',
      entityId: 1,
      severity: 'info',
    },
    {
      id: 2,
      timestamp: new Date(Date.now() - 2 * 3600000).toISOString(),
      action: 'Department Assigned',
      description: 'Department moved to branch.',
      userId: 0,
      userName: 'System Admin',
      entityType: 'department',
      entityId: 1,
      severity: 'info',
    },
    {
      id: 3,
      timestamp: new Date(Date.now() - 24 * 3600000).toISOString(),
      action: 'User Import',
      description: 'Bulk user import completed.',
      userId: 0,
      userName: 'System Admin',
      entityType: 'user',
      entityId: 0,
      severity: 'info',
    },
  ]);

  readonly userPreview = computed<UserPreview | null>(() => {
    const branch = this.selectedBranch();
    if (!branch) return null;

    const placeholderUsers: User[] = [
      { id: 1, fullName: 'Branch Manager', email: 'manager@example.com', role: 'Branch Manager', departmentId: null, branchId: branch.id, isActive: true },
      { id: 2, fullName: 'Operations Lead', email: 'ops@example.com', role: 'Operations Lead', departmentId: null, branchId: branch.id, isActive: true },
    ];

    return {
      users: placeholderUsers,
      totalCount: branch.userCount,
      branchId: branch.id,
    };
  });

  readonly tabs = computed<TabItem[]>(() => {
    const branch = this.selectedBranch();
    return [
      { id: 'overview', label: 'Overview' },
      { id: 'departments', label: 'Departments', count: branch?.departmentCount ?? 0 },
      { id: 'users', label: 'Users', count: branch?.userCount ?? 0 },
      { id: 'assets', label: 'Assets' },
    ];
  });

  readonly tenants = computed(() => this.tenantsCache());

  constructor() {
    effect(() => {
      const tenantId = this.tenantContext.currentTenantId;
      if (tenantId) {
        this.loadData(tenantId);
      }
    }, { allowSignalWrites: true });
  }

  loadTenants(): Observable<BackendTenant[]> {
    return this.platform.getTenants().pipe(
      tap({
        next: (tenants) => {
          this.tenantsCache.set(tenants.filter(t => t.isActive));
        },
        error: () => {
          this.tenantsCache.set([]);
        },
      }),
      map((tenants) => tenants.filter(t => t.isActive)),
      catchError(() => {
        this.tenantsCache.set([]);
        return of([]);
      })
    );
  }

  loadData(tenantId: number): void {
    this.loading.set(true);
    this.error.set(null);

    forkJoin({
      tree: this.platform.getOrganizationTree(tenantId),
      tenant: this.platform.getTenantById(tenantId),
    }).pipe(
      catchError((err) => {
        this.error.set(apiErrorMessage(err, 'Failed to load organization data'));
        return of(null);
      })
    ).subscribe({
      next: (result) => {
        if (result) {
          this.rawOrgTree.set(result.tree);
          this.rawTenantDetail.set(result.tenant);

          if (result.tree.branches.length > 0 && !this.selectedBranchId()) {
            this.selectedBranchId.set(result.tree.branches[0].id);
          }
        }
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  selectTenantById(tenantId: number): void {
    this.tenantContext.selectTenantById(tenantId);
    this.selectedBranchId.set(null);
    this.selectedDepartmentId.set(null);
    this.activeTab.set('overview');
    // Clear previous tenant payload so the UI does not flash another org's tree.
    this.rawOrgTree.set(null);
    this.rawTenantDetail.set(null);
    this.error.set(null);
    this.loadData(tenantId);
  }

  selectBranch(branchId: number): void {
    this.selectedBranchId.set(branchId);
    this.selectedDepartmentId.set(null);
    this.activeTab.set('overview');
  }

  selectDepartment(departmentId: number): void {
    this.selectedDepartmentId.set(departmentId);
    const dept = this.departments().find(d => d.id === departmentId);
    if (dept?.branchId) {
      this.selectedBranchId.set(dept.branchId);
      this.activeTab.set('departments');
    }
  }

  setActiveTab(tabId: string): void {
    this.activeTab.set(tabId);
  }

  clearSelection(): void {
    this.selectedBranchId.set(null);
    this.selectedDepartmentId.set(null);
  }

  deleteBranch(branchId: number): Observable<boolean> {
    const tenantId = this.rawTenantDetail()?.id;
    if (!tenantId) return of(false);

    return this.platform.deleteBranchForTenant(tenantId, branchId).pipe(
      tap(() => {
        if (this.selectedBranchId() === branchId) {
          this.clearSelection();
        }
        this.loadData(tenantId);
      })
    );
  }

  createDepartment(name: string, branchId: number | null): Observable<number> {
    const tenantId = this.rawTenantDetail()?.id;
    if (!tenantId) {
      return new Observable(subscriber => {
        subscriber.error(new Error('No tenant selected'));
      });
    }

    return this.platform.createDepartmentForTenant(tenantId, {
      name,
      branchId,
      departmentHeadUserId: null,
    }).pipe(
      tap(() => this.loadData(tenantId))
    );
  }

  updateDepartment(departmentId: number, name: string, branchId: number | null, isActive: boolean): Observable<boolean> {
    const tenantId = this.rawTenantDetail()?.id;
    if (!tenantId) {
      return new Observable(subscriber => {
        subscriber.error(new Error('No tenant selected'));
      });
    }

    return this.platform.updateDepartmentForTenant(
      tenantId,
      departmentId,
      { name, branchId, departmentHeadUserId: null },
      isActive
    ).pipe(
      tap(() => this.loadData(tenantId))
    );
  }

  deleteDepartment(departmentId: number): Observable<boolean> {
    const tenantId = this.rawTenantDetail()?.id;
    if (!tenantId) return of(false);

    return this.platform.deleteDepartmentForTenant(tenantId, departmentId).pipe(
      tap(() => {
        if (this.selectedDepartmentId() === departmentId) {
          this.selectedDepartmentId.set(null);
        }
        this.loadData(tenantId);
      })
    );
  }

  getHierarchyExportPayload(): object | null {
    const tree = this.rawOrgTree();
    if (!tree) return null;

    return {
      tenant: tree.tenantName,
      branches: tree.branches.map(b => ({
        code: b.branchCode,
        name: b.name,
        type: b.branchType,
        city: b.city,
        country: b.country,
        departments: b.departments.map(d => ({ name: d.name, staffCount: d.staffCount })),
      })),
      unassignedDepartments: tree.unassignedDepartments.map(d => ({
        name: d.name,
        staffCount: d.staffCount,
      })),
    };
  }

  saveConfiguration(): boolean {
    if (this.pendingChanges().length === 0) {
      return false;
    }
    this.pendingChanges.set([]);
    this.lastAutoSaved.set(new Date().toLocaleTimeString('en-US', { hour12: false }));
    return true;
  }

  discardChanges(): boolean {
    if (this.pendingChanges().length === 0) {
      return false;
    }
    this.pendingChanges.set([]);
    return true;
  }

  getBranchHealth(branch: Branch): BranchHealth {
    if (branch.status !== 'Active') return 'Inactive';
    if (branch.departmentCount === 0) return 'NoDepartments';
    return 'Healthy';
  }

  private mapBackendBranch(b: BackendBranch, tenantId: number): Branch {
    const userCount = b.departments.reduce((sum, d) => sum + d.staffCount, 0);
    const departmentCount = b.departments.length;

    return {
      id: b.id,
      branchCode: b.branchCode,
      name: b.name,
      status: this.mapBranchStatus(b.status, b.isActive),
      parentBranchId: b.parentBranchId ?? null,
      tenantId,
      city: b.city ?? undefined,
      country: b.country ?? undefined,
      userCount,
      vehicleCount: 0,
      driverCount: 0,
      departmentCount,
      complianceScore: 95,
      isGpsEnabled: true,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };
  }

  private mapBackendDepartment(d: BackendDepartment, tenantId: number): Department {
    return {
      id: d.id,
      name: d.name,
      branchId: d.branchId ?? null,
      tenantId,
      departmentHeadUserId: null,
      departmentHeadName: d.departmentHeadName ?? undefined,
      staffCount: d.staffCount,
      vehicleCount: 0,
      isActive: d.isActive,
    };
  }

  /** Maps API BranchStatus (Active=1 … Closed=4) to hierarchy UI labels. */
  private mapBranchStatus(status: number, isActive: boolean): BranchStatus {
    if (!isActive) return 'Inactive';
    switch (status) {
      case 1: return 'Active';
      case 2: return 'Inactive';
      case 3: return 'Maintenance';
      case 4: return 'Closed';
      default: return isActive ? 'Active' : 'Inactive';
    }
  }

  private buildBranchNodes(branches: BackendBranch[], tenantId: number): OrganizationNode[] {
    const branchMap = new Map<number, OrganizationNode>();
    const rootBranches: OrganizationNode[] = [];

    for (const b of branches) {
      const mapped = this.mapBackendBranch(b, tenantId);
      const deptChildren: OrganizationNode[] = b.departments.map(d => ({
        id: `dept-${d.id}`,
        type: 'department' as const,
        name: d.name,
        parentId: `branch-${b.id}`,
        data: this.mapBackendDepartment(d, tenantId),
        stats: {
          userCount: d.staffCount,
          vehicleCount: 0,
        },
        isExpanded: false,
        isSelected: false,
        children: [],
      }));

      const branchNode: OrganizationNode = {
        id: `branch-${b.id}`,
        type: 'branch',
        name: b.name,
        code: b.branchCode,
        parentId: b.parentBranchId ? `branch-${b.parentBranchId}` : `tenant-${tenantId}`,
        data: mapped,
        stats: {
          departmentCount: mapped.departmentCount,
          userCount: mapped.userCount,
          vehicleCount: mapped.vehicleCount,
          driverCount: mapped.driverCount,
        },
        isExpanded: true,
        isSelected: this.selectedBranchId() === b.id,
        children: deptChildren,
      };

      branchMap.set(b.id, branchNode);
    }

    for (const b of branches) {
      const node = branchMap.get(b.id)!;
      if (b.parentBranchId && branchMap.has(b.parentBranchId)) {
        branchMap.get(b.parentBranchId)!.children.push(node);
      } else {
        rootBranches.push(node);
      }
    }

    return rootBranches;
  }
}
