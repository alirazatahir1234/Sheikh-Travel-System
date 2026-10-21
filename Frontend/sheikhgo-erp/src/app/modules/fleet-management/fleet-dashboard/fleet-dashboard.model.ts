export type KpiTone = 'primary' | 'secondary' | 'error';

export interface FleetKpi {
  id: string;
  label: string;
  value: string;
  icon: string;
  tone: KpiTone;
  trend?: string;
  trendUp?: boolean;
  /** Highlights the card border to flag attention (e.g. maintenance). */
  alert?: boolean;
}

export interface ChartSeries {
  label: string;
  values: number[];
  color: string;
}

export interface UtilizationChart {
  labels: string[];
  series: ChartSeries[];
  /** Shown under the title — must match the series (e.g. GPS online %, not capacity). */
  subtitle?: string;
  /** When true, chart widgets show an empty state instead of a flat/fake series. */
  empty?: boolean;
  emptyMessage?: string;
}

export interface FuelMaintenanceChart {
  labels: string[];
  fuel: number[];
  maintenance: number[];
  empty?: boolean;
  emptyMessage?: string;
}

export type AlertTone = 'error' | 'warning';

export interface CriticalAlert {
  id: string;
  icon: string;
  tone: AlertTone;
  title: string;
  detail: string;
  actionLabel?: string;
  /** Deep-link for the action button (e.g. live map, maintenance). */
  actionRoute?: string;
}

export interface ActivityEvent {
  id: string;
  title: string;
  timeAgo: string;
  tone: 'primary' | 'secondary' | 'muted';
}

export interface QuickAction {
  id: string;
  label: string;
  icon: string;
  route?: string;
  /** Colored icon background for dashboard quick actions. */
  tone?: 'green' | 'blue' | 'orange' | 'red' | 'neutral';
}

export interface AssignmentItem {
  id: number;
  /** Primary vehicle label (name / plate). */
  vehicleId: string;
  /** Secondary line under vehicle (registration / code). */
  vehicleModel: string;
  driver: string;
  /** Pickup → drop, purpose, or em dash when unknown. */
  route: string;
  /** Assignment type from API (Permanent, Trip, …). */
  assignmentType: string;
  status: string;
  eta: string;
}

export interface FleetDashboardView {
  kpis: FleetKpi[];
  utilization: UtilizationChart;
  fuelMaintenance: FuelMaintenanceChart;
  quickActions: QuickAction[];
  alerts: CriticalAlert[];
  activities: ActivityEvent[];
  assignments: AssignmentItem[];
}
