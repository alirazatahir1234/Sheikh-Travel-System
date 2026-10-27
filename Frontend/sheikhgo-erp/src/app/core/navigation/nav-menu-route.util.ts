/** Fields used to normalize sidebar routes from PlatformMenus / menus/me. */
export interface NavMenuRouteSource {
  route?: string | null;
  label?: string | null;
  name?: string | null;
  displayName?: string | null;
  permissionCode?: string | null;
  id?: string | null;
}

const CANONICAL_BY_PERMISSION: Record<string, string> = {
  'Booking.View': '/bookings',
  'Trip.View': '/trips',
  'Route.View': '/routes',
};

/**
 * Maps PlatformMenus rows (and legacy seeds) to Angular ERP routes that exist in app-routing.
 */
export function normalizeNavMenuRoute(source: NavMenuRouteSource): string {
  const raw = (source.route ?? '').trim();
  const [pathPart, queryPart] = raw.split('?', 2);
  let path = pathPart || '';

  const label = (source.displayName || source.label || source.name || '').trim();
  const labelLower = label.toLowerCase();
  const id = (source.id || '').trim().toLowerCase();
  const perm = (source.permissionCode || '').trim();

  if (perm && CANONICAL_BY_PERMISSION[perm]) {
    path = CANONICAL_BY_PERMISSION[perm];
  } else if (
    path === '/bookings' &&
    (labelLower === 'trips' ||
      id === 'trips' ||
      perm === 'Trip.View')
  ) {
    path = '/trips';
  } else if (
    labelLower === 'routes' ||
    labelLower === 'roads' ||
    id === 'routes' ||
    id === 'roads' ||
    perm === 'Route.View'
  ) {
    if (!path || path === '/dashboard' || path === '/bookings' || path === '/roads') {
      path = '/routes';
    }
  } else if (labelLower === 'trips' || id === 'trips' || perm === 'Trip.View') {
    if (!path || path === '/dashboard' || path === '/bookings') {
      path = '/trips';
    }
  } else if (labelLower === 'bookings' || id === 'bookings' || perm === 'Booking.View') {
    if (!path || path === '/dashboard') {
      path = '/bookings';
    }
  }

  if (!path.startsWith('/')) {
    path = path ? `/${path}` : '/dashboard';
  }

  return queryPart ? `${path}?${queryPart}` : path;
}
