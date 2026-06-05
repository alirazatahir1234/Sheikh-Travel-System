import { Injectable } from '@angular/core';
import { BehaviorSubject, combineLatest, distinctUntilChanged, interval, map, startWith } from 'rxjs';

/**
 * Resolves a display timezone and (when available) a city/region from IP geolocation.
 * Time is formatted in that IANA zone so the top bar clock matches the user's area.
 * Falls back to the browser's Intl zone if the lookup fails.
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
  private timeZone$ = new BehaviorSubject<string | null>(null);
  private cityLine$ = new BehaviorSubject<string>('');

  constructor() {
    const fromIntl = Intl.DateTimeFormat().resolvedOptions().timeZone;
    this.timeZone$.next(fromIntl);
    this.cityLine$.next(this.humanIanaName(fromIntl));
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
    return combineLatest([
      this.timeZone$.pipe(
        map(z => z ?? Intl.DateTimeFormat().resolvedOptions().timeZone),
        distinctUntilChanged()
      ),
      this.cityLine$,
      interval(tickMs).pipe(startWith(0))
    ]).pipe(
      map(([timeZone, cityLine]) => {
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
          cityLine,
          dateLine
        } satisfies LocalTimeDisplay;
      })
    );
  }
}
