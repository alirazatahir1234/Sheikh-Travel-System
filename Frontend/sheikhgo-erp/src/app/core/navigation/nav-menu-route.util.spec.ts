import { normalizeNavMenuRoute } from './nav-menu-route.util';

describe('normalizeNavMenuRoute', () => {
  it('fixes legacy Trips row pointing at /bookings', () => {
    expect(
      normalizeNavMenuRoute({
        route: '/bookings',
        label: 'Trips',
        permissionCode: 'Trip.View',
      })
    ).toBe('/trips');
  });

  it('maps Roads label and /roads path to /routes', () => {
    expect(
      normalizeNavMenuRoute({
        route: '/roads',
        label: 'Roads',
        permissionCode: 'Route.View',
      })
    ).toBe('/routes');
  });

  it('keeps Bookings on /bookings', () => {
    expect(
      normalizeNavMenuRoute({
        route: '/bookings',
        label: 'Bookings',
        permissionCode: 'Booking.View',
      })
    ).toBe('/bookings');
  });
});
