import { polishAssistantEmojis } from './ai-assistant-ui.constants';

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
  const raw = polishAssistantEmojis(text).trim();
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

/** Structured trip + booking analytics payload from GetTripSummary (and compatible MCP). */
export interface TripBookingAnalyticsKpi {
  key: string;
  label: string;
  value: number;
  unit: 'count' | 'percent' | 'currency' | string;
  icon?: string;
  tone?: 'teal' | 'blue' | 'orange' | 'purple' | string;
  deltaPercent?: number | null;
  sharePercent?: number | null;
  deltaLabel?: string;
}

/** Sales / revenue analytics (responseType=sales_analytics) — Payments.Status=Paid. */
export interface SalesAnalyticsCustomer {
  customerId: number;
  customerName: string;
  revenue: number;
  paymentCount: number;
  sharePercent: number;
}

export interface SalesAnalyticsTrendPoint {
  date: string;
  revenue: number;
  paymentCount: number;
}

export interface SalesAnalyticsData {
  responseType: 'sales_analytics';
  period?: { start: string; end: string; label?: string };
  comparisonPeriod?: { start: string; end: string };
  kpis: TripBookingAnalyticsKpi[];
  statusDistribution: TripBookingStatusSlice[];
  trends: SalesAnalyticsTrendPoint[];
  topCustomers: SalesAnalyticsCustomer[];
  insights: string[];
  followUps: string[];
}

export interface TripBookingStatusSlice {
  key: string;
  label: string;
  count: number;
}

export interface TripBookingTrendPoint {
  date: string;
  trips: number;
  completed: number;
  bookings: number;
}

export interface TripBookingRecentTrip {
  tripId: number;
  tripNumber: string;
  vehicleName: string;
  status: number;
  statusLabel: string;
  statusTone: string;
  startTime?: string | null;
  endTime?: string | null;
  distanceKm?: number | null;
}

export interface TripBookingAnalyticsData {
  responseType: 'trip_booking_analytics';
  period?: { start: string; end: string };
  comparisonPeriod?: { start: string; end: string };
  kpis: TripBookingAnalyticsKpi[];
  statusDistribution: TripBookingStatusSlice[];
  trends: TripBookingTrendPoint[];
  recentTrips: TripBookingRecentTrip[];
  insights: string[];
  followUps: string[];
}

function asNumber(v: unknown): number | null {
  if (typeof v === 'number' && Number.isFinite(v)) return v;
  if (typeof v === 'string' && v.trim() !== '' && !Number.isNaN(Number(v))) return Number(v);
  return null;
}

function asString(v: unknown): string | null {
  return typeof v === 'string' && v.trim() ? v : null;
}

/** Detect enriched trip/booking analytics (responseType or GetTripSummary shape). */
export function asTripBookingAnalytics(data: unknown, toolUsed?: string | null): TripBookingAnalyticsData | null {
  if (!data || typeof data !== 'object') return null;
  const root = data as Record<string, unknown>;
  const tool = (toolUsed ?? '').toLowerCase();
  const typed = asString(root['responseType']) === 'trip_booking_analytics';
  const hasKpis = Array.isArray(root['kpis']) && (root['kpis'] as unknown[]).length > 0;
  const tripMcpTool =
    tool.includes('get_trip_statistics') ||
    tool.includes('gettripstatistics') ||
    tool.includes('get_trip_summary') ||
    tool === 'gettripsummary';
  const legacyTripTool = tripMcpTool && !!root['trips'] && !!root['bookings'];

  if (!typed && !hasKpis && !legacyTripTool) return null;

  const kpisRaw = Array.isArray(root['kpis']) ? (root['kpis'] as Record<string, unknown>[]) : [];
  let kpis: TripBookingAnalyticsKpi[] = [];
  for (const k of kpisRaw) {
    const value = asNumber(k['value']);
    const label = asString(k['label']);
    const key = asString(k['key']) ?? label ?? 'kpi';
    if (value == null || !label) continue;
    kpis.push({
      key,
      label,
      value,
      unit: (asString(k['unit']) as TripBookingAnalyticsKpi['unit']) ?? 'count',
      icon: asString(k['icon']) ?? undefined,
      tone: asString(k['tone']) ?? undefined,
      deltaPercent: asNumber(k['deltaPercent']),
      sharePercent: asNumber(k['sharePercent']),
      deltaLabel: asString(k['deltaLabel']) ?? undefined
    });
  }

  if (!kpis.length && legacyTripTool) {
    kpis = buildLegacyTripKpis(root);
  }

  const statusRaw = Array.isArray(root['statusDistribution'])
    ? (root['statusDistribution'] as Record<string, unknown>[])
    : [];
  let statusDistribution: TripBookingStatusSlice[] = statusRaw
    .map(s => {
      const count = asNumber(s['count']);
      const label = asString(s['label']);
      const key = asString(s['key']) ?? label ?? 'status';
      if (count == null || !label) return null;
      return { key, label, count };
    })
    .filter((x): x is TripBookingStatusSlice => !!x);

  if (!statusDistribution.length) {
    statusDistribution = buildLegacyStatusDistribution(root);
  }

  const trendsRaw = Array.isArray(root['trends']) ? (root['trends'] as Record<string, unknown>[]) : [];
  const trends: TripBookingTrendPoint[] = trendsRaw
    .map(t => {
      const date = asString(t['date']) ?? (t['date'] != null ? String(t['date']) : null);
      if (!date) return null;
      return {
        date,
        trips: asNumber(t['trips']) ?? 0,
        completed: asNumber(t['completed']) ?? 0,
        bookings: asNumber(t['bookings']) ?? 0
      };
    })
    .filter((x): x is TripBookingTrendPoint => !!x);

  const recentRaw = Array.isArray(root['recentTrips']) ? (root['recentTrips'] as Record<string, unknown>[]) : [];
  const recentTrips: TripBookingRecentTrip[] = [];
  for (const r of recentRaw) {
    const tripId = asNumber(r['tripId']);
    if (tripId == null) continue;
    recentTrips.push({
      tripId,
      tripNumber: asString(r['tripNumber']) ?? `TR-${tripId}`,
      vehicleName: asString(r['vehicleName']) ?? '—',
      status: asNumber(r['status']) ?? 0,
      statusLabel: asString(r['statusLabel']) ?? 'Other',
      statusTone: asString(r['statusTone']) ?? 'warning',
      startTime: asString(r['startTime']) ?? (r['startTime'] != null ? String(r['startTime']) : null),
      endTime: asString(r['endTime']) ?? (r['endTime'] != null ? String(r['endTime']) : null),
      distanceKm: asNumber(r['distanceKm'])
    });
  }

  const insights = Array.isArray(root['insights'])
    ? (root['insights'] as unknown[]).map(x => String(x)).filter(Boolean)
    : [];
  const followUps = Array.isArray(root['followUps'])
    ? (root['followUps'] as unknown[]).map(x => String(x)).filter(Boolean)
    : [];

  const period = root['period'] as { start?: unknown; end?: unknown } | undefined;
  const comparisonPeriod = root['comparisonPeriod'] as { start?: unknown; end?: unknown } | undefined;

  return {
    responseType: 'trip_booking_analytics',
    period:
      period?.start != null && period?.end != null
        ? { start: String(period.start), end: String(period.end) }
        : undefined,
    comparisonPeriod:
      comparisonPeriod?.start != null && comparisonPeriod?.end != null
        ? { start: String(comparisonPeriod.start), end: String(comparisonPeriod.end) }
        : undefined,
    kpis,
    statusDistribution,
    trends,
    recentTrips,
    insights,
    followUps
  };
}

function buildLegacyTripKpis(root: Record<string, unknown>): TripBookingAnalyticsKpi[] {
  const trips = (root['trips'] ?? {}) as Record<string, unknown>;
  const bookings = (root['bookings'] ?? {}) as Record<string, unknown>;
  const total = asNumber(trips['total']) ?? 0;
  const completed = asNumber(trips['completed']) ?? 0;
  const rate = total > 0 ? Math.round((completed * 1000) / total) / 10 : 0;
  const totalBookings = asNumber(bookings['totalBookings'] ?? bookings['TotalBookings']) ?? 0;
  const pending = asNumber(bookings['pending'] ?? bookings['Pending']) ?? 0;
  const share = totalBookings > 0 ? Math.round((pending * 1000) / totalBookings) / 10 : 0;
  return [
    { key: 'totalTrips', label: 'Total Trips', value: total, unit: 'count', tone: 'teal', icon: 'route' },
    { key: 'totalBookings', label: 'Total Bookings', value: totalBookings, unit: 'count', tone: 'blue', icon: 'event' },
    {
      key: 'pendingBookings',
      label: 'Pending Bookings',
      value: pending,
      unit: 'count',
      tone: 'orange',
      icon: 'schedule',
      sharePercent: share,
      deltaLabel: totalBookings > 0 ? `${share}% of total` : undefined
    },
    {
      key: 'tripCompletionRate',
      label: 'Trip Completion Rate',
      value: asNumber(trips['completionRatePercent']) ?? rate,
      unit: 'percent',
      tone: 'purple',
      icon: 'donut_large'
    }
  ];
}

function buildLegacyStatusDistribution(root: Record<string, unknown>): TripBookingStatusSlice[] {
  const trips = (root['trips'] ?? {}) as Record<string, unknown>;
  const byStatus = Array.isArray(trips['byStatus']) ? (trips['byStatus'] as Record<string, unknown>[]) : [];
  const completed = asNumber(trips['completed']) ?? 0;
  const cancelled = asNumber(trips['cancelled']) ?? 0;
  const inProgress = asNumber(trips['inProgress']) ?? 0;
  const pending = asNumber(trips['pending']) ?? 0;
  const others = asNumber(trips['others']) ?? 0;
  if (completed || cancelled || inProgress || pending || others) {
    return [
      { key: 'completed', label: 'Completed', count: completed },
      { key: 'inProgress', label: 'In Progress', count: inProgress },
      { key: 'pending', label: 'Pending', count: pending },
      { key: 'cancelled', label: 'Cancelled', count: cancelled },
      { key: 'others', label: 'Others', count: others }
    ];
  }
  // Raw byStatus only — map known enums when present
  if (!byStatus.length) return [];
  let c = 0, ip = 0, p = 0, x = 0, o = 0;
  for (const row of byStatus) {
    const status = asNumber(row['status'] ?? row['Status']) ?? 0;
    const cnt = asNumber(row['cnt'] ?? row['Cnt']) ?? 0;
    if (status === 9) c += cnt;
    else if ([5, 6, 7, 8].includes(status)) ip += cnt;
    else if ([1, 2, 3, 4].includes(status)) p += cnt;
    else if (status === 10) x += cnt;
    else o += cnt;
  }
  return [
    { key: 'completed', label: 'Completed', count: c },
    { key: 'inProgress', label: 'In Progress', count: ip },
    { key: 'pending', label: 'Pending', count: p },
    { key: 'cancelled', label: 'Cancelled', count: x },
    { key: 'others', label: 'Others', count: o }
  ];
}

/** Detect enriched sales analytics (responseType=sales_analytics or sales MCP tools). */
export function asSalesAnalytics(data: unknown, toolUsed?: string | null): SalesAnalyticsData | null {
  if (!data || typeof data !== 'object') return null;
  const root = data as Record<string, unknown>;
  const tool = (toolUsed ?? '').toLowerCase();
  const typed = asString(root['responseType']) === 'sales_analytics';
  const salesTool =
    tool.includes('sales') ||
    tool.includes('revenueanalytics') ||
    tool.includes('get_sales_summary') ||
    tool.includes('get_top_customers') ||
    tool.includes('get_revenue_summary') ||
    tool.includes('topcustomersbyrevenue');
  const hasKpis = Array.isArray(root['kpis']) && (root['kpis'] as unknown[]).length > 0;
  if (!typed && !(salesTool && hasKpis)) return null;
  // Prefer trip_booking_analytics when that type is explicit.
  if (asString(root['responseType']) === 'trip_booking_analytics') return null;

  const kpisRaw = Array.isArray(root['kpis']) ? (root['kpis'] as Record<string, unknown>[]) : [];
  const kpis: TripBookingAnalyticsKpi[] = [];
  for (const k of kpisRaw) {
    const value = asNumber(k['value']);
    const label = asString(k['label']);
    const key = asString(k['key']) ?? label ?? 'kpi';
    if (value == null || !label) continue;
    kpis.push({
      key,
      label,
      value,
      unit: (asString(k['unit']) as TripBookingAnalyticsKpi['unit']) ?? 'currency',
      icon: asString(k['icon']) ?? undefined,
      tone: asString(k['tone']) ?? undefined,
      deltaPercent: asNumber(k['deltaPercent']),
      sharePercent: asNumber(k['sharePercent']),
      deltaLabel: asString(k['deltaLabel']) ?? undefined
    });
  }
  if (!kpis.length) return null;

  const trendsRaw = Array.isArray(root['trends']) ? (root['trends'] as Record<string, unknown>[]) : [];
  const trends: SalesAnalyticsTrendPoint[] = trendsRaw
    .map(t => {
      const date = asString(t['date']) ?? (t['date'] != null ? String(t['date']) : null);
      if (!date) return null;
      return {
        date,
        revenue: asNumber(t['revenue']) ?? asNumber(t['trips']) ?? 0,
        paymentCount: asNumber(t['paymentCount']) ?? asNumber(t['bookings']) ?? 0
      };
    })
    .filter((x): x is SalesAnalyticsTrendPoint => !!x);

  const custRaw = Array.isArray(root['topCustomers'])
    ? (root['topCustomers'] as Record<string, unknown>[])
    : [];
  const topCustomers: SalesAnalyticsCustomer[] = [];
  for (const c of custRaw) {
    const customerId = asNumber(c['customerId']);
    const revenue = asNumber(c['revenue']);
    if (customerId == null || revenue == null) continue;
    topCustomers.push({
      customerId,
      customerName: asString(c['customerName']) ?? `Customer #${customerId}`,
      revenue,
      paymentCount: asNumber(c['paymentCount']) ?? 0,
      sharePercent: asNumber(c['sharePercent']) ?? 0
    });
  }

  const statusRaw = Array.isArray(root['statusDistribution'])
    ? (root['statusDistribution'] as Record<string, unknown>[])
    : [];
  const statusDistribution: TripBookingStatusSlice[] = statusRaw
    .map(s => {
      const count = asNumber(s['count']);
      const label = asString(s['label']);
      const key = asString(s['key']) ?? label ?? 'status';
      if (count == null || !label) return null;
      return { key, label, count };
    })
    .filter((x): x is TripBookingStatusSlice => !!x);

  const insights = Array.isArray(root['insights'])
    ? (root['insights'] as unknown[]).map(x => String(x)).filter(Boolean)
    : [];
  const followUps = Array.isArray(root['followUps'])
    ? (root['followUps'] as unknown[]).map(x => String(x)).filter(Boolean)
    : [];

  const period = root['period'] as { start?: unknown; end?: unknown; label?: unknown } | undefined;
  const comparisonPeriod = root['comparisonPeriod'] as { start?: unknown; end?: unknown } | undefined;

  return {
    responseType: 'sales_analytics',
    period:
      period?.start != null && period?.end != null
        ? {
            start: String(period.start),
            end: String(period.end),
            label: period.label != null ? String(period.label) : undefined
          }
        : undefined,
    comparisonPeriod:
      comparisonPeriod?.start != null && comparisonPeriod?.end != null
        ? { start: String(comparisonPeriod.start), end: String(comparisonPeriod.end) }
        : undefined,
    kpis,
    statusDistribution,
    trends,
    topCustomers,
    insights,
    followUps
  };
}

/** Pull follow-up chips from any assistant `data` payload. */
export function extractFollowUps(data: unknown): string[] {
  if (!data || typeof data !== 'object') return [];
  const root = data as Record<string, unknown>;
  if (!Array.isArray(root['followUps'])) return [];
  return (root['followUps'] as unknown[]).map(x => String(x)).filter(Boolean);
}
