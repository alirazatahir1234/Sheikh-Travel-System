import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/common.model';
import {
  User,
  CreateUserRequest,
  UpdateUserRequest,
  UpdateUserStatusRequest,
  ResetPasswordResponse,
  CompanyUserSummary,
  UserListFilters,
  AssignedRole,
  SetUserRolesRequest,
  parseUserRole
} from '../models/user.model';
import { EffectivePermission, CompanyDataScope } from '../models/platform.model';

@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly base = `${environment.apiUrl}/users`;

  constructor(private http: HttpClient) {}

  getAll(
    page = 1,
    pageSize = 20,
    tenantId?: number | null,
    filters?: UserListFilters
  ): Observable<PagedResult<User>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    const tid = filters?.tenantId ?? tenantId;
    if (tid != null) params = params.set('tenantId', tid);
    if (filters?.branchId != null) params = params.set('branchId', filters.branchId);
    if (filters?.departmentId != null) params = params.set('departmentId', filters.departmentId);
    if (filters?.status) params = params.set('status', filters.status);
    if (filters?.employeeType) params = params.set('employeeType', filters.employeeType);
    if (filters?.search) params = params.set('search', filters.search);

    return this.http.get<PagedResult<User>>(this.base, { params }).pipe(
      map(result => ({
        ...result,
        items: result.items.map(user => this.normalizeUser(user)),
      }))
    );
  }

  getById(id: number): Observable<User> {
    return this.http.get<User>(`${this.base}/${id}`).pipe(
      map(user => this.normalizeUser(user))
    );
  }

  getUserRoles(userId: number): Observable<AssignedRole[]> {
    return this.http.get<AssignedRole[]>(`${this.base}/${userId}/roles`);
  }

  getUserPermissions(userId: number): Observable<EffectivePermission[]> {
    return this.http.get<EffectivePermission[]>(`${this.base}/${userId}/permissions`);
  }

  getUserDataScope(userId: number): Observable<CompanyDataScope> {
    return this.http.get<CompanyDataScope>(`${this.base}/${userId}/data-scope`);
  }

  setUserRoles(userId: number, request: SetUserRolesRequest): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${userId}/roles`, request);
  }

  getCompanySummary(tenantId?: number | null): Observable<CompanyUserSummary> {
    let params = new HttpParams();
    if (tenantId != null) params = params.set('tenantId', tenantId);
    return this.http.get<CompanyUserSummary>(`${this.base}/company/summary`, { params });
  }

  private normalizeUser(user: User): User {
    return {
      ...user,
      role: parseUserRole(user.role),
      status: user.status || (user.isActive ? 'Active' : 'Inactive'),
    };
  }

  create(request: CreateUserRequest): Observable<number> {
    return this.http.post<number>(this.base, request);
  }

  update(request: UpdateUserRequest): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${request.id}`, request);
  }

  updateStatus(request: UpdateUserStatusRequest): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${request.id}/status`, {
      isActive: request.isActive,
      status: request.status
    });
  }

  resetPassword(id: number): Observable<ResetPasswordResponse> {
    return this.http.post<ResetPasswordResponse>(`${this.base}/${id}/reset-password`, {});
  }

  delete(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/${id}`);
  }
}
