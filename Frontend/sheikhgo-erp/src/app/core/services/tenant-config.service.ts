import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError, shareReplay, tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { persistTenantSlug, resolveTenantSlug } from '../utils/tenant-slug';

export interface TenantBranding {
  id: number;
  name: string;
  slug: string;
  enabledModules: string[];
  /** Company IANA timezone from TenantBranding.TimeZone when set. */
  timeZone?: string | null;
}

const MODULE_ROUTE_MAP: Record<string, string> = {
  dashboard: '/dashboard',
  bookings: '/bookings',
  trips: '/trips',
  vehicles: '/vehicles',
  drivers: '/drivers',
  customers: '/customers',
  routes: '/routes',
  'fuel-logs': '/fuel-logs',
  maintenance: '/maintenance',
  'gps-tracking': '/gps-tracking',
  payments: '/payments',
  invoices: '/invoices',
  settlements: '/settlements',
  'driver-wallets': '/driver-wallets',
  reports: '/reports',
  'driver-allowance-rules': '/driver-allowance-rules',
  users: '/users',
  'audit-logs': '/platform/audit-center',
  /** Premium SheikhGo AI Assistant (TenantModuleCatalog code AI / legacy key ai). */
  ai: '/ai',
  /** TravelNexus (TenantModuleCatalog code TRAVEL_AGENCY). */
  'travel-agency': '/travel'
};

@Injectable({ providedIn: 'root' })
export class TenantConfigService {
  private readonly http = inject(HttpClient);
  private branding$?: Observable<TenantBranding | null>;

  loadBranding(): Observable<TenantBranding | null> {
    if (!this.branding$) {
      this.branding$ = this.http
        .get<TenantBranding>(`${environment.apiUrl}/tenants/branding`, {
          headers: { 'X-Tenant-Slug': resolveTenantSlug(environment.tenantSlug) }
        })
        .pipe(
          tap(b => {
            if (b?.slug) persistTenantSlug(b.slug);
          }),
          catchError(() => of(null)),
          shareReplay(1)
        );
    }
    return this.branding$;
  }

  isRouteEnabled(route: string, modules: string[]): boolean {
    if (!modules?.length) return true;
    const key = Object.entries(MODULE_ROUTE_MAP).find(([, path]) => path === route)?.[0];
    return !key || modules.includes(key);
  }
}
