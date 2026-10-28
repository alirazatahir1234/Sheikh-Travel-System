import { groupContainingRoute, resolveMenu } from './menu-config';
import { TenantType } from './tenant-type';
import { NavGroup, ResolvedMenu } from './nav-models';

describe('resolveMenu Fleet Operations (travel)', () => {
  it('nests Trips and Routes under Fleet Operations for travel agency', () => {
    const menu = resolveMenu({
      tenantType: TenantType.TravelAgency,
      roles: ['SUPER_ADMIN'],
      enabledModules: ['dashboard', 'bookings', 'trips', 'routes']
    });

    const ops = menu.groups.find(g => g.id === 'operations');
    expect(ops?.items.map(i => i.id)).toEqual(['dispatch-board', 'trip-smart-assignment']);

    const fleetOps = menu.groups.find(g => g.id === 'travel-fleet-operations');
    expect(fleetOps?.label).toBe('Fleet Operations');
    expect(fleetOps?.collapsible).toBe(true);
    expect(fleetOps!.items.map(i => i.id)).toEqual(['bookings', 'trips', 'routes']);
  });

  it('shows Electric fleet under Fleet Operations when vehicles module is enabled', () => {
    const menu = resolveMenu({
      tenantType: TenantType.TravelAgency,
      roles: ['SUPER_ADMIN'],
      enabledModules: ['dashboard', 'bookings', 'trips', 'routes', 'vehicles']
    });

    const fleetOps = menu.groups.find(g => g.id === 'travel-fleet-operations');
    expect(fleetOps?.items.map(i => i.id)).toContain('ev-fleet');
  });
});

describe('groupContainingRoute', () => {
  const menu: ResolvedMenu = {
    isDriverLayout: false,
    standaloneItems: [],
    groups: [
      {
        id: 'fleet',
        label: 'Fleet Management',
        icon: 'local_shipping',
        collapsible: true,
        items: [
          { id: 'gps-tracking', label: 'Fleet Tracking', icon: 'gps_fixed', route: '/gps-tracking/live' },
          { id: 'gps-hub', label: 'GPS Hub', icon: 'gps_fixed', route: '/gps-tracking' }
        ]
      } as NavGroup,
      {
        id: 'analytics',
        label: 'Analytics',
        icon: 'bar_chart',
        collapsible: true,
        items: [
          { id: 'driver-performance', label: 'Driver Performance', icon: 'speed', route: '/gps-tracking/driver-performance' },
          { id: 'fleet-analytics', label: 'Fleet Analytics', icon: 'analytics', route: '/gps-tracking/analytics' }
        ]
      } as NavGroup
    ]
  };

  it('expands Analytics for Driver Performance (not Fleet hub prefix)', () => {
    expect(groupContainingRoute(menu, '/gps-tracking/driver-performance')).toBe('analytics');
    expect(groupContainingRoute(menu, '/gps-tracking/analytics')).toBe('analytics');
  });

  it('still maps live map to Fleet when that is the longest match', () => {
    expect(groupContainingRoute(menu, '/gps-tracking/live')).toBe('fleet');
  });
});

