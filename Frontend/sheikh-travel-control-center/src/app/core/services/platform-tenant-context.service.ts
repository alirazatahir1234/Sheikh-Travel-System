import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { Tenant } from '../models/platform.model';

const STORAGE_KEY = 'selectedTenantId';

@Injectable({
  providedIn: 'root'
})
export class PlatformTenantContextService {
  private selectedTenantId$ = new BehaviorSubject<number | null>(this.loadFromStorage());
  private selectedTenant$ = new BehaviorSubject<Tenant | null>(null);

  get tenantId$(): Observable<number | null> {
    return this.selectedTenantId$.asObservable();
  }

  get tenant$(): Observable<Tenant | null> {
    return this.selectedTenant$.asObservable();
  }

  get currentTenantId(): number | null {
    return this.selectedTenantId$.value;
  }

  get currentTenant(): Tenant | null {
    return this.selectedTenant$.value;
  }

  selectTenant(tenant: Tenant | null): void {
    const id = tenant?.id ?? null;
    this.selectedTenantId$.next(id);
    this.selectedTenant$.next(tenant);
    this.saveToStorage(id);
  }

  selectTenantById(id: number | null): void {
    this.selectedTenantId$.next(id);
    if (id === null) {
      this.selectedTenant$.next(null);
    }
    this.saveToStorage(id);
  }

  setTenantDetails(tenant: Tenant): void {
    if (tenant.id === this.selectedTenantId$.value) {
      this.selectedTenant$.next(tenant);
    }
  }

  clear(): void {
    this.selectedTenantId$.next(null);
    this.selectedTenant$.next(null);
    localStorage.removeItem(STORAGE_KEY);
  }

  private loadFromStorage(): number | null {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (!stored) return null;
    const parsed = parseInt(stored, 10);
    return Number.isFinite(parsed) ? parsed : null;
  }

  private saveToStorage(id: number | null): void {
    if (id !== null) {
      localStorage.setItem(STORAGE_KEY, String(id));
    } else {
      localStorage.removeItem(STORAGE_KEY);
    }
  }
}
