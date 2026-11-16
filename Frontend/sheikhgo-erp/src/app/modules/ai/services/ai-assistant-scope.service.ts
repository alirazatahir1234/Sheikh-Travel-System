import { Injectable, inject } from '@angular/core';
import { BehaviorSubject, Observable, combineLatest } from 'rxjs';
import { distinctUntilChanged, map, tap } from 'rxjs/operators';
import { Tenant } from '../../../core/models/platform.model';
import { VehicleListItem } from '../../../core/models/vehicle.model';
import { TenantContextService } from '../../../core/services/tenant-context.service';

export interface AiAssistantScopeState {
  /** Company from global tenant context (platform users) or null for JWT tenant users. */
  company: Tenant | null;
  /** null = all vehicles for the selected company. */
  vehicleId: number | null;
  vehicle: VehicleListItem | null;
  /** Brief notice after company switch (cleared by UI). */
  scopeNotice: string | null;
}

/**
 * AI assistant scope syncs company from {@link TenantContextService} (no fifth switcher).
 * Vehicle filter remains local to the assistant UI.
 */
@Injectable({ providedIn: 'root' })
export class AiAssistantScopeService {
  private readonly tenantContext = inject(TenantContextService);
  private readonly vehicleSubject = new BehaviorSubject<{
    vehicleId: number | null;
    vehicle: VehicleListItem | null;
  }>({ vehicleId: null, vehicle: null });
  private readonly scopeNoticeSubject = new BehaviorSubject<string | null>(null);
  private lastCompanyId: number | null = null;

  private readonly stateSubject = new BehaviorSubject<AiAssistantScopeState>({
    company: null,
    vehicleId: null,
    vehicle: null,
    scopeNotice: null
  });

  constructor() {
    this.tenantContext.tenantId$.pipe(
      distinctUntilChanged(),
      tap(id => {
        const concrete = this.tenantContext.hasConcreteTenant ? id : null;
        if (concrete !== this.lastCompanyId) {
          if (this.vehicleSubject.value.vehicleId != null) {
            this.vehicleSubject.next({ vehicleId: null, vehicle: null });
          }
          const name = this.tenantContext.currentTenant?.name;
          if (concrete != null && name) {
            this.scopeNoticeSubject.next(
              this.lastCompanyId == null
                ? `Company scope set to ${name}.`
                : `Company scope changed to ${name}.`
            );
          }
          this.lastCompanyId = concrete;
        }
      })
    ).subscribe();

    combineLatest([
      this.tenantContext.tenant$,
      this.tenantContext.tenantId$,
      this.vehicleSubject,
      this.scopeNoticeSubject
    ]).pipe(
      map(([tenant, tenantId, vehicle, scopeNotice]) => {
        const company =
          this.tenantContext.canUseSwitcher && this.tenantContext.hasConcreteTenant
            ? (tenant ?? (tenantId != null
              ? {
                  id: tenantId,
                  name: `Company #${tenantId}`,
                  slug: '',
                  isActive: true,
                  branchCount: 0,
                  departmentCount: 0,
                  roleCount: 0,
                  createdAt: '',
                  activeUserCount: 0,
                  activeVehicleCount: 0
                } as Tenant
              : null))
            : null;
        return {
          company,
          vehicleId: vehicle.vehicleId,
          vehicle: vehicle.vehicle,
          scopeNotice
        } as AiAssistantScopeState;
      })
    ).subscribe(s => this.stateSubject.next(s));
  }

  get snapshot(): AiAssistantScopeState {
    return this.stateSubject.value;
  }

  get changes$(): Observable<AiAssistantScopeState> {
    return this.stateSubject.asObservable();
  }

  /** Tenant id for AI requests; null if not set / All companies. Interceptor sends X-Tenant-Id. */
  get targetTenantId(): number | null {
    return this.tenantContext.overrideTenantId;
  }

  get selectedVehicleId(): number | null {
    return this.stateSubject.value.vehicleId;
  }

  /** @deprecated Company comes from the header switcher — kept as no-op for old callers. */
  selectCompany(_company: Tenant | null): void {
    /* SG-06: synced from TenantContextService */
  }

  selectVehicle(vehicle: VehicleListItem | null): void {
    this.vehicleSubject.next({
      vehicleId: vehicle?.id ?? null,
      vehicle
    });
  }

  clearScopeNotice(): void {
    if (!this.scopeNoticeSubject.value) return;
    this.scopeNoticeSubject.next(null);
  }

  clear(): void {
    this.vehicleSubject.next({ vehicleId: null, vehicle: null });
    this.scopeNoticeSubject.next(null);
  }
}
