import {
  HTTP_INTERCEPTORS,
  HttpClient,
  HttpEvent,
  HttpHandler,
  HttpInterceptor,
  HttpRequest,
  provideHttpClient,
  withInterceptorsFromDi
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Injectable } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Observable } from 'rxjs';
import { GEOAPIFY_AUTOCOMPLETE_URL, GEOAPIFY_KEY, PlaceSearchService, PlaceSuggestion } from './place-search.service';

/** Stands in for the app's auth interceptor, which puts these on every request. */
@Injectable()
class AddsLoginHeaders implements HttpInterceptor {
  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    return next.handle(req.clone({ setHeaders: { Authorization: 'Bearer login-token', 'X-Tenant-Id': '7' } }));
  }
}

describe('PlaceSearchService', () => {
  let http: HttpTestingController;

  function setUp(key: string): PlaceSearchService {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
        { provide: HTTP_INTERCEPTORS, useClass: AddsLoginHeaders, multi: true },
        { provide: GEOAPIFY_KEY, useValue: key }
      ]
    });
    http = TestBed.inject(HttpTestingController);
    return TestBed.inject(PlaceSearchService);
  }

  const isGeoapify = (r: HttpRequest<unknown>) => r.url === GEOAPIFY_AUTOCOMPLETE_URL;

  afterEach(() => http.verify());

  it('asks for six places in the UAE, Saudi Arabia and Pakistan, near the map centre', () => {
    const service = setUp('test-key');
    service.search('  Liberty Market ', { lat: 31.5497, lng: 74.3436 }).subscribe();

    const req = http.expectOne(isGeoapify);
    expect(req.request.method).toBe('GET');
    const p = req.request.params;
    expect(p.get('text')).toBe('Liberty Market');
    expect(p.get('format')).toBe('json');
    expect(p.get('limit')).toBe('6');
    expect(p.get('filter')).toBe('countrycode:ae,sa,pk');
    expect(p.get('bias')).toBe('proximity:74.3436,31.5497');
    expect(p.get('lang')).toBe('en');
    expect(p.get('apiKey')).toBe('test-key');
    req.flush({ results: [] });
  });

  it('sends Geoapify no login token or company header, although the app adds them to its own calls', () => {
    const service = setUp('test-key');
    TestBed.inject(HttpClient).get('/api/vehicles').subscribe();
    const own = http.expectOne('/api/vehicles');
    expect(own.request.headers.get('Authorization')).toBe('Bearer login-token');
    own.flush([]);

    service.search('Lahore').subscribe();
    const req = http.expectOne(isGeoapify);
    expect(req.request.headers.has('Authorization')).toBeFalse();
    expect(req.request.headers.has('X-Tenant-Id')).toBeFalse();
    expect(req.request.params.has('bias')).toBeFalse();
    req.flush({ results: [] });
  });

  it('keeps the label, position and box of each place, and skips places without a position', () => {
    const service = setUp('test-key');
    let got: PlaceSuggestion[] = [];
    service.search('Liberty').subscribe(r => (got = r));

    http.expectOne(isGeoapify).flush({
      results: [
        {
          formatted: 'Liberty Market, Gulberg, Lahore Cant, Pakistan',
          lat: 31.5101,
          lon: 74.3446,
          bbox: { lon1: 74.3428, lat1: 31.5087, lon2: 74.3463, lat2: 31.5116 }
        },
        { formatted: 'Somewhere without coordinates' },
        { formatted: 'Gulberg, Lahore', lat: 31.52, lon: 74.35, bbox: { lon1: 74.34 } }
      ]
    });

    expect(got).toEqual([
      {
        label: 'Liberty Market, Gulberg, Lahore Cant, Pakistan',
        lat: 31.5101,
        lng: 74.3446,
        bbox: { lon1: 74.3428, lat1: 31.5087, lon2: 74.3463, lat2: 31.5116 }
      },
      { label: 'Gulberg, Lahore', lat: 31.52, lng: 74.35 }
    ]);
  });

  it('sends nothing and finds nothing without a key', () => {
    const service = setUp('');
    const got: PlaceSuggestion[][] = [];

    expect(service.available).toBeFalse();
    service.search('Lahore').subscribe(r => got.push(r));

    http.expectNone(isGeoapify);
    expect(got).toEqual([[]]);
  });
});
