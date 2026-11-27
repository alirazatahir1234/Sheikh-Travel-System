/**
 * Geofences workspace — API shapes for /gps/geofences/workspace, summary, visits, groups, types, bulk, import and
 * test-shape. Field names mirror GeofenceWorkspaceDtos.cs (camelCase over the wire); every string "enum" there is a
 * union here. Times from the API are UTC; "HH:mm" values are in the company's time zone.
 */

export type GeofenceAreaType = 'circle' | 'polygon' | 'rectangle' | 'corridor';

/** GeofenceAppliesTo in C#. */
export type GeofenceAppliesTo = 'all' | 'groups' | 'vehicles' | 'drivers';

/** GeofenceVisitStatus in C#; the first match wins in this order. */
export type GeofenceVisitStatus = 'inside' | 'overspeed' | 'overstay' | 'after_hours' | 'idle' | 'completed';

/** GeofenceActivityKind in C#. */
export type GeofenceActivityKind =
  | 'entered'
  | 'exited'
  | 'dwell'
  | 'overspeed'
  | 'idle'
  | 'after_hours_exit'
  | 'not_arrived';

/** GeofenceBulkAction in C#. */
export type GeofenceBulkAction = 'assign' | 'group' | 'enable' | 'disable' | 'delete';

/** GeofenceNotifyChannels in C#. Webhook is deliberately not offered. */
export type GeofenceNotifyChannel = 'InApp' | 'WhatsApp' | 'Email' | 'Sms' | 'Push' | 'DriverPush';

/** GeofenceVisitGroupBy in C#. 'visit' is the plain paged table and never reaches the API. */
export type GeofenceVisitGroupBy = 'visit' | 'vehicle' | 'driver' | 'geofence' | 'day';

/** Alert event types a geofence can raise (GpsAlertEvents.EventType). */
export type GeofenceRuleEventType =
  | 'geofence_enter'
  | 'geofence_exit'
  | 'geofence_dwell'
  | 'geofence_overspeed'
  | 'geofence_idle'
  | 'geofence_not_arrived'
  | 'geofence_after_hours_exit';

/** Severity shown next to each rule, as in the design (Info / Warning / Critical). */
export type GeofenceRuleSeverity = 'info' | 'warning' | 'critical';

export interface GeofenceListItem {
  id: number;
  name: string;
  areaType: GeofenceAreaType | string;
  centerLat: number;
  centerLng: number;
  radiusMeters: number;
  geoJson?: string | null;
  isActive: boolean;
  color: string;
  type?: string | null;
  isRestricted: boolean;
  groupId?: number | null;
  groupName?: string | null;
  address?: string | null;
  description?: string | null;
  bufferMeters: number;
  corridorWidthMeters?: number | null;
  areaSquareMeters?: number | null;
  perimeterMeters?: number | null;
  appliesToAll: boolean;
  vehiclesCovered: number;
  driversAssigned: number;
  insideNow: number;
  entriesToday: number;
  violationsToday: number;
  ruleCount: number;
  alertOnEnter: boolean;
  alertOnExit: boolean;
}

export interface GeofenceDetail {
  id: number;
  name: string;
  type?: string | null;
  isRestricted: boolean;
  groupId?: number | null;
  groupName?: string | null;
  address?: string | null;
  description?: string | null;
  color: string;
  isActive: boolean;
  areaType: GeofenceAreaType | string;
  centerLat: number;
  centerLng: number;
  radiusMeters: number;
  geoJson?: string | null;
  corridorWidthMeters?: number | null;
  bufferMeters: number;
  areaSquareMeters?: number | null;
  perimeterMeters?: number | null;
  alertOnEnter: boolean;
  alertOnExit: boolean;
  dwellMinutes?: number | null;
  speedLimitKmh?: number | null;
  idleMinutes?: number | null;
  /** "HH:mm", company time zone. */
  notArrivedBy?: string | null;
  engineBlockOnExit: boolean;
  /** Mon 1 … Sun 64; null = rules always active. */
  scheduleDays?: number | null;
  scheduleStart?: string | null;
  scheduleEnd?: string | null;
  afterHoursExitCritical: boolean;
  notifyChannels: GeofenceNotifyChannel[];
  cooldownMinutes: number;
  appliesTo: GeofenceAppliesTo;
  vehicleIds: number[];
  branchIds: number[];
  departmentIds: number[];
  driverIds: number[];
  recipientRoleIds: number[];
  recipientUserIds: number[];
  vehiclesCovered: number;
  ruleCount: number;
}

/** POST /geofences/workspace and PUT /geofences/workspace/{id}. */
export interface SaveGeofencePayload {
  name: string;
  type?: string | null;
  groupId?: number | null;
  address?: string | null;
  description?: string | null;
  color?: string | null;
  isActive: boolean;
  areaType: GeofenceAreaType;
  centerLat: number;
  centerLng: number;
  radiusMeters: number;
  geoJson?: string | null;
  corridorWidthMeters?: number | null;
  bufferMeters: number;
  alertOnEnter: boolean;
  alertOnExit: boolean;
  dwellMinutes?: number | null;
  speedLimitKmh?: number | null;
  idleMinutes?: number | null;
  notArrivedBy?: string | null;
  engineBlockOnExit: boolean;
  scheduleDays?: number | null;
  scheduleStart?: string | null;
  scheduleEnd?: string | null;
  afterHoursExitCritical: boolean;
  appliesTo: GeofenceAppliesTo;
  vehicleIds?: number[] | null;
  branchIds?: number[] | null;
  departmentIds?: number[] | null;
  driverIds?: number[] | null;
  notifyChannels?: GeofenceNotifyChannel[] | null;
  recipientRoleIds?: number[] | null;
  recipientUserIds?: number[] | null;
  cooldownMinutes: number;
}

/** Main screen KPI cards; each name says its window. */
export interface GeofenceSummary {
  total: number;
  active: number;
  vehiclesInsideNow: number;
  vehiclesAssigned: number;
  entriesToday: number;
  exitsToday: number;
  violationsToday: number;
  restrictedEntriesToday: number;
  overstaysToday: number;
  overspeedsToday: number;
  idleAlertsToday: number;
  afterHoursExitsToday: number;
  notArrivedToday: number;
  /** Average of completed visits that ended in the last 7 days; null when there were none. */
  avgMinutesOnSiteLast7Days?: number | null;
}

export interface GeofenceInsideVehicle {
  visitId: number;
  vehicleId: number;
  vehicleLabel: string;
  driverName?: string | null;
  enteredUtc: string;
  minutesInside: number;
  overDwell: boolean;
}

export interface GeofenceActivityItem {
  atUtc: string;
  kind: GeofenceActivityKind;
  vehicleId?: number | null;
  vehicleLabel?: string | null;
  driverName?: string | null;
  message?: string | null;
}

export interface GeofenceVisitRow {
  id: number;
  geofenceId: number;
  geofenceName: string;
  vehicleId: number;
  vehicleLabel: string;
  driverId?: number | null;
  driverName?: string | null;
  enteredUtc: string;
  exitedUtc?: string | null;
  durationMinutes: number;
  idleMinutes: number;
  maxSpeedKmh: number;
  status: GeofenceVisitStatus;
}

export interface GeofenceVisitsPage {
  items: GeofenceVisitRow[];
  total: number;
  page: number;
  pageSize: number;
}

export interface GeofenceVisitsSummary {
  visits: number;
  vehicles: number;
  totalMinutes: number;
  avgMinutes?: number | null;
  idleMinutes: number;
  breaches: number;
  overstays: number;
  overspeeds: number;
  afterHoursExits: number;
  idleBreaches: number;
  /** The selected geofence's dwell limit; null when several or none. */
  dwellTargetMinutes?: number | null;
  /** Vehicles inside during each local hour of today (24 values). */
  occupancyToday: number[];
  /** True for hours of today in which a dwell rule was breached (24 values). */
  dwellBreachHoursToday: boolean[];
}

export interface GeofenceVisitGroup {
  key: string;
  label: string;
  visits: number;
  vehicles: number;
  insideNow: number;
  totalMinutes: number;
  avgMinutes?: number | null;
  idleMinutes: number;
  breaches: number;
}

export interface GeofenceVisitGroups {
  groupBy: Exclude<GeofenceVisitGroupBy, 'visit'>;
  items: GeofenceVisitGroup[];
  capped: boolean;
  visitsRead: number;
}

/** Filters shared by the visits table, its KPIs and its groups. Dates are company-local yyyy-MM-dd, inclusive. */
export interface GeofenceVisitQuery {
  geofenceId?: number | null;
  vehicleIds?: number[] | null;
  driverId?: number | null;
  from: string;
  to: string;
  status?: GeofenceVisitStatus | 'all' | null;
  page?: number;
  pageSize?: number;
}

export interface TestGeofenceShapePayload {
  areaType: GeofenceAreaType;
  centerLat: number;
  centerLng: number;
  radiusMeters: number;
  geoJson?: string | null;
  corridorWidthMeters?: number | null;
  bufferMeters: number;
  /** Only these vehicles; null = every vehicle of the company. */
  vehicleIds?: number[] | null;
}

export interface TestGeofenceShapeResult {
  entries: number;
  vehicles: number;
  days: number;
  /** True when the position cap was reached; the count is then a lower bound. */
  capped: boolean;
}

export interface GeofenceGroup {
  id: number;
  name: string;
  sortOrder: number;
  geofenceCount: number;
}

export interface GeofenceTypeOption {
  id?: number | null;
  name: string;
  color: string;
  isRestricted: boolean;
  isBuiltIn: boolean;
}

export interface GeofenceRecipientOption {
  id: number;
  name: string;
}

export interface GeofenceRecipientOptions {
  roles: GeofenceRecipientOption[];
  users: GeofenceRecipientOption[];
}

export interface BulkGeofenceActionPayload {
  ids: number[];
  action: GeofenceBulkAction;
  /** For "group": the target group, or null to ungroup. */
  groupId?: number | null;
  /** For "assign": vehicles added to each geofence's existing assignments. */
  vehicleIds?: number[] | null;
}

export interface BulkGeofenceResult {
  affected: number;
}

export interface ImportGeofencesResult {
  created: number;
  skipped: { name: string; reason: string }[];
}

/** A shape as the editor and the map hold it, before it becomes API fields. Points are lat/lng. */
export interface GeofenceShape {
  areaType: GeofenceAreaType;
  /** Circle centre; for other shapes the API centre is computed from the points. */
  center?: { lat: number; lng: number } | null;
  radiusMeters?: number | null;
  /** Polygon / rectangle ring (open, no repeated first point) or the corridor's centre line. */
  path?: { lat: number; lng: number }[] | null;
  corridorWidthMeters?: number | null;
}
