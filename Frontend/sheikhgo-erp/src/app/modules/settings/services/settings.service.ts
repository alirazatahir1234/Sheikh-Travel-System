import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, shareReplay } from 'rxjs/operators';
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
      this.categories$ = this.http
        .get<SettingsCategory[]>(`${this.base}/categories`)
        .pipe(
          catchError(() => of(SETTINGS_CATEGORIES_FALLBACK)),
          shareReplay(1)
        );
    }
    return this.categories$;
  }

  getValues(category: string): Observable<SettingsValues> {
    return this.http.get<SettingsValues>(`${this.base}/${category}`);
  }

  update(category: string, values: SettingsValues): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${category}`, values);
  }
}
