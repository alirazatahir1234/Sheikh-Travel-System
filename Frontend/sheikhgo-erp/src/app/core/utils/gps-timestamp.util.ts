/**
 * Parse GPS / telemetry timestamps for age calculations.
 * SQL DATETIME2 values often serialize without a timezone (no Z / offset).
 * Browsers treat those as local time, which in UTC+5 makes a fresh UTC instant
 * look ~5 hours old and flips Idle → Offline. Treat timezone-less ISO as UTC.
 */
export function parseGpsTimestamp(iso: string | null | undefined): number {
  if (!iso) return NaN;
  const trimmed = iso.trim();
  if (!trimmed) return NaN;

  const hasZone = /([zZ]|[+-]\d{2}:?\d{2})$/.test(trimmed);
  const normalized = hasZone ? trimmed : `${trimmed}Z`;
  return new Date(normalized).getTime();
}

/** Compare two GPS timestamp strings; returns positive if a is newer than b. */
export function compareGpsTimestamps(
  a: string | null | undefined,
  b: string | null | undefined
): number {
  const aMs = parseGpsTimestamp(a);
  const bMs = parseGpsTimestamp(b);
  const aOk = Number.isFinite(aMs);
  const bOk = Number.isFinite(bMs);
  if (!aOk && !bOk) return 0;
  if (!aOk) return -1;
  if (!bOk) return 1;
  return aMs - bMs;
}
