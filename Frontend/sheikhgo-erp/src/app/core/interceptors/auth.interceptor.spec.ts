import { TestBed } from '@angular/core/testing';
import { HttpClient, HTTP_INTERCEPTORS, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Injector } from '@angular/core';
import { AuthInterceptor } from './auth.interceptor';
import { AuthService } from '../services/auth.service';
import { TenantContextService } from '../services/tenant-context.service';
import { environment } from '../../../environments/environment';

describe('AuthInterceptor X-Tenant-Id (SG-06)', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let tenantContext: jasmine.SpyObj<TenantContextService>;
  let auth: jasmine.SpyObj<AuthService>;

  beforeEach(() => {
    tenantContext = jasmine.createSpyObj('TenantContextService', [], {
      overrideTenantId: null as number | null
    });
    auth = jasmine.createSpyObj('AuthService', ['getToken', 'logout', 'refreshToken']);
    auth.getToken.and.returnValue('tok');

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
        { provide: TenantContextService, useValue: tenantContext },
        { provide: Injector, useValue: TestBed },
        { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true }
      ]
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('sets X-Tenant-Id from TenantContextService when override is set', () => {
    Object.defineProperty(tenantContext, 'overrideTenantId', { get: () => 42 });
    http.get(`${environment.apiUrl}/vehicles`).subscribe();
    const req = httpMock.expectOne(`${environment.apiUrl}/vehicles`);
    expect(req.request.headers.get('X-Tenant-Id')).toBe('42');
    req.flush([]);
  });

  it('omits X-Tenant-Id in All / unset mode', () => {
    Object.defineProperty(tenantContext, 'overrideTenantId', { get: () => null });
    http.get(`${environment.apiUrl}/vehicles`).subscribe();
    const req = httpMock.expectOne(`${environment.apiUrl}/vehicles`);
    expect(req.request.headers.has('X-Tenant-Id')).toBeFalse();
    req.flush([]);
  });

  it('strips caller-supplied X-Tenant-Id so interceptor is the only source', () => {
    Object.defineProperty(tenantContext, 'overrideTenantId', { get: () => 9 });
    http.get(`${environment.apiUrl}/vehicles`, {
      headers: { 'X-Tenant-Id': '999' }
    }).subscribe();
    const req = httpMock.expectOne(`${environment.apiUrl}/vehicles`);
    expect(req.request.headers.get('X-Tenant-Id')).toBe('9');
    req.flush([]);
  });
});
