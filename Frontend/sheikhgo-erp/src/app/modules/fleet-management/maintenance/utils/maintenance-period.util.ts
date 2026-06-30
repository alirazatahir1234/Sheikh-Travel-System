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
      const from = new Date(now.getFullYear(), now.getMonth(), now.getDate() - now.getDay());
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

export function isWithinMaintenancePeriod(
  value: string | Date | null | undefined,
  period: string,
  now = new Date()
): boolean {
  if (!value) return false;
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return false;

  const { from, to } = resolveMaintenancePeriodRange(period, now);
  return date >= from && date < to;
}
