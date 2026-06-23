import {
  ActivityEvent,
  AssignmentItem,
  CriticalAlert,
  FuelMaintenanceChart,
  QuickAction,
  UtilizationChart
} from './fleet-dashboard.model';

export const FLEET_PRIMARY = '#005f49';
export const FLEET_SECONDARY = '#535c86';
export const FLEET_NEUTRAL = '#dae3ee';

export const UTILIZATION_MOCK: UtilizationChart = {
  labels: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'],
  series: [
    { label: '2024', values: [78, 82, 75, 86, 90, 84, 92], color: FLEET_PRIMARY },
    { label: '2023', values: [70, 74, 72, 78, 80, 76, 82], color: FLEET_NEUTRAL }
  ]
};

export const FUEL_MAINTENANCE_MOCK: FuelMaintenanceChart = {
  labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun'],
  fuel: [128, 96, 112, 142, 120, 104],
  maintenance: [64, 80, 48, 96, 128, 72]
};

export const QUICK_ACTIONS_MOCK: QuickAction[] = [
  { id: 'add-vehicle', label: 'Add Vehicle', icon: 'add_circle', route: '/vehicles' },
  { id: 'add-driver', label: 'Add Driver', icon: 'person_add', route: '/drivers' },
  { id: 'schedule-maint', label: 'Schedule Maint.', icon: 'schedule', route: '/fleet/maintenance' },
  { id: 'assign-driver', label: 'Assign Driver', icon: 'assignment_ind', route: '/fleet/assignments' }
];

export const ALERTS_MOCK: CriticalAlert[] = [
  {
    id: 'a1',
    icon: 'error',
    tone: 'error',
    title: 'Inspection Overdue: SF-2108',
    detail: 'Safety certification expired 2 days ago.',
    actionLabel: 'Schedule Now'
  },
  {
    id: 'a2',
    icon: 'local_gas_station',
    tone: 'warning',
    title: 'Low Fuel Warning: SF-9042',
    detail: 'Estimated range: 42 miles. Route requires 120 miles.'
  },
  {
    id: 'a3',
    icon: 'tire_repair',
    tone: 'error',
    title: 'Pressure Alert: SF-4421',
    detail: 'Front-left tire below 80 PSI. Immediate check recommended.'
  }
];

export const ACTIVITIES_MOCK: ActivityEvent[] = [
  { id: 'e1', title: 'SF-1290 arrived at Destination Depot', timeAgo: '12 minutes ago', tone: 'primary' },
  { id: 'e2', title: 'Maintenance completed for SF-4011', timeAgo: '1 hour ago', tone: 'secondary' },
  { id: 'e3', title: 'New driver Elena Vance onboarded', timeAgo: '3 hours ago', tone: 'primary' },
  { id: 'e4', title: 'Route optimized for East Coast Region', timeAgo: '5 hours ago', tone: 'muted' }
];

export const ASSIGNMENTS_MOCK: AssignmentItem[] = [
  { id: 1, vehicleId: 'SF-9042', vehicleModel: 'Volvo FH16', driver: 'Marcus Thorne', route: 'Chicago → Nashville', status: 'In Transit', eta: '4:10 PM' },
  { id: 2, vehicleId: 'SF-2108', vehicleModel: 'Tesla Semi', driver: 'Sarah Jenkins', route: 'Phoenix → San Diego', status: 'Loading', eta: '5:30 PM' },
  { id: 3, vehicleId: 'SF-4421', vehicleModel: 'Scania R500', driver: 'Robert King', route: 'Dallas → Houston', status: 'In Transit', eta: '6:45 PM' }
];
