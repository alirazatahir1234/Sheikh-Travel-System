import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map, of } from 'rxjs';
import { environment } from '../../../environments/environment';
import { NavGroup, NavItem, ResolvedMenu } from '../navigation/nav-models';
import { isRouteAllowedForPrimaryRole, resolveMenu } from '../navigation/menu-config';
import { selectPrimaryRole } from '../auth/role-priority';
import { normalizeNavMenuRoute } from '../navigation/nav-menu-route.util';
import { resolveTenantType } from '../navigation/tenant-type';
import { AuthService } from './auth.service';

export interface MenuModuleDto {
  id: string;
  label: string;
  icon: string;
  collapsible: boolean;
  sortOrder: number;
  items: MenuItemDto[];
  displayName?: string | null;
  description?: string | null;
  visible?: boolean;
}

export interface MenuItemDto {
  id: string;
  label: string;
  name?: string | null;
  icon: string;
  route: string;
  permissionCode?: string | null;
  sortOrder: number;
  displayName?: string | null;
  description?: string | null;
  category?: string | null;
  featureKey?: string | null;
  moduleKey?: string | null;
  isMobileSupported?: boolean;
  visible?: boolean;
}

const MCP_NAV_ITEM: NavItem = {
  id: 'mcp-console',
  label: 'MCP Console',
  icon: 'hub',
  route: '/mcp',
  moduleKey: 'ai'
};

const AI_NAV_ITEM: NavItem = {
  id: 'ai-center',
  label: 'SheikhGo AI Assistant',
  icon: 'smart_toy',
  route: '/ai',
  moduleKey: 'ai'
};

/** Analytics GPS pages — must appear even when PlatformMenus seed lagged behind the UI. */
const GPS_ANALYTICS_NAV_ITEMS: NavItem[] = [
  {
    id: 'fleet-analytics',
    label: 'Fleet Analytics',
    icon: 'analytics',
    route: '/gps-tracking/analytics',
    moduleKey: 'gps-tracking'
  },
  {
    id: 'driver-performance',
    label: 'Driver Performance',
    icon: 'speed',
    route: '/gps-tracking/driver-performance',
    moduleKey: 'gps-tracking'
  },
  {
    id: 'client-tracking-links',
    label: 'Client Tracking Links',
    icon: 'link',
    route: '/gps-tracking/client-tracking-links',
    moduleKey: 'gps-tracking'
  }
];

@Injectable({ providedIn: 'root' })
export class MenuService {
  constructor(
    private http: HttpClient,
    private auth: AuthService
  ) {}

  loadMenu(roles: string[], enabledModules: string[] = []): Observable<ResolvedMenu> {
    return this.loadMenuModules().pipe(
      map(modules => this.resolveMenu(modules, roles, enabledModules))
    );
  }

  /**
   * GET /platform/menus/me — the only HTTP part of building the sidebar.
   * Emits null when the request fails; {@link resolveMenu} then uses the static fallback.
   */
  loadMenuModules(): Observable<MenuModuleDto[] | null> {
    return this.http
      .get<MenuModuleDto[]>(`${environment.apiUrl}/platform/menus/me`)
      .pipe(
        map(modules => modules ?? []),
        catchError(() => of(null))
      );
  }

  /**
   * Builds the sidebar from modules that are already loaded. No HTTP, so the shell can
   * re-run it when the enabled modules change without fetching menus/me again.
   */
  resolveMenu(modules: MenuModuleDto[] | null, roles: string[], enabledModules: string[] = []): ResolvedMenu {
    const primaryRole = selectPrimaryRole(roles, '');
    const base = modules ? this.fromApi(modules) : this.fallbackMenu(roles, enabledModules);
    return this.filterMenuByRole(
      this.ensureGpsAnalyticsNav(this.ensureMcpNav(base, enabledModules), enabledModules),
      primaryRole
    );
  }

  private filterMenuByRole(menu: ResolvedMenu, primaryRole: string): ResolvedMenu {
    if (menu.isDriverLayout || !primaryRole) return menu;
    return {
      ...menu,
      groups: menu.groups
        .map(g => ({
          ...g,
          items: g.items.filter(i => isRouteAllowedForPrimaryRole(i.route, primaryRole))
        }))
        .filter(g => g.items.length > 0),
      standaloneItems: (menu.standaloneItems ?? []).filter(i =>
        isRouteAllowedForPrimaryRole(i.route, primaryRole)
      )
    };
  }

  private fromApi(modules: MenuModuleDto[]): ResolvedMenu {
    const groups: NavGroup[] = modules
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map(module => ({
        id: module.id,
        label: module.displayName || module.label,
        icon: module.icon,
        collapsible: module.collapsible,
        items: module.items
          .sort((a, b) => a.sortOrder - b.sortOrder)
          .map(item => this.toNavItem(item))
      }));

    return { groups, standaloneItems: [], isDriverLayout: false };
  }

  /**
   * Align MCP / AI sidebar links with permissionGuard on /mcp and /ai
   * (Ai.View + tenant module legacy key `ai`).
   */
  private ensureMcpNav(menu: ResolvedMenu, enabledModules: string[] = []): ResolvedMenu {
    if (menu.isDriverLayout) return menu;
    if (!this.canSeeMcp(enabledModules)) {
      return this.stripAiModuleNav(menu);
    }

    const groups = menu.groups.map(g => ({ ...g, items: [...g.items] }));
    const alreadyHasMcp = groups.some(g =>
      g.items.some(i => i.id === 'mcp-console' || i.route === '/mcp' || i.route.startsWith('/mcp/'))
    );
    if (alreadyHasMcp) return { ...menu, groups };

    let admin = groups.find(g =>
      g.id === 'administration' ||
      g.id === 'fleet-admin' ||
      g.label.toLowerCase() === 'administration'
    );

    if (!admin) {
      admin = {
        id: 'administration',
        label: 'Administration',
        icon: 'admin_panel_settings',
        collapsible: true,
        items: []
      };
      groups.push(admin);
    }

    const hasAi = admin.items.some(i => i.id === 'ai-center' || i.route === '/ai');
    if (!hasAi) {
      admin.items.push({ ...AI_NAV_ITEM });
    }
    admin.items.push({ ...MCP_NAV_ITEM });

    return { ...menu, groups };
  }

  /** Hide MCP Console / SheikhGo AI Assistant links when AI module is off (same gate as /mcp and /ai routes). */
  private stripAiModuleNav(menu: ResolvedMenu): ResolvedMenu {
    const isAiNav = (i: NavItem) =>
      i.id === 'mcp-console'
      || i.id === 'ai-center'
      || i.route === '/mcp'
      || i.route.startsWith('/mcp/')
      || i.route === '/ai'
      || i.route.startsWith('/ai/');

    return {
      ...menu,
      groups: menu.groups
        .map(g => ({ ...g, items: g.items.filter(i => !isAiNav(i)) }))
        .filter(g => g.items.length > 0),
      standaloneItems: (menu.standaloneItems ?? []).filter(i => !isAiNav(i))
    };
  }

  private canSeeMcp(enabledModules: string[]): boolean {
    return this.canAccessAiAssistant(enabledModules);
  }

  /**
   * Inject Driver Performance / Fleet Analytics under Analytics when GPS is entitled
   * and the API catalog omitted them (historical PlatformMenus gap).
   */
  private ensureGpsAnalyticsNav(menu: ResolvedMenu, enabledModules: string[] = []): ResolvedMenu {
    if (menu.isDriverLayout) return menu;
    // Soft: empty entitlement list → do not hide (matches backend PassesFeatureGate).
    if (enabledModules.length > 0 && !this.isGpsTrackingEnabled(enabledModules)) return menu;
    if (!this.canSeeGpsTracking()) return menu;

    const groups = menu.groups.map(g => ({ ...g, items: [...g.items] }));
    let analytics = groups.find(g =>
      g.id === 'analytics' || g.label.toLowerCase() === 'analytics'
    );

    if (!analytics) {
      analytics = {
        id: 'analytics',
        label: 'Analytics',
        icon: 'bar_chart',
        collapsible: true,
        items: []
      };
      groups.push(analytics);
    }

    for (const item of GPS_ANALYTICS_NAV_ITEMS) {
      const existing = analytics.items.find(i =>
        i.id === item.id
        || i.route === item.route
        || (i.route?.startsWith(item.route + '/') ?? false)
      );
      if (existing) {
        // Repair stale PlatformMenus rows (empty / hub-only / typo routes).
        if (existing.route !== item.route) {
          existing.route = item.route;
        }
        continue;
      }
      analytics.items.push({ ...item });
    }

    return { ...menu, groups };
  }

  private isGpsTrackingEnabled(enabledModules: string[]): boolean {
    return (enabledModules ?? []).some(e => e.toLowerCase() === 'gps-tracking');
  }

  private canSeeGpsTracking(): boolean {
    if (this.auth.hasPermission('GPS.View')) return true;
    const roles = this.auth.getCurrentUser()?.roles ?? [];
    return roles.some(r => {
      const code = r.toUpperCase();
      return code === 'ADMIN'
        || code === 'SUPER_ADMIN'
        || code === 'TENANT_ADMIN'
        || code === 'SUPERADMIN';
    });
  }

  /**
   * SheikhGo AI Assistant / MCP visibility — same gate as `/ai` routes + floating FAB:
   * tenant module `ai`/`AI` + (Ai.View | Ai.Chat | elevated admin).
   */
  canAccessAiAssistant(enabledModules: string[] = []): boolean {
    if (!this.isAiModuleEnabled(enabledModules)) return false;

    if (this.auth.hasPermission('Ai.View') || this.auth.hasPermission('Ai.Chat')) {
      return true;
    }
    const roles = this.auth.getCurrentUser()?.roles ?? [];
    return roles.some(r => {
      const code = r.toUpperCase();
      return code === 'ADMIN'
        || code === 'SUPER_ADMIN'
        || code === 'TENANT_ADMIN'
        || code === 'SUPERADMIN';
    });
  }

  isAiModuleEnabled(enabledModules: string[]): boolean {
    return (enabledModules ?? []).some(e => e.toLowerCase() === 'ai');
  }

  private toNavItem(item: MenuItemDto): NavItem {
    const normalized = normalizeNavMenuRoute({
      route: item.route,
      label: item.label,
      name: item.name,
      displayName: item.displayName,
      permissionCode: item.permissionCode,
      id: item.id,
    });
    const [path, queryString] = normalized.split('?', 2);
    let queryParams: Record<string, string> | undefined;
    if (queryString) {
      const params = new URLSearchParams(queryString);
      queryParams = {};
      params.forEach((value, key) => {
        queryParams![key] = value;
      });
    }

    return {
      id: item.id,
      label: item.displayName || item.label,
      icon: item.icon,
      route: path || '/dashboard',
      queryParams: queryParams && Object.keys(queryParams).length ? queryParams : undefined,
      moduleKey: this.resolveNavModuleKey(item)
    };
  }

  private resolveNavModuleKey(item: MenuItemDto): string | undefined {
    const perm = item.permissionCode?.trim();
    if (perm === 'Booking.View') return 'bookings';
    if (perm === 'Trip.View') return 'trips';
    if (perm === 'Route.View') return 'routes';
    if (item.moduleKey) {
      const key = item.moduleKey.toLowerCase();
      if (key === 'travel') return 'routes';
      if (key === 'bookings') return 'bookings';
      if (key === 'trips') return 'trips';
      return item.moduleKey;
    }
    return perm?.split('.')[0]?.toLowerCase();
  }

  private fallbackMenu(roles: string[], enabledModules: string[]): ResolvedMenu {
    return resolveMenu({
      tenantType: resolveTenantType(roles),
      roles,
      enabledModules
    });
  }
}
