import { parseGpsTimestamp } from './gps-timestamp.util';

/**
 * Human-readable relative time for GPS / API timestamps.
 * Uses parseGpsTimestamp so timezone-less UTC DATETIME2 values are not
 * misread as local time (e.g. PKT +5h → false "5 hr ago" / Offline).
 */
export function formatRelativeTime(iso: string | Date | null | undefined, now = Date.now()): string {
  if (!iso) return '—';
  const then = typeof iso === 'string'
    ? parseGpsTimestamp(iso)
    : iso.getTime();
  if (!Number.isFinite(then)) return '—';

  const diffSec = Math.max(0, Math.floor((now - then) / 1000));
  if (diffSec < 60) return `${diffSec} sec ago`;
  const diffMin = Math.floor(diffSec / 60);
  if (diffMin < 60) return `${diffMin} min ago`;
  const diffHr = Math.floor(diffMin / 60);
  if (diffHr < 24) return `${diffHr} hr ago`;
  const diffDay = Math.floor(diffHr / 24);
  return `${diffDay} day${diffDay === 1 ? '' : 's'} ago`;
}
