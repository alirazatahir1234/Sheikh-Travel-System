import { DriverStatus } from '../../../core/models/driver.model';

export type LicenseExpiryFilter = 'ALL' | 'VALID' | 'EXPIRING' | 'EXPIRED';

export interface DriverFilters {
  search: string;
  status: DriverStatus | 'ALL';
  branchId: number | 'ALL';
  licenseExpiry: LicenseExpiryFilter;
  verificationStatus: string | 'ALL';
}

export interface DriverPagination {
  page: number;
  pageSize: number;
  total: number;
}

export const EMPTY_DRIVER_FILTERS: DriverFilters = {
  search: '',
  status: 'ALL',
  branchId: 'ALL',
  licenseExpiry: 'ALL',
  verificationStatus: 'ALL'
};

export type LicenseExpiryState = 'valid' | 'expiring' | 'expired';

export function licenseExpiryState(expired: boolean, expiringSoon: boolean): LicenseExpiryState {
  if (expired) return 'expired';
  if (expiringSoon) return 'expiring';
  return 'valid';
}

export function licenseExpiryLabel(state: LicenseExpiryState): string {
  switch (state) {
    case 'expired': return 'Expired';
    case 'expiring': return 'Expiring Soon';
    default: return 'Valid';
  }
}

export function availabilityBucketLabel(bucket?: string | null): string {
  switch (bucket) {
    case 'Busy': return 'Busy';
    case 'OnTrip': return 'On Trip';
    case 'Unavailable': return 'Unavailable';
    case 'Available': return 'Available';
    default: return bucket || '—';
  }
}

export function statusBadgeVariant(status: DriverStatus): 'success' | 'warning' | 'danger' | 'neutral' | 'info' {
  switch (status) {
    case DriverStatus.Available: return 'success';
    case DriverStatus.OnTrip: return 'info';
    case DriverStatus.OnLeave: return 'warning';
    case DriverStatus.Suspended: return 'danger';
    default: return 'neutral';
  }
}
