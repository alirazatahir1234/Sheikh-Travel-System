import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';
import { map, take } from 'rxjs/operators';
import { Observable } from 'rxjs';
import { isAnyModuleEntitled } from '../navigation/module-entitlement.util';
import { AuthService } from '../services/auth.service';
import { TenantConfigService } from '../services/tenant-config.service';

/**
 * Requires one of the permissions listed in route data.permissions.
 * Optionally requires tenant module legacy keys in route data.modules
 * (e.g. modules: ['ai'] for premium SheikhGo AI Assistant).
 *
 * Example: data: { permissions: ['Ai.View', 'Ai.Chat'], modules: ['ai'] }
 *
 * For AI module routes, elevated admins match MenuService.canAccessAiAssistant
 * (menu link + FAB) even when Ai.* permissions are not yet assigned.
 */
export const permissionGuard: CanActivateFn = (route): boolean | UrlTree | Observable<boolean | UrlTree> => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const tenantConfig = inject(TenantConfigService);

  if (!auth.isLoggedIn()) {
    return router.createUrlTree(['/auth/login']);
  }

  const required = (route.data?.['permissions'] as string[] | undefined) ?? [];
  const requiredModules = (route.data?.['modules'] as string[] | undefined) ?? [];
  const isAiRoute = requiredModules.some(m => m.toLowerCase() === 'ai');
  const permissionOk =
    required.length === 0
    || auth.hasAnyPermission(required)
    || (isAiRoute && isElevatedAdmin(auth));

  if (!permissionOk) {
    return router.createUrlTree([auth.getHomeRoute()]);
  }

  if (requiredModules.length === 0) {
    return true;
  }

  return tenantConfig.loadBranding().pipe(
    take(1),
    map(branding => {
      const enabled = branding?.enabledModules ?? [];
      const entitled = isAnyModuleEntitled(enabled, requiredModules);
      return entitled ? true : router.createUrlTree([auth.getHomeRoute()]);
    })
  );
};

function isElevatedAdmin(auth: AuthService): boolean {
  const roles = auth.getCurrentUser()?.roles ?? [];
  return roles.some(r => {
    const code = r.toUpperCase();
    return code === 'ADMIN'
      || code === 'SUPER_ADMIN'
      || code === 'TENANT_ADMIN'
      || code === 'SUPERADMIN';
  });
}
