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

  it('canonicalizes GPS hub /gps-tracking to live leaf', () => {
    expect(
      normalizeNavMenuRoute({
        route: '/gps-tracking',
        label: 'Fleet Tracking',
      })
    ).toBe('/gps-tracking/live');
  });

  it('keeps Driver Performance leaf route intact', () => {
    expect(
      normalizeNavMenuRoute({
        route: '/gps-tracking/driver-performance',
        label: 'Driver Performance',
      })
    ).toBe('/gps-tracking/driver-performance');
  });
});
