import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError, map, shareReplay, tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface TenantBranding {
  id: number;
  name: string;
  slug: string;
  logoUrl: string | null;
  primaryColor: string | null;
  enabledModules: string[];
}

const MODULE_ROUTE_MAP: Record<string, string> = {
  dashboard: '/dashboard',
  bookings: '/bookings',
  vehicles: '/vehicles',
  drivers: '/drivers',
  customers: '/customers',
  routes: '/routes',
  'fuel-logs': '/fuel-logs',
  maintenance: '/maintenance',
  'gps-tracking': '/gps-tracking',
  payments: '/payments',
  reports: '/reports',
  'driver-allowance-rules': '/driver-allowance-rules',
  users: '/users',
  'audit-logs': '/audit-logs'
};

@Injectable({ providedIn: 'root' })
export class TenantConfigService {
  private readonly http = inject(HttpClient);
  private branding$?: Observable<TenantBranding | null>;

  loadBranding(): Observable<TenantBranding | null> {
    if (!this.branding$) {
      this.branding$ = this.http
        .get<TenantBranding>(`${environment.apiUrl}/tenants/branding`, {
          headers: { 'X-Tenant-Slug': environment.tenantSlug ?? 'default' }
        })
        .pipe(catchError(() => of(null)), shareReplay(1));
    }
    return this.branding$;
  }

  isRouteEnabled(route: string, modules: string[]): boolean {
    if (!modules?.length) return true;
    const key = Object.entries(MODULE_ROUTE_MAP).find(([, path]) => path === route)?.[0];
    return !key || modules.includes(key);
  }
}
