import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, shareReplay } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface CountryOption { name: string; code: string; }
export interface CurrencyOption { code: string; name: string; }
export interface TimezoneOption { id: string; displayName: string; offset: number; }

@Injectable({ providedIn: 'root' })
export class LookupService {
  private readonly base = `${environment.apiUrl}/lookup`;

  private countries$?: Observable<CountryOption[]>;
  private currencies$?: Observable<CurrencyOption[]>;
  private timezones$?: Observable<TimezoneOption[]>;

  constructor(private http: HttpClient) {}

  getCountries(): Observable<CountryOption[]> {
    this.countries$ ??= this.http.get<CountryOption[]>(`${this.base}/countries`).pipe(shareReplay(1));
    return this.countries$;
  }

  getCurrencies(): Observable<CurrencyOption[]> {
    this.currencies$ ??= this.http.get<CurrencyOption[]>(`${this.base}/currencies`).pipe(shareReplay(1));
    return this.currencies$;
  }

  getTimezones(): Observable<TimezoneOption[]> {
    this.timezones$ ??= this.http.get<TimezoneOption[]>(`${this.base}/timezones`).pipe(shareReplay(1));
    return this.timezones$;
  }

  getCountryNames(): Observable<string[]> {
    return this.getCountries().pipe(map(c => c.map(x => x.name)));
  }

  getCurrencyCodes(): Observable<string[]> {
    return this.getCurrencies().pipe(map(c => c.map(x => x.code)));
  }

  getTimezoneIds(): Observable<string[]> {
    return this.getTimezones().pipe(map(t => t.map(x => x.id)));
  }
}
