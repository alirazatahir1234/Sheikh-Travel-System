import { Injectable, signal } from '@angular/core';
import { DEFAULT_TIME_ZONE } from '../constants/time-zone.constants';
import {
  DateInput,
  activeTimeZone,
  formatDateInZone,
  formatInZone,
  formatTimeInZone,
  isValidTimeZone,
  resolveZone,
  setActiveTimeZone
} from '../utils/zoned-time.util';

/**
 * Company (tenant) display timezone for dates and times. GPS screens for one vehicle pass the
 * vehicle's branch zone (API `timeZoneId`) explicitly; everything else uses the company zone.
 * Never the browser timezone: a dispatcher in Dubai viewing a Pakistan fleet sees Pakistan time.
 */
@Injectable({ providedIn: 'root' })
export class TenantTimeService {
  private readonly zone = signal<string>(activeTimeZone() || DEFAULT_TIME_ZONE);

  /** Bumps whenever the zone changes, so impure pipes can re-render. */
  readonly version = signal(0);

  /** Apply the company timezone from branding (effectiveTimeZone). */
  setCompanyTimeZone(iana: string | null | undefined): void {
    if (!isValidTimeZone(iana)) return;
    const z = iana!.trim();
    if (z === this.zone()) return;
    setActiveTimeZone(z);
    this.zone.set(z);
    this.version.update(v => v + 1);
  }

  /** Company zone (or the platform default until branding loads). */
  timeZone(): string {
    return this.zone();
  }

  /** Explicit zone when valid, else the company zone. */
  resolve(zone?: string | null): string {
    return resolveZone(zone);
  }

  format(value: DateInput, options?: Intl.DateTimeFormatOptions, zone?: string | null): string {
    return formatInZone(value, options, zone);
  }

  formatTime(value: DateInput, options?: Intl.DateTimeFormatOptions, zone?: string | null): string {
    return formatTimeInZone(value, options, zone);
  }

  formatDate(value: DateInput, options?: Intl.DateTimeFormatOptions, zone?: string | null): string {
    return formatDateInZone(value, options, zone);
  }
}
