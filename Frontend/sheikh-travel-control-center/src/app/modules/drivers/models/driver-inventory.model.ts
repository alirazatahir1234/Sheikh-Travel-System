import { DriverStats, DriverStatus } from '../../../core/models/driver.model';
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
}

export const EMPTY_DRIVER_FILTERS: DriverFilters = {
  search: '',
  status: 'ALL',
  branchId: 'ALL',
  licenseExpiry: 'ALL',
  verificationStatus: 'ALL'
};

export function buildDriverKpiCards(stats: DriverStats | null): FleetSummaryCardData[] {
  const total      = stats?.totalDrivers         ?? 0;
  const active     = stats?.active               ?? 0;
  const onTrip     = stats?.onTrip               ?? 0;
  const available  = stats?.available            ?? 0;
  const expiring   = stats?.licensesExpiringSoon ?? 0;
  const suspended  = stats?.suspended            ?? 0;
  const operational = available + onTrip;
  const utilization = total > 0 ? Math.round((operational / total) * 100) : 0;
  return [
    { title: 'Total Drivers', value: String(total),     icon: 'groups' },
    { title: 'Active',        value: String(active || operational), icon: 'verified_user',  progress: utilization },
    { title: 'On Trip',       value: String(onTrip),    icon: 'directions_car', trend: 'Live', trendUp: true, subtext: 'Live tracking on' },
    { title: 'Available',     value: String(available), icon: 'check_circle',   subtext: 'Ready for dispatch' },
    { title: 'Expiring',      value: String(expiring),  icon: 'warning',        alert: expiring > 0,   subtext: 'Action required' },
    { title: 'Suspended',     value: String(suspended), icon: 'block',          alert: suspended > 0,  subtext: 'Under review' },
  ];
}
