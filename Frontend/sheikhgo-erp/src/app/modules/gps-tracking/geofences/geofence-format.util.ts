import {
  GeofenceActivityKind,
  GeofenceAppliesTo,
  GeofenceAreaType,
  GeofenceNotifyChannel,
  GeofenceRuleSeverity,
  GeofenceVisitStatus
} from './geofence.models';
import { formatDistance, normalizeAreaType } from './geofence-geometry.util';

/** Labels and wording for the geofences screens. Pure, so the specs pin exactly what users read. */

export const SHAPE_LABELS: Record<GeofenceAreaType, string> = {
  circle: 'Circle',
  polygon: 'Polygon',
  rectangle: 'Rectangle',
  corridor: 'Corridor'
};

export function plural(n: number, one: string, many = `${one}s`): string {
  return `${n.toLocaleString('en-US')} ${n === 1 ? one : many}`;
}

/** "Circle 250 m", "Polygon", "Corridor 200 m wide". */
export function shapeLabel(fence: { areaType: string; radiusMeters?: number | null; corridorWidthMeters?: number | null }): string {
  const type = normalizeAreaType(fence.areaType);
  if (type === 'circle' && (fence.radiusMeters ?? 0) > 0) return `Circle ${formatDistance(fence.radiusMeters)}`;
  if (type === 'corridor' && (fence.corridorWidthMeters ?? 0) > 0) return `Corridor ${formatDistance(fence.corridorWidthMeters)} wide`;
  return SHAPE_LABELS[type];
}

/** Who a geofence covers, for list rows: "all vehicles", "12 vehicles", "3 drivers", "nobody yet". */
export function coverageLabel(fence: { appliesToAll: boolean; vehiclesCovered: number; driversAssigned?: number }): string {
  if (fence.appliesToAll) return 'all vehicles';
  const parts: string[] = [];
  if (fence.vehiclesCovered > 0) parts.push(plural(fence.vehiclesCovered, 'vehicle'));
  if ((fence.driversAssigned ?? 0) > 0) parts.push(plural(fence.driversAssigned!, 'driver'));
  return parts.length ? parts.join(', ') : 'nobody yet';
}

/** "Office · Polygon · 12 vehicles". */
export function listMeta(fence: {
  type?: string | null;
  areaType: string;
  radiusMeters?: number | null;
  corridorWidthMeters?: number | null;
  appliesToAll: boolean;
  vehiclesCovered: number;
  driversAssigned?: number;
}): string {
  return [fence.type?.trim() || 'No type', shapeLabel(fence), coverageLabel(fence)].join(' · ');
}

/** Minutes as "1h 20m", "42m", "61h 20m" (hours are not rolled into days, as on the design). */
export function formatDuration(minutes: number | null | undefined): string {
  const total = Math.max(0, Math.round(Number(minutes) || 0));
  const h = Math.floor(total / 60);
  const m = total % 60;
  if (h === 0) return `${m}m`;
  return `${h}h ${m.toString().padStart(2, '0')}m`;
}

export type BadgeTone = 'neutral' | 'teal' | 'amber' | 'red';

export interface Badge {
  label: string;
  tone: BadgeTone;
}

const VISIT_STATUS: Record<GeofenceVisitStatus, Badge> = {
  completed: { label: 'Completed', tone: 'neutral' },
  inside: { label: 'Inside now', tone: 'teal' },
  overstay: { label: 'Overstay', tone: 'amber' },
  idle: { label: 'Idle too long', tone: 'amber' },
  overspeed: { label: 'Overspeed', tone: 'red' },
  after_hours: { label: 'After hours', tone: 'red' }
};

export function visitStatusBadge(status: string | null | undefined): Badge {
  return VISIT_STATUS[status as GeofenceVisitStatus] ?? { label: status || 'Unknown', tone: 'neutral' };
}

/** Filter options for the visits report, in the order the server ranks statuses. */
export const VISIT_STATUS_OPTIONS: { value: GeofenceVisitStatus | 'all'; label: string }[] = [
  { value: 'all', label: 'All' },
  { value: 'inside', label: 'Inside now' },
  { value: 'overspeed', label: 'Overspeed' },
  { value: 'overstay', label: 'Overstay' },
  { value: 'after_hours', label: 'After hours' },
  { value: 'idle', label: 'Idle too long' },
  { value: 'completed', label: 'Completed' }
];

export interface ActivityRuleLimits {
  dwellMinutes?: number | null;
  speedLimitKmh?: number | null;
  idleMinutes?: number | null;
  notArrivedBy?: string | null;
}

/** "Entered", "Dwell over 30 min", … with the dot colour of the design's Today list. */
export function activityLabel(kind: GeofenceActivityKind | string, limits?: ActivityRuleLimits | null): Badge {
  switch (kind) {
    case 'entered':
      return { label: 'Entered', tone: 'teal' };
    case 'exited':
      return { label: 'Exited', tone: 'neutral' };
    case 'dwell':
      return { label: limits?.dwellMinutes ? `Dwell over ${limits.dwellMinutes} min` : 'Stayed too long', tone: 'amber' };
    case 'overspeed':
      return { label: limits?.speedLimitKmh ? `Over ${limits.speedLimitKmh} km/h inside` : 'Speeding inside', tone: 'red' };
    case 'idle':
      return { label: limits?.idleMinutes ? `Idled over ${limits.idleMinutes} min` : 'Idled too long', tone: 'amber' };
    case 'after_hours_exit':
      return { label: 'Left outside hours', tone: 'red' };
    case 'not_arrived':
      return { label: limits?.notArrivedBy ? `No vehicle by ${formatClock(limits.notArrivedBy)}` : 'No vehicle arrived', tone: 'red' };
    default:
      return { label: String(kind), tone: 'neutral' };
  }
}

/** "08:00" → "8:00 AM", "20:30" → "8:30 PM". Invalid input comes back unchanged. */
export function formatClock(hhmm: string | null | undefined): string {
  const m = /^(\d{1,2}):(\d{2})$/.exec((hhmm ?? '').trim());
  if (!m) return hhmm ?? '';
  const h = Number(m[1]);
  const suffix = h >= 12 ? 'PM' : 'AM';
  const h12 = h % 12 === 0 ? 12 : h % 12;
  return `${h12}:${m[2]} ${suffix}`;
}

export const WEEK_DAYS: { bit: number; short: string }[] = [
  { bit: 1, short: 'Mon' },
  { bit: 2, short: 'Tue' },
  { bit: 4, short: 'Wed' },
  { bit: 8, short: 'Thu' },
  { bit: 16, short: 'Fri' },
  { bit: 32, short: 'Sat' },
  { bit: 64, short: 'Sun' }
];

export const ALL_DAYS = 127;

/** Mon–Sun bitmask as "Every day", "Mon–Fri", "Mon–Sat", "Mon, Wed, Fri", "Fri–Sun". */
export function daysLabel(mask: number | null | undefined): string {
  const m = Number(mask) || 0;
  if ((m & ALL_DAYS) === ALL_DAYS) return 'Every day';
  const on = WEEK_DAYS.map(d => (m & d.bit) !== 0);
  const parts: string[] = [];
  let i = 0;
  while (i < 7) {
    if (!on[i]) {
      i++;
      continue;
    }
    let j = i;
    while (j + 1 < 7 && on[j + 1]) j++;
    const run = j - i + 1;
    if (run >= 3) parts.push(`${WEEK_DAYS[i].short}–${WEEK_DAYS[j].short}`);
    else for (let k = i; k <= j; k++) parts.push(WEEK_DAYS[k].short);
    i = j + 1;
  }
  return parts.length ? parts.join(', ') : 'No days';
}

/** "Always", or "Mon–Sat · 8:00 AM – 8:00 PM". */
export function scheduleLabel(days: number | null | undefined, start?: string | null, end?: string | null): string {
  if (days == null) return 'Always';
  const hours = start && end && start !== end ? ` · ${formatClock(start)} – ${formatClock(end)}` : ' · all day';
  return `${daysLabel(days)}${hours}`;
}

export interface RuleView {
  label: string;
  severity: GeofenceRuleSeverity;
}

export interface GeofenceRuleFields extends ActivityRuleLimits {
  alertOnEnter: boolean;
  alertOnExit: boolean;
  engineBlockOnExit: boolean;
  scheduleDays?: number | null;
  scheduleStart?: string | null;
  scheduleEnd?: string | null;
  afterHoursExitCritical: boolean;
}

/** Active rules with their severity, in the editor's order. */
export function ruleViews(f: GeofenceRuleFields): RuleView[] {
  const rules: RuleView[] = [];
  if (f.alertOnEnter) rules.push({ label: 'Entry alert', severity: 'info' });
  if (f.alertOnExit) rules.push({ label: 'Exit alert', severity: 'info' });
  if (f.dwellMinutes) rules.push({ label: `Dwell over ${f.dwellMinutes} min`, severity: 'warning' });
  if (f.speedLimitKmh) rules.push({ label: `Speed limit ${f.speedLimitKmh} km/h`, severity: 'warning' });
  if (f.idleMinutes) rules.push({ label: `Idle over ${f.idleMinutes} min`, severity: 'warning' });
  if (f.notArrivedBy) rules.push({ label: `Not arrived by ${formatClock(f.notArrivedBy)}`, severity: 'critical' });
  if (f.engineBlockOnExit) rules.push({ label: 'Engine block on exit (manager approves)', severity: 'critical' });
  if (f.afterHoursExitCritical && f.scheduleDays != null) {
    const end = f.scheduleEnd ? `after ${formatClock(f.scheduleEnd)}` : 'outside hours';
    rules.push({ label: `Exit ${end}`, severity: 'critical' });
  }
  return rules;
}

export function severityLabel(s: GeofenceRuleSeverity): string {
  return s === 'info' ? 'Info' : s === 'warning' ? 'Warning' : 'Critical';
}

export const NOTIFY_CHANNELS: { value: GeofenceNotifyChannel; label: string; short: string }[] = [
  { value: 'InApp', label: 'In-app', short: 'in-app' },
  { value: 'WhatsApp', label: 'WhatsApp', short: 'WhatsApp' },
  { value: 'Email', label: 'Email', short: 'email' },
  { value: 'Sms', label: 'SMS', short: 'SMS' },
  { value: 'DriverPush', label: 'Push to driver app', short: 'push to the driver' }
];

const CHANNEL_SHORT: Record<string, string> = {
  ...Object.fromEntries(NOTIFY_CHANNELS.map(c => [c.value, c.short])),
  Push: 'push'
};

/**
 * "Notifies: in-app, WhatsApp, email to Operations, Company owner. Schedule Mon–Sat · 8:00 AM – 8:00 PM."
 * With no channels the company's default alert delivery applies (GeofenceAlertNotifier), and with no recipients the
 * role policy picks who hears about it; the sentence says so rather than implying silence.
 */
export function notifySummary(
  channels: readonly string[],
  recipientNames: readonly string[],
  schedule: { days?: number | null; start?: string | null; end?: string | null }
): string {
  const how = channels.length
    ? `Notifies: ${channels.map(c => CHANNEL_SHORT[c] ?? c).join(', ')}`
    : 'Notifies by the company’s default alert channels';
  const who = recipientNames.length ? ` to ${recipientNames.join(', ')}` : ' to the usual alert recipients';
  const when = schedule.days == null ? 'Rules always active' : `Schedule ${scheduleLabel(schedule.days, schedule.start, schedule.end)}`;
  return `${how}${who}. ${when}.`;
}

export const APPLIES_TO_OPTIONS: { value: GeofenceAppliesTo; label: string }[] = [
  { value: 'all', label: 'All vehicles' },
  { value: 'groups', label: 'Groups and branches' },
  { value: 'vehicles', label: 'Specific vehicles' },
  { value: 'drivers', label: 'Drivers' }
];

/** "2:31 PM"-style times are formatted by TenantTimeService; this is the shared option set. */
export const TIME_OPTIONS: Intl.DateTimeFormatOptions = { hour: 'numeric', minute: '2-digit' };

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/**
 * "27 Nov, 1:27 PM" from wall-clock fields already converted to the company's zone (zonedParts) — the design's
 * day-first form, the same in every browser locale.
 */
export function formatDayTime(p: { month: number; day: number; hours: number; minutes: number } | null | undefined): string {
  if (!p) return '—';
  const h12 = p.hours % 12 === 0 ? 12 : p.hours % 12;
  return `${p.day} ${MONTHS[p.month - 1]}, ${h12}:${String(p.minutes).padStart(2, '0')} ${p.hours >= 12 ? 'PM' : 'AM'}`;
}
