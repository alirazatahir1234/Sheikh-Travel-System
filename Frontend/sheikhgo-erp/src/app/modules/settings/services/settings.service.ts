import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map, shareReplay } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import { SETTINGS_CATEGORIES_FALLBACK } from '../config/settings-categories';
import { SettingsCategory, SettingsValues } from '../models/settings.model';

@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/settings`;
  private categories$?: Observable<SettingsCategory[]>;

  getCategories(): Observable<SettingsCategory[]> {
    if (!this.categories$) {
      this.categories$ = this.http.get<unknown>(`${this.base}/categories`).pipe(
        map(raw => this.normalizeCategories(raw)),
        catchError(() => of([...SETTINGS_CATEGORIES_FALLBACK])),
        shareReplay({ bufferSize: 1, refCount: false })
      );
    }
    return this.categories$;
  }

  getValues(category: string): Observable<SettingsValues> {
    return this.http.get<SettingsValues>(`${this.base}/${encodeURIComponent(category)}`).pipe(
      map(raw => (raw && typeof raw === 'object' && !Array.isArray(raw) ? raw : {})),
      catchError(() => of({} as SettingsValues))
    );
  }

  update(category: string, values: SettingsValues): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${encodeURIComponent(category)}`, values);
  }

  /** Accept camelCase or PascalCase API payloads and always return a usable list. */
  private normalizeCategories(raw: unknown): SettingsCategory[] {
    const list = Array.isArray(raw) ? raw : [];
    const mapped: SettingsCategory[] = [];

    for (const item of list) {
      if (!item || typeof item !== 'object') continue;
      const row = item as Record<string, unknown>;
      const id = String(row['id'] ?? row['Id'] ?? '').trim();
      if (!id) continue;
      mapped.push({
        id,
        label: String(row['label'] ?? row['Label'] ?? id),
        icon: String(row['icon'] ?? row['Icon'] ?? 'settings'),
        description: String(row['description'] ?? row['Description'] ?? ''),
        isImplemented: Boolean(row['isImplemented'] ?? row['IsImplemented'] ?? false)
      });
    }

    return mapped.length ? mapped : [...SETTINGS_CATEGORIES_FALLBACK];
  }
}
