import { DriverListItem, DriverStatus } from '../../../core/models/driver.model';
import { UiSelectOption } from '../../../shared/components/ui/types/ui.types';

export function parseOptionalBookingId(value: unknown): number | null {
  if (value === null || value === undefined || value === '') return null;
  const n = Number(value);
  if (!Number.isFinite(n) || n <= 0) return null;
  return Math.floor(n);
}

export function driverAssignBlockReason(driver: DriverListItem): string | null {
  if (!driver.isActive) return 'Inactive';
  if (driver.licenseExpired) return 'License expired';
  if (driver.status === DriverStatus.Suspended) return 'Suspended';
  if (driver.status === DriverStatus.OnLeave) return 'On leave';
  if (driver.status === DriverStatus.OnTrip) return 'On trip';

  const verification = (driver.verificationStatus || '').toLowerCase();
  if (verification === 'rejected') return 'Verification rejected';
  if (verification === 'expireddocs') return 'Documents expired';

  return null;
}

export function buildDriverAssignOptions(drivers: DriverListItem[]): UiSelectOption[] {
  return drivers.map(driver => {
    const issue = driverAssignBlockReason(driver);
    return {
      value: String(driver.id),
      label: issue ? `${driver.fullName} (${driver.phone}) — ${issue}` : `${driver.fullName} (${driver.phone})`,
      disabled: !!issue
    };
  });
}
