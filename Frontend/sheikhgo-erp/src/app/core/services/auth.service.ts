import { Injectable, Injector, NgZone } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, finalize, map, shareReplay, tap } from 'rxjs';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { LoginRequest, LoginResponse, LoginResponseDto } from '../models/auth.model';
import { SecurityCompanySummary } from '../models/platform.model';
import { isDriverPrimaryAccount, orderRolesByPrivilege } from '../auth/role-priority';
import { TenantContextService } from './tenant-context.service';
import { AppConfigService } from './app-config.service';

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

  /** One refresh at a time: the refresh token rotates, so a second concurrent call would fail. */
  private refreshInFlight$: Observable<LoginResponse> | null = null;

  constructor(
    private http: HttpClient,
    private router: Router,
    private zone: NgZone,
    private injector: Injector,
    private config: AppConfigService
  ) {
    if (this.usesCookieSession && this.getRefreshToken()) {
      // Signed in before cookie sessions were switched on: trade the localStorage tokens for
      // HttpOnly cookies now, so they do not sit in script-readable storage any longer.
      setTimeout(() => this.refreshToken().subscribe({ error: () => { /* the 401 path handles it */ } }));
    }
    if (this.isLoggedIn()) {
      // Restore idle watch after refresh if policy was previously applied via login.
      const storedIdle = Number(localStorage.getItem('security_idle_minutes') || 0);
      const storedAbs = Number(localStorage.getItem('security_absolute_minutes') || 0);
      if (storedIdle > 0 || storedAbs > 0) {
        this.startSessionWatch(storedIdle, storedAbs);
      }
    }
  }

  /**
   * SG-54: session tokens are HttpOnly cookies set by the API. Page scripts (and so any XSS)
   * cannot read them; only the non-secret user profile is kept in localStorage.
   */
  get usesCookieSession(): boolean {
    return this.config.authMode === 'cookie';
  }

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http
      .post<LoginResponseDto>(
        `${environment.apiUrl}/auth/login`,
        this.usesCookieSession ? { ...request, useCookies: true } : request)
      .pipe(
        map(dto => this.normalize(dto)),
        tap(user => {
          this.persistSession(user);
          localStorage.setItem(this.loginAtKey, String(Date.now()));
        })
      );
  }

  forgotPassword(email: string): Observable<{ success?: boolean; message?: string }> {
    return this.http.post<{ success?: boolean; message?: string }>(
      `${environment.apiUrl}/auth/forgot-password`,
      { email }
    );
  }

  resetPassword(token: string, newPassword: string): Observable<{ success?: boolean; message?: string }> {
    return this.http.post<{ success?: boolean; message?: string }>(
      `${environment.apiUrl}/auth/reset-password`,
      { token, newPassword }
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
    let normalized = cleaned.startsWith('/') ? cleaned : `/${cleaned}`;
    // Administrators land on the operational dashboard, not the platform hub.
    if (normalized === '/platform' || normalized === '/platform/') {
      normalized = '/dashboard';
    }
    localStorage.setItem(this.homeRouteKey, normalized);
  }

  logout(): void {
    this.stopSessionWatch();
    const refreshToken = this.getRefreshToken();
    // Cookie session: always tell the API, so it clears the HttpOnly cookies.
    if (refreshToken || (this.usesCookieSession && this.getCurrentUser())) {
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
    try {
      this.injector.get(TenantContextService).clear();
    } catch {
      /* tenant context optional during early bootstrap */
    }
    // Always leave the app shell when the session ends (401 / idle / absolute).
    this.zone.run(() => {
      void this.router.navigateByUrl('/auth/login', { replaceUrl: true });
    });
  }

  refreshToken(): Observable<LoginResponse> {
    if (!this.refreshInFlight$) {
      const storedToken = this.getRefreshToken();
      // Cookie session: the API reads the refresh token from its cookie. A token still in
      // localStorage (pre-cookie sign-in) is sent once so the API can swap it for cookies.
      const body = this.usesCookieSession
        ? { useCookies: true, ...(storedToken ? { refreshToken: storedToken } : {}) }
        : { refreshToken: storedToken };
      this.refreshInFlight$ = this.http
        .post<LoginResponseDto>(`${environment.apiUrl}/auth/refresh-token`, body)
        .pipe(
          map(dto => this.normalize(dto)),
          tap(user => this.persistSession(user)),
          finalize(() => { this.refreshInFlight$ = null; }),
          shareReplay({ bufferSize: 1, refCount: false })
        );
    }
    return this.refreshInFlight$;
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(this.refreshKey);
  }

  isLoggedIn(): boolean {
    const token = this.getToken();
    if (!token) {
      // Cookie session: the token is not readable here. The stored profile marks the session
      // and the API has the final say (401 → refresh → sign out).
      return this.usesCookieSession && !!this.getCurrentUser();
    }
    const exp = this.readJwtExpSeconds(token);
    if (exp == null) return true; // opaque / non-JWT — defer to API
    // Access token expired: still treat as logged-in when refresh can recover the session.
    if (exp * 1000 <= Date.now()) {
      return !!this.getRefreshToken();
    }
    return true;
  }

  /** Driver / Field Driver workspace accounts (primary role only). */
  isDriverUser(): boolean {
    return isDriverPrimaryAccount(this.getCurrentUser()?.roles);
  }

  getCurrentUser(): LoginResponse | null {
    return this.currentUserSubject.value;
  }

  hasRole(role: string): boolean {
    const user = this.getCurrentUser();
    if (!user?.roles?.length || !role) return false;
    const needle = role.toUpperCase().replace(/[\s-]/g, '_');
    return user.roles.some(r => (r || '').toUpperCase().replace(/[\s-]/g, '_') === needle);
  }

  getHomeRoute(): string {
    const stored = localStorage.getItem(this.homeRouteKey);
    if (stored?.startsWith('/')) {
      // Platform hub is not the default landing for administrators.
      if (stored === '/platform' || stored === '/platform/') return '/dashboard';
      return stored;
    }
    const roles = this.getCurrentUser()?.roles ?? [];
    const primary = orderRolesByPrivilege(roles)[0]?.toUpperCase() ?? '';
    if (primary === 'DRIVER' || primary === 'FIELD_DRIVER') return '/my-trips';
    return '/dashboard';
  }

  hasPermission(permission: string): boolean {
    const user = this.getCurrentUser();
    if (!user) return false;
    // Authorization is by permission only (Backend/CLAUDE.md). The platform operator's permissions arrive in the
    // token like everyone else's; a role name never grants anything here, so the UI can never show more than the API allows.
    return user.permissions?.includes(permission) ?? false;
  }

  hasAnyPermission(permissions: string[]): boolean {
    if (!permissions.length) return true;
    return permissions.some(p => this.hasPermission(p));
  }

  /** Backend sends role(s) and permissions; normalize for the rest of the app. */
  private normalize(dto: LoginResponseDto): LoginResponse {
    // Cookie session: the tokens are in HttpOnly cookies, so a valid reply carries the profile only.
    const cookieReply = this.usesCookieSession && !!dto && !!(dto.fullName || dto.role || dto.roles?.length);
    if (!dto?.accessToken && !cookieReply) {
      throw new Error('Authentication failed.');
    }
    const rawRoles = dto.roles?.length
      ? dto.roles
      : dto.role
        ? [dto.role]
        : [];
    // Highest privilege first — never Role.Id / insertion order (hides sidebar items).
    const roles = orderRolesByPrivilege(rawRoles);
    return {
      accessToken:  dto.accessToken ?? '',
      refreshToken: dto.refreshToken ?? '',
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
    // The API blanks both tokens when it has put them in HttpOnly cookies. If tokens still
    // arrive (an API that predates cookie sessions), keep working the bearer way.
    if (this.usesCookieSession && !user.accessToken) {
      localStorage.removeItem(this.tokenKey);
      localStorage.removeItem(this.refreshKey);
      user = { ...user, accessToken: '', refreshToken: '' };
    } else {
      localStorage.setItem(this.tokenKey,   user.accessToken);
      localStorage.setItem(this.refreshKey, user.refreshToken);
    }
    localStorage.setItem(this.userKey,    JSON.stringify(user));
    this.currentUserSubject.next(user);
  }

  private getStoredUser(): LoginResponse | null {
    const stored = localStorage.getItem(this.userKey);
    if (!stored) return null;
    try {
      const user = JSON.parse(stored) as LoginResponse;
      // Re-order roles for sessions persisted before privilege sort existed.
      if (user?.roles?.length) {
        user.roles = orderRolesByPrivilege(user.roles);
      }
      return user;
    }
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

  /** Best-effort JWT `exp` (seconds). Returns null when token is not a readable JWT. */
  private readJwtExpSeconds(token: string): number | null {
    try {
      const parts = token.split('.');
      if (parts.length < 2) return null;
      const payload = parts[1].replace(/-/g, '+').replace(/_/g, '/');
      const padded = payload + '='.repeat((4 - (payload.length % 4)) % 4);
      const json = JSON.parse(atob(padded)) as { exp?: unknown };
      const exp = Number(json?.exp);
      return Number.isFinite(exp) ? exp : null;
    } catch {
      return null;
    }
  }
}
