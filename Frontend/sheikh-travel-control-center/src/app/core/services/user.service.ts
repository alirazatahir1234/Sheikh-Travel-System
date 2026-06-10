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
  parseUserRole
} from '../models/user.model';

/**
 * Front door to the /api/users endpoints (Admin-only).
 *
 * Capabilities:
 * - List all users (paginated)
 * - Get single user by ID
 * - Create new user
 * - Update existing user (profile + role)
 * - Toggle active status (activate/deactivate)
 * - Reset password (returns temporary password)
 * - Delete user (soft delete)
 */
@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly base = `${environment.apiUrl}/users`;

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 20, tenantId?: number | null): Observable<PagedResult<User>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (tenantId != null) {
      params = params.set('tenantId', tenantId);
    }
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

  private normalizeUser(user: User): User {
    return {
      ...user,
      role: parseUserRole(user.role),
    };
  }

  create(request: CreateUserRequest): Observable<number> {
    return this.http.post<number>(this.base, request);
  }

  update(request: UpdateUserRequest): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${request.id}`, request);
  }

  updateStatus(request: UpdateUserStatusRequest): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${request.id}/status`, { isActive: request.isActive });
  }

  resetPassword(id: number): Observable<ResetPasswordResponse> {
    return this.http.post<ResetPasswordResponse>(`${this.base}/${id}/reset-password`, {});
  }

  delete(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/${id}`);
  }
}
