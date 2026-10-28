import { polishAssistantEmojis } from './ai-assistant-ui.constants';

/** Parsed assistant prose — safe for template binding (no HTML). */
export type AssistantAnswerBlock =
  | { kind: 'paragraph'; text: string }
  | { kind: 'list'; items: string[] }
  | { kind: 'heading'; text: string; level: 2 | 3 };

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
const HEADING = /^(#{2,3})\s+(.+)$/;

/** Strip simple markdown markers for safe text display (no HTML). */
export function stripInlineMarkdown(text: string): string {
  return text
    .replace(/\*\*([^*]+)\*\*/g, '$1')
    .replace(/__([^_]+)__/g, '$1')
    .replace(/`([^`]+)`/g, '$1')
    .trim();
}

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
    const heading = HEADING.exec(trimmed);
    if (heading) {
      flushList();
      blocks.push({
        kind: 'heading',
        level: heading[1].length === 2 ? 2 : 3,
        text: stripInlineMarkdown(heading[2])
      });
      continue;
    }
    if (BULLET.test(line)) {
      list.push(stripInlineMarkdown(trimmed.replace(BULLET, '').trim()));
      continue;
    }
    flushList();
    blocks.push({ kind: 'paragraph', text: stripInlineMarkdown(trimmed) });
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

/** Friendly labels for MCP / legacy tool ids — never show raw MCP:GET_* in the UI. */
const FRIENDLY_TOOL_LABELS: Record<string, string> = {
  get_fleet_status: 'Fleet Status Analysis',
  getfleethealth: 'Fleet Health Analysis',
  get_fleet_health: 'Fleet Health Analysis',
  gettripsummary: 'Trip & Booking Analytics',
  get_trip_summary: 'Trip & Booking Analytics',
  get_trip_statistics: 'Trip Statistics',
  get_sales_summary: 'Sales Analytics',
  get_top_customers: 'Top Customers',
  get_revenue_summary: 'Revenue Summary',
  get_revenue_breakdown: 'Revenue Breakdown',
  get_period_comparison: 'Period Comparison',
  get_driver_performance: 'Driver Performance',
  get_fuel_expenses: 'Fuel Expenses',
  get_fuel_cost_summary: 'Fuel Cost Summary',
  list_fuel_records: 'Fuel Records',
  get_maintenance_due: 'Maintenance Due',
  get_maintenance_kpi_summary: 'Maintenance KPIs',
  list_smart_alerts: 'Smart Alerts',
  get_vehicle_utilization: 'Vehicle Utilization',
  get_gps_coverage_summary: 'GPS Coverage',
  get_live_positions: 'Live Positions',
  get_booking_summary: 'Booking Summary',
  get_booking_analytics: 'Booking Analytics',
  list_vehicles: 'Vehicle List',
  list_drivers: 'Driver List',
  list_trips: 'Trip List',
  list_bookings: 'Booking List',
  mcp_tool_loop: 'Live Data Analysis',
  mcp_database: 'Database Lookup',
  get_finance_revenue_summary: 'Finance Revenue',
  get_invoice_status: 'Invoice Status',
  get_payment_status: 'Payment Status'
};

export function formatToolLabel(tool?: string | null): string | null {
  if (!tool?.trim()) return null;
  let key = tool.trim();
  // Strip mcp: / MCP: prefixes and normalize
  key = key.replace(/^mcp:/i, '');
  const compact = key.toLowerCase().replace(/[\s-]+/g, '_');
  const noUnderscore = compact.replace(/_/g, '');
  if (FRIENDLY_TOOL_LABELS[compact]) return FRIENDLY_TOOL_LABELS[compact];
  if (FRIENDLY_TOOL_LABELS[noUnderscore]) return FRIENDLY_TOOL_LABELS[noUnderscore];
  // get_fleet_status → Fleet Status; GetFleetHealth → Get Fleet Health
  const spaced = key
    .replace(/_/g, ' ')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .trim()
    .replace(/\s+/g, ' ');
  return spaced.replace(/\b\w/g, c => c.toUpperCase());
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
  const completionPct = asNumber(trips['completionRatePercent']) ?? rate;
  return [
    {
      key: 'totalTrips',
      label: 'Total Trips',
      value: total,
      unit: 'count',
      tone: 'teal',
      icon: 'route',
      sharePercent: total > 0 ? completionPct : undefined
    },
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
      value: completionPct,
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

/** Answer / Context / Evidence / Interpretation / Next action — required narrative frame. */
export interface AssistantAcein {
  answer?: string;
  context?: string;
  interpretation?: string;
  nextAction?: string;
  evidence: { label: string; detail: string }[];
}

export function extractAcein(data: unknown): AssistantAcein | null {
  if (!data || typeof data !== 'object') return null;
  const root = data as Record<string, unknown>;
  const answer = asString(root['answer']);
  const context = asString(root['context']);
  const interpretation = asString(root['interpretation']);
  const nextAction = asString(root['nextAction']) ?? asString(root['next_action']);
  const evidenceRaw = Array.isArray(root['evidence']) ? (root['evidence'] as Record<string, unknown>[]) : [];
  const evidence = evidenceRaw
    .map(e => {
      const label = asString(e['label']);
      const detail = asString(e['detail']);
      if (!label || !detail) return null;
      return { label, detail };
    })
    .filter((x): x is { label: string; detail: string } => !!x);

  if (!answer && !context && !interpretation && !nextAction && !evidence.length) return null;
  return { answer: answer ?? undefined, context: context ?? undefined, interpretation: interpretation ?? undefined, nextAction: nextAction ?? undefined, evidence };
}

export interface FleetStatusVehicleRow {
  vehicleId: number;
  vehicleName: string;
  registrationNumber: string;
  gpsBucket: string;
  statusLabel: string;
  statusTone: string;
  vehicleStatusName?: string | null;
  speed?: number | null;
  lastUpdate?: string | null;
  ignition?: boolean | null;
}

export interface FleetStatusAnalyticsData {
  responseType: 'fleet_status_analytics';
  summary: {
    total: number;
    online: number;
    offline: number;
    moving: number;
    idle: number;
    parked: number;
    sos: number;
    neverSeen: number;
    unknown: number;
    onlinePercent?: number;
    offlinePercent?: number;
  };
  kpis: TripBookingAnalyticsKpi[];
  statusDistribution: TripBookingStatusSlice[];
  vehicles: FleetStatusVehicleRow[];
  insights: string[];
  recommendedActions: string[];
  followUps: string[];
  acein: AssistantAcein | null;
  empty: boolean;
}

function parseKpiList(raw: unknown): TripBookingAnalyticsKpi[] {
  if (!Array.isArray(raw)) return [];
  const out: TripBookingAnalyticsKpi[] = [];
  for (const k of raw as Record<string, unknown>[]) {
    const value = asNumber(k['value']);
    const label = asString(k['label']);
    const key = asString(k['key']) ?? label ?? 'kpi';
    if (value == null || !label) continue;
    out.push({
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
  return out;
}

function parseStatusSlices(raw: unknown): TripBookingStatusSlice[] {
  if (!Array.isArray(raw)) return [];
  return (raw as Record<string, unknown>[])
    .map(s => {
      const count = asNumber(s['count']);
      const label = asString(s['label']);
      const key = asString(s['key']) ?? label ?? 'status';
      if (count == null || !label) return null;
      return { key, label, count };
    })
    .filter((x): x is TripBookingStatusSlice => !!x);
}

function bucketLabel(bucket: string): string {
  switch (bucket.toLowerCase()) {
    case 'moving':
      return 'Moving';
    case 'idle':
      return 'Idle';
    case 'parked':
      return 'Parked';
    case 'sos':
      return 'SOS';
    case 'offline':
      return 'Offline';
    case 'never_seen':
      return 'Never seen';
    default:
      return 'Unknown';
  }
}

function bucketTone(bucket: string): string {
  switch (bucket.toLowerCase()) {
    case 'moving':
      return 'ok';
    case 'idle':
      return 'info';
    case 'parked':
      return 'neutral';
    case 'sos':
      return 'danger';
    case 'offline':
    case 'never_seen':
      return 'warn';
    default:
      return 'neutral';
  }
}

/** Detect enriched or raw get_fleet_status MCP payload. */
export function asFleetStatusAnalytics(data: unknown, toolUsed?: string | null): FleetStatusAnalyticsData | null {
  if (!data || typeof data !== 'object') return null;
  const root = data as Record<string, unknown>;
  const tool = (toolUsed ?? '').toLowerCase();
  const typed = asString(root['responseType']) === 'fleet_status_analytics';
  const fleetTool = tool.includes('get_fleet_status') || tool.includes('getfleetstatus');
  const summaryObj = (root['summary'] ?? null) as Record<string, unknown> | null;
  const looksRaw =
    !!summaryObj
    && asNumber(summaryObj['total']) != null
    && (asNumber(summaryObj['online']) != null || asNumber(summaryObj['offline']) != null || asNumber(summaryObj['moving']) != null);

  if (!typed && !looksRaw && !(fleetTool && looksRaw)) return null;
  if (!typed && !looksRaw) return null;

  const summarySrc = summaryObj ?? {};
  const total = asNumber(summarySrc['total']) ?? 0;
  const online = asNumber(summarySrc['online']) ?? 0;
  const offline = asNumber(summarySrc['offline']) ?? 0;
  const moving = asNumber(summarySrc['moving']) ?? 0;
  const idle = asNumber(summarySrc['idle']) ?? 0;
  const parked = asNumber(summarySrc['parked']) ?? 0;
  const sos = asNumber(summarySrc['sos']) ?? 0;
  const neverSeen = asNumber(summarySrc['neverSeen']) ?? 0;
  const unknown = asNumber(summarySrc['unknown']) ?? 0;
  const onlinePercent =
    asNumber(summarySrc['onlinePercent']) ?? (total > 0 ? Math.round((online * 1000) / total) / 10 : 0);
  const offlinePercent =
    asNumber(summarySrc['offlinePercent']) ?? (total > 0 ? Math.round((offline * 1000) / total) / 10 : 0);

  let kpis = parseKpiList(root['kpis']);
  if (!kpis.length) {
    kpis = [
      { key: 'totalVehicles', label: 'Total Vehicles', value: total, unit: 'count', tone: 'teal', icon: 'directions_car' },
      { key: 'online', label: 'Online', value: online, unit: 'count', tone: 'blue', icon: 'wifi', sharePercent: onlinePercent },
      { key: 'offline', label: 'Offline', value: offline, unit: 'count', tone: 'orange', icon: 'wifi_off', sharePercent: offlinePercent },
      { key: 'moving', label: 'Moving', value: moving, unit: 'count', tone: 'purple', icon: 'near_me' },
      { key: 'parked', label: 'Parked', value: parked, unit: 'count', tone: 'teal', icon: 'local_parking' },
      { key: 'idle', label: 'Idle', value: idle, unit: 'count', tone: 'blue', icon: 'pause_circle' },
      { key: 'sos', label: 'SOS', value: sos, unit: 'count', tone: 'orange', icon: 'sos' }
    ];
  }

  let statusDistribution = parseStatusSlices(root['statusDistribution']);
  if (!statusDistribution.length) {
    statusDistribution = [
      { key: 'online', label: 'Online', count: online },
      { key: 'offline', label: 'Offline', count: offline },
      { key: 'parked', label: 'Parked', count: parked },
      { key: 'moving', label: 'Moving', count: moving },
      { key: 'idle', label: 'Idle', count: idle },
      { key: 'sos', label: 'SOS', count: sos },
      { key: 'neverSeen', label: 'Never seen', count: neverSeen },
      { key: 'unknown', label: 'Unknown', count: unknown }
    ];
  }

  const vehicleSrc = Array.isArray(root['vehicles'])
    ? (root['vehicles'] as Record<string, unknown>[])
    : Array.isArray(root['sample'])
      ? (root['sample'] as Record<string, unknown>[])
      : [];
  const vehicles: FleetStatusVehicleRow[] = [];
  for (const r of vehicleSrc) {
    const vehicleId = asNumber(r['vehicleId'] ?? r['VehicleId']);
    if (vehicleId == null) continue;
    const gpsBucket = asString(r['gpsBucket'] ?? r['GpsBucket']) ?? 'unknown';
    vehicles.push({
      vehicleId,
      vehicleName: asString(r['vehicleName'] ?? r['VehicleName']) ?? `Vehicle #${vehicleId}`,
      registrationNumber: asString(r['registrationNumber'] ?? r['RegistrationNumber']) ?? '—',
      gpsBucket,
      statusLabel: asString(r['statusLabel']) ?? bucketLabel(gpsBucket),
      statusTone: asString(r['statusTone']) ?? bucketTone(gpsBucket),
      vehicleStatusName: asString(r['vehicleStatusName'] ?? r['VehicleStatusName']),
      speed: asNumber(r['speed'] ?? r['Speed']),
      lastUpdate: asString(r['lastUpdate'] ?? r['LastUpdate']) ?? (r['LastUpdate'] != null ? String(r['LastUpdate']) : null),
      ignition: typeof r['ignition'] === 'boolean' ? r['ignition'] : typeof r['Ignition'] === 'boolean' ? r['Ignition'] : null
    });
  }

  const insights = Array.isArray(root['insights'])
    ? (root['insights'] as unknown[]).map(x => String(x)).filter(Boolean)
    : [];
  const recommendedActions = Array.isArray(root['recommendedActions'])
    ? (root['recommendedActions'] as unknown[]).map(x => String(x)).filter(Boolean)
    : [];
  const followUps = Array.isArray(root['followUps'])
    ? (root['followUps'] as unknown[]).map(x => String(x)).filter(Boolean)
    : [];

  let acein = extractAcein(root);
  if (!acein && looksRaw) {
    acein = {
      answer:
        total === 0
          ? 'No vehicles were returned for live fleet status.'
          : `Fleet status: ${online} of ${total} vehicles online (${onlinePercent}%), ${offline} offline.`,
      context: 'Live GPS buckets from VehicleCurrentLocation + GpsDevices (tenant-scoped).',
      interpretation:
        offlinePercent >= 50
          ? 'Offline share is high — verify GPS devices and last-update freshness.'
          : onlinePercent >= 80
            ? 'Fleet connectivity looks healthy in this sample.'
            : 'Connectivity is mixed — review offline and never-seen units.',
      nextAction: offline > 0 ? 'Investigate offline vehicles on Live Map.' : 'Open Live Map to verify positions.',
      evidence: [
        { label: 'Tool', detail: 'get_fleet_status' },
        { label: 'Sample', detail: `${vehicles.length} vehicle row(s)` }
      ]
    };
  }

  return {
    responseType: 'fleet_status_analytics',
    summary: {
      total,
      online,
      offline,
      moving,
      idle,
      parked,
      sos,
      neverSeen,
      unknown,
      onlinePercent,
      offlinePercent
    },
    kpis,
    statusDistribution,
    vehicles,
    insights,
    recommendedActions,
    followUps,
    acein,
    empty: total === 0
  };
}

/** Generic KPI analytics for driver / fuel / maintenance / alerts when backend sends kpis. */
export interface GenericAnalyticsData {
  responseType: string;
  kpis: TripBookingAnalyticsKpi[];
  statusDistribution: TripBookingStatusSlice[];
  insights: string[];
  recommendedActions: string[];
  followUps: string[];
  acein: AssistantAcein | null;
  title: string;
}

export function asGenericAnalytics(data: unknown, toolUsed?: string | null): GenericAnalyticsData | null {
  if (!data || typeof data !== 'object') return null;
  const root = data as Record<string, unknown>;
  const type = asString(root['responseType']);
  if (type === 'trip_booking_analytics' || type === 'sales_analytics' || type === 'fleet_status_analytics') {
    return null;
  }
  const kpis = parseKpiList(root['kpis']);
  if (!kpis.length) return null;

  const insights = Array.isArray(root['insights'])
    ? (root['insights'] as unknown[]).map(x => String(x)).filter(Boolean)
    : [];
  const recommendedActions = Array.isArray(root['recommendedActions'])
    ? (root['recommendedActions'] as unknown[]).map(x => String(x)).filter(Boolean)
    : [];
  const followUps = extractFollowUps(root);
  const title = formatToolLabel(toolUsed) ?? (type ? humanizeKey(type.replace(/_analytics$/, '')) : 'Analysis');

  return {
    responseType: type ?? 'generic_analytics',
    kpis,
    statusDistribution: parseStatusSlices(root['statusDistribution']),
    insights,
    recommendedActions,
    followUps,
    acein: extractAcein(root),
    title
  };
}

export function extractRecommendedActions(data: unknown): string[] {
  if (!data || typeof data !== 'object') return [];
  const root = data as Record<string, unknown>;
  if (!Array.isArray(root['recommendedActions'])) return [];
  return (root['recommendedActions'] as unknown[]).map(x => String(x)).filter(Boolean);
}

export function hasStructuredError(data: unknown): string | null {
  if (!data || typeof data !== 'object') return null;
  const root = data as Record<string, unknown>;
  if (root['error'] === true || asString(root['status']) === 'error') {
    return asString(root['message']) ?? asString(root['errorMessage']) ?? 'The data tool returned an error.';
  }
  if (typeof root['error'] === 'string' && root['error'].trim()) return String(root['error']);
  return null;
}
