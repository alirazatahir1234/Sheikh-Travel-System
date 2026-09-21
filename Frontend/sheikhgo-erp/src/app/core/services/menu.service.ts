import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map, of } from 'rxjs';
import { environment } from '../../../environments/environment';
import { NavGroup, NavItem, ResolvedMenu } from '../navigation/nav-models';
import { resolveMenu } from '../navigation/menu-config';
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
  moduleKey: 'dashboard'
};

const AI_NAV_ITEM: NavItem = {
  id: 'ai-center',
  label: 'AI Management',
  icon: 'smart_toy',
  route: '/ai',
  moduleKey: 'dashboard'
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
        map(modules => this.ensureMcpNav(this.fromApi(modules ?? []))),
        catchError(() => of(this.ensureMcpNav(this.fallbackMenu(roles, enabledModules))))
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

  /** Guarantee MCP Console (and AI Management) appear under Administration in the sidebar. */
  private ensureMcpNav(menu: ResolvedMenu): ResolvedMenu {
    if (menu.isDriverLayout) return menu;
    if (!this.canSeeMcp()) return menu;

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

  private canSeeMcp(): boolean {
    if (this.auth.hasPermission('Ai.View')) return true;
    const roles = this.auth.getCurrentUser()?.roles ?? [];
    return roles.some(r => {
      const code = r.toUpperCase();
      return code === 'ADMIN'
        || code === 'SUPER_ADMIN'
        || code === 'TENANT_ADMIN'
        || code === 'SUPERADMIN';
    });
  }

  private toNavItem(item: MenuItemDto): NavItem {
    const rawRoute = item.route || '/dashboard';
    const [path, queryString] = rawRoute.split('?', 2);
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
      route: path,
      queryParams: queryParams && Object.keys(queryParams).length ? queryParams : undefined,
      moduleKey: item.moduleKey || item.permissionCode?.split('.')[0]?.toLowerCase()
    };
  }

  private fallbackMenu(roles: string[], enabledModules: string[]): ResolvedMenu {
    return resolveMenu({
      tenantType: resolveTenantType(roles),
      roles,
      enabledModules
    });
  }
}
