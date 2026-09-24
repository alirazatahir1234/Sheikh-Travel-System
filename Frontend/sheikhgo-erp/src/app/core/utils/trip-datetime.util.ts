/**
 * Trip schedule helpers — keep Trip Date + time-only fields consistent
 * across create payload and edit reload (local wall-clock, no duplicate date UI).
 */

/** Build a local Date from `YYYY-MM-DD` + `HH:mm` (or `HH:mm:ss`). */
export function combineLocalDateAndTime(dateStr: string, timeStr: string): Date | null {
  if (!dateStr || !timeStr) return null;
  const [y, mo, d] = dateStr.split('-').map(Number);
  const timeParts = timeStr.split(':').map(Number);
  if (!y || !mo || !d || timeParts.length < 2) return null;
  if (![y, mo, d, timeParts[0], timeParts[1]].every(n => Number.isFinite(n))) return null;
  const result = new Date(y, mo - 1, d, timeParts[0], timeParts[1], 0, 0);
  return Number.isNaN(result.getTime()) ? null : result;
}

/**
 * Calendar date for API TripDate. Uses UTC noon so server `.Date` keeps the same
 * calendar day regardless of tenant timezone.
 */
export function calendarDateToApiIso(dateStr: string): string {
  const [y, mo, d] = dateStr.split('-').map(Number);
  return new Date(Date.UTC(y, mo - 1, d, 12, 0, 0)).toISOString();
}

/**
 * Serialize local wall-clock DateTime without Z/offset so SQL DATETIME2 stores
 * the same HH:mm the user entered (matches Unspecified DateTime round-trip).
 */
export function toLocalDateTimeApiString(d: Date): string {
  const pad = (n: number) => n.toString().padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
}

export function formatLocalTimeHHmm(d: Date): string {
  const pad = (n: number) => n.toString().padStart(2, '0');
  return `${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export function isoToLocalTimeHHmm(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  return formatLocalTimeHHmm(d);
}

/** Auto Expected Arrival (HH:mm) from trip date + pickup time + duration minutes. */
export function computeArrivalTimeHHmm(
  tripDate: string,
  pickupTime: string,
  durationMinutes: number
): string | null {
  if (!Number.isFinite(durationMinutes) || durationMinutes <= 0) return null;
  const start = combineLocalDateAndTime(tripDate, pickupTime);
  if (!start) return null;
  return formatLocalTimeHHmm(new Date(start.getTime() + durationMinutes * 60_000));
}
