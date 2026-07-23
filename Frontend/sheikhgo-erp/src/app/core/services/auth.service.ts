import { Injectable, NgZone } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, map, tap } from 'rxjs';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { LoginRequest, LoginResponse, LoginResponseDto } from '../models/auth.model';
import { SecurityCompanySummary } from '../models/platform.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly tokenKey = 'access_token';
  private readonly refreshKey = 'refresh_token';
  private readonly userKey = 'current_user';
  private readonly homeRouteKey = 'home_route';
  private readonly loginAtKey = 'session_login_at';

  private currentUserSubject = new BehaviorSubject<LoginResponse | null>(this.getStoredUser());
  currentUser$ = this.currentUserSubject.asObservable();

  private idleTimer: ReturnType<typeof setTimeout> | null = null;
  private absoluteTimer: ReturnType<typeof setTimeout> | null = null;
  private idleMinutes = 0;
  private absoluteMinutes = 0;
  private readonly activityHandler = (): void => this.resetIdleTimer();

  constructor(
    private http: HttpClient,
    private router: Router,
    private zone: NgZone
  ) {
    if (this.isLoggedIn()) {
      // Restore idle watch after refresh if policy was previously applied via login.
      const storedIdle = Number(localStorage.getItem('security_idle_minutes') || 0);
      const storedAbs = Number(localStorage.getItem('security_absolute_minutes') || 0);
      if (storedIdle > 0 || storedAbs > 0) {
        this.startSessionWatch(storedIdle, storedAbs);
      }
    }
  }

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http
      .post<LoginResponseDto>(`${environment.apiUrl}/auth/login`, request)
      .pipe(
        map(dto => this.normalize(dto)),
        tap(user => {
          this.persistSession(user);
          localStorage.setItem(this.loginAtKey, String(Date.now()));
        })
      );
  }

  /** Apply Stage 13 session soft-enforcement from security/me summary. */
  applySecuritySessionPolicy(summary: SecurityCompanySummary | null | undefined): void {
    const idle = summary?.idleTimeoutMinutes ?? 0;
    const absolute = summary?.absoluteTimeoutMinutes ?? 0;
    localStorage.setItem('security_idle_minutes', String(idle || 0));
    localStorage.setItem('security_absolute_minutes', String(absolute || 0));
    this.startSessionWatch(idle, absolute);

    if (summary?.passwordExpired) {
      // Soft banner only — login already blocks when hard-expired server-side.
    }
  }

  /** Persist resolved workspace home route (Stage 10). */
  setHomeRoute(route: string | null | undefined): void {
    const cleaned = (route || '').trim();
    if (!cleaned) {
      localStorage.removeItem(this.homeRouteKey);
      return;
    }
    localStorage.setItem(this.homeRouteKey, cleaned.startsWith('/') ? cleaned : `/${cleaned}`);
  }

  logout(): void {
    this.stopSessionWatch();
    const refreshToken = this.getRefreshToken();
    if (refreshToken) {
      this.http.post(`${environment.apiUrl}/auth/logout`, { refreshToken }).subscribe({
        error: () => { /* swallow — we still want to clear local state. */ }
      });
    }
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.refreshKey);
    localStorage.removeItem(this.userKey);
    localStorage.removeItem(this.homeRouteKey);
    localStorage.removeItem(this.loginAtKey);
    localStorage.removeItem('security_idle_minutes');
    localStorage.removeItem('security_absolute_minutes');
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

  getHomeRoute(): string {
    const stored = localStorage.getItem(this.homeRouteKey);
    if (stored?.startsWith('/')) return stored;
    if (this.hasRole('Driver')) return '/my-trips';
    if (this.hasRole('SUPER_ADMIN') || this.hasRole('SuperAdmin')) return '/platform';
    return '/dashboard';
  }

  hasPermission(permission: string): boolean {
    const user = this.getCurrentUser();
    if (!user) return false;
    if (user.roles?.some(r => r.toUpperCase() === 'SUPER_ADMIN')) return true;
    return user.permissions?.includes(permission) ?? false;
  }

  hasAnyPermission(permissions: string[]): boolean {
    if (!permissions.length) return true;
    if (this.hasRole('SUPER_ADMIN')) return true;
    return permissions.some(p => this.hasPermission(p));
  }

  /** Backend sends role(s) and permissions; normalize for the rest of the app. */
  private normalize(dto: LoginResponseDto): LoginResponse {
    if (!dto?.accessToken) {
      throw new Error('Authentication failed.');
    }
    const roles = dto.roles?.length
      ? dto.roles
      : dto.role
        ? [dto.role]
        : [];
    return {
      accessToken:  dto.accessToken,
      refreshToken: dto.refreshToken,
      fullName:     dto.fullName,
      roles,
      permissions:  dto.permissions ?? [],
      userId:       dto.userId,
      tenantId:     dto.tenantId,
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

  private startSessionWatch(idleMinutes: number, absoluteMinutes: number): void {
    this.stopSessionWatch();
    this.idleMinutes = Math.max(0, idleMinutes || 0);
    this.absoluteMinutes = Math.max(0, absoluteMinutes || 0);

    if (this.idleMinutes > 0) {
      this.zone.runOutsideAngular(() => {
        window.addEventListener('mousemove', this.activityHandler, { passive: true });
        window.addEventListener('keydown', this.activityHandler, { passive: true });
        window.addEventListener('click', this.activityHandler, { passive: true });
        window.addEventListener('touchstart', this.activityHandler, { passive: true });
      });
      this.resetIdleTimer();
    }

    if (this.absoluteMinutes > 0) {
      const loginAt = Number(localStorage.getItem(this.loginAtKey) || Date.now());
      const elapsed = Date.now() - loginAt;
      const remaining = this.absoluteMinutes * 60_000 - elapsed;
      if (remaining <= 0) {
        this.zone.run(() => this.logout());
        return;
      }
      this.absoluteTimer = setTimeout(() => {
        this.zone.run(() => this.logout());
      }, remaining);
    }
  }

  private resetIdleTimer(): void {
    if (this.idleMinutes <= 0) return;
    if (this.idleTimer) clearTimeout(this.idleTimer);
    this.idleTimer = setTimeout(() => {
      this.zone.run(() => this.logout());
    }, this.idleMinutes * 60_000);
  }

  private stopSessionWatch(): void {
    if (this.idleTimer) {
      clearTimeout(this.idleTimer);
      this.idleTimer = null;
    }
    if (this.absoluteTimer) {
      clearTimeout(this.absoluteTimer);
      this.absoluteTimer = null;
    }
    window.removeEventListener('mousemove', this.activityHandler);
    window.removeEventListener('keydown', this.activityHandler);
    window.removeEventListener('click', this.activityHandler);
    window.removeEventListener('touchstart', this.activityHandler);
  }
}
