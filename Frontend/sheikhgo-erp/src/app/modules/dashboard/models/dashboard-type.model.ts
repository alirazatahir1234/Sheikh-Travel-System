import { Type } from '@angular/core';

/**
 * Identifies a selectable dashboard view on `/dashboard`.
 * Fleet asset-health lives at `/fleet/dashboard` (single primary entry) — not in this selector.
 */
export enum DashboardType {
  DEFAULT = 'DEFAULT',
  /** @deprecated Kept for localStorage migration only — redirects to DEFAULT. */
  FLEET = 'FLEET'
}

/** Display metadata for the dashboard selector (no component reference — keeps it lazy-safe). */
export interface DashboardMenuOption {
  type: DashboardType;
  label: string;
  icon: string;
}

/** Maps a dashboard type to the component the container renders via NgComponentOutlet. */
export interface DashboardRegistryEntry extends DashboardMenuOption {
  component: Type<unknown>;
}

/** Options surfaced in the `/dashboard` selector. Fleet is only under Fleet → Dashboard. */
export const DASHBOARD_OPTIONS: readonly DashboardMenuOption[] = [
  { type: DashboardType.DEFAULT, label: 'Operations Dashboard', icon: 'dashboard' }
];
