import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';
import { map, take } from 'rxjs/operators';
import { Observable } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { TenantConfigService } from '../services/tenant-config.service';

/**
 * Requires one of the permissions listed in route data.permissions.
 * Optionally requires tenant module legacy keys in route data.modules
 * (e.g. modules: ['ai'] for premium AAS Assistant).
 *
 * Example: data: { permissions: ['Ai.View'], modules: ['ai'] }
 */
export const permissionGuard: CanActivateFn = (route): boolean | UrlTree | Observable<boolean | UrlTree> => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const tenantConfig = inject(TenantConfigService);

  if (!auth.isLoggedIn()) {
    return router.createUrlTree(['/auth/login']);
  }

  const required = (route.data?.['permissions'] as string[] | undefined) ?? [];
  if (required.length > 0 && !auth.hasAnyPermission(required)) {
    return router.createUrlTree([auth.getHomeRoute()]);
  }

  const requiredModules = (route.data?.['modules'] as string[] | undefined) ?? [];
  if (requiredModules.length === 0) {
    return true;
  }

  return tenantConfig.loadBranding().pipe(
    take(1),
    map(branding => {
      const enabled = branding?.enabledModules ?? [];
      // Premium modules: deny unless we can prove the legacy key is entitled.
      const entitled = requiredModules.every(m =>
        enabled.some(e => e.toLowerCase() === m.toLowerCase())
      );
      return entitled ? true : router.createUrlTree([auth.getHomeRoute()]);
    })
  );
};
