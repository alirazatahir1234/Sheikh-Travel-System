import { DriverListItem, DriverStats, DriverStatus } from '../../../core/models/driver.model';
import { FleetSummaryCardData } from '../../vehicles/components/fleet-summary-card/fleet-summary-card.component';

export interface DriverPagination {
  page: number;
  pageSize: number;
  total: number;
}

export const DEFAULT_DRIVER_PAGE_SIZE = 5;
export const DRIVER_PAGE_SIZE_OPTIONS = [5, 10, 25, 50, 100] as const;

export interface DriverFilters {
  search: string;
  status: DriverStatus | 'ALL';
  branchId: number | 'ALL';
  licenseExpiry: 'ALL' | 'VALID' | 'EXPIRING' | 'EXPIRED';
  verificationStatus: string | 'ALL';
  availability: 'ALL' | 'Available' | 'Busy' | 'OnTrip' | 'Unavailable';
}

export const EMPTY_DRIVER_FILTERS: DriverFilters = {
  search: '',
  status: 'ALL',
  branchId: 'ALL',
  licenseExpiry: 'ALL',
  verificationStatus: 'ALL',
  availability: 'ALL'
};

export interface DriverKpiGroups {
  operational: FleetSummaryCardData[];
  risk: FleetSummaryCardData[];
}

export interface OperationsSummary {
  onTripNow: number;
  driversAvailable: number;
  driversAssigned: number;
  openIncidents: number;
}

export interface AssignmentCoverageView {
  assignedDrivers: number;
  unassignedDrivers: number;
  assignedVehicles: number;
  totalDrivers: number;
  driverAssignmentPct: number;
}

export interface ComplianceAttentionItem {
  label: string;
  icon: string;
  tone: string;
  count: number;
}

/** Client-side safety score; uses API rating when available. */
export function computeDriverScore(row: DriverListItem): number {
  if (row.rating != null) {
    return Math.round(Math.max(0, Math.min(100, row.rating * 20)));
  }
  let score = 88;
  if (row.licenseExpired) score -= 35;
  else if (row.licenseExpiringSoon) score -= 12;
  if (row.status === DriverStatus.Suspended) score -= 45;
  else if (row.status === DriverStatus.OnLeave) score -= 8;
  if (row.verificationStatus?.toLowerCase() === 'verified') score += 5;
  if (row.assignedVehicleId) score += 3;
  return Math.max(0, Math.min(100, score));
}

export function scoreTone(score: number): 'success' | 'warning' | 'error' {
  if (score >= 80) return 'success';
  if (score >= 60) return 'warning';
  return 'error';
}

/**
 * Compact KPI groups aligned with /vehicles density:
 * Operational = live capacity; Risk = compliance / attention.
 */
export function buildDriverKpiGroups(stats: DriverStats | null): DriverKpiGroups {
  const active     = stats?.active               ?? 0;
  const onTrip     = stats?.onTrip               ?? 0;
  const available  = stats?.available            ?? 0;
  const total      = stats?.totalDrivers         ?? 0;
  const expiring   = stats?.licensesExpiringSoon ?? 0;
  const expired    = stats?.licensesExpired      ?? 0;
  const suspended  = stats?.suspended            ?? 0;
  const pending    = stats?.pendingVerification  ?? 0;
  const gpsOnline  = stats?.gpsOnline            ?? 0;
  const utilization = total > 0 ? Math.round((onTrip / total) * 100) : 0;

  return {
    operational: [
      {
        title: 'Active Drivers',
        value: String(active),
        icon: 'verified_user',
        variant: 'operational',
        subtext: `${total} total roster`
      },
      {
        title: 'Available',
        value: String(available),
        icon: 'check_circle',
        variant: 'operational',
        subtext: 'Ready for dispatch'
      },
      {
        title: 'On Trip',
        value: String(onTrip),
        icon: 'route',
        variant: 'operational',
        progress: utilization,
        trend: onTrip > 0 ? 'Live' : undefined,
        trendUp: onTrip > 0,
        subtext: 'Currently en route'
      },
      {
        title: 'GPS Online',
        value: String(gpsOnline),
        icon: 'gps_fixed',
        variant: 'operational',
        subtext: 'Devices connected'
      }
    ],
    risk: [
      {
        title: 'License Expired',
        value: String(expired),
        icon: 'cancel',
        variant: 'risk',
        alert: expired > 0,
        subtext: 'Cannot dispatch'
      },
      {
        title: 'Expiring Soon',
        value: String(expiring),
        icon: 'warning',
        variant: 'risk',
        alert: expiring > 0,
        subtext: 'Within 30 days'
      },
      {
        title: 'Suspended',
        value: String(suspended),
        icon: 'block',
        variant: 'risk',
        alert: suspended > 0,
        subtext: 'Under review'
      },
      {
        title: 'Pending Verify',
        value: String(pending),
        icon: 'pending',
        variant: 'risk',
        alert: pending > 0,
        subtext: 'Awaiting review'
      }
    ]
  };
}

export function buildOperationsSummary(stats: DriverStats | null): OperationsSummary {
  return {
    onTripNow: stats?.onTrip ?? 0,
    driversAvailable: stats?.available ?? 0,
    driversAssigned: stats?.assignedDrivers ?? 0,
    openIncidents: stats?.openViolations ?? 0
  };
}

export function buildAssignmentCoverage(stats: DriverStats | null): AssignmentCoverageView {
  const total = stats?.totalDrivers ?? 0;
  const assignedDrivers = stats?.assignedDrivers ?? 0;
  const unassignedDrivers = Math.max(0, total - assignedDrivers);
  const driverAssignmentPct = total > 0 ? Math.round((assignedDrivers / total) * 100) : 0;
  return {
    assignedDrivers,
    unassignedDrivers,
    assignedVehicles: assignedDrivers,
    totalDrivers: total,
    driverAssignmentPct
  };
}

export function buildComplianceAttention(stats: DriverStats | null): ComplianceAttentionItem[] {
  return [
    { label: 'License Expired', icon: 'cancel', tone: 'accident', count: stats?.licensesExpired ?? 0 },
    { label: 'Expiring (30d)', icon: 'warning', tone: 'speed', count: stats?.licensesExpiringSoon ?? 0 },
    { label: 'Suspended', icon: 'block', tone: 'complaint', count: stats?.suspended ?? 0 },
    { label: 'Open Incidents', icon: 'report', tone: 'geofence', count: stats?.openViolations ?? 0 }
  ];
}

/** @deprecated Use buildDriverKpiGroups instead */
export function buildDriverKpiCards(stats: DriverStats | null): FleetSummaryCardData[] {
  const g = buildDriverKpiGroups(stats);
  return [...g.operational, ...g.risk];
}
