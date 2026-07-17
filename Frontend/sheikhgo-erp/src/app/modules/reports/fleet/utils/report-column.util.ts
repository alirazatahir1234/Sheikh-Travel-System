export const REPORT_CATALOG = [
  { id: 'trip', label: 'Trip Report', icon: 'route', description: 'Per-trip vehicle movement, duration, distance, and speed. Idle/engine-hour totals shown only for fleet-wide (no single vehicle selected).' },
  { id: 'vehicle', label: 'Vehicle Report', icon: 'directions_bus', description: 'Fleet roster with driver, tracker, status, and mileage. Registration Expiry is not tracked in this system — only Insurance Expiry is shown.' },
  { id: 'driver', label: 'Driver Report', icon: 'badge', description: 'Per-driver trips, distance, driving/idle hours, and score.' },
  { id: 'fuel', label: 'Fuel Report', icon: 'local_gas_station', description: 'Per-fill-up fuel logs with cost and consumption between odometer readings.' },
  { id: 'speed', label: 'Speed Report', icon: 'speed', description: 'Overspeed events with configured speed limit where set. Per-event duration is not tracked — only the point-in-time event.' },
  { id: 'idle', label: 'Idle Report', icon: 'pause_circle', description: 'Per-vehicle idle periods from GPS telemetry.' },
  { id: 'stop', label: 'Stop Report', icon: 'local_parking', description: 'Per-vehicle stop periods (arrival/departure) from GPS telemetry.' },
  { id: 'event', label: 'Event Report', icon: 'notifications', description: 'All GPS telemetry events (ignition, overspeed, SOS, geofence, connectivity).' },
  { id: 'alert', label: 'Alert Report', icon: 'warning', description: 'Alert events with severity, status, and resolution notes.' },
  { id: 'maintenance', label: 'Maintenance Report', icon: 'build', description: 'Service history and cost — pick a sub-type below (mirrors the existing Maintenance Reports page).' }
] as const;

export type ReportCatalogId = typeof REPORT_CATALOG[number]['id'];

export const MAINTENANCE_SUB_REPORT_CATALOG = [
  { id: 'cost-analysis', label: 'Cost Analysis' },
  { id: 'vehicle-maintenance', label: 'Vehicle Maintenance' },
  { id: 'service-due', label: 'Service Due' },
  { id: 'overdue-maintenance', label: 'Overdue Maintenance' },
  { id: 'workshop-performance', label: 'Workshop Performance' },
  { id: 'vendor-performance', label: 'Vendor Performance' },
  { id: 'breakdown', label: 'Breakdown Report' }
] as const;

export function statusOptionsForReport(reportType: string): { value: string; label: string }[] {
  switch (reportType) {
    case 'alert':
      return [
        { value: '', label: 'All' },
        { value: 'Open', label: 'Open' },
        { value: 'Acknowledged', label: 'Acknowledged' },
        { value: 'Resolved', label: 'Resolved' }
      ];
    case 'vehicle':
      return [
        { value: '', label: 'All' },
        { value: 'Available', label: 'Available' },
        { value: 'On Trip', label: 'On Trip' },
        { value: 'Maintenance', label: 'Maintenance' },
        { value: 'Retired', label: 'Retired' }
      ];
    default:
      return [];
  }
}

export function showStatusFilter(reportType: string): boolean {
  return statusOptionsForReport(reportType).length > 0;
}

export function formatFieldValue(value: unknown, format: string): string {
  if (value == null || value === '') return '—';
  if (format === 'currency') return `PKR ${Number(value).toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 2 })}`;
  if (format === 'date') {
    const d = new Date(String(value));
    return Number.isNaN(d.getTime()) ? String(value) : d.toLocaleString();
  }
  if (format === 'number') return Number(value).toLocaleString();
  return String(value);
}
