import { Injectable, inject } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { AuthService } from './auth.service';
import { SwitchableTenant, Tenant } from '../models/platform.model';

const STORAGE_KEY = 'selectedTenantId';
const ALL_SENTINEL = 'all';

/**
 * Single global tenant context for the ERP (SG-06).
 *
 * Platform users with Platform.Tenants.View / DataRead see the header switcher.
 * - Concrete tenant → interceptor sends X-Tenant-Id
 * - All companies → interceptor omits X-Tenant-Id; write screens must block
 * - Unset → omit header; screens must show pick-a-company (never silently use JWT home)
 *
 * Ordinary tenant users: no switcher; interceptor sends no override.
 */
@Injectable({
  providedIn: 'root'
})
export class TenantContextService {
  private readonly auth = inject(AuthService);

  private readonly selectedTenantId$ = new BehaviorSubject<number | null>(this.loadIdFromStorage());
  private readonly selectedTenant$ = new BehaviorSubject<Tenant | null>(null);
  private readonly allCompanies$ = new BehaviorSubject<boolean>(this.loadAllFromStorage());

  get tenantId$(): Observable<number | null> {
    return this.selectedTenantId$.asObservable();
  }

  get tenant$(): Observable<Tenant | null> {
    return this.selectedTenant$.asObservable();
  }

  get isAllCompanies$(): Observable<boolean> {
    return this.allCompanies$.asObservable();
  }

  get currentTenantId(): number | null {
    return this.selectedTenantId$.value;
  }

  get currentTenant(): Tenant | null {
    return this.selectedTenant$.value;
  }

  get isAllCompanies(): boolean {
    return this.allCompanies$.value;
  }

  /** True when a concrete company is selected (not All, not unset). */
  get hasConcreteTenant(): boolean {
    return this.currentTenantId != null && !this.isAllCompanies;
  }

  /** Write / create screens require a concrete tenant when the switcher is available. */
  get canMutate(): boolean {
    if (!this.canUseSwitcher) return true;
    return this.hasConcreteTenant;
  }

  /**
   * Platform act-as permission. Prefer View; DataRead also unlocks the switcher
   * for read-oriented platform roles.
   */
  get canUseSwitcher(): boolean {
    return this.auth.hasPermission('Platform.Tenants.View')
      || this.auth.hasPermission('Platform.Tenants.DataRead');
  }

  /**
   * Value for auth.interceptor only. Null → do not send X-Tenant-Id
   * (All / unset / non-switcher users).
   */
  get overrideTenantId(): number | null {
    if (!this.canUseSwitcher) return null;
    if (this.isAllCompanies) return null;
    return this.currentTenantId;
  }

  selectTenant(tenant: Tenant | null): void {
    if (!tenant) {
      this.clearSelectionKeepSwitcher();
      return;
    }
    this.allCompanies$.next(false);
    this.selectedTenantId$.next(tenant.id);
    this.selectedTenant$.next(tenant);
    this.saveToStorage(tenant.id);
  }

  selectTenantById(id: number | null): void {
    if (id == null) {
      this.clearSelectionKeepSwitcher();
      return;
    }
    this.allCompanies$.next(false);
    this.selectedTenantId$.next(id);
    if (this.selectedTenant$.value?.id !== id) {
      this.selectedTenant$.next(null);
    }
    this.saveToStorage(id);
  }

  selectAll(): void {
    this.allCompanies$.next(true);
    this.selectedTenantId$.next(null);
    this.selectedTenant$.next(null);
    this.saveAllToStorage();
  }

  setTenantDetails(tenant: Tenant): void {
    if (tenant.id === this.selectedTenantId$.value && !this.isAllCompanies) {
      this.selectedTenant$.next(tenant);
    }
  }

  /** Apply a switchable-list row (hydrates name/status). */
  selectSwitchable(row: SwitchableTenant): void {
    this.selectTenant(this.toTenant(row));
  }

  /** Clear selection and All mode (logout / exit act-as). */
  clear(): void {
    this.selectedTenantId$.next(null);
    this.selectedTenant$.next(null);
    this.allCompanies$.next(false);
    localStorage.removeItem(STORAGE_KEY);
  }

  private clearSelectionKeepSwitcher(): void {
    this.allCompanies$.next(false);
    this.selectedTenantId$.next(null);
    this.selectedTenant$.next(null);
    localStorage.removeItem(STORAGE_KEY);
  }

  private loadIdFromStorage(): number | null {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (!stored || stored === ALL_SENTINEL) return null;
    const parsed = parseInt(stored, 10);
    return Number.isFinite(parsed) ? parsed : null;
  }

  private loadAllFromStorage(): boolean {
    return localStorage.getItem(STORAGE_KEY) === ALL_SENTINEL;
  }

  private saveToStorage(id: number): void {
    localStorage.setItem(STORAGE_KEY, String(id));
  }

  private saveAllToStorage(): void {
    localStorage.setItem(STORAGE_KEY, ALL_SENTINEL);
  }

  private toTenant(row: SwitchableTenant): Tenant {
    return {
      id: row.id,
      name: row.name,
      slug: row.slug,
      code: row.code,
      isActive: row.isActive,
      status: row.status,
      subscriptionStatus: row.subscriptionStatus,
      branchCount: 0,
      departmentCount: 0,
      roleCount: 0,
      createdAt: '',
      activeUserCount: 0,
      activeVehicleCount: 0
    };
  }
}

/** @deprecated Use TenantContextService (SG-06). */
export { TenantContextService as PlatformTenantContextService };
