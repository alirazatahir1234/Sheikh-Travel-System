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

  readonly isFleet = computed(() => this.selectedDashboard() === DashboardType.FLEET);

  setDashboard(type: DashboardType): void {
    this.selectedDashboard.set(type);
    try {
      localStorage.setItem(STORAGE_KEY, type);
    } catch {
      // Persistence is best-effort; ignore unavailable storage (e.g. private mode).
    }
  }

  private readInitial(): DashboardType {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored && Object.values(DashboardType).includes(stored as DashboardType)) {
        return stored as DashboardType;
      }
    } catch {
      // Fall through to default.
    }
    return DashboardType.DEFAULT;
  }
}
