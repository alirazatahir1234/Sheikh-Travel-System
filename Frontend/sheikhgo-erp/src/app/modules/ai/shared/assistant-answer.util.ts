/** Parsed assistant prose — safe for template binding (no HTML). */
export type AssistantAnswerBlock =
  | { kind: 'paragraph'; text: string }
  | { kind: 'list'; items: string[] };

export interface AssistantFleetHealthData {
  healthPercent: number;
  gpsOnlineRate: number;
  maintenanceScore: number;
  complianceScore: number;
  driverScore: number;
  criticalAlerts: number;
  summary?: string;
}

export interface AssistantMetricTile {
  label: string;
  value: number;
  unit: '%' | '';
  hint?: string;
}

export interface AssistantDataTable {
  title: string;
  columns: { key: string; label: string; align?: 'left' | 'right' }[];
  rows: Record<string, unknown>[];
}

const BULLET = /^[\s]*[•\-*]\s+/;

export function parseAssistantAnswer(text: string): AssistantAnswerBlock[] {
  const raw = (text ?? '').trim();
  if (!raw) return [];

  const blocks: AssistantAnswerBlock[] = [];
  let list: string[] = [];
  const flushList = (): void => {
    if (list.length) {
      blocks.push({ kind: 'list', items: [...list] });
      list = [];
    }
  };

  for (const line of raw.split(/\r?\n/)) {
    const trimmed = line.trim();
    if (!trimmed) {
      flushList();
      continue;
    }
    if (BULLET.test(line)) {
      list.push(trimmed.replace(BULLET, '').trim());
      continue;
    }
    flushList();
    blocks.push({ kind: 'paragraph', text: trimmed });
  }
  flushList();
  return blocks;
}

export function asFleetHealthData(data: unknown): AssistantFleetHealthData | null {
  if (!data || typeof data !== 'object') return null;
  const d = data as Record<string, unknown>;
  const num = (k: string): number | null => {
    const v = d[k];
    if (typeof v === 'number' && Number.isFinite(v)) return v;
    if (typeof v === 'string' && v.trim() !== '' && !Number.isNaN(Number(v))) return Number(v);
    return null;
  };
  const healthPercent = num('healthPercent');
  const gpsOnlineRate = num('gpsOnlineRate');
  if (healthPercent == null || gpsOnlineRate == null) return null;
  return {
    healthPercent,
    gpsOnlineRate,
    maintenanceScore: num('maintenanceScore') ?? 0,
    complianceScore: num('complianceScore') ?? 0,
    driverScore: num('driverScore') ?? 0,
    criticalAlerts: num('criticalAlerts') ?? 0,
    summary: typeof d['summary'] === 'string' ? d['summary'] : undefined
  };
}

export function fleetHealthMetrics(h: AssistantFleetHealthData): AssistantMetricTile[] {
  return [
    { label: 'GPS online', value: h.gpsOnlineRate, unit: '%' },
    { label: 'Maintenance', value: h.maintenanceScore, unit: '%' },
    { label: 'Compliance', value: h.complianceScore, unit: '%' },
    { label: 'Driver score', value: h.driverScore, unit: '' },
    {
      label: 'Critical alerts',
      value: h.criticalAlerts,
      unit: '',
      hint: 'Open GPS / ops alerts requiring attention'
    }
  ];
}

export function healthBand(percent: number): 'good' | 'fair' | 'poor' {
  if (percent >= 80) return 'good';
  if (percent >= 60) return 'fair';
  return 'poor';
}

function humanizeKey(key: string): string {
  return key
    .replace(/([A-Z])/g, ' $1')
    .replace(/_/g, ' ')
    .trim()
    .replace(/^\w/, c => c.toUpperCase());
}

function isScalar(v: unknown): boolean {
  return v == null || typeof v === 'string' || typeof v === 'number' || typeof v === 'boolean';
}

function formatCell(v: unknown): string {
  if (v == null || v === '') return '—';
  if (typeof v === 'number') {
    return Number.isInteger(v) ? String(v) : v.toLocaleString(undefined, { maximumFractionDigits: 2 });
  }
  if (typeof v === 'boolean') return v ? 'Yes' : 'No';
  const s = String(v);
  return s.length > 120 ? `${s.slice(0, 117)}…` : s;
}

const COLUMN_LABELS: Record<string, string> = {
  vehicleName: 'Vehicle',
  plateNumber: 'Plate',
  registrationNumber: 'Plate',
  totalCost: 'Total cost',
  totalLiters: 'Liters',
  entryCount: 'Entries',
  name: 'Name',
  status: 'Status',
  severity: 'Severity',
  message: 'Message'
};

/** Best-effort tabular view of tool `data` payloads (reporting tools). */
export function extractDataTable(data: unknown, toolUsed?: string | null): AssistantDataTable | null {
  if (!data || typeof data !== 'object') return null;
  const root = data as Record<string, unknown>;

  let rows: Record<string, unknown>[] | null = null;
  let title = 'Details';

  for (const [key, val] of Object.entries(root)) {
    if (!Array.isArray(val) || val.length === 0) continue;
    if (!val.every(x => x && typeof x === 'object' && !Array.isArray(x))) continue;
    rows = val as Record<string, unknown>[];
    title = humanizeKey(key);
    break;
  }

  if (!rows?.length) return null;

  const keys = Object.keys(rows[0]).filter(k => isScalar(rows![0][k]));
  if (!keys.length) return null;

  const columns = keys.slice(0, 6).map(key => ({
    key,
    label: COLUMN_LABELS[key] ?? humanizeKey(key),
    align: typeof rows![0][key] === 'number' ? ('right' as const) : ('left' as const)
  }));

  const tool = (toolUsed ?? '').toLowerCase();
  if (tool.includes('fuel')) title = 'Fuel by vehicle';
  if (tool.includes('invoice')) title = 'Overdue invoices';
  if (tool.includes('offline')) title = 'Offline vehicles';

  return {
    title,
    columns,
    rows: rows.map(r => {
      const out: Record<string, unknown> = {};
      for (const c of columns) out[c.key] = formatCell(r[c.key]);
      return out;
    })
  };
}

export function formatToolLabel(tool?: string | null): string | null {
  if (!tool?.trim()) return null;
  return tool.replace(/([a-z])([A-Z])/g, '$1 $2').trim();
}
