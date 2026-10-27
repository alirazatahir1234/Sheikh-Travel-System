/** Non-moving time above this threshold is highlighted on Fleet Tracking history. */
export const NON_MOVING_ALERT_MINUTES = 22 * 60;

/** English “standing still” phrase for history KPIs (e.g. `Standing for 22 hours 18 minutes`). */
export function formatNonMovingStandingEnglish(totalMinutes: number): string {
  const minutes = Math.max(0, Math.floor(totalMinutes));
  if (minutes < 1) return 'Standing for 0 minutes';

  const days = Math.floor(minutes / (24 * 60));
  const afterDays = minutes % (24 * 60);
  const hours = Math.floor(afterDays / 60);
  const mins = afterDays % 60;

  const parts: string[] = [];
  if (days > 0) {
    parts.push(days === 1 ? '1 day' : `${days} days`);
  }
  if (hours > 0) {
    parts.push(hours === 1 ? '1 hour' : `${hours} hours`);
  }
  if (mins > 0) {
    parts.push(mins === 1 ? '1 minute' : `${mins} minutes`);
  }

  if (parts.length === 0) return 'Standing for 0 minutes';

  return `Standing for ${parts.join(' ')}`;
}

export function nonMovingExceedsAlertThreshold(totalMinutes: number): boolean {
  return Math.max(0, Math.floor(totalMinutes)) > NON_MOVING_ALERT_MINUTES;
}
