import { resolveMenu } from './menu-config';
import { TenantType } from './tenant-type';

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
});
