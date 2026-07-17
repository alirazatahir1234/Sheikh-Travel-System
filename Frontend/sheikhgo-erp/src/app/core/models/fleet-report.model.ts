/** Mirrors Backend/SheikhTravelSystem.Application/Common/ReportDtos.cs (ReportResponseDto) — the shared, self-describing shape every Fleet Reports endpoint returns. */

export interface FleetReportFilters {
  vehicleId?: number | null;
  driverId?: number | null;
  branchId?: number | null;
  departmentId?: number | null;
  from?: string;
  to?: string;
  status?: string;
  /** Only used when reportType is 'maintenance' — selects one of the existing Maintenance Reports sub-types. */
  maintenanceReportType?: string;
}

export interface FleetReportColumn {
  key: string;
  label: string;
  format: 'text' | 'currency' | 'date' | 'number';
}

export interface FleetReportRow {
  key: string;
  label: string;
  count: number;
  totalValue: number;
  fields: Record<string, unknown>;
}

export interface FleetReport {
  reportType: string;
  title: string;
  columns: FleetReportColumn[];
  rows: FleetReportRow[];
  totalValue: number;
  summary?: Record<string, unknown>;
}
