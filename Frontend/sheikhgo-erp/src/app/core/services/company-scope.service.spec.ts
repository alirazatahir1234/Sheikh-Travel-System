import { isScopedArea } from './company-scope.service';

describe('isScopedArea', () => {
  it('keeps the Super Admin company scope inside Drivers and Vehicles', () => {
    expect(isScopedArea('/drivers')).toBeTrue();
    expect(isScopedArea('/drivers/new?tenantId=7')).toBeTrue();
    expect(isScopedArea('/drivers/12/edit')).toBeTrue();
    expect(isScopedArea('/vehicles?tenantId=3')).toBeTrue();
    expect(isScopedArea('/vehicles/5#docs')).toBeTrue();
  });

  it('clears the scope everywhere else', () => {
    expect(isScopedArea('/')).toBeFalse();
    expect(isScopedArea('/dashboard')).toBeFalse();
    expect(isScopedArea('/driversettlements')).toBeFalse();
    expect(isScopedArea('/platform/tenants?tenantId=3')).toBeFalse();
  });
});
