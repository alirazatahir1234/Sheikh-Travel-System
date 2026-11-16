import { TestBed } from '@angular/core/testing';
import { TenantContextService } from './tenant-context.service';
import { AuthService } from './auth.service';

describe('TenantContextService', () => {
  let service: TenantContextService;
  let auth: jasmine.SpyObj<AuthService>;

  beforeEach(() => {
    localStorage.removeItem('selectedTenantId');
    auth = jasmine.createSpyObj('AuthService', ['hasPermission']);
    auth.hasPermission.and.callFake((p: string) =>
      p === 'Platform.Tenants.View' || p === 'Platform.Tenants.DataRead'
    );

    TestBed.configureTestingModule({
      providers: [
        TenantContextService,
        { provide: AuthService, useValue: auth }
      ]
    });
    service = TestBed.inject(TenantContextService);
  });

  afterEach(() => localStorage.removeItem('selectedTenantId'));

  it('exposes the switcher when Platform.Tenants.View is held', () => {
    expect(service.canUseSwitcher).toBeTrue();
  });

  it('hides the switcher without platform tenant permissions', () => {
    auth.hasPermission.and.returnValue(false);
    expect(service.canUseSwitcher).toBeFalse();
    expect(service.overrideTenantId).toBeNull();
  });

  it('sets overrideTenantId only for a concrete company', () => {
    service.selectTenant({
      id: 7,
      name: 'Hannan',
      slug: 'hannan',
      isActive: true,
      branchCount: 0,
      departmentCount: 0,
      roleCount: 0,
      createdAt: '',
      activeUserCount: 0,
      activeVehicleCount: 0
    });
    expect(service.hasConcreteTenant).toBeTrue();
    expect(service.canMutate).toBeTrue();
    expect(service.overrideTenantId).toBe(7);
  });

  it('All companies omits override and blocks mutations', () => {
    service.selectAll();
    expect(service.isAllCompanies).toBeTrue();
    expect(service.hasConcreteTenant).toBeFalse();
    expect(service.canMutate).toBeFalse();
    expect(service.overrideTenantId).toBeNull();
    expect(localStorage.getItem('selectedTenantId')).toBe('all');
  });

  it('clear removes storage', () => {
    service.selectTenantById(3);
    service.clear();
    expect(service.currentTenantId).toBeNull();
    expect(localStorage.getItem('selectedTenantId')).toBeNull();
  });
});
