import { QuickAction } from './fleet-dashboard.model';

/** Chart / brand accents shared by fleet dashboard widgets. */
export const FLEET_PRIMARY = '#005f49';
export const FLEET_SECONDARY = '#535c86';
export const FLEET_NEUTRAL = '#dae3ee';

/** Primary quick actions — routes point at real create/list hubs (not demo paths). */
export const QUICK_ACTIONS: QuickAction[] = [
  { id: 'add-vehicle', label: 'Add Vehicle', icon: 'add_circle', route: '/vehicles/new', tone: 'green' },
  { id: 'add-driver', label: 'Add Driver', icon: 'person_add', route: '/drivers', tone: 'blue' },
  { id: 'schedule-maint', label: 'Schedule Maint.', icon: 'schedule', route: '/fleet/maintenance', tone: 'orange' },
  { id: 'assign-driver', label: 'Assign Driver', icon: 'assignment_ind', route: '/fleet/assignments', tone: 'blue' },
  { id: 'live-map', label: 'Live Map', icon: 'map', route: '/gps-tracking/live', tone: 'green' }
];

/** @deprecated Use QUICK_ACTIONS */
export const QUICK_ACTIONS_MOCK = QUICK_ACTIONS;
