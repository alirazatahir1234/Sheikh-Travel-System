/** Non-moving time above this threshold is highlighted on Fleet Tracking history. */
export const NON_MOVING_ALERT_MINUTES = 22 * 60;

/** Urdu “standing still” phrase for history KPIs (e.g. `22 گھنٹے سے کھڑی ہے`). */
export function formatNonMovingStandingUrdu(totalMinutes: number): string {
  const minutes = Math.max(0, Math.floor(totalMinutes));
  if (minutes < 1) return '0 منٹ سے کھڑی ہے';

  const days = Math.floor(minutes / (24 * 60));
  const afterDays = minutes % (24 * 60);
  const hours = Math.floor(afterDays / 60);
  const mins = afterDays % 60;

  if (days > 0) {
    const parts: string[] = [`${days} دن`];
    if (hours > 0) parts.push(`${hours} گھنٹے`);
    if (mins > 0) parts.push(`${mins} منٹ`);
    return `${parts.join(' ')} سے کھڑی ہے`;
  }

  if (hours > 0) {
    if (mins === 0) return `${hours} گھنٹے سے کھڑی ہے`;
    return `${hours} گھنٹے ${mins} منٹ سے کھڑی ہے`;
  }

  return `${mins} منٹ سے کھڑی ہے`;
}

export function nonMovingExceedsAlertThreshold(totalMinutes: number): boolean {
  return Math.max(0, Math.floor(totalMinutes)) > NON_MOVING_ALERT_MINUTES;
}
