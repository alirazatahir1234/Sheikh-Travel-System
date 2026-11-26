/** Date helpers for TravelNexus forms. The API stores UTC; the browser shows local time. */

/** ISO string → `yyyy-MM-dd` for `<input type="date">` (date-only values, no timezone shift). */
export const toDateInput = (iso: string | null | undefined): string => (iso ? iso.substring(0, 10) : '');

/** ISO UTC string → local `yyyy-MM-ddTHH:mm` for `<input type="datetime-local">`. */
export function toDateTimeLocal(iso: string | null | undefined): string {
  if (!iso) return '';
  const d = new Date(iso.endsWith('Z') || iso.includes('+') ? iso : `${iso}Z`);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** Local `yyyy-MM-ddTHH:mm` → ISO UTC string, or null when blank. */
export const fromDateTimeLocal = (value: string | null | undefined): string | null =>
  value ? new Date(value).toISOString() : null;

/** API timestamps without an offset are UTC; make that explicit for the date pipe. */
export const asUtc = (iso: string | null | undefined): string | null =>
  !iso ? null : iso.endsWith('Z') || iso.includes('+') ? iso : `${iso}Z`;

/** True when a follow-up time is now or in the past. */
export const isDue = (iso: string | null | undefined, now = new Date()): boolean =>
  !!iso && new Date(asUtc(iso)!) <= now;

/** A `?status=` query value when it is one of the allowed statuses, else '' (never trust the URL). */
export function statusFromQuery<T extends string>(value: string | null, allowed: readonly T[]): T | '' {
  return value && (allowed as readonly string[]).includes(value) ? (value as T) : '';
}
