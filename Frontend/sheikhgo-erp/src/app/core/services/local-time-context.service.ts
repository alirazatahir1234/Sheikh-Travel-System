import { Injectable } from '@angular/core';
import { BehaviorSubject, combineLatest, distinctUntilChanged, interval, map, startWith } from 'rxjs';

/**
 * Resolves a display timezone and (when available) a city/region.
 * Preference order (product rule): company/tenant timezone → user profile → browser Intl.
 */
export interface LocalTimeDisplay {
  time: string;
  timeZoneId: string;
  offsetAndAbbr: string;
  cityLine: string;
  dateLine: string;
}

@Injectable({ providedIn: 'root' })
export class LocalTimeContextService {
  private companyTimeZone$ = new BehaviorSubject<string | null>(null);
  private userTimeZone$ = new BehaviorSubject<string | null>(null);
  private browserTimeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;

  /** Apply company (tenant) IANA timezone when known. */
  setCompanyTimeZone(iana: string | null | undefined): void {
    const cleaned = (iana || '').trim();
    this.companyTimeZone$.next(cleaned || null);
  }

  /** Apply user profile IANA timezone (used when company timezone is unset). */
  setUserTimeZone(iana: string | null | undefined): void {
    const cleaned = (iana || '').trim();
    this.userTimeZone$.next(cleaned || null);
  }

  /** Effective zone for formatting. */
  resolveTimeZoneId(): string {
    return this.companyTimeZone$.value
      || this.userTimeZone$.value
      || this.browserTimeZone;
  }

  private humanIanaName(iana: string): string {
    return iana.split('/').pop()?.replace(/_/g, ' ') || iana;
  }

  private formatOffsetAndAbbr(d: Date, timeZone: string): string {
    const longOff = new Intl.DateTimeFormat('en-GB', {
      timeZone,
      timeZoneName: 'longOffset'
    })
      .formatToParts(d)
      .find(p => p.type === 'timeZoneName')?.value;
    const short = new Intl.DateTimeFormat('en-GB', {
      timeZone,
      timeZoneName: 'short'
    })
      .formatToParts(d)
      .find(p => p.type === 'timeZoneName')?.value;
    if (longOff && short && longOff !== short) {
      return `${longOff} · ${short}`;
    }
    return short || longOff || '';
  }

  /**
   * Emits display info on load and every 30s.
   */
  clockDisplay$(tickMs = 30_000) {
    const effectiveZone$ = combineLatest([this.companyTimeZone$, this.userTimeZone$]).pipe(
      map(([company, user]) => company || user || this.browserTimeZone),
      distinctUntilChanged()
    );

    return combineLatest([
      effectiveZone$,
      interval(tickMs).pipe(startWith(0))
    ]).pipe(
      map(([timeZone]) => {
        const d = new Date();
        const time = d.toLocaleTimeString(undefined, {
          timeZone,
          hour: 'numeric',
          minute: '2-digit',
          hour12: true
        });
        const dateLine = d.toLocaleDateString(undefined, {
          timeZone,
          weekday: 'short',
          month: 'short',
          day: 'numeric',
          year: 'numeric'
        });
        return {
          time,
          timeZoneId: timeZone,
          offsetAndAbbr: this.formatOffsetAndAbbr(d, timeZone),
          cityLine: this.humanIanaName(timeZone),
          dateLine
        } satisfies LocalTimeDisplay;
      })
    );
  }
}
