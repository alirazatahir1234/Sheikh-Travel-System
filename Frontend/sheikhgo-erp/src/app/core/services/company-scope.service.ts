import { Injectable, inject, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';

/** Areas where a Super Admin can work inside another company (lists, profiles, wizards). */
const SCOPED_AREAS = ['/drivers', '/vehicles'];

/**
 * Super Admin "work inside another company" scope for the Drivers / Vehicles areas.
 *
 * While set, {@link AuthInterceptor} adds `X-Tenant-Id` to API calls that do not already carry one,
 * so lists, profiles, lookups, uploads and create/update calls all target the chosen company. The
 * backend only honours a differing `X-Tenant-Id` for platform operators.
 *
 * The scope survives navigation inside the Drivers / Vehicles areas (list → profile → edit) and is
 * cleared automatically as soon as the user navigates anywhere else.
 */
@Injectable({ providedIn: 'root' })
export class CompanyScopeService {
  private readonly scopedTenantId = signal<number | null>(null);

  readonly tenantId = this.scopedTenantId.asReadonly();

  constructor() {
    inject(Router).events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe(e => {
        if (!isScopedArea(e.urlAfterRedirects)) this.clear();
      });
  }

  set(tenantId: number): void {
    this.scopedTenantId.set(tenantId);
  }

  clear(): void {
    this.scopedTenantId.set(null);
  }
}

export function isScopedArea(url: string): boolean {
  const path = url.split(/[?#]/)[0];
  return SCOPED_AREAS.some(area => path === area || path.startsWith(`${area}/`));
}
