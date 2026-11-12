import { TestBed } from '@angular/core/testing';
import { BehaviorSubject } from 'rxjs';
import { AuthService } from './auth.service';
import { COMPANY_SCOPE_STORAGE_KEY, CompanyScopeService, isScopedArea } from './company-scope.service';

describe('isScopedArea', () => {
  it('recognises Drivers and Vehicles list/detail routes', () => {
    expect(isScopedArea('/drivers')).toBeTrue();
    expect(isScopedArea('/drivers/new?tenantId=7')).toBeTrue();
    expect(isScopedArea('/drivers/12/edit')).toBeTrue();
    expect(isScopedArea('/vehicles?tenantId=3')).toBeTrue();
    expect(isScopedArea('/vehicles/5#docs')).toBeTrue();
  });

  it('returns false outside Drivers / Vehicles', () => {
    expect(isScopedArea('/')).toBeFalse();
    expect(isScopedArea('/dashboard')).toBeFalse();
    expect(isScopedArea('/driversettlements')).toBeFalse();
    expect(isScopedArea('/platform/tenants?tenantId=3')).toBeFalse();
    expect(isScopedArea('/gps-tracking/devices')).toBeFalse();
  });
});

describe('CompanyScopeService', () => {
  const currentUser$ = new BehaviorSubject<{ id: number } | null>({ id: 1 });

  beforeEach(() => {
    sessionStorage.removeItem(COMPANY_SCOPE_STORAGE_KEY);
    currentUser$.next({ id: 1 });
    TestBed.configureTestingModule({
      providers: [
        CompanyScopeService,
        { provide: AuthService, useValue: { currentUser$: currentUser$.asObservable() } }
      ]
    });
  });

  afterEach(() => {
    sessionStorage.removeItem(COMPANY_SCOPE_STORAGE_KEY);
  });

  it('persists selection to sessionStorage and restores it', () => {
    const scope = TestBed.inject(CompanyScopeService);
    expect(scope.tenantId()).toBeNull();

    scope.set(42);
    expect(scope.tenantId()).toBe(42);
    expect(sessionStorage.getItem(COMPANY_SCOPE_STORAGE_KEY)).toBe('42');

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        CompanyScopeService,
        { provide: AuthService, useValue: { currentUser$: currentUser$.asObservable() } }
      ]
    });
    const restored = TestBed.inject(CompanyScopeService);
    expect(restored.tenantId()).toBe(42);

    restored.clear();
    expect(restored.tenantId()).toBeNull();
    expect(sessionStorage.getItem(COMPANY_SCOPE_STORAGE_KEY)).toBeNull();
  });

  it('clears scope when the user session ends', () => {
    const scope = TestBed.inject(CompanyScopeService);
    scope.set(7);
    expect(scope.tenantId()).toBe(7);

    currentUser$.next(null);
    expect(scope.tenantId()).toBeNull();
    expect(sessionStorage.getItem(COMPANY_SCOPE_STORAGE_KEY)).toBeNull();
  });
});
