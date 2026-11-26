import { formatDate } from '@angular/common';
import { LOCALE_ID, Pipe, PipeTransform, inject } from '@angular/core';
import { TenantTimeService } from '../../core/services/tenant-time.service';
import { DateInput, isDateOnly, resolveZone, toDate, zoneOffsetString } from '../../core/utils/zoned-time.util';

/**
 * Shared date pipe: Angular DatePipe formats, rendered in the company timezone — or an explicit zone
 * such as a vehicle's branch (`timeZoneId` from the API). Never the browser's timezone.
 *
 *   {{ p.timestamp | tenantDate:'medium' }}
 *   {{ p.timestamp | tenantDate:'mediumTime':bundle.timeZoneId }}
 *
 * Impure so it re-renders when the company zone loads; results are memoised per input.
 */
@Pipe({ name: 'tenantDate', standalone: true, pure: false })
export class TenantDatePipe implements PipeTransform {
  private readonly tenantTime = inject(TenantTimeService);
  private readonly locale = inject(LOCALE_ID);

  private lastKey = '';
  private lastValue: string | null = null;

  transform(value: DateInput, format = 'mediumDate', zone?: string | null): string | null {
    // Calendar dates ('YYYY-MM-DD') have no instant — never shift them by a timezone.
    if (isDateOnly(value)) return formatDate(value.trim(), format, this.locale);

    const date = toDate(value);
    if (!date) return null;

    const resolved = resolveZone(zone);
    const key = `${date.getTime()}|${format}|${resolved}|${this.tenantTime.version()}`;
    if (key === this.lastKey) return this.lastValue;

    this.lastKey = key;
    this.lastValue = formatDate(date, format, this.locale, zoneOffsetString(date, resolved));
    return this.lastValue;
  }
}
