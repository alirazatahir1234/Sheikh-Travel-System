import { isRouteAllowedForPrimaryRole, resolveMenu } from '../../core/navigation/menu-config';
import { TenantType } from '../../core/navigation/tenant-type';

describe('TravelNexus navigation', () => {
  it('shows the TravelNexus group with Enquiries, Customers, Visa Management, Umrah Groups, Packages, Bookings, Suppliers, Invoices & Payments and Settings when the travel-agency module is enabled', () => {
    const menu = resolveMenu({
      tenantType: TenantType.TravelAgency,
      roles: ['TENANT_ADMIN'],
      enabledModules: ['dashboard', 'travel-agency']
    });

    const group = menu.groups.find(g => g.id === 'travel-nexus');
    expect(group?.label).toBe('TravelNexus');
    expect(group!.items.map(i => i.route)).toEqual(['/travel/enquiries', '/travel/customers', '/travel/visa-cases', '/travel/checklists', '/travel/groups', '/travel/packages', '/travel/bookings', '/travel/suppliers', '/travel/finance', '/travel/settings']);
  });

  it('hides TravelNexus when the company does not have the module', () => {
    const menu = resolveMenu({
      tenantType: TenantType.TravelAgency,
      roles: ['TENANT_ADMIN'],
      enabledModules: ['dashboard', 'bookings']
    });

    expect(menu.groups.find(g => g.id === 'travel-nexus')).toBeUndefined();
  });

  it('never shows TravelNexus to fleet operators', () => {
    const menu = resolveMenu({
      tenantType: TenantType.FleetOperator,
      roles: ['OWNER'],
      enabledModules: ['dashboard', 'travel-agency']
    });

    expect(menu.groups.find(g => g.id === 'travel-nexus')).toBeUndefined();
  });

  it('allows owners and admins on /travel routes but not drivers or GPS operators', () => {
    expect(isRouteAllowedForPrimaryRole('/travel/customers', 'OWNER')).toBeTrue();
    expect(isRouteAllowedForPrimaryRole('/travel/settings', 'TENANT_ADMIN')).toBeTrue();
    expect(isRouteAllowedForPrimaryRole('/travel/customers', 'DRIVER')).toBeFalse();
    expect(isRouteAllowedForPrimaryRole('/travel/customers', 'GPS_OPERATOR')).toBeFalse();
  });
});
