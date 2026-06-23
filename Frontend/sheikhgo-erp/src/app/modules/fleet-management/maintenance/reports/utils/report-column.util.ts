export const REPORT_CATALOG = [
  { id: 'vehicle-maintenance', label: 'Vehicle Maintenance', icon: 'directions_bus', description: 'Per-vehicle service history and costs' },
  { id: 'service-due', label: 'Service Due', icon: 'event_upcoming', description: 'Upcoming scheduled services' },
  { id: 'overdue-maintenance', label: 'Overdue Maintenance', icon: 'warning', description: 'Vehicles past due date' },
  { id: 'workshop-performance', label: 'Workshop Performance', icon: 'build', description: 'Workshop throughput and costs' },
  { id: 'vendor-performance', label: 'Vendor Performance', icon: 'store', description: 'Vendor spend and usage' },
  { id: 'cost-analysis', label: 'Cost Analysis', icon: 'payments', description: 'Maintenance cost breakdown' },
  { id: 'breakdown', label: 'Breakdown Report', icon: 'car_crash', description: 'Vehicle breakdown incidents' }
] as const;

export type ReportCatalogId = typeof REPORT_CATALOG[number]['id'];

export function statusOptionsForReport(reportType: string): { value: string; label: string }[] {
  switch (reportType) {
    case 'service-due':
    case 'overdue-maintenance':
      return [
        { value: '', label: 'All' },
        { value: 'DueSoon', label: 'Due Soon' },
        { value: 'Overdue', label: 'Overdue' },
        { value: 'Scheduled', label: 'Scheduled' }
      ];
    case 'workshop-performance':
    case 'vehicle-maintenance':
      return [
        { value: '', label: 'All' },
        { value: 'Open', label: 'Open' },
        { value: 'Completed', label: 'Completed' }
      ];
    case 'breakdown':
      return [
        { value: '', label: 'All' },
        { value: 'Open', label: 'Open' },
        { value: 'Resolved', label: 'Resolved' }
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
  if (format === 'currency') return new Intl.NumberFormat(undefined, { style: 'currency', currency: 'USD' }).format(Number(value));
  if (format === 'date') {
    const d = new Date(String(value));
    return Number.isNaN(d.getTime()) ? String(value) : d.toLocaleDateString();
  }
  if (format === 'number') return Number(value).toLocaleString();
  return String(value);
}
