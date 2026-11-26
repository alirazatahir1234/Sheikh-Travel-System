/**
 * Tracker IMEIs are device identifiers. Outside device management only the last four
 * digits are shown ("•••• 0598") — enough to tell two trackers apart, not enough to reuse.
 */
export function maskImei(imei: string | null | undefined): string {
  const value = (imei ?? '').trim();
  if (!value) return '';
  return `•••• ${value.slice(-4)}`;
}
