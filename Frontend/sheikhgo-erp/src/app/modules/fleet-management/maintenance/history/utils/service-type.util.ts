export type ServiceTypeTone = 'primary' | 'warning' | 'danger';

const SERVICE_ICONS: Record<string, string> = {
  oil: 'oil_barrel',
  brake: 'disc_full',
  tire: 'tire_repair',
  battery: 'battery_charging_full',
  accident: 'car_crash'
};

export function serviceTypeIcon(serviceType: string): string {
  const lower = serviceType.toLowerCase();
  if (lower.includes('oil')) return SERVICE_ICONS['oil'];
  if (lower.includes('brake')) return SERVICE_ICONS['brake'];
  if (lower.includes('tire') || lower.includes('tyre')) return SERVICE_ICONS['tire'];
  if (lower.includes('battery')) return SERVICE_ICONS['battery'];
  if (lower.includes('accident') || lower.includes('body') || lower.includes('damage')) return SERVICE_ICONS['accident'];
  return 'build';
}

export function serviceTypeTone(serviceType: string): ServiceTypeTone {
  const t = serviceType.toLowerCase();
  if (t.includes('accident') || t.includes('breakdown') || t.includes('body') || t.includes('damage')) return 'danger';
  if (t.includes('brake') || t.includes('battery')) return 'warning';
  return 'primary';
}

export function serviceTypeBadgeClass(serviceType: string): string {
  return `badge--${serviceTypeTone(serviceType)}`;
}

export function isAccidentService(serviceType: string): boolean {
  return /accident|body|damage/i.test(serviceType);
}

export function computeAvgIntervalDays(rows: { serviceDate: string }[]): number | null {
  if (rows.length < 2) return null;
  const sorted = [...rows]
    .map(r => new Date(r.serviceDate).getTime())
    .filter(t => !Number.isNaN(t))
    .sort((a, b) => a - b);
  if (sorted.length < 2) return null;
  let totalGap = 0;
  for (let i = 1; i < sorted.length; i++) {
    totalGap += sorted[i] - sorted[i - 1];
  }
  return Math.round(totalGap / (sorted.length - 1) / 86_400_000);
}
