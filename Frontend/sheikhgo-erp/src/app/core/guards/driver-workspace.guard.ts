import { inject } from '@angular/core';
import { CanActivateChildFn, CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

const driverAllowedPrefixes = ['/my-trips', '/profile', '/wallet'];

function isDriverAllowedPath(url: string): boolean {
  const path = url.split('?')[0];
  return driverAllowedPrefixes.some(prefix => path === prefix || path.startsWith(`${prefix}/`));
}

/** Restricts Driver / Field Driver accounts to the driver workspace routes. */
export const driverWorkspaceGuard: CanActivateChildFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isDriverUser()) {
    return true;
  }

  const path = state.url.split('?')[0];
  if (path === '/' || path === '' || path === '/dashboard' || path.startsWith('/dashboard/')) {
    return router.createUrlTree(['/my-trips']);
  }

  if (isDriverAllowedPath(state.url)) {
    return true;
  }

  return router.createUrlTree(['/my-trips']);
};

/** Blocks non-drivers from driver-only pages. */
export const driverOnlyGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isDriverUser() ? true : router.createUrlTree(['/dashboard']);
};
