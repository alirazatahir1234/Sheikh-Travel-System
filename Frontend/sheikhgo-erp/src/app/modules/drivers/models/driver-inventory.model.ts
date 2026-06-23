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
  tripsToday: number;
  driversAvailable: number;
  driversAssigned: number;
  incidentsToday: number;
}

export interface AssignmentCoverageView {
  assignedDrivers: number;
  unassignedDrivers: number;
  assignedVehicles: number;
  totalDrivers: number;
  driverAssignmentPct: number;
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

export function buildDriverKpiGroups(stats: DriverStats | null): DriverKpiGroups {
  const active     = stats?.active               ?? 0;
  const onTrip     = stats?.onTrip               ?? 0;
  const available  = stats?.available            ?? 0;
  const total      = stats?.totalDrivers         ?? 0;
  const expiring   = stats?.licensesExpiringSoon ?? 0;
  const suspended  = stats?.suspended            ?? 0;
  const onLeave    = stats?.onLeave              ?? 0;
  const assigned   = stats?.assignedDrivers      ?? 0;
  const utilization = total > 0 ? Math.round(((available + onTrip) / total) * 100) : 0;
  const assignPct  = total > 0 ? Math.round((assigned / total) * 100) : 0;

  return {
    operational: [
      {
        title: 'Active Drivers',
        value: String(active || available + onTrip),
        icon: 'verified_user',
        variant: 'operational',
        progress: utilization,
        subtext: `${available + onTrip} operational`
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
        icon: 'directions_car',
        variant: 'operational',
        trend: onTrip > 0 ? 'Live' : undefined,
        trendUp: onTrip > 0,
        subtext: 'Live tracking'
      },
      {
        title: 'GPS Online',
        value: String(stats?.gpsOnline ?? 0),
        icon: 'gps_fixed',
        variant: 'operational',
        subtext: 'Devices connected'
      }
    ],
    risk: [
      {
        title: 'License Expiring',
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
        title: 'On Leave',
        value: String(onLeave),
        icon: 'event_busy',
        variant: 'risk',
        subtext: 'Scheduled absence'
      },
      {
        title: 'Incidents',
        value: String((stats?.licensesExpiringIn7Days ?? 0) + suspended),
        icon: 'report',
        variant: 'risk',
        alert: ((stats?.licensesExpiringIn7Days ?? 0) + suspended) > 0,
        subtext: 'Requires attention'
      },
      {
        title: 'Assignment Health',
        value: `${assignPct}%`,
        icon: 'assignment_turned_in',
        variant: 'risk',
        progress: assignPct,
        subtext: `${assigned} of ${total} assigned`
      }
    ]
  };
}

export function buildOperationsSummary(stats: DriverStats | null): OperationsSummary {
  return {
    tripsToday: stats?.onTrip ?? 0,
    driversAvailable: stats?.available ?? 0,
    driversAssigned: stats?.assignedDrivers ?? 0,
    incidentsToday: (stats?.licensesExpiringIn7Days ?? 0) + (stats?.suspended ?? 0)
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

/** @deprecated Use buildDriverKpiGroups instead */
export function buildDriverKpiCards(stats: DriverStats | null): FleetSummaryCardData[] {
  const g = buildDriverKpiGroups(stats);
  return [...g.operational, ...g.risk];
}
