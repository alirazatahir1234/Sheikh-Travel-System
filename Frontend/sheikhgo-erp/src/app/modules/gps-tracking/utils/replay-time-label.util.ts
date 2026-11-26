import { formatInZone, formatTimeInZone } from '../../../core/utils/zoned-time.util';

const DAY_MS = 24 * 60 * 60 * 1000;

/**
 * Replay timeline label. Across a multi-day span a bare time ("06:03 AM") cannot be
 * read as a sequence, so the date is included whenever the span exceeds 24 hours.
 */
export function formatReplayTimeLabel(
  value: string | Date,
  spanMs: number,
  zone?: string | null
): string {
  if (spanMs > DAY_MS) {
    return formatInZone(value, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' }, zone);
  }
  return formatTimeInZone(value, { hour: '2-digit', minute: '2-digit' }, zone);
}
