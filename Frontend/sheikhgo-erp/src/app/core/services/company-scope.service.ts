import { Injectable, inject, signal } from '@angular/core';
import { filter } from 'rxjs/operators';
import { AuthService } from './auth.service';

/** sessionStorage key for Super Admin global company scope (survives in-app navigation). */
export const COMPANY_SCOPE_STORAGE_KEY = 'stb-company-scope-tenant-id';

/** Areas that historically used per-page company gates (still valid for banners / deep links). */
const SCOPED_AREAS = ['/drivers', '/vehicles'];

/**
 * Super Admin global company (tenant) scope for the ERP shell.
 *
 * While set, {@link AuthInterceptor} adds `X-Tenant-Id` to API calls that do not already carry one,
 * so vehicles, drivers, trackers, and other tenant-scoped APIs target the chosen company. The
 * backend only honours a differing `X-Tenant-Id` for platform operators.
 *
 * Selection is set from the top-bar company dropdown (and still from per-page gates/banners). It
 * persists for the browser tab via sessionStorage and is not cleared on route changes.
 */
@Injectable({ providedIn: 'root' })
export class CompanyScopeService {
  private readonly scopedTenantId = signal<number | null>(loadFromSession());

  readonly tenantId = this.scopedTenantId.asReadonly();

  constructor() {
    // Drop scoped tenant when the session ends (logout / idle / 401).
    inject(AuthService).currentUser$
      .pipe(filter(user => !user))
      .subscribe(() => this.clear());
  }

  set(tenantId: number): void {
    if (!(tenantId > 0)) {
      this.clear();
      return;
    }
    this.scopedTenantId.set(tenantId);
    persistToSession(tenantId);
  }

  clear(): void {
    this.scopedTenantId.set(null);
    persistToSession(null);
  }
}

export function isScopedArea(url: string): boolean {
  const path = url.split(/[?#]/)[0];
  return SCOPED_AREAS.some(area => path === area || path.startsWith(`${area}/`));
}

function loadFromSession(): number | null {
  try {
    const raw = sessionStorage.getItem(COMPANY_SCOPE_STORAGE_KEY);
    if (!raw) return null;
    const parsed = parseInt(raw, 10);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
  } catch {
    return null;
  }
}

function persistToSession(id: number | null): void {
  try {
    if (id != null && id > 0) {
      sessionStorage.setItem(COMPANY_SCOPE_STORAGE_KEY, String(id));
    } else {
      sessionStorage.removeItem(COMPANY_SCOPE_STORAGE_KEY);
    }
  } catch {
    /* private mode / quota — ignore */
  }
}
