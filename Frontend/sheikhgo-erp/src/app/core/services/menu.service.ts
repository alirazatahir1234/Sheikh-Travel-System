import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map, of } from 'rxjs';
import { environment } from '../../../environments/environment';
import { NavGroup, NavItem, ResolvedMenu } from '../navigation/nav-models';
import { resolveMenu } from '../navigation/menu-config';
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

@Injectable({ providedIn: 'root' })
export class MenuService {
  constructor(
    private http: HttpClient,
    private auth: AuthService
  ) {}

  loadMenu(roles: string[], enabledModules: string[] = []): Observable<ResolvedMenu> {
    return this.http
      .get<MenuModuleDto[]>(`${environment.apiUrl}/platform/menus/me`)
      .pipe(
        map(modules => this.ensureMcpNav(this.fromApi(modules ?? []), enabledModules)),
        catchError(() => of(this.ensureMcpNav(this.fallbackMenu(roles, enabledModules), enabledModules)))
      );
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
