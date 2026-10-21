import { Injectable, computed, signal } from '@angular/core';
import { DashboardType } from '../models/dashboard-type.model';

const STORAGE_KEY = 'stcc.selectedDashboard';

/**
 * Holds the currently selected dashboard for the Admin Portal `/dashboard` route.
 *
 * Deliberately free of component references so the eager app shell can inject it
 * to coordinate chrome without pulling the lazy dashboard bundle into main.
 */
@Injectable({ providedIn: 'root' })
export class DashboardService {
  readonly selectedDashboard = signal<DashboardType>(this.readInitial());

  /** @deprecated Fleet dashboard is only at `/fleet/dashboard`. Always false. */
  readonly isFleet = computed(() => false);

  setDashboard(type: DashboardType): void {
    const resolved = type === DashboardType.FLEET ? DashboardType.DEFAULT : type;
    this.selectedDashboard.set(resolved);
    try {
      localStorage.setItem(STORAGE_KEY, resolved);
    } catch {
      // Persistence is best-effort; ignore unavailable storage (e.g. private mode).
    }
  }

  private readInitial(): DashboardType {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      // Migrate legacy "Fleet" selector choice → ops default (Fleet lives at /fleet/dashboard).
      if (stored === DashboardType.FLEET) {
        localStorage.setItem(STORAGE_KEY, DashboardType.DEFAULT);
        return DashboardType.DEFAULT;
      }
      if (stored && Object.values(DashboardType).includes(stored as DashboardType)) {
        return stored as DashboardType;
      }
    } catch {
      // Fall through to default.
    }
    return DashboardType.DEFAULT;
  }
}
