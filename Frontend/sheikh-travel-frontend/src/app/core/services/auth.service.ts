import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, map, tap } from 'rxjs';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { LoginRequest, LoginResponse, LoginResponseDto } from '../models/auth.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly tokenKey = 'access_token';
  private readonly refreshKey = 'refresh_token';
  private readonly userKey = 'current_user';

  private currentUserSubject = new BehaviorSubject<LoginResponse | null>(this.getStoredUser());
  currentUser$ = this.currentUserSubject.asObservable();

  constructor(private http: HttpClient, private router: Router) {}

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http
      .post<LoginResponseDto>(`${environment.apiUrl}/auth/login`, request)
      .pipe(
        map(dto => this.normalize(dto)),
        tap(user => this.persistSession(user))
      );
  }

  logout(): void {
    const refreshToken = this.getRefreshToken();
    if (refreshToken) {
      this.http.post(`${environment.apiUrl}/auth/logout`, { refreshToken }).subscribe({
        error: () => { /* swallow — we still want to clear local state. */ }
      });
    }
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.refreshKey);
    localStorage.removeItem(this.userKey);
    this.currentUserSubject.next(null);
    this.router.navigate(['/auth/login']);
  }

  refreshToken(): Observable<LoginResponse> {
    return this.http
      .post<LoginResponseDto>(`${environment.apiUrl}/auth/refresh-token`, {
        refreshToken: this.getRefreshToken()
      })
      .pipe(
        map(dto => this.normalize(dto)),
        tap(user => this.persistSession(user))
      );
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(this.refreshKey);
  }

  isLoggedIn(): boolean {
    return !!this.getToken();
  }

  getCurrentUser(): LoginResponse | null {
    return this.currentUserSubject.value;
  }

  hasRole(role: string): boolean {
    const user = this.getCurrentUser();
    return user?.roles?.includes(role) ?? false;
  }

  /** Backend sends a single `role` string; normalize to `roles[]` for the rest of the app. */
  private normalize(dto: LoginResponseDto): LoginResponse {
    if (!dto?.accessToken) {
      throw new Error('Authentication failed.');
    }
    return {
      accessToken:  dto.accessToken,
      refreshToken: dto.refreshToken,
      fullName:     dto.fullName,
      roles:        dto.role ? [dto.role] : [],
      email:        dto.email,
      phoneNumber:  dto.phoneNumber,
    };
  }

  persistSession(user: LoginResponse): void {
    localStorage.setItem(this.tokenKey,   user.accessToken);
    localStorage.setItem(this.refreshKey, user.refreshToken);
    localStorage.setItem(this.userKey,    JSON.stringify(user));
    this.currentUserSubject.next(user);
  }

  private getStoredUser(): LoginResponse | null {
    const stored = localStorage.getItem(this.userKey);
    if (!stored) return null;
    try { return JSON.parse(stored) as LoginResponse; }
    catch { return null; }
  }
}
