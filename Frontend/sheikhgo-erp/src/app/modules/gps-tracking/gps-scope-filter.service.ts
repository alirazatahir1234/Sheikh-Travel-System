import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, combineLatest } from 'rxjs';
import { distinctUntilChanged, map } from 'rxjs/operators';
import { AuthService } from '../../core/services/auth.service';
import { PlatformService } from '../../core/services/platform.service';
import { VehicleService } from '../../core/services/vehicle.service';
import { Tenant } from '../../core/models/platform.model';
import { VehicleListItem, VehicleStatus } from '../../core/models/vehicle.model';

export interface GpsScopeState {
  companyId: number | null;
  vehicleId: number | null;
}

const STORAGE_KEY = 'stb-gps-scope-filters';

/**
 * Shared Company (Super Admin) + Vehicle filters for the GPS module.
 * Survives tab switches (live / history / alerts) via root singleton + localStorage.
 *
 * Super Admin must select a company before any fleet data loads (empty map until then).
 * Tenant users are always scoped by JWT — no company picker required.
 */
@Injectable({ providedIn: 'root' })
export class GpsScopeFilterService {
  readonly isSuperAdmin: boolean;

  private readonly companyIdSubject = new BehaviorSubject<number | null>(null);
  private readonly vehicleIdSubject = new BehaviorSubject<number | null>(null);
  private readonly companiesSubject = new BehaviorSubject<Tenant[]>([]);
  private readonly vehiclesSubject = new BehaviorSubject<VehicleListItem[]>([]);
  private readonly companiesLoadingSubject = new BehaviorSubject(false);
  private readonly vehiclesLoadingSubject = new BehaviorSubject(false);

  readonly companyId$ = this.companyIdSubject.asObservable().pipe(distinctUntilChanged());
  readonly vehicleId$ = this.vehicleIdSubject.asObservable().pipe(distinctUntilChanged());
  readonly companies$ = this.companiesSubject.asObservable();
  readonly vehicles$ = this.vehiclesSubject.asObservable();
  readonly companiesLoading$ = this.companiesLoadingSubject.asObservable();
  readonly vehiclesLoading$ = this.vehiclesLoadingSubject.asObservable();

  /** Emits whenever company and/or vehicle scope changes. */
  readonly scope$: Observable<GpsScopeState> = combineLatest([
    this.companyId$,
    this.vehicleId$
  ]).pipe(
    map(([companyId, vehicleId]) => ({ companyId, vehicleId })),
    distinctUntilChanged(
      (a, b) => a.companyId === b.companyId && a.vehicleId === b.vehicleId
    )
  );

  /** Emits only when company (tenant) scope changes — live map must clear other-tenant cars. */
  readonly companyScope$ = this.companyId$;

  constructor(
    private auth: AuthService,
    private platform: PlatformService,
    private vehiclesApi: VehicleService
  ) {
    this.isSuperAdmin = this.auth.hasRole('SUPER_ADMIN');
    this.restore();
    if (this.isSuperAdmin) {
      this.loadCompanies();
    }
    this.loadVehicles();
  }

  get companyFilterId(): number | null {
    return this.companyIdSubject.value;
  }

  get vehicleFilterId(): number | null {
    return this.vehicleIdSubject.value;
  }

  get companies(): Tenant[] {
    return this.companiesSubject.value;
  }

  get vehicles(): VehicleListItem[] {
    return this.vehiclesSubject.value;
  }

  get companiesLoading(): boolean {
    return this.companiesLoadingSubject.value;
  }

  get vehiclesLoading(): boolean {
    return this.vehiclesLoadingSubject.value;
  }

  /**
   * True when fleet APIs may run.
   * Super Admin: only after a company is selected.
   * Tenant users: always (JWT tenant is implicit).
   */
  get isFleetScopeReady(): boolean {
    return !this.isSuperAdmin || this.companyFilterId != null;
  }

  /** Vehicle dropdown is empty/disabled until Super Admin picks a company. */
  get vehicleFilterDisabled(): boolean {
    return this.isSuperAdmin && this.companyFilterId == null;
  }

  /**
   * X-Tenant-Id override for Super Admin when a company is selected.
   * Undefined for tenant users (JWT tenant applies).
   * Null for Super Admin with no company — callers must not fetch fleet.
   */
  get tenantScopeId(): number | null | undefined {
    return this.isSuperAdmin ? this.companyFilterId : undefined;
  }

  get hasActiveScope(): boolean {
    return (
      this.vehicleFilterId != null ||
      (this.isSuperAdmin && this.companyFilterId != null)
    );
  }

  setCompany(tenantId: number | null): void {
    if (!this.isSuperAdmin) return;
    if (this.companyFilterId === tenantId) return;
    this.companyIdSubject.next(tenantId);
    // Vehicle list is tenant-scoped — clear selection until options reload.
    this.vehicleIdSubject.next(null);
    this.persist();
    this.loadVehicles();
  }

  setVehicle(vehicleId: number | null): void {
    if (this.vehicleFilterDisabled && vehicleId != null) return;
    if (this.vehicleFilterId === vehicleId) return;
    this.vehicleIdSubject.next(vehicleId);
    this.persist();
  }

  clearScope(): void {
    const hadCompany = this.isSuperAdmin && this.companyFilterId != null;
    this.vehicleIdSubject.next(null);
    if (this.isSuperAdmin) {
      this.companyIdSubject.next(null);
    }
    this.persist();
    if (hadCompany || this.isSuperAdmin) {
      this.loadVehicles();
    }
  }

  companyLabel(): string {
    if (this.companyFilterId == null) return 'Select a company';
    return (
      this.companies.find(c => c.id === this.companyFilterId)?.name ??
      `Company #${this.companyFilterId}`
    );
  }

  vehicleLabel(): string {
    if (this.vehicleFilterId == null) return 'All vehicles';
    const v = this.vehicles.find(x => x.id === this.vehicleFilterId);
    if (v) return `${v.name} (${v.registrationNumber || '—'})`;
    return `Vehicle #${this.vehicleFilterId}`;
  }

  private loadCompanies(): void {
    this.companiesLoadingSubject.next(true);
    this.platform.getSwitchableTenants().subscribe({
      next: list => {
        // SG-11: include suspended/expired/deactivated for inspect workflows.
        const mapped: Tenant[] = (list ?? []).map(t => ({
          id: t.id,
          name: t.name,
          slug: t.slug,
          code: t.code,
          isActive: t.isActive,
          status: t.status,
          subscriptionStatus: t.subscriptionStatus,
          branchCount: 0,
          departmentCount: 0,
          roleCount: 0,
          createdAt: '',
          activeUserCount: 0,
          activeVehicleCount: 0
        }));
        this.companiesSubject.next(mapped);
        this.companiesLoadingSubject.next(false);
      },
      error: () => {
        this.companiesLoadingSubject.next(false);
      }
    });
  }

  private loadVehicles(): void {
    if (!this.isFleetScopeReady) {
      this.vehiclesSubject.next([]);
      this.vehiclesLoadingSubject.next(false);
      if (this.vehicleFilterId != null) {
        this.vehicleIdSubject.next(null);
        this.persist();
      }
      return;
    }

    const tenantScope = this.isSuperAdmin ? this.companyFilterId : undefined;
    this.vehiclesLoadingSubject.next(true);
    this.vehiclesApi.getAll(1, 500, false, tenantScope ?? undefined).subscribe({
      next: r => {
        const items = r.items.filter(v => v.status !== VehicleStatus.Retired);
        this.vehiclesSubject.next(items);
        this.vehiclesLoadingSubject.next(false);
        if (
          this.vehicleFilterId != null &&
          !items.some(v => v.id === this.vehicleFilterId)
        ) {
          this.vehicleIdSubject.next(null);
          this.persist();
        }
      },
      error: () => {
        this.vehiclesSubject.next([]);
        this.vehiclesLoadingSubject.next(false);
      }
    });
  }

  private persist(): void {
    try {
      localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({
          companyFilterId: this.isSuperAdmin ? this.companyFilterId : null,
          vehicleFilterId: this.vehicleFilterId
        })
      );
    } catch {
      /* ignore */
    }
  }

  private restore(): void {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return;
      const parsed = JSON.parse(raw) as {
        companyFilterId?: number | null;
        vehicleFilterId?: number | null;
      };
      if (
        this.isSuperAdmin &&
        typeof parsed.companyFilterId === 'number' &&
        parsed.companyFilterId > 0
      ) {
        this.companyIdSubject.next(parsed.companyFilterId);
      }
      // Only restore vehicle when company scope is ready (or non-SA).
      if (
        this.isFleetScopeReady &&
        (parsed.vehicleFilterId == null ||
          (typeof parsed.vehicleFilterId === 'number' && parsed.vehicleFilterId > 0))
      ) {
        this.vehicleIdSubject.next(parsed.vehicleFilterId ?? null);
      }
    } catch {
      /* ignore */
    }
  }
}
