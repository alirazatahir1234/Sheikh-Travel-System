import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { AppConfigService, AuthMode } from './app-config.service';
import { TenantContextService } from './tenant-context.service';
import { environment } from '../../../environments/environment';

/** SG-54: in cookie mode no session token may ever land in script-readable storage. */
describe('AuthService session storage', () => {
  const KEYS = ['access_token', 'refresh_token', 'current_user', 'home_route', 'session_login_at'];
  const profile = { fullName: 'Test User', role: 'Admin', roles: ['ADMIN'], permissions: ['GPS.View'], userId: 7 };
  let http: HttpTestingController;

  function create(authMode: AuthMode): AuthService {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        { provide: AppConfigService, useValue: { authMode } },
        { provide: Router, useValue: { navigateByUrl: () => Promise.resolve(true) } },
        { provide: TenantContextService, useValue: { clear: () => undefined } }
      ]
    });
    http = TestBed.inject(HttpTestingController);
    return TestBed.inject(AuthService);
  }

  beforeEach(() => KEYS.forEach(k => localStorage.removeItem(k)));
  afterEach(() => {
    http.verify();
    KEYS.forEach(k => localStorage.removeItem(k));
  });

  describe('cookie mode', () => {
    it('asks the API for cookies and stores no token after sign-in', () => {
      const service = create('cookie');

      service.login({ email: 'user@example.com', password: 'pw' }).subscribe();
      const req = http.expectOne(`${environment.apiUrl}/auth/login`);
      expect(req.request.body.useCookies).toBeTrue();
      req.flush({ ...profile, accessToken: '', refreshToken: '' });

      expect(localStorage.getItem('access_token')).toBeNull();
      expect(localStorage.getItem('refresh_token')).toBeNull();
      expect(service.getToken()).toBeNull();
      expect(service.isLoggedIn()).toBeTrue();
      expect(service.getCurrentUser()?.fullName).toBe('Test User');
      expect(localStorage.getItem('current_user')).not.toContain('jwt');
    });

    it('swaps tokens left in localStorage for cookies on the next refresh', () => {
      localStorage.setItem('access_token', 'legacy-access');
      localStorage.setItem('refresh_token', 'legacy-refresh');
      const service = create('cookie');

      service.refreshToken().subscribe();
      const req = http.expectOne(`${environment.apiUrl}/auth/refresh-token`);
      expect(req.request.body).toEqual({ useCookies: true, refreshToken: 'legacy-refresh' });
      req.flush({ ...profile, accessToken: '', refreshToken: '' });

      expect(localStorage.getItem('access_token')).toBeNull();
      expect(localStorage.getItem('refresh_token')).toBeNull();
    });

    it('sends no token on refresh once the session is cookie-only', () => {
      const service = create('cookie');

      service.refreshToken().subscribe();
      const req = http.expectOne(`${environment.apiUrl}/auth/refresh-token`);
      expect(req.request.body).toEqual({ useCookies: true });
      req.flush({ ...profile, accessToken: '', refreshToken: '' });
    });

    it('keeps working the bearer way when the API still returns tokens', () => {
      const service = create('cookie');

      service.login({ email: 'user@example.com', password: 'pw' }).subscribe();
      http.expectOne(`${environment.apiUrl}/auth/login`)
        .flush({ ...profile, accessToken: 'old-api-access', refreshToken: 'old-api-refresh' });

      expect(service.getToken()).toBe('old-api-access');
      expect(service.isLoggedIn()).toBeTrue();
    });

    it('tells the API on sign-out so it can clear the cookies', () => {
      const service = create('cookie');
      service.persistSession({ ...profile, accessToken: '', refreshToken: '' } as never);

      service.logout();

      http.expectOne(`${environment.apiUrl}/auth/logout`).flush({ success: true });
      expect(service.isLoggedIn()).toBeFalse();
      expect(localStorage.getItem('current_user')).toBeNull();
    });
  });

  it('still rejects a reply with neither a token nor a profile', () => {
    const service = create('cookie');
    let failed = false;

    service.login({ email: 'user@example.com', password: 'pw' }).subscribe({ error: () => (failed = true) });
    http.expectOne(`${environment.apiUrl}/auth/login`).flush({ accessToken: '', refreshToken: '' });

    expect(failed).toBeTrue();
    expect(service.isLoggedIn()).toBeFalse();
  });

  it('bearer mode rejects a reply without a token', () => {
    const service = create('bearer');
    let failed = false;

    service.login({ email: 'user@example.com', password: 'pw' }).subscribe({ error: () => (failed = true) });
    http.expectOne(`${environment.apiUrl}/auth/login`).flush({ ...profile, accessToken: '', refreshToken: '' });

    expect(failed).toBeTrue();
  });

  describe('bearer mode (unchanged)', () => {
    it('stores tokens and does not ask for cookies', () => {
      const service = create('bearer');

      service.login({ email: 'user@example.com', password: 'pw' }).subscribe();
      const req = http.expectOne(`${environment.apiUrl}/auth/login`);
      expect(req.request.body.useCookies).toBeUndefined();
      req.flush({ ...profile, accessToken: 'access', refreshToken: 'refresh' });

      expect(localStorage.getItem('access_token')).toBe('access');
      expect(localStorage.getItem('refresh_token')).toBe('refresh');
    });

    it('is signed out when there is a profile but no token', () => {
      localStorage.setItem('current_user', JSON.stringify(profile));

      expect(create('bearer').isLoggedIn()).toBeFalse();
    });
  });

  it('shares one request between concurrent refresh calls', () => {
    localStorage.setItem('refresh_token', 'refresh');
    const service = create('bearer');

    service.refreshToken().subscribe();
    service.refreshToken().subscribe();

    http.expectOne(`${environment.apiUrl}/auth/refresh-token`)
      .flush({ ...profile, accessToken: 'access-2', refreshToken: 'refresh-2' });
    expect(localStorage.getItem('refresh_token')).toBe('refresh-2');
  });
});
