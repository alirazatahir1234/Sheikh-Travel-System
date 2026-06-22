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
}

export interface FuelMaintenanceChart {
  labels: string[];
  fuel: number[];
  maintenance: number[];
}

export type AlertTone = 'error' | 'warning';

export interface CriticalAlert {
  id: string;
  icon: string;
  tone: AlertTone;
  title: string;
  detail: string;
  actionLabel?: string;
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
  vehicleId: string;
  vehicleModel: string;
  driver: string;
  route: string;
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
