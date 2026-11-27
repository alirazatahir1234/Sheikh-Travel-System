import {
  GeofenceAppliesTo,
  GeofenceDetail,
  GeofenceNotifyChannel,
  GeofenceShape,
  SaveGeofencePayload
} from './geofence.models';
import {
  MAX_BUFFER_M,
  MAX_CIRCLE_RADIUS_M,
  MAX_CORRIDOR_WIDTH_M,
  MIN_CIRCLE_RADIUS_M,
  MIN_CORRIDOR_WIDTH_M,
  completeShape,
  shapeToApi
} from './geofence-geometry.util';
import { NOTIFY_CHANNELS } from './geofence-format.util';

/**
 * The create / edit screen's form, kept as one plain object so it can be validated, saved as a draft and turned
 * into the API payload by pure functions. Rule values stay filled in while a rule is switched off, so ticking it
 * again brings the last number back; only ticked rules reach the payload.
 */
export interface GeofenceEditorModel {
  name: string;
  type: string | null;
  groupId: number | null;
  address: string;
  description: string;
  color: string;
  isActive: boolean;
  bufferMeters: number;
  alertOnEnter: boolean;
  alertOnExit: boolean;
  dwellOn: boolean;
  dwellMinutes: number;
  speedOn: boolean;
  speedLimitKmh: number;
  idleOn: boolean;
  idleMinutes: number;
  notArrivedOn: boolean;
  notArrivedBy: string;
  engineBlockOnExit: boolean;
  scheduleMode: 'always' | 'schedule';
  scheduleDays: number;
  scheduleStart: string;
  scheduleEnd: string;
  afterHoursExitCritical: boolean;
  appliesTo: GeofenceAppliesTo;
  vehicleIds: number[];
  branchIds: number[];
  departmentIds: number[];
  driverIds: number[];
  notifyChannels: GeofenceNotifyChannel[];
  recipientRoleIds: number[];
  recipientUserIds: number[];
  cooldownMinutes: number;
}

export const DEFAULT_COLOR = '#0f766e';

export function defaultEditorModel(): GeofenceEditorModel {
  return {
    name: '',
    type: 'Office',
    groupId: null,
    address: '',
    description: '',
    color: DEFAULT_COLOR,
    isActive: true,
    bufferMeters: 25,
    alertOnEnter: true,
    alertOnExit: true,
    dwellOn: false,
    dwellMinutes: 30,
    speedOn: false,
    speedLimitKmh: 30,
    idleOn: false,
    idleMinutes: 10,
    notArrivedOn: false,
    notArrivedBy: '09:00',
    engineBlockOnExit: false,
    scheduleMode: 'always',
    scheduleDays: 63, // Mon–Sat
    scheduleStart: '08:00',
    scheduleEnd: '20:00',
    afterHoursExitCritical: false,
    appliesTo: 'all',
    vehicleIds: [],
    branchIds: [],
    departmentIds: [],
    driverIds: [],
    notifyChannels: ['InApp'],
    recipientRoleIds: [],
    recipientUserIds: [],
    cooldownMinutes: 15
  };
}

/** The editor's form for a saved geofence. Switched-off rules keep the defaults so ticking them shows a sensible number. */
export function modelFromDetail(d: GeofenceDetail): GeofenceEditorModel {
  const base = defaultEditorModel();
  return {
    ...base,
    name: d.name,
    type: d.type ?? null,
    groupId: d.groupId ?? null,
    address: d.address ?? '',
    description: d.description ?? '',
    color: d.color || DEFAULT_COLOR,
    isActive: d.isActive,
    bufferMeters: d.bufferMeters ?? 25,
    alertOnEnter: d.alertOnEnter,
    alertOnExit: d.alertOnExit,
    dwellOn: d.dwellMinutes != null,
    dwellMinutes: d.dwellMinutes ?? base.dwellMinutes,
    speedOn: d.speedLimitKmh != null,
    speedLimitKmh: d.speedLimitKmh ?? base.speedLimitKmh,
    idleOn: d.idleMinutes != null,
    idleMinutes: d.idleMinutes ?? base.idleMinutes,
    notArrivedOn: !!d.notArrivedBy,
    notArrivedBy: d.notArrivedBy || base.notArrivedBy,
    engineBlockOnExit: d.engineBlockOnExit,
    scheduleMode: d.scheduleDays != null ? 'schedule' : 'always',
    scheduleDays: d.scheduleDays ?? base.scheduleDays,
    scheduleStart: d.scheduleStart || base.scheduleStart,
    scheduleEnd: d.scheduleEnd || base.scheduleEnd,
    afterHoursExitCritical: d.afterHoursExitCritical,
    appliesTo: d.appliesTo,
    vehicleIds: [...(d.vehicleIds ?? [])],
    branchIds: [...(d.branchIds ?? [])],
    departmentIds: [...(d.departmentIds ?? [])],
    driverIds: [...(d.driverIds ?? [])],
    notifyChannels: [...(d.notifyChannels ?? [])],
    recipientRoleIds: [...(d.recipientRoleIds ?? [])],
    recipientUserIds: [...(d.recipientUserIds ?? [])],
    cooldownMinutes: d.cooldownMinutes || base.cooldownMinutes
  };
}

export type EditorField =
  | 'name'
  | 'shape'
  | 'bufferMeters'
  | 'dwellMinutes'
  | 'speedLimitKmh'
  | 'idleMinutes'
  | 'notArrivedBy'
  | 'schedule'
  | 'appliesTo'
  | 'cooldownMinutes'
  | 'color';

export type EditorErrors = Partial<Record<EditorField, string>>;

const HH_MM = /^([01]\d|2[0-3]):[0-5]\d$/;

const inRange = (v: unknown, min: number, max: number): boolean => {
  const n = Number(v);
  return Number.isFinite(n) && n >= min && n <= max;
};

/** The server's SaveGeofenceDtoValidator rules, plus the editor's own (a shape and someone to apply to). */
export function validateEditor(m: GeofenceEditorModel, shape: GeofenceShape | null, opts?: { restrictedType?: boolean }): EditorErrors {
  const e: EditorErrors = {};
  if (!m.name.trim()) e.name = 'Give the geofence a name.';
  else if (m.name.trim().length > 100) e.name = 'Keep the name to 100 characters.';

  const s = completeShape(shape);
  if (!s) {
    e.shape = shape?.areaType === 'corridor'
      ? 'Draw the corridor: click at least two points along the route and set a width.'
      : shape?.areaType === 'circle' || !shape
        ? 'Place the circle on the map.'
        : 'Draw the shape on the map: click at least three points.';
  } else if (s.areaType === 'circle' && !inRange(s.radiusMeters, MIN_CIRCLE_RADIUS_M, MAX_CIRCLE_RADIUS_M)) {
    e.shape = `Radius must be between ${MIN_CIRCLE_RADIUS_M} m and ${MAX_CIRCLE_RADIUS_M / 1000} km.`;
  } else if (s.areaType === 'corridor' && !inRange(s.corridorWidthMeters, MIN_CORRIDOR_WIDTH_M, MAX_CORRIDOR_WIDTH_M)) {
    e.shape = `Corridor width must be between ${MIN_CORRIDOR_WIDTH_M} and ${MAX_CORRIDOR_WIDTH_M.toLocaleString('en-US')} m.`;
  }

  if (!inRange(m.bufferMeters, 0, MAX_BUFFER_M)) e.bufferMeters = `Buffer must be 0–${MAX_BUFFER_M} m.`;
  if (m.dwellOn && !inRange(m.dwellMinutes, 1, 10_080)) e.dwellMinutes = 'Dwell limit must be between 1 minute and 7 days.';
  if (m.speedOn && !inRange(m.speedLimitKmh, 1, 300)) e.speedLimitKmh = 'Speed limit must be between 1 and 300 km/h.';
  if (m.idleOn && !inRange(m.idleMinutes, 1, 1_440)) e.idleMinutes = 'Idle limit must be between 1 and 1440 minutes.';
  if (m.notArrivedOn && !HH_MM.test(m.notArrivedBy)) e.notArrivedBy = 'Use a 24-hour time such as 09:00.';
  if (m.scheduleMode === 'schedule') {
    if (!m.scheduleDays) e.schedule = 'Pick at least one day.';
    else if (!HH_MM.test(m.scheduleStart) || !HH_MM.test(m.scheduleEnd)) e.schedule = 'Set start and end as 24-hour times such as 08:00.';
  }
  if (!inRange(m.cooldownMinutes, 1, 1_440)) e.cooldownMinutes = 'Repeat window must be between 1 and 1440 minutes.';

  if (m.appliesTo === 'vehicles' && !m.vehicleIds.length) e.appliesTo = 'Pick at least one vehicle, or choose All vehicles.';
  else if (m.appliesTo === 'groups' && !m.branchIds.length && !m.departmentIds.length) e.appliesTo = 'Pick at least one branch or department.';
  else if (m.appliesTo === 'drivers' && !m.driverIds.length) e.appliesTo = 'Pick at least one driver.';

  if (!/^#[0-9a-fA-F]{6}$/.test(m.color)) e.color = 'Pick a colour.';
  else if (m.color.toLowerCase() === RESTRICTED_RED && !opts?.restrictedType) e.color = 'Red is kept for restricted zones; pick another colour or a restricted type.';
  return e;
}

/** The design keeps this red for restricted zones (GeofenceTypeCatalog.RestrictedColor). */
export const RESTRICTED_RED = '#dc2626';

const CHANNELS_ON_SCREEN = new Set<string>(NOTIFY_CHANNELS.map(c => c.value));

/**
 * The API payload. Channels the screen does not show (set through the API, such as plain Push) are kept from
 * <paramref name="keepChannels"/> so saving never drops them silently.
 */
export function buildSavePayload(m: GeofenceEditorModel, shape: GeofenceShape, keepChannels: readonly string[] = []): SaveGeofencePayload {
  const geometry = shapeToApi(shape);
  const schedule = m.scheduleMode === 'schedule';
  const channels = [
    ...m.notifyChannels.filter(c => CHANNELS_ON_SCREEN.has(c)),
    ...keepChannels.filter(c => !CHANNELS_ON_SCREEN.has(c))
  ] as GeofenceNotifyChannel[];
  return {
    name: m.name.trim(),
    type: m.type?.trim() || null,
    groupId: m.groupId ?? null,
    address: m.address.trim() || null,
    description: m.description.trim() || null,
    color: m.color.toLowerCase(),
    isActive: m.isActive,
    ...geometry,
    bufferMeters: Math.round(Number(m.bufferMeters) || 0),
    alertOnEnter: m.alertOnEnter,
    alertOnExit: m.alertOnExit,
    dwellMinutes: m.dwellOn ? Math.round(Number(m.dwellMinutes)) : null,
    speedLimitKmh: m.speedOn ? Math.round(Number(m.speedLimitKmh) * 10) / 10 : null,
    idleMinutes: m.idleOn ? Math.round(Number(m.idleMinutes)) : null,
    notArrivedBy: m.notArrivedOn ? m.notArrivedBy : null,
    engineBlockOnExit: m.engineBlockOnExit,
    scheduleDays: schedule ? m.scheduleDays : null,
    scheduleStart: schedule ? m.scheduleStart : null,
    scheduleEnd: schedule ? m.scheduleEnd : null,
    afterHoursExitCritical: schedule && m.afterHoursExitCritical,
    // Every list is sent unless the geofence covers all vehicles: lists outside the chosen kind are shown on the
    // screen as "Also assigned" (with Remove) and the server keeps what it is sent.
    appliesTo: m.appliesTo,
    vehicleIds: m.appliesTo === 'all' ? null : [...m.vehicleIds],
    branchIds: m.appliesTo === 'all' ? null : [...m.branchIds],
    departmentIds: m.appliesTo === 'all' ? null : [...m.departmentIds],
    driverIds: m.appliesTo === 'all' ? null : [...m.driverIds],
    notifyChannels: channels,
    recipientRoleIds: [...m.recipientRoleIds],
    recipientUserIds: [...m.recipientUserIds],
    cooldownMinutes: Math.round(Number(m.cooldownMinutes))
  };
}

export type AssignmentList = 'vehicleIds' | 'branchIds' | 'departmentIds' | 'driverIds';

const MODE_LISTS: Record<GeofenceAppliesTo, AssignmentList[]> = {
  all: [],
  vehicles: ['vehicleIds'],
  groups: ['branchIds', 'departmentIds'],
  drivers: ['driverIds']
};

/** Assignment lists that are not part of the chosen "applies to" but will be saved too (shown as "Also assigned"). */
export function extraAssignments(m: GeofenceEditorModel): AssignmentList[] {
  if (m.appliesTo === 'all') return [];
  const own = new Set(MODE_LISTS[m.appliesTo]);
  return (['vehicleIds', 'branchIds', 'departmentIds', 'driverIds'] as AssignmentList[])
    .filter(list => !own.has(list) && m[list].length > 0);
}

/** A full copy of a saved geofence under a new name, for Duplicate (the old duplicate route drops rules). */
export function duplicatePayload(d: GeofenceDetail, shape: GeofenceShape): SaveGeofencePayload {
  const name = `${d.name.slice(0, 92)} (copy)`;
  return { ...buildSavePayload({ ...modelFromDetail(d), name }, shape, d.notifyChannels ?? []), isActive: d.isActive };
}

// ---- Drafts ---------------------------------------------------------------------------------------------------------

export interface GeofenceDraft {
  savedAt: string;
  model: GeofenceEditorModel;
  shape: GeofenceShape | null;
}

/** One draft per company, user and geofence ("new" for a geofence not saved yet). */
export function draftKey(tenantId: number | null | undefined, userId: number | null | undefined, geofenceId: number | null): string {
  return `sg_geofence_draft:${tenantId ?? 0}:${userId ?? 0}:${geofenceId ?? 'new'}`;
}

export function saveDraft(key: string, draft: GeofenceDraft): boolean {
  try {
    localStorage.setItem(key, JSON.stringify(draft));
    return true;
  } catch {
    return false;
  }
}

export function loadDraft(key: string): GeofenceDraft | null {
  try {
    const raw = localStorage.getItem(key);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as GeofenceDraft;
    if (!parsed?.model || typeof parsed.savedAt !== 'string') return null;
    return { ...parsed, model: { ...defaultEditorModel(), ...parsed.model } };
  } catch {
    return null;
  }
}

export function clearDraft(key: string): void {
  try {
    localStorage.removeItem(key);
  } catch {
    /* storage blocked — nothing to clear */
  }
}

// ---- Undo / redo ----------------------------------------------------------------------------------------------------

/** Undo and redo for the drawn shape. Push the state before each change; undo and redo hand back the state to show. */
export class ShapeHistory {
  private undoStack: (GeofenceShape | null)[] = [];
  private redoStack: (GeofenceShape | null)[] = [];

  constructor(private readonly limit = 100) {}

  get canUndo(): boolean {
    return this.undoStack.length > 0;
  }

  get canRedo(): boolean {
    return this.redoStack.length > 0;
  }

  push(before: GeofenceShape | null): void {
    const last = this.undoStack[this.undoStack.length - 1];
    if (this.undoStack.length && JSON.stringify(last) === JSON.stringify(before)) return;
    this.undoStack.push(clone(before));
    if (this.undoStack.length > this.limit) this.undoStack.shift();
    this.redoStack = [];
  }

  undo(current: GeofenceShape | null): GeofenceShape | null | undefined {
    if (!this.undoStack.length) return undefined;
    this.redoStack.push(clone(current));
    return this.undoStack.pop();
  }

  redo(current: GeofenceShape | null): GeofenceShape | null | undefined {
    if (!this.redoStack.length) return undefined;
    this.undoStack.push(clone(current));
    return this.redoStack.pop();
  }

  clear(): void {
    this.undoStack = [];
    this.redoStack = [];
  }
}

function clone<T>(v: T): T {
  return v === null || v === undefined ? v : (JSON.parse(JSON.stringify(v)) as T);
}
