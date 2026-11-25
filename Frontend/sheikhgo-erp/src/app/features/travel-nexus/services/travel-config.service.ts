import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  SaveTravelDocumentType, SaveTravelLeadSource, SaveTravelVisaType, TravelCountry, TravelLookups,
  TravelDocumentType, TravelLeadSource, TravelSettings, TravelVisaType
} from '../models/travel.models';

/** TravelNexus configuration API (`api/travel/config`). Responses are unwrapped by ApiEnvelopeInterceptor. */
@Injectable({ providedIn: 'root' })
export class TravelConfigService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/travel/config`;

  /** ISO country list rarely changes; cached for the session. */
  readonly countries$: Observable<TravelCountry[]> = this.http
    .get<TravelCountry[]>(`${this.base}/countries`)
    .pipe(shareReplay({ bufferSize: 1, refCount: false }));

  /** Tenant branches + staff (id, name) for dropdowns. */
  getLookups(): Observable<TravelLookups> {
    return this.http.get<TravelLookups>(`${this.base}/lookups`);
  }

  getVisaTypes(countryCode?: string, isActive?: boolean): Observable<TravelVisaType[]> {
    let params = new HttpParams();
    if (countryCode) params = params.set('countryCode', countryCode);
    if (isActive !== undefined) params = params.set('isActive', isActive);
    return this.http.get<TravelVisaType[]>(`${this.base}/visa-types`, { params });
  }
  createVisaType(dto: SaveTravelVisaType): Observable<number> {
    return this.http.post<number>(`${this.base}/visa-types`, dto);
  }
  updateVisaType(id: number, dto: SaveTravelVisaType): Observable<number> {
    return this.http.put<number>(`${this.base}/visa-types/${id}`, dto);
  }
  deleteVisaType(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/visa-types/${id}`);
  }

  getDocumentTypes(isActive?: boolean): Observable<TravelDocumentType[]> {
    const params = isActive === undefined ? undefined : new HttpParams().set('isActive', isActive);
    return this.http.get<TravelDocumentType[]>(`${this.base}/document-types`, { params });
  }
  createDocumentType(dto: SaveTravelDocumentType): Observable<number> {
    return this.http.post<number>(`${this.base}/document-types`, dto);
  }
  updateDocumentType(id: number, dto: SaveTravelDocumentType): Observable<number> {
    return this.http.put<number>(`${this.base}/document-types/${id}`, dto);
  }
  deleteDocumentType(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/document-types/${id}`);
  }

  getLeadSources(isActive?: boolean): Observable<TravelLeadSource[]> {
    const params = isActive === undefined ? undefined : new HttpParams().set('isActive', isActive);
    return this.http.get<TravelLeadSource[]>(`${this.base}/lead-sources`, { params });
  }
  createLeadSource(dto: SaveTravelLeadSource): Observable<number> {
    return this.http.post<number>(`${this.base}/lead-sources`, dto);
  }
  updateLeadSource(id: number, dto: SaveTravelLeadSource): Observable<number> {
    return this.http.put<number>(`${this.base}/lead-sources/${id}`, dto);
  }
  deleteLeadSource(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/lead-sources/${id}`);
  }

  getSettings(): Observable<TravelSettings> {
    return this.http.get<TravelSettings>(`${this.base}/settings`);
  }
  updateSettings(dto: TravelSettings): Observable<TravelSettings> {
    return this.http.put<TravelSettings>(`${this.base}/settings`, dto);
  }
}
