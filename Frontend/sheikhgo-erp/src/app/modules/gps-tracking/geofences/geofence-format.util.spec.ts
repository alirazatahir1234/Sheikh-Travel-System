import {
  activityLabel,
  coverageLabel,
  daysLabel,
  formatClock,
  formatDayTime,
  formatDuration,
  listMeta,
  notifySummary,
  ruleViews,
  scheduleLabel,
  shapeLabel,
  visitStatusBadge,
  VISIT_STATUS_OPTIONS
} from './geofence-format.util';

describe('geofence wording', () => {
  it('describes list rows the way the design does', () => {
    expect(listMeta({ type: 'Office', areaType: 'polygon', appliesToAll: false, vehiclesCovered: 12 }))
      .toBe('Office · Polygon · 12 vehicles');
    expect(listMeta({ type: 'Customer', areaType: 'circle', radiusMeters: 250, appliesToAll: false, vehiclesCovered: 1 }))
      .toBe('Customer · Circle 250 m · 1 vehicle');
    expect(listMeta({ type: 'Fuel station', areaType: 'rectangle', appliesToAll: true, vehiclesCovered: 40 }))
      .toBe('Fuel station · Rectangle · all vehicles');
    expect(shapeLabel({ areaType: 'corridor', corridorWidthMeters: 200 })).toBe('Corridor 200 m wide');
    expect(coverageLabel({ appliesToAll: false, vehiclesCovered: 0, driversAssigned: 2 })).toBe('2 drivers');
    expect(coverageLabel({ appliesToAll: false, vehiclesCovered: 0 })).toBe('nobody yet');
  });

  it('formats time on site as hours and minutes without rolling into days', () => {
    expect(formatDuration(80)).toBe('1h 20m');
    expect(formatDuration(42)).toBe('42m');
    expect(formatDuration(245)).toBe('4h 05m');
    expect(formatDuration(3680)).toBe('61h 20m');
    expect(formatDuration(null)).toBe('0m');
  });

  it('maps every visit status the server sends to a label and colour', () => {
    expect(visitStatusBadge('completed')).toEqual({ label: 'Completed', tone: 'neutral' });
    expect(visitStatusBadge('inside')).toEqual({ label: 'Inside now', tone: 'teal' });
    expect(visitStatusBadge('overstay')).toEqual({ label: 'Overstay', tone: 'amber' });
    expect(visitStatusBadge('overspeed')).toEqual({ label: 'Overspeed', tone: 'red' });
    expect(visitStatusBadge('after_hours')).toEqual({ label: 'After hours', tone: 'red' });
    expect(visitStatusBadge('idle').tone).toBe('amber');
    expect(visitStatusBadge('something_new')).toEqual({ label: 'something_new', tone: 'neutral' });
    // Filter values are exactly the server's GeofenceVisitStatus values plus "all".
    expect(VISIT_STATUS_OPTIONS.map(o => o.value)).toEqual(['all', 'inside', 'overspeed', 'overstay', 'after_hours', 'idle', 'completed']);
  });

  it('names activity with the rule threshold when it is known', () => {
    expect(activityLabel('entered')).toEqual({ label: 'Entered', tone: 'teal' });
    expect(activityLabel('dwell', { dwellMinutes: 30 })).toEqual({ label: 'Dwell over 30 min', tone: 'amber' });
    expect(activityLabel('dwell').label).toBe('Stayed too long');
    expect(activityLabel('not_arrived', { notArrivedBy: '09:00' }).label).toBe('No vehicle by 9:00 AM');
    expect(activityLabel('after_hours_exit').tone).toBe('red');
  });

  it('compresses schedule days and hours', () => {
    expect(daysLabel(127)).toBe('Every day');
    expect(daysLabel(31)).toBe('Mon–Fri');
    expect(daysLabel(63)).toBe('Mon–Sat');
    expect(daysLabel(1 | 4 | 16)).toBe('Mon, Wed, Fri');
    expect(daysLabel(16 | 32 | 64)).toBe('Fri–Sun');
    expect(daysLabel(0)).toBe('No days');
    expect(scheduleLabel(null)).toBe('Always');
    expect(scheduleLabel(63, '08:00', '20:00')).toBe('Mon–Sat · 8:00 AM – 8:00 PM');
    expect(scheduleLabel(127, '22:00', '06:00')).toBe('Every day · 10:00 PM – 6:00 AM');
    expect(formatClock('00:15')).toBe('12:15 AM');
    expect(formatClock('12:00')).toBe('12:00 PM');
  });

  it('writes visit times day first, as on the design', () => {
    expect(formatDayTime({ month: 11, day: 27, hours: 13, minutes: 27 })).toBe('27 Nov, 1:27 PM');
    expect(formatDayTime({ month: 1, day: 5, hours: 0, minutes: 5 })).toBe('5 Jan, 12:05 AM');
    expect(formatDayTime(null)).toBe('—');
  });

  it('lists active rules with their severity in the editor order', () => {
    const rules = ruleViews({
      alertOnEnter: true, alertOnExit: true, dwellMinutes: 30, speedLimitKmh: 30, idleMinutes: null,
      notArrivedBy: '09:00', engineBlockOnExit: true, scheduleDays: 63, scheduleStart: '08:00', scheduleEnd: '20:00',
      afterHoursExitCritical: true
    });
    expect(rules.map(r => `${r.label}|${r.severity}`)).toEqual([
      'Entry alert|info',
      'Exit alert|info',
      'Dwell over 30 min|warning',
      'Speed limit 30 km/h|warning',
      'Not arrived by 9:00 AM|critical',
      'Engine block on exit (manager approves)|critical',
      'Exit after 8:00 PM|critical'
    ]);
    expect(ruleViews({ alertOnEnter: false, alertOnExit: false, engineBlockOnExit: false, afterHoursExitCritical: true, scheduleDays: null }))
      .toEqual([]);
  });

  it('says who is notified, and what happens when nothing is picked', () => {
    expect(notifySummary(['InApp', 'WhatsApp', 'Email'], ['Operations', 'Company owner'], { days: 63, start: '08:00', end: '20:00' }))
      .toBe('Notifies: in-app, WhatsApp, email to Operations, Company owner. Schedule Mon–Sat · 8:00 AM – 8:00 PM.');
    expect(notifySummary([], [], { days: null }))
      .toBe('Notifies by the company’s default alert channels to the usual alert recipients. Rules always active.');
  });
});
