/** Chart bucket size passed to the maintenance dashboard API. */
export function maintenanceDashboardGranularity(period: string): string {
  switch (period.trim().toLowerCase()) {
    case 'year':
    case 'quarter':
      return 'Month';
    case 'month':
      return 'Week';
    default:
      return 'Day';
  }
}

/** Mirrors backend MaintenanceDashboardQueries.ResolveRange for client-side filtering. */
export function resolveMaintenancePeriodRange(period: string, now = new Date()): { from: Date; to: Date } {
  const end = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1);
  const key = period.trim().toLowerCase();

  switch (key) {
    case 'today':
      return { from: new Date(now.getFullYear(), now.getMonth(), now.getDate()), to: end };
    case 'week': {
      // Monday-start week so "Week" matches ops expectation (not Sunday-start JS default).
      const day = now.getDay(); // 0 Sun .. 6 Sat
      const daysFromMonday = day === 0 ? 6 : day - 1;
      const from = new Date(now.getFullYear(), now.getMonth(), now.getDate() - daysFromMonday);
      return { from, to: end };
    }
    case 'quarter': {
      const quarterMonth = Math.floor(now.getMonth() / 3) * 3;
      return { from: new Date(now.getFullYear(), quarterMonth, 1), to: end };
    }
    case 'year':
      return { from: new Date(now.getFullYear(), 0, 1), to: end };
    default:
      return { from: new Date(now.getFullYear(), now.getMonth(), 1), to: end };
  }
}

/** Parse API dates (ISO or date-only) into local calendar dates for period checks. */
export function parseMaintenanceDate(value: string | Date | null | undefined): Date | null {
  if (!value) return null;
  if (value instanceof Date) {
    return Number.isNaN(value.getTime()) ? null : value;
  }

  const raw = String(value).trim();
  if (!raw) return null;

  // Date-only (yyyy-MM-dd) → local midnight to avoid UTC shifting the calendar day.
  const dateOnly = /^(\d{4})-(\d{2})-(\d{2})$/.exec(raw);
  if (dateOnly) {
    const y = Number(dateOnly[1]);
    const m = Number(dateOnly[2]) - 1;
    const d = Number(dateOnly[3]);
    const local = new Date(y, m, d);
    return Number.isNaN(local.getTime()) ? null : local;
  }

  const parsed = new Date(raw);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

export function isWithinMaintenancePeriod(
  value: string | Date | null | undefined,
  period: string,
  now = new Date()
): boolean {
  const date = parseMaintenanceDate(value);
  if (!date) return false;

  const { from, to } = resolveMaintenancePeriodRange(period, now);
  return date >= from && date < to;
}
