import { Injectable, inject } from '@angular/core';
import { BehaviorSubject, Observable, Subscription, combineLatest } from 'rxjs';
import { distinctUntilChanged, map, skip } from 'rxjs/operators';
import { TenantContextService } from '../../core/services/tenant-context.service';
import { VehicleService } from '../../core/services/vehicle.service';
import { VehicleListItem, VehicleStatus } from '../../core/models/vehicle.model';

export interface GpsScopeState {
  companyId: number | null;
  vehicleId: number | null;
}

const STORAGE_KEY = 'stb-gps-scope-filters';

/**
 * GPS vehicle filter (and company readiness) driven by the global {@link TenantContextService}.
 * Company dropdown lives in the header switcher — this service only keeps the vehicle filter.
 */
@Injectable({ providedIn: 'root' })
export class GpsScopeFilterService {
  private readonly tenantContext = inject(TenantContextService);
  private readonly vehiclesApi = inject(VehicleService);

  private readonly vehicleIdSubject = new BehaviorSubject<number | null>(null);
  private readonly vehiclesSubject = new BehaviorSubject<VehicleListItem[]>([]);
  private readonly vehiclesLoadingSubject = new BehaviorSubject(false);

  readonly vehicleId$ = this.vehicleIdSubject.asObservable().pipe(distinctUntilChanged());
  readonly vehicles$ = this.vehiclesSubject.asObservable();
  readonly vehiclesLoading$ = this.vehiclesLoadingSubject.asObservable();

  /** Emits whenever company and/or vehicle scope changes. */
  readonly scope$: Observable<GpsScopeState> = combineLatest([
    this.tenantContext.tenantId$,
    this.vehicleId$
  ]).pipe(
    map(([companyId, vehicleId]) => ({
      companyId: this.tenantContext.canUseSwitcher
        ? (this.tenantContext.hasConcreteTenant ? companyId : null)
        : null,
      vehicleId
    })),
    distinctUntilChanged(
      (a, b) => a.companyId === b.companyId && a.vehicleId === b.vehicleId
    )
  );

  /** Emits only when company (tenant) scope changes — live map must clear other-tenant cars. */
  readonly companyScope$ = this.scope$.pipe(
    map(s => s.companyId),
    distinctUntilChanged()
  );

  private vehiclesSub?: Subscription;

  constructor() {
    this.restoreVehicleOnly();
    // One vehicles load at start, then one per real company change. Both tenant streams are
    // BehaviorSubjects: subscribing to each separately fired /api/vehicles three times on
    // every GPS page open and wiped the vehicle filter that had just been restored.
    combineLatest([this.tenantContext.tenantId$, this.tenantContext.isAllCompanies$]).pipe(
      distinctUntilChanged(([idA, allA], [idB, allB]) => idA === idB && allA === allB),
      skip(1)
    ).subscribe(() => this.onCompanyChanged());
    this.loadVehicles();
  }

  /** True when the header switcher is available (platform act-as). */
  get usesGlobalSwitcher(): boolean {
    return this.tenantContext.canUseSwitcher;
  }

  get companyFilterId(): number | null {
    if (!this.tenantContext.canUseSwitcher) return null;
    return this.tenantContext.hasConcreteTenant ? this.tenantContext.currentTenantId : null;
  }

  get vehicleFilterId(): number | null {
    return this.vehicleIdSubject.value;
  }

  get vehicles(): VehicleListItem[] {
    return this.vehiclesSubject.value;
  }

  get vehiclesLoading(): boolean {
    return this.vehiclesLoadingSubject.value;
  }

  /**
   * True when fleet APIs may run.
   * Switcher users: only after a concrete company is selected (never All / unset → JWT home).
   * Tenant users: always (JWT tenant is implicit).
   */
  get isFleetScopeReady(): boolean {
    return !this.tenantContext.canUseSwitcher || this.tenantContext.hasConcreteTenant;
  }

  get vehicleFilterDisabled(): boolean {
    return this.tenantContext.canUseSwitcher && !this.tenantContext.hasConcreteTenant;
  }

  /**
   * Concrete tenant id for callers that still accept an optional scope arg.
   * Interceptor owns X-Tenant-Id; this is only for readiness / local filtering.
   */
  get tenantScopeId(): number | null | undefined {
    if (!this.tenantContext.canUseSwitcher) return undefined;
    return this.tenantContext.hasConcreteTenant ? this.tenantContext.currentTenantId : null;
  }

  get hasActiveScope(): boolean {
    return this.vehicleFilterId != null;
  }

  /** Deep-link helper: update the global header tenant context. */
  setCompany(tenantId: number | null): void {
    if (!this.tenantContext.canUseSwitcher) return;
    if (tenantId == null) {
      this.tenantContext.clear();
      return;
    }
    this.tenantContext.selectTenantById(tenantId);
  }

  setVehicle(vehicleId: number | null): void {
    if (this.vehicleFilterDisabled && vehicleId != null) return;
    if (this.vehicleFilterId === vehicleId) return;
    this.vehicleIdSubject.next(vehicleId);
    this.persist();
  }

  clearScope(): void {
    this.vehicleIdSubject.next(null);
    this.persist();
  }

  vehicleLabel(): string {
    if (this.vehicleFilterId == null) return 'All vehicles';
    const v = this.vehicles.find(x => x.id === this.vehicleFilterId);
    if (v) return `${v.name} (${v.registrationNumber || '—'})`;
    return `Vehicle #${this.vehicleFilterId}`;
  }

  private onCompanyChanged(): void {
    this.vehicleIdSubject.next(null);
    this.persist();
    this.loadVehicles();
  }

  private loadVehicles(): void {
    // A newer scope wins: drop any request still in flight for the previous company.
    this.vehiclesSub?.unsubscribe();
    if (!this.isFleetScopeReady) {
      this.vehiclesSubject.next([]);
      this.vehiclesLoadingSubject.next(false);
      if (this.vehicleFilterId != null) {
        this.vehicleIdSubject.next(null);
        this.persist();
      }
      return;
    }

    this.vehiclesLoadingSubject.next(true);
    this.vehiclesSub = this.vehiclesApi.getAll(1, 500, false).subscribe({
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
          vehicleFilterId: this.vehicleFilterId
        })
      );
    } catch {
      /* ignore */
    }
  }

  private restoreVehicleOnly(): void {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return;
      const parsed = JSON.parse(raw) as {
        vehicleFilterId?: number | null;
      };
      if (
        parsed.vehicleFilterId == null ||
        (typeof parsed.vehicleFilterId === 'number' && parsed.vehicleFilterId > 0)
      ) {
        this.vehicleIdSubject.next(parsed.vehicleFilterId ?? null);
      }
    } catch {
      /* ignore */
    }
  }
}
