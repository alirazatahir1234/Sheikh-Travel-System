import { BehaviorSubject, of, throwError } from 'rxjs';
import { TenantSwitcherComponent } from './tenant-switcher.component';
import { PlatformService } from '../../../core/services/platform.service';
import { TenantContextService } from '../../../core/services/tenant-context.service';
import { SwitchableTenant } from '../../../core/models/platform.model';

describe('TenantSwitcherComponent', () => {
  const tenant = { id: 2, name: 'Acme Fleet', slug: 'acme', status: 'Active', isActive: true } as SwitchableTenant;

  function create(getSwitchableTenants: jasmine.Spy) {
    const platform = { getSwitchableTenants } as unknown as PlatformService;
    const context = {
      canUseSwitcher: true,
      isAllCompanies: false,
      currentTenantId: null,
      currentTenant: null,
      tenantId$: new BehaviorSubject<number | null>(null),
      isAllCompanies$: new BehaviorSubject(false),
      tenant$: new BehaviorSubject(null)
    } as unknown as TenantContextService;
    const component = new TenantSwitcherComponent(platform, context);
    component.ngOnInit();
    return component;
  }

  it('shows an error state with Retry instead of an empty list when the API fails', () => {
    const component = create(jasmine.createSpy().and.returnValue(throwError(() => ({ status: 500 }))));

    expect(component.loadError).toBeTrue();
    expect(component.loading).toBeFalse();
    expect(component.tenants).toEqual([]);
    expect(component.label).toContain("Couldn't load companies");
  });

  it('retry reloads the list and clears the error', () => {
    const spy = jasmine.createSpy()
      .and.returnValues(throwError(() => ({ status: 500 })), of([tenant]));
    const component = create(spy);

    component.retry();

    expect(spy).toHaveBeenCalledTimes(2);
    expect(component.loadError).toBeFalse();
    expect(component.tenants.map(t => t.id)).toEqual([2]);
    expect(component.label).toBe('Pick a company');
  });
});
