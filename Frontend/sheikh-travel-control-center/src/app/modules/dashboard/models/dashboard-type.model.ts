import { Type } from '@angular/core';

/** Identifies a selectable dashboard view. Add a value here to register a new dashboard. */
export enum DashboardType {
  DEFAULT = 'DEFAULT',
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

/** Options surfaced in the selector dropdown. */
export const DASHBOARD_OPTIONS: readonly DashboardMenuOption[] = [
  { type: DashboardType.DEFAULT, label: 'Default Dashboard', icon: 'dashboard' },
  { type: DashboardType.FLEET, label: 'Fleet Dashboard', icon: 'local_shipping' }
];
