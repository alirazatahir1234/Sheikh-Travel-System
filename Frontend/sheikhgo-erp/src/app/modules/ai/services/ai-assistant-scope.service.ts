import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { Tenant } from '../../../core/models/platform.model';
import { VehicleListItem } from '../../../core/models/vehicle.model';

export interface AiAssistantScopeState {
  /** SuperAdmin selected company; null until chosen. */
  company: Tenant | null;
  /** null = all vehicles for the selected company. */
  vehicleId: number | null;
  vehicle: VehicleListItem | null;
  /** Brief notice after company switch (cleared by UI). */
  scopeNotice: string | null;
}

const INITIAL: AiAssistantScopeState = {
  company: null,
  vehicleId: null,
  vehicle: null,
  scopeNotice: null
};

/**
 * Shared company/vehicle scope for floating + full-page SheikhGo AI Assistant.
 * SuperAdmin selection drives X-Tenant-Id on AI HTTP calls only (not global ERP).
 */
@Injectable({ providedIn: 'root' })
export class AiAssistantScopeService {
  private readonly state$ = new BehaviorSubject<AiAssistantScopeState>(INITIAL);

  get snapshot(): AiAssistantScopeState {
    return this.state$.value;
  }

  get changes$(): Observable<AiAssistantScopeState> {
    return this.state$.asObservable();
  }

  /** Tenant id for SuperAdmin-scoped AI requests; null if not set. */
  get targetTenantId(): number | null {
    return this.state$.value.company?.id ?? null;
  }

  get selectedVehicleId(): number | null {
    return this.state$.value.vehicleId;
  }

  selectCompany(company: Tenant | null): void {
    const prev = this.state$.value.company;
    const notice =
      company && prev && prev.id !== company.id
        ? `Company scope changed to ${company.name}.`
        : company && !prev
          ? `Company scope set to ${company.name}.`
          : null;

    this.state$.next({
      company,
      vehicleId: null,
      vehicle: null,
      scopeNotice: notice
    });
  }

  selectVehicle(vehicle: VehicleListItem | null): void {
    this.state$.next({
      ...this.state$.value,
      vehicleId: vehicle?.id ?? null,
      vehicle
    });
  }

  clearScopeNotice(): void {
    if (!this.state$.value.scopeNotice) return;
    this.state$.next({ ...this.state$.value, scopeNotice: null });
  }

  clear(): void {
    this.state$.next(INITIAL);
  }
}
