import {
  ShapeHistory,
  buildSavePayload,
  clearDraft,
  defaultEditorModel,
  draftKey,
  duplicatePayload,
  extraAssignments,
  loadDraft,
  modelFromDetail,
  saveDraft,
  validateEditor
} from './geofence-editor.model';
import { GeofenceDetail, GeofenceShape } from './geofence.models';

const CIRCLE: GeofenceShape = { areaType: 'circle', center: { lat: 32.26, lng: 74.66 }, radiusMeters: 250 };
const POLYGON: GeofenceShape = {
  areaType: 'polygon',
  path: [{ lat: 31.5, lng: 74.3 }, { lat: 31.5, lng: 74.31 }, { lat: 31.51, lng: 74.31 }]
};

function detail(overrides: Partial<GeofenceDetail> = {}): GeofenceDetail {
  return {
    id: 7, name: 'Pasrur Head Office', type: 'Office', isRestricted: false, groupId: 2, groupName: 'Depots', address: 'Sialkot Rd',
    description: 'Main yard', color: '#0f766e', isActive: true, areaType: 'circle', centerLat: 32.26, centerLng: 74.66,
    radiusMeters: 250, geoJson: null, corridorWidthMeters: null, bufferMeters: 30, areaSquareMeters: 196_350, perimeterMeters: 1_571,
    alertOnEnter: true, alertOnExit: false, dwellMinutes: 45, speedLimitKmh: null, idleMinutes: null, notArrivedBy: '09:30',
    engineBlockOnExit: false, scheduleDays: 63, scheduleStart: '08:00', scheduleEnd: '20:00', afterHoursExitCritical: true,
    notifyChannels: ['InApp', 'Push'], cooldownMinutes: 20, appliesTo: 'groups', vehicleIds: [11, 12], branchIds: [3],
    departmentIds: [], driverIds: [], recipientRoleIds: [5], recipientUserIds: [9], vehiclesCovered: 9, ruleCount: 4,
    ...overrides
  };
}

describe('geofence editor model', () => {
  it('starts a new geofence with entry and exit alerts, all vehicles and in-app notices', () => {
    const m = defaultEditorModel();
    expect(m.alertOnEnter && m.alertOnExit).toBeTrue();
    expect(m.appliesTo).toBe('all');
    expect(m.notifyChannels).toEqual(['InApp']);
    expect(m.bufferMeters).toBe(25);
    expect(m.cooldownMinutes).toBe(15);
  });

  it('turns the form into the API payload: only ticked rules, schedule only when chosen', () => {
    const m = { ...defaultEditorModel(), name: '  Depot  ', dwellOn: true, dwellMinutes: 30, speedOn: false, speedLimitKmh: 40 };
    const p = buildSavePayload(m, CIRCLE);
    expect(p.name).toBe('Depot');
    expect(p.areaType).toBe('circle');
    expect(p.radiusMeters).toBe(250);
    expect(p.dwellMinutes).toBe(30);
    expect(p.speedLimitKmh).toBeNull();
    expect(p.scheduleDays).toBeNull();
    expect(p.scheduleStart).toBeNull();
    expect(p.afterHoursExitCritical).toBeFalse();
    expect(p.vehicleIds).toBeNull();

    const scheduled = buildSavePayload({ ...m, scheduleMode: 'schedule', afterHoursExitCritical: true, speedOn: true, speedLimitKmh: 32.25 }, POLYGON);
    expect(scheduled.scheduleDays).toBe(63);
    expect(scheduled.scheduleStart).toBe('08:00');
    expect(scheduled.scheduleEnd).toBe('20:00');
    expect(scheduled.afterHoursExitCritical).toBeTrue();
    expect(scheduled.speedLimitKmh).toBe(32.3);
    expect(JSON.parse(scheduled.geoJson!).geometry.type).toBe('Polygon');
  });

  it('keeps assignment lists outside the chosen kind, so saving never drops them', () => {
    const m = modelFromDetail(detail());
    expect(m.appliesTo).toBe('groups');
    expect(extraAssignments(m)).toEqual(['vehicleIds']);
    const p = buildSavePayload(m, CIRCLE);
    expect(p.branchIds).toEqual([3]);
    expect(p.vehicleIds).toEqual([11, 12]);

    const all = buildSavePayload({ ...m, appliesTo: 'all' }, CIRCLE);
    expect(all.vehicleIds).toBeNull();
    expect(all.branchIds).toBeNull();
    expect(extraAssignments({ ...m, appliesTo: 'all' })).toEqual([]);
  });

  it('keeps channels the screen does not show (set through the API)', () => {
    const m = modelFromDetail(detail());
    const p = buildSavePayload({ ...m, notifyChannels: ['WhatsApp'] }, CIRCLE, ['InApp', 'Push']);
    expect(p.notifyChannels).toEqual(['WhatsApp', 'Push']);
  });

  it('loads a saved geofence into the form, keeping default numbers for rules that are off', () => {
    const m = modelFromDetail(detail());
    expect(m.dwellOn).toBeTrue();
    expect(m.dwellMinutes).toBe(45);
    expect(m.speedOn).toBeFalse();
    expect(m.speedLimitKmh).toBe(30);
    expect(m.notArrivedOn).toBeTrue();
    expect(m.notArrivedBy).toBe('09:30');
    expect(m.scheduleMode).toBe('schedule');
    expect(m.recipientRoleIds).toEqual([5]);
  });

  it('checks the form like the server does, plus the editor’s own rules', () => {
    const ok = { ...defaultEditorModel(), name: 'Depot' };
    expect(validateEditor(ok, CIRCLE)).toEqual({});

    const errors = validateEditor(
      { ...ok, name: '', bufferMeters: 900, dwellOn: true, dwellMinutes: 0, notArrivedOn: true, notArrivedBy: '9am',
        scheduleMode: 'schedule', scheduleDays: 0, cooldownMinutes: 0, appliesTo: 'vehicles', vehicleIds: [], color: '#dc2626' },
      null
    );
    expect(Object.keys(errors).sort()).toEqual(
      ['appliesTo', 'bufferMeters', 'color', 'cooldownMinutes', 'dwellMinutes', 'name', 'notArrivedBy', 'schedule', 'shape'].sort()
    );
    expect(validateEditor({ ...ok, color: '#dc2626' }, CIRCLE, { restrictedType: true })).toEqual({});
    expect(validateEditor(ok, { ...CIRCLE, radiusMeters: 60_000 }).shape).toContain('50 km');
    expect(validateEditor(ok, { areaType: 'corridor', path: POLYGON.path!.slice(0, 2), corridorWidthMeters: 5 }).shape).toContain('width');
    expect(validateEditor({ ...ok, dwellOn: false, dwellMinutes: 0 }, CIRCLE).dwellMinutes).toBeUndefined();
  });

  it('copies a geofence with all its rules under a new name', () => {
    const p = duplicatePayload(detail(), CIRCLE);
    expect(p.name).toBe('Pasrur Head Office (copy)');
    expect(p.dwellMinutes).toBe(45);
    expect(p.scheduleDays).toBe(63);
    expect(p.notifyChannels).toEqual(['InApp', 'Push']);
    expect(p.branchIds).toEqual([3]);
  });

  it('saves drafts per company, user and geofence', () => {
    expect(draftKey(4, 9, null)).toBe('sg_geofence_draft:4:9:new');
    expect(draftKey(4, 9, 7)).toBe('sg_geofence_draft:4:9:7');
    const key = draftKey(999, 999, 999);
    clearDraft(key);
    expect(loadDraft(key)).toBeNull();
    expect(saveDraft(key, { savedAt: '2026-11-27T09:46:00Z', model: { ...defaultEditorModel(), name: 'Draft' }, shape: CIRCLE })).toBeTrue();
    const back = loadDraft(key)!;
    expect(back.model.name).toBe('Draft');
    expect(back.shape).toEqual(CIRCLE);
    clearDraft(key);
    expect(loadDraft(key)).toBeNull();
  });

  it('undoes and redoes shape changes, including back to no shape', () => {
    const h = new ShapeHistory();
    expect(h.canUndo).toBeFalse();
    h.push(null); // before the circle was drawn
    h.push(CIRCLE); // before it became a polygon
    expect(h.undo(POLYGON)).toEqual(CIRCLE);
    expect(h.undo(CIRCLE)).toBeNull();
    expect(h.undo(null)).toBeUndefined();
    expect(h.redo(null)).toEqual(CIRCLE);
    expect(h.redo(CIRCLE)).toEqual(POLYGON);
    expect(h.canRedo).toBeFalse();
    h.undo(POLYGON);
    h.push(POLYGON);
    expect(h.canRedo).toBeFalse();
  });
});
