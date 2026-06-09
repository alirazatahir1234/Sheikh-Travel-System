import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map, of } from 'rxjs';
import { environment } from '../../../environments/environment';
import { NavGroup, NavItem, ResolvedMenu } from '../navigation/nav-models';
import { resolveMenu } from '../navigation/menu-config';
import { resolveTenantType } from '../navigation/tenant-type';

export interface MenuModuleDto {
  id: string;
  label: string;
  icon: string;
  collapsible: boolean;
  sortOrder: number;
  items: MenuItemDto[];
}

export interface MenuItemDto {
  id: string;
  label: string;
  icon: string;
  route: string;
  permissionCode?: string | null;
  sortOrder: number;
}

@Injectable({ providedIn: 'root' })
export class MenuService {
  constructor(private http: HttpClient) {}

  loadMenu(roles: string[], enabledModules: string[] = []): Observable<ResolvedMenu> {
    return this.http
      .get<MenuModuleDto[]>(`${environment.apiUrl}/platform/menus/me`)
      .pipe(
        map(modules => this.fromApi(modules ?? [])),
        catchError(() => of(this.fallbackMenu(roles, enabledModules)))
      );
  }

  private fromApi(modules: MenuModuleDto[]): ResolvedMenu {
    const groups: NavGroup[] = modules
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map(module => ({
        id: module.id,
        label: module.label,
        icon: module.icon,
        collapsible: module.collapsible,
        items: module.items
          .sort((a, b) => a.sortOrder - b.sortOrder)
          .map(item => this.toNavItem(item))
      }));

    return { groups, standaloneItems: [], isDriverLayout: false };
  }

  private toNavItem(item: MenuItemDto): NavItem {
    return {
      id: item.id,
      label: item.label,
      icon: item.icon,
      route: item.route,
      moduleKey: item.permissionCode?.split('.')[0]?.toLowerCase()
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
