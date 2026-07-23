import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
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
  EffectivePermission,
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
  UpdateTenantPayload,
  CompanyFeature,
  ModuleRegistryEntry,
  CompanyLicense,
  MenuCatalog,
  UpdateMenuModulePayload,
  UpdateMenuItemPayload,
  CreateMenuItemPayload,
  WorkspaceDefinition,
  CompanyWorkspace,
  ResolvedWorkspace,
  UpdateWorkspaceDefinitionPayload,
  CreateWorkspaceDefinitionPayload,
  DashboardDefinition,
  DashboardWidgetDefinition,
  DashboardDetail,
  ResolvedDashboard,
  UpdateDashboardDefinitionPayload,
  UpdateDashboardLayoutPayload,
  CompanyDataScope,
  SecurityPolicyValue,
  SecurityPolicyDefinition,
  UpdateSecurityCompanyPoliciesPayload,
  SecurityCompanySummary,
  AuditEventListItem,
  AuditEventDetail,
  AuditEventDefinition,
  AuditEventSearchFilter,
  AuditRetention
} from '../models/platform.model';
import { PagedResult } from '../models/common.model';

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

  getCompanyFeatures(tenantId: number): Observable<CompanyFeature[]> {
    return this.http.get<CompanyFeature[]>(`${this.base}/features/company/${tenantId}`);
  }

  getFeatureCatalog(): Observable<CompanyFeature[]> {
    return this.http.get<CompanyFeature[]>(`${this.base}/features/catalog`);
  }

  setCompanyFeatures(tenantId: number, enabledFeatureKeys: string[]): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/features/company`, {
      tenantId,
      enabledFeatureKeys
    });
  }

  getModuleCatalog(): Observable<ModuleRegistryEntry[]> {
    return this.http.get<ModuleRegistryEntry[]>(`${this.base}/modules/catalog`);
  }

  getCompanyModules(): Observable<ModuleRegistryEntry[]> {
    return this.http.get<ModuleRegistryEntry[]>(`${this.base}/modules/company`);
  }

  getModuleByKey(codeOrId: string): Observable<ModuleRegistryEntry> {
    return this.http.get<ModuleRegistryEntry>(`${this.base}/modules/${encodeURIComponent(codeOrId)}`);
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

  getPermissions(filters?: {
    category?: string | null;
    moduleKey?: string | null;
    action?: string | null;
    visible?: boolean | null;
  }): Observable<Permission[]> {
    let params = new HttpParams();
    if (filters?.category) params = params.set('category', filters.category);
    if (filters?.moduleKey) params = params.set('moduleKey', filters.moduleKey);
    if (filters?.action) params = params.set('action', filters.action);
    if (filters?.visible != null) params = params.set('visible', String(filters.visible));
    return this.http.get<Permission[]>(`${this.base}/permissions`, { params });
  }

  getEffectivePermissions(): Observable<EffectivePermission[]> {
    return this.http.get<EffectivePermission[]>(`${this.base}/permissions/effective`);
  }

  getMyDataScope(): Observable<CompanyDataScope> {
    return this.http.get<CompanyDataScope>(`${this.base}/data-scope/me`);
  }

  getUserDataScope(userId: number): Observable<CompanyDataScope> {
    return this.http.get<CompanyDataScope>(`${environment.apiUrl}/Users/${userId}/data-scope`);
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

  getCompanyRoles(tenantId?: number | null): Observable<RoleSummary[]> {
    let params = new HttpParams();
    if (tenantId != null) params = params.set('tenantId', tenantId);
    return this.http.get<RoleSummary[]>(`${this.base}/roles/company`, { params });
  }

  createRoleForTenant(tenantId: number, name: string, code: string): Observable<number> {
    return this.http.post<number>(`${this.base}/tenants/${tenantId}/roles`, { name, code });
  }

  updateRoleForTenant(
    tenantId: number,
    roleId: number,
    name: string,
    isActive: boolean,
    extras?: { displayName?: string | null; description?: string | null; category?: string | null }
  ): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/tenants/${tenantId}/roles/${roleId}`, {
      name,
      isActive,
      displayName: extras?.displayName ?? name,
      description: extras?.description ?? null,
      category: extras?.category ?? null
    });
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

  // Subscription Management (Stage 4)

  getSubscriptionOverview(tenantId: number): Observable<SubscriptionOverview> {
    return this.http.get<SubscriptionOverview>(`${this.base}/tenants/${tenantId}/subscription`);
  }

  getCompanyLicense(tenantId: number): Observable<CompanyLicense> {
    return this.http.get<CompanyLicense>(`${this.base}/tenants/${tenantId}/license`);
  }

  getLicenseSummary(tenantId: number): Observable<CompanyLicense> {
    return this.http.get<CompanyLicense>(`${this.base}/tenants/${tenantId}/license/summary`);
  }

  getSubscriptionCatalog(): Observable<unknown[]> {
    return this.http.get<unknown[]>(`${this.base}/subscriptions/catalog`);
  }

  updateSubscription(tenantId: number, request: UpdateSubscriptionRequest): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/tenants/${tenantId}/subscription/action`, request);
  }

  // Menu Builder (Stage 9)

  getMenuCatalog(): Observable<MenuCatalog> {
    return this.http.get<MenuCatalog>(`${this.base}/menus/catalog`);
  }

  updateMenuModule(id: number, payload: UpdateMenuModulePayload): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/menus/modules/${id}`, payload);
  }

  updateMenuItem(id: number, payload: UpdateMenuItemPayload): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/menus/${id}`, payload);
  }

  createMenuItem(payload: CreateMenuItemPayload): Observable<number> {
    return this.http.post<number>(`${this.base}/menus`, payload);
  }

  deactivateMenuItem(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/menus/${id}`);
  }

  // Workspace Builder (Stage 10)

  getMyWorkspace(): Observable<ResolvedWorkspace> {
    return this.http.get<ResolvedWorkspace>(`${this.base}/workspaces/me`);
  }

  getWorkspaceCatalog(): Observable<WorkspaceDefinition[]> {
    return this.http.get<WorkspaceDefinition[]>(`${this.base}/workspaces/catalog`);
  }

  getCompanyWorkspaces(tenantId: number): Observable<CompanyWorkspace[]> {
    return this.http.get<CompanyWorkspace[]>(`${this.base}/workspaces/company`, {
      params: { tenantId }
    });
  }

  setCompanyWorkspaces(tenantId: number, enabledWorkspaceKeys: string[]): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/workspaces/company`, {
      tenantId,
      enabledWorkspaceKeys
    });
  }

  updateWorkspaceDefinition(key: string, payload: UpdateWorkspaceDefinitionPayload): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/workspaces/${encodeURIComponent(key)}`, payload);
  }

  createWorkspaceDefinition(payload: CreateWorkspaceDefinitionPayload): Observable<string> {
    return this.http.post<string>(`${this.base}/workspaces`, payload);
  }

  deactivateWorkspaceDefinition(key: string): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/workspaces/${encodeURIComponent(key)}`);
  }

  // Dashboard Builder (Stage 11)

  getMyDashboard(audience?: string): Observable<ResolvedDashboard> {
    return this.http.get<ResolvedDashboard>(`${this.base}/dashboards/me`, {
      params: audience ? { audience } : undefined
    });
  }

  getDashboardCatalog(activeOnly = false): Observable<DashboardDefinition[]> {
    return this.http.get<DashboardDefinition[]>(`${this.base}/dashboards/catalog`, {
      params: { activeOnly }
    });
  }

  getDashboardWidgets(activeOnly = false): Observable<DashboardWidgetDefinition[]> {
    return this.http.get<DashboardWidgetDefinition[]>(`${this.base}/dashboards/widgets`, {
      params: { activeOnly }
    });
  }

  getDashboardByKey(key: string): Observable<DashboardDetail> {
    return this.http.get<DashboardDetail>(`${this.base}/dashboards/${encodeURIComponent(key)}`);
  }

  updateDashboardDefinition(key: string, payload: UpdateDashboardDefinitionPayload): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/dashboards/${encodeURIComponent(key)}`, payload);
  }

  updateDashboardLayout(key: string, payload: UpdateDashboardLayoutPayload): Observable<boolean> {
    return this.http.put<boolean>(
      `${this.base}/dashboards/${encodeURIComponent(key)}/layout`,
      payload
    );
  }

  getSecurityCompanyPolicies(tenantId?: number | null): Observable<SecurityPolicyValue[]> {
    const params: Record<string, string | number | boolean> = {};
    if (tenantId != null) params['tenantId'] = tenantId;
    return this.http.get<SecurityPolicyValue[]>(`${this.base}/security/company`, { params });
  }

  getSecurityCatalog(activeOnly = false): Observable<SecurityPolicyDefinition[]> {
    return this.http.get<SecurityPolicyDefinition[]>(`${this.base}/security/catalog`, {
      params: { activeOnly }
    });
  }

  updateSecurityCompanyPolicies(payload: UpdateSecurityCompanyPoliciesPayload): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/security/company`, payload);
  }

  getMySecuritySummary(): Observable<SecurityCompanySummary> {
    return this.http.get<SecurityCompanySummary>(`${this.base}/security/me`);
  }

  searchAuditEvents(filter: AuditEventSearchFilter = {}): Observable<PagedResult<AuditEventListItem>> {
    const params: Record<string, string | number | boolean> = {
      page: filter.page ?? 1,
      pageSize: filter.pageSize ?? 20
    };
    if (filter.tenantId != null) params['tenantId'] = filter.tenantId;
    if (filter.userId != null) params['userId'] = filter.userId;
    if (filter.category) params['category'] = filter.category;
    if (filter.eventKey) params['eventKey'] = filter.eventKey;
    if (filter.entityType) params['entityType'] = filter.entityType;
    if (filter.entityId != null) params['entityId'] = filter.entityId;
    if (filter.severity) params['severity'] = filter.severity;
    if (filter.success != null) params['success'] = filter.success;
    if (filter.fromDate) params['fromDate'] = filter.fromDate;
    if (filter.toDate) params['toDate'] = filter.toDate;
    if (filter.search) params['search'] = filter.search;
    return this.http.get<PagedResult<AuditEventListItem>>(`${this.base}/audit`, { params });
  }

  getAuditCatalog(activeOnly = false): Observable<AuditEventDefinition[]> {
    return this.http.get<AuditEventDefinition[]>(`${this.base}/audit/catalog`, {
      params: { activeOnly }
    });
  }

  getAuditEventById(id: number, tenantId?: number | null): Observable<AuditEventDetail> {
    const params: Record<string, string | number | boolean> = {};
    if (tenantId != null) params['tenantId'] = tenantId;
    return this.http.get<AuditEventDetail>(`${this.base}/audit/${id}`, { params });
  }

  getAuditRetention(tenantId?: number | null): Observable<AuditRetention> {
    const params: Record<string, string | number | boolean> = {};
    if (tenantId != null) params['tenantId'] = tenantId;
    return this.http.get<AuditRetention>(`${this.base}/audit/retention`, { params });
  }

  getRecentAuditEvents(
    tenantId?: number | null,
    userId?: number | null,
    take = 20
  ): Observable<AuditEventListItem[]> {
    const params: Record<string, string | number | boolean> = { take };
    if (tenantId != null) params['tenantId'] = tenantId;
    if (userId != null) params['userId'] = userId;
    return this.http.get<AuditEventListItem[]>(`${this.base}/audit/recent`, { params });
  }

  exportAuditEvents(filter: AuditEventSearchFilter = {}, format: 'csv' | 'excel' = 'csv'): Observable<Blob> {
    const params: Record<string, string | number | boolean> = { format };
    if (filter.tenantId != null) params['tenantId'] = filter.tenantId;
    if (filter.userId != null) params['userId'] = filter.userId;
    if (filter.category) params['category'] = filter.category;
    if (filter.eventKey) params['eventKey'] = filter.eventKey;
    if (filter.entityType) params['entityType'] = filter.entityType;
    if (filter.severity) params['severity'] = filter.severity;
    if (filter.success != null) params['success'] = filter.success;
    if (filter.fromDate) params['fromDate'] = filter.fromDate;
    if (filter.toDate) params['toDate'] = filter.toDate;
    if (filter.search) params['search'] = filter.search;
    return this.http.get(`${this.base}/audit/export`, { params, responseType: 'blob' });
  }
}
