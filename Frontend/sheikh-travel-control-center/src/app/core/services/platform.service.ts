import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Branch,
  BranchPayload,
  Department,
  DepartmentPayload,
  Permission,
  PlatformRole,
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
}
