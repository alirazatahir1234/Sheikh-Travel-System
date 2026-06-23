import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Branch,
  BranchPayload,
  Department,
  DepartmentPayload,
  DepartmentPayloadWithBranch,
  OrganizationTree,
  Permission,
  PlatformRole,
  RoleSummary,
  RoleTemplate,
  SubscriptionOverview,
  TenantModuleOverview,
  TenantSecuritySettings,
  UpdateSubscriptionRequest,
  ProvisionTenantRequest,
  Tenant,
  TenantDetail,
  TenantManagementStats,
  TenantModuleDefinition,
  UpdateTenantBrandingPayload,
  UpdateTenantPayload
} from '../models/platform.model';

@Injectable({ providedIn: 'root' })
export class PlatformService {
  private readonly base = `${environment.apiUrl}/platform`;

  constructor(private http: HttpClient) {}

  getTenants(): Observable<Tenant[]> {
    return this.http.get<Tenant[]>(`${environment.apiUrl}/tenants`);
  }

  getTenantById(id: number): Observable<TenantDetail> {
    return this.http.get<TenantDetail>(`${environment.apiUrl}/tenants/${id}`);
  }

  getTenantManagementStats(): Observable<TenantManagementStats> {
    return this.http.get<TenantManagementStats>(`${environment.apiUrl}/tenants/management-stats`);
  }

  getModules(): Observable<TenantModuleDefinition[]> {
    return this.http.get<TenantModuleDefinition[]>(`${this.base}/modules`);
  }

  provisionTenant(payload: ProvisionTenantRequest): Observable<number> {
    return this.http.post<number>(`${environment.apiUrl}/tenants/provision`, payload);
  }

  updateTenant(id: number, payload: UpdateTenantPayload): Observable<boolean> {
    return this.http.put<boolean>(`${environment.apiUrl}/tenants/${id}`, payload);
  }

  updateTenantBranding(id: number, payload: UpdateTenantBrandingPayload): Observable<boolean> {
    return this.http.put<boolean>(`${environment.apiUrl}/tenants/${id}/branding`, payload);
  }

  resetTenantAdminPassword(tenantId: number, newPassword: string): Observable<boolean> {
    return this.http.post<boolean>(
      `${environment.apiUrl}/tenants/${tenantId}/reset-admin-password`,
      { newPassword }
    );
  }

  getBranches(): Observable<Branch[]> {
    return this.http.get<Branch[]>(`${this.base}/branches`);
  }

  getBranchById(id: number): Observable<Branch> {
    return this.http.get<Branch>(`${this.base}/branches/${id}`);
  }

  createBranch(payload: BranchPayload): Observable<number> {
    return this.http.post<number>(`${this.base}/branches`, payload);
  }

  updateBranch(id: number, payload: BranchPayload): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/branches/${id}`, payload);
  }

  deleteBranch(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/branches/${id}`);
  }

  getDepartments(): Observable<Department[]> {
    return this.http.get<Department[]>(`${this.base}/departments`);
  }

  createDepartment(payload: DepartmentPayload): Observable<number> {
    return this.http.post<number>(`${this.base}/departments`, payload);
  }

  updateDepartment(id: number, payload: DepartmentPayload, isActive: boolean): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/departments/${id}`, { payload, isActive });
  }

  deleteDepartment(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/departments/${id}`);
  }

  getRoles(): Observable<PlatformRole[]> {
    return this.http.get<PlatformRole[]>(`${this.base}/roles`);
  }

  createRole(name: string, code: string): Observable<number> {
    return this.http.post<number>(`${this.base}/roles`, { name, code });
  }

  getPermissions(): Observable<Permission[]> {
    return this.http.get<Permission[]>(`${this.base}/permissions`);
  }

  updateRolePermissions(roleId: number, permissionCodes: string[]): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/roles/${roleId}/permissions`, { permissionCodes });
  }

  // Tenant-scoped Organization Designer endpoints

  getOrganizationTree(tenantId: number): Observable<OrganizationTree> {
    return this.http.get<OrganizationTree>(`${this.base}/tenants/${tenantId}/organization`);
  }

  getBranchesForTenant(tenantId: number): Observable<Branch[]> {
    return this.http.get<Branch[]>(`${this.base}/tenants/${tenantId}/branches`);
  }

  createBranchForTenant(tenantId: number, payload: BranchPayload): Observable<number> {
    return this.http.post<number>(`${this.base}/tenants/${tenantId}/branches`, payload);
  }

  updateBranchForTenant(tenantId: number, branchId: number, payload: BranchPayload): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/tenants/${tenantId}/branches/${branchId}`, payload);
  }

  deleteBranchForTenant(tenantId: number, branchId: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/tenants/${tenantId}/branches/${branchId}`);
  }

  getDepartmentsForTenant(tenantId: number): Observable<Department[]> {
    return this.http.get<Department[]>(`${this.base}/tenants/${tenantId}/departments`);
  }

  createDepartmentForTenant(tenantId: number, payload: DepartmentPayloadWithBranch): Observable<number> {
    return this.http.post<number>(`${this.base}/tenants/${tenantId}/departments`, payload);
  }

  updateDepartmentForTenant(tenantId: number, departmentId: number, payload: DepartmentPayloadWithBranch, isActive: boolean): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/tenants/${tenantId}/departments/${departmentId}`, { payload, isActive });
  }

  deleteDepartmentForTenant(tenantId: number, departmentId: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/tenants/${tenantId}/departments/${departmentId}`);
  }

  moveDepartment(tenantId: number, departmentId: number, newBranchId: number | null): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/tenants/${tenantId}/departments/${departmentId}/move`, { newBranchId });
  }

  // Access Control (Sprint 2)

  getRolesForTenant(tenantId: number): Observable<RoleSummary[]> {
    return this.http.get<RoleSummary[]>(`${this.base}/tenants/${tenantId}/roles`);
  }

  createRoleForTenant(tenantId: number, name: string, code: string): Observable<number> {
    return this.http.post<number>(`${this.base}/tenants/${tenantId}/roles`, { name, code });
  }

  updateRoleForTenant(tenantId: number, roleId: number, name: string, isActive: boolean): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/tenants/${tenantId}/roles/${roleId}`, { name, isActive });
  }

  deleteRoleForTenant(tenantId: number, roleId: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/tenants/${tenantId}/roles/${roleId}`);
  }

  updateRolePermissionsForTenant(tenantId: number, roleId: number, permissionCodes: string[]): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/tenants/${tenantId}/roles/${roleId}/permissions`, { permissionCodes });
  }

  getTenantSecuritySettings(tenantId: number): Observable<TenantSecuritySettings> {
    return this.http.get<TenantSecuritySettings>(`${this.base}/tenants/${tenantId}/security`);
  }

  updateTenantSecuritySettings(tenantId: number, payload: TenantSecuritySettings): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/tenants/${tenantId}/security`, payload);
  }

  getRoleTemplates(): Observable<RoleTemplate[]> {
    return this.http.get<RoleTemplate[]>(`${this.base}/role-templates`);
  }

  applyRoleTemplate(tenantId: number, roleCode: string): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/tenants/${tenantId}/roles/apply-template`, { roleCode });
  }

  // Module Management (Sprint 3)

  getTenantModuleOverview(tenantId: number): Observable<TenantModuleOverview> {
    return this.http.get<TenantModuleOverview>(`${this.base}/tenants/${tenantId}/module-overview`);
  }

  setTenantModules(tenantId: number, moduleCodes: string[]): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/tenants/${tenantId}/modules`, { moduleCodes });
  }

  // Subscription Management (Sprint 4)

  getSubscriptionOverview(tenantId: number): Observable<SubscriptionOverview> {
    return this.http.get<SubscriptionOverview>(`${this.base}/tenants/${tenantId}/subscription`);
  }

  updateSubscription(tenantId: number, request: UpdateSubscriptionRequest): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/tenants/${tenantId}/subscription/action`, request);
  }
}
