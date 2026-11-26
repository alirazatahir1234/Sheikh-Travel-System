import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { firstValueFrom, isObservable, Observable, of } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { TenantConfigService } from '../services/tenant-config.service';
import { UiToastService } from '../../shared/components/ui/toast/ui-toast.service';
import { DENY_MESSAGES, permissionGuard } from './permission.guard';

describe('permissionGuard', () => {
  let auth: jasmine.SpyObj<AuthService>;
  let tenantConfig: jasmine.SpyObj<TenantConfigService>;
  let toast: jasmine.SpyObj<UiToastService>;

  // Same shape as the /travel route in app-routing.module.ts.
  const travelData = { permissions: ['Travel.View'], modules: ['travel-agency'] };

  beforeEach(() => {
    auth = jasmine.createSpyObj<AuthService>('AuthService',
      ['isLoggedIn', 'hasRole', 'getCurrentUser', 'hasAnyPermission', 'getHomeRoute']);
    auth.isLoggedIn.and.returnValue(true);
    auth.getCurrentUser.and.returnValue({ roles: ['TENANT_ADMIN'] } as ReturnType<AuthService['getCurrentUser']>);
    auth.hasAnyPermission.and.returnValue(true);
    auth.getHomeRoute.and.returnValue('/dashboard');

    tenantConfig = jasmine.createSpyObj<TenantConfigService>('TenantConfigService', ['loadBranding']);
    tenantConfig.loadBranding.and.returnValue(of({ enabledModules: ['travel-agency'] } as never));

    toast = jasmine.createSpyObj<UiToastService>('UiToastService', ['warning']);
    spyOn(console, 'warn');

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: TenantConfigService, useValue: tenantConfig },
        { provide: UiToastService, useValue: toast }
      ]
    });
  });

  async function run(url: string, data: Record<string, unknown>): Promise<boolean | UrlTree> {
    const route = { data } as unknown as ActivatedRouteSnapshot;
    const state = { url } as RouterStateSnapshot;
    const result = TestBed.runInInjectionContext(() => permissionGuard(route, state));
    return isObservable(result)
      ? firstValueFrom(result as Observable<boolean | UrlTree>)
      : (result as boolean | UrlTree);
  }

  function urlOf(result: boolean | UrlTree): string {
    return TestBed.inject(Router).serializeUrl(result as UrlTree);
  }

  it('allows the route when permission and module are present, without a toast', async () => {
    expect(await run('/travel/enquiries', travelData)).toBeTrue();
    expect(toast.warning).not.toHaveBeenCalled();
  });

  it('explains a module that is not enabled for the company instead of bouncing silently', async () => {
    tenantConfig.loadBranding.and.returnValue(of({ enabledModules: ['bookings', 'gps'] } as never));

    const result = await run('/travel/enquiries', travelData);

    expect(urlOf(result)).toBe('/dashboard');
    expect(toast.warning).toHaveBeenCalledOnceWith(DENY_MESSAGES.module, 'Page not opened');
  });

  it('explains a missing permission', async () => {
    auth.hasAnyPermission.and.returnValue(false);

    const result = await run('/travel/enquiries', travelData);

    expect(urlOf(result)).toBe('/dashboard');
    expect(toast.warning).toHaveBeenCalledOnceWith(DENY_MESSAGES.permission, 'Page not opened');
    expect(tenantConfig.loadBranding).not.toHaveBeenCalled();
  });

  it('explains a route outside the user\'s role domain', async () => {
    const result = await run('/platform/tenants', { permissions: [] });

    expect(urlOf(result)).toBe('/dashboard');
    expect(toast.warning).toHaveBeenCalledOnceWith(DENY_MESSAGES.role, 'Page not opened');
  });

  it('does not toast when the blocked route is the home route itself (no toast per redirect)', async () => {
    auth.hasAnyPermission.and.returnValue(false);

    const result = await run('/dashboard?tab=1', { permissions: ['Platform.Dashboard.View'] });

    expect(urlOf(result)).toBe('/dashboard');
    expect(toast.warning).not.toHaveBeenCalled();
  });
});
