import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Requires one of the permissions listed in route data.permissions.
 * Example: data: { permissions: ['Booking.View'] }
 */
export const permissionGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isLoggedIn()) {
    return router.createUrlTree(['/auth/login']);
  }

  const required = (route.data?.['permissions'] as string[] | undefined) ?? [];
  if (required.length === 0) {
    return true;
  }

  if (auth.hasAnyPermission(required)) {
    return true;
  }

  return router.createUrlTree([auth.getHomeRoute()]);
};
