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
  // Do not block solely on Drivers.Status=OnTrip — that flag can be stale.
  // Backend EnsureDriverNotOnActiveTripAsync checks live bookings/trips.

  const verification = (driver.verificationStatus || '').toLowerCase();
  if (verification === 'rejected') return 'Verification rejected';
  if (verification === 'expireddocs') return 'Documents expired';
  if (verification !== 'verified') return 'Driver not verified';

  return null;
}

export function buildDriverAssignOptions(
  drivers: DriverListItem[],
  opts?: { includeBlocked?: boolean }
): UiSelectOption[] {
  const includeBlocked = opts?.includeBlocked ?? true;
  // flatMap avoids (T | null)[] + type-predicate mismatch with UiSelectOption.disabled?: boolean
  return drivers.flatMap(driver => {
    const block = driverAssignBlockReason(driver);
    if (block && !includeBlocked) return [];
    const option: UiSelectOption = {
      value: String(driver.id),
      label: block
        ? `${driver.fullName} (${driver.phone}) — ${block}`
        : `${driver.fullName} (${driver.phone})`,
      disabled: !!block
    };
    return [option];
  });
}
