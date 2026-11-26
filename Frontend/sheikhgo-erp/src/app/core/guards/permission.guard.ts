import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { map, take } from 'rxjs/operators';
import { Observable } from 'rxjs';
import { selectPrimaryRole } from '../auth/role-priority';
import { isAnyModuleEntitled } from '../navigation/module-entitlement.util';
import { isRouteAllowedForPrimaryRole } from '../navigation/menu-config';
import { AuthService } from '../services/auth.service';
import { UiToastService } from '../../shared/components/ui/toast/ui-toast.service';
import { TenantConfigService } from '../services/tenant-config.service';

/**
 * Requires one of the permissions listed in route data.permissions,
 * optional roles in route data.roles, and strict primary-role domain isolation.
 * Optionally requires tenant module legacy keys in route data.modules
 * (e.g. modules: ['ai'] for premium SheikhGo AI Assistant).
 */
export const permissionGuard: CanActivateFn = (
  route: ActivatedRouteSnapshot,
  state: RouterStateSnapshot
): boolean | UrlTree | Observable<boolean | UrlTree> => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const tenantConfig = inject(TenantConfigService);
  const toast = inject(UiToastService);
  const deny = (reason: DenyReason): UrlTree => {
    const home = auth.getHomeRoute();
    // Bouncing off the home route itself would re-toast on every redirect.
    if (!isSameRoute(state.url, home)) {
      toast.warning(DENY_MESSAGES[reason], 'Page not opened');
    }
    console.warn(`[permissionGuard] ${state.url} blocked: ${reason}`, {
      permissions: route.data?.['permissions'] ?? [],
      modules: route.data?.['modules'] ?? []
    });
    return router.createUrlTree([home]);
  };

  if (!auth.isLoggedIn()) {
    return router.createUrlTree(['/auth/login']);
  }

  const requiredRoles = (route.data?.['roles'] as string[] | undefined) ?? [];
  if (requiredRoles.length > 0 && !requiredRoles.some(r => auth.hasRole(r))) {
    return deny('role');
  }

  const userRoles = auth.getCurrentUser()?.roles ?? [];
  const primaryRole = selectPrimaryRole(userRoles, '');
  if (primaryRole && !isRouteAllowedForPrimaryRole(state.url, primaryRole)) {
    return deny('role');
  }

  const required = (route.data?.['permissions'] as string[] | undefined) ?? [];
  const requiredModules = (route.data?.['modules'] as string[] | undefined) ?? [];
  const isAiRoute = requiredModules.some(m => m.toLowerCase() === 'ai');
  const permissionOk =
    required.length === 0
    || auth.hasAnyPermission(required)
    || (isAiRoute && isElevatedAdmin(auth));

  if (!permissionOk) {
    return deny('permission');
  }

  if (requiredModules.length === 0) {
    return true;
  }

  return tenantConfig.loadBranding().pipe(
    take(1),
    map(branding => {
      const enabled = branding?.enabledModules ?? [];
      const entitled = isAnyModuleEntitled(enabled, requiredModules);
      return entitled ? true : deny('module');
    })
  );
};

type DenyReason = 'role' | 'permission' | 'module';

/** Shown instead of a silent bounce to the home route, so a blocked menu click explains itself. */
export const DENY_MESSAGES: Record<DenyReason, string> = {
  role: 'Your role does not have access to this page.',
  permission: 'You do not have permission to open this page. If access was granted recently, sign out and sign in again.',
  module: 'This module is not enabled for the selected company. Switch company, or ask a platform administrator to enable it.'
};

function isSameRoute(url: string, home: string): boolean {
  const path = url.split(/[?#]/)[0].replace(/\/+$/, '');
  return path === home.replace(/\/+$/, '');
}

/**
 * Tenant administrators may open the AI module's routes before its permissions are assigned. The platform operator
 * is deliberately not listed: their access comes from the permissions in their token, never from the role name.
 */
function isElevatedAdmin(auth: AuthService): boolean {
  const roles = auth.getCurrentUser()?.roles ?? [];
  return roles.some(r => {
    const code = r.toUpperCase();
    return code === 'ADMIN' || code === 'TENANT_ADMIN';
  });
}
