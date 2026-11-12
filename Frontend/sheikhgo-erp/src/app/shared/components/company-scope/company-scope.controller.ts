import { Injectable, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { catchError, of } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { CompanyScopeService } from '../../../core/services/company-scope.service';
import { PlatformService } from '../../../core/services/platform.service';
import { PlatformTenantContextService } from '../../../core/services/platform-tenant-context.service';
import { Tenant } from '../../../core/models/platform.model';

export interface ScopedCompany {
  id: number;
  name: string;
}

/**
 * Per-page helper (provide it in the component) that lets a Super Admin work on another company's
 * drivers / vehicles. It reads `?tenantId=`, drives the global {@link CompanyScopeService} header
 * override (which persists inside the Drivers / Vehicles areas) and holds the banner state.
 * Non-Super-Admins are unaffected.
 */
@Injectable()
export class CompanyScopeController {
  private readonly auth = inject(AuthService);
  private readonly scope = inject(CompanyScopeService);
  private readonly platform = inject(PlatformService);
  private readonly tenantContext = inject(PlatformTenantContextService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly isSuperAdmin = this.auth.hasRole('SUPER_ADMIN') || this.auth.hasRole('SUPERADMIN');
  /** Create wizards: true while the Super Admin still has to pick a company. */
  readonly needsGate = signal(false);
  readonly company = signal<ScopedCompany | null>(null);
  /** Preselection for the gate (platform's last selected company). */
  readonly initialTenantId = signal<number | null>(null);

  /** `?tenantId=` on the current route, if any. */
  get routeTenantId(): number | null {
    const raw = Number(this.route.snapshot.queryParamMap.get('tenantId'));
    return Number.isFinite(raw) && raw > 0 ? raw : null;
  }

  /**
   * Create wizards. Returns true when the wizard may initialise now; false means show the gate first
   * (so nothing — e.g. a vehicle draft — is created in the wrong company). A Super Admin always
   * confirms the company unless the URL already names it.
   */
  beginCreate(): boolean {
    if (!this.isSuperAdmin) return true;
    const fromRoute = this.routeTenantId;
    if (fromRoute) {
      this.apply(fromRoute);
      return true;
    }
    this.initialTenantId.set(this.scope.tenantId() ?? this.tenantContext.currentTenantId);
    this.needsGate.set(true);
    return false;
  }

  /**
   * Lists / profiles / edit wizards: `?tenantId=` wins, then the company already in scope for this
   * area, then {@link fallbackTenantId}. No id ⇒ the Super Admin's own company.
   */
  beginExisting(fallbackTenantId: number | null = null): void {
    if (!this.isSuperAdmin) return;
    const id = this.routeTenantId ?? this.scope.tenantId() ?? fallbackTenantId;
    if (id) this.apply(id);
  }

  /** Lists: same as {@link beginExisting}, falling back to the company selected on platform pages. */
  beginList(): void {
    this.beginExisting(this.tenantContext.currentTenantId);
  }

  /**
   * Leave the other company and reload the list for the Super Admin's own company. Also clears the
   * platform company selection, otherwise {@link beginList} would fall straight back into it.
   */
  exitToOwnCompany(path: string): void {
    this.scope.clear();
    this.tenantContext.clear();
    this.company.set(null);
    this.reload(path);
  }

  /** Gate confirmed: scope all API calls to the company and remember it in the URL. */
  confirm(tenant: Tenant): void {
    this.apply(tenant.id, tenant.name);
    this.tenantContext.selectTenant(tenant);
    this.needsGate.set(false);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tenantId: tenant.id },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }

  /** Query params to carry the company to the next page (e.g. back to the list after save). */
  queryParams(): Record<string, number> {
    const id = this.scope.tenantId();
    return this.isSuperAdmin && id != null ? { tenantId: id } : {};
  }

  /** Re-open a create wizard from scratch so the Super Admin can pick another company. */
  restart(path: string): void {
    this.reload(path);
  }

  /**
   * Edit wizards: show the company gate again so a Super Admin can move the record to another
   * company without leaving the page. Cancel restores the form under the current company.
   */
  beginChange(): void {
    if (!this.isSuperAdmin) return;
    this.initialTenantId.set(this.company()?.id ?? this.scope.tenantId() ?? this.tenantContext.currentTenantId);
    this.needsGate.set(true);
  }

  /** Dismiss a mid-edit company gate without navigating away. */
  cancelChange(): void {
    this.needsGate.set(false);
  }

  /** Force the banner/header scope onto the vehicle's real company after load. */
  syncTo(tenantId: number, name?: string): void {
    if (!this.isSuperAdmin || !(tenantId > 0)) return;
    this.apply(tenantId, name);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tenantId },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }

  /**
   * Recreate the routed component (same-URL navigation would reuse it). Hopping via `/` also clears
   * the area scope; {@link exitToOwnCompany} wants that, and a restarted gate preselects from
   * the platform tenant context instead.
   */
  private reload(path: string): void {
    void this.router.navigateByUrl('/', { skipLocationChange: true })
      .then(() => this.router.navigate([path]));
  }

  private apply(tenantId: number, name?: string): void {
    this.scope.set(tenantId);
    this.tenantContext.selectTenantById(tenantId);
    this.company.set({ id: tenantId, name: name ?? `Company #${tenantId}` });
    if (name) return;
    this.platform.getTenants().pipe(catchError(() => of([] as Tenant[]))).subscribe(list => {
      const match = (list ?? []).find(t => t.id === tenantId);
      if (match && this.company()?.id === tenantId) {
        this.company.set({ id: tenantId, name: match.name });
        this.tenantContext.setTenantDetails(match);
      }
    });
  }
}
