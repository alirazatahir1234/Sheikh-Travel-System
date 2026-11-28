import { HttpBackend, HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, InjectionToken, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

/** Geoapify browser key. Empty turns the place search off. */
export const GEOAPIFY_KEY = new InjectionToken<string>('GEOAPIFY_KEY', {
  providedIn: 'root',
  factory: () => environment.geoapifyKey
});

export const GEOAPIFY_AUTOCOMPLETE_URL = 'https://api.geoapify.com/v1/geocode/autocomplete';

/** The area a result covers, as Geoapify sends it (lon1/lat1 and lon2/lat2 are opposite corners). */
export interface PlaceBbox {
  lon1: number;
  lat1: number;
  lon2: number;
  lat2: number;
}

export interface PlaceSuggestion {
  label: string;
  lat: number;
  lng: number;
  bbox?: PlaceBbox;
}

interface GeoapifyResult {
  formatted?: string;
  lat?: number;
  lon?: number;
  bbox?: Partial<PlaceBbox>;
}

/**
 * Place search for the geofence editor (Geoapify address autocomplete).
 *
 * The request goes through an HttpClient built on HttpBackend, so none of the app's interceptors
 * run. The auth interceptor would otherwise send the user's login token and company headers to
 * Geoapify (its URL contains "/api"), and would take Geoapify's 401 for a bad key as an expired login.
 */
@Injectable({ providedIn: 'root' })
export class PlaceSearchService {
  private readonly http = new HttpClient(inject(HttpBackend));
  private readonly apiKey = (inject(GEOAPIFY_KEY) || '').trim();

  /** False without a key: the search box then only takes a pasted "lat, lng". */
  get available(): boolean {
    return this.apiKey.length > 0;
  }

  /** Up to six places in the UAE, Saudi Arabia and Pakistan, those near `near` first. */
  search(text: string, near?: { lat: number; lng: number } | null): Observable<PlaceSuggestion[]> {
    const query = text.trim();
    if (!this.available || !query) return of([]);

    let params = new HttpParams()
      .set('text', query)
      .set('format', 'json')
      .set('limit', 6)
      .set('filter', 'countrycode:ae,sa,pk');
    if (near && Number.isFinite(near.lat) && Number.isFinite(near.lng)) {
      params = params.set('bias', `proximity:${near.lng},${near.lat}`);
    }
    params = params.set('lang', 'en').set('apiKey', this.apiKey);

    return this.http
      .get<{ results?: GeoapifyResult[] }>(GEOAPIFY_AUTOCOMPLETE_URL, { params })
      .pipe(map(body => toSuggestions(body?.results ?? [])));
  }
}

function toSuggestions(results: GeoapifyResult[]): PlaceSuggestion[] {
  const out: PlaceSuggestion[] = [];
  for (const r of results) {
    if (!isFiniteNumber(r.lat) || !isFiniteNumber(r.lon)) continue;
    const bbox = toBbox(r.bbox);
    out.push({
      label: r.formatted?.trim() || `${r.lat.toFixed(5)}, ${r.lon.toFixed(5)}`,
      lat: r.lat,
      lng: r.lon,
      ...(bbox ? { bbox } : {})
    });
  }
  return out;
}

function toBbox(b: Partial<PlaceBbox> | undefined): PlaceBbox | null {
  if (!b) return null;
  const { lon1, lat1, lon2, lat2 } = b;
  return isFiniteNumber(lon1) && isFiniteNumber(lat1) && isFiniteNumber(lon2) && isFiniteNumber(lat2)
    ? { lon1, lat1, lon2, lat2 }
    : null;
}

function isFiniteNumber(v: unknown): v is number {
  return typeof v === 'number' && Number.isFinite(v);
}
