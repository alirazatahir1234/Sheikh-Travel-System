import { fakeAsync, tick } from '@angular/core/testing';
import { Observable, Subject, of, throwError } from 'rxjs';
import { PLACE_SEARCH_DEBOUNCE_MS, convertShape, placeBounds, placeSuggestions, shapeAt } from './geofence-editor.component';
import { GeofenceShape } from '../geofence.models';
import { shapeMetrics } from '../geofence-geometry.util';
import { PlaceSuggestion } from '../../../../core/services/place-search.service';

const SQUARE: GeofenceShape = {
  areaType: 'polygon',
  path: [{ lat: 31.5, lng: 74.3 }, { lat: 31.5, lng: 74.302 }, { lat: 31.498, lng: 74.302 }, { lat: 31.498, lng: 74.3 }]
};

describe('geofence editor shape switching', () => {
  it('keeps the drawing when switching between polygon, rectangle and circle', () => {
    const rect = convertShape(SQUARE, 'rectangle')!;
    expect(rect.areaType).toBe('rectangle');
    expect(rect.path!.length).toBe(4);

    const circle = convertShape(SQUARE, 'circle')!;
    expect(circle.areaType).toBe('circle');
    // Same area as the square.
    expect(Math.abs(shapeMetrics(circle).areaSqMeters - shapeMetrics(SQUARE).areaSqMeters) / shapeMetrics(SQUARE).areaSqMeters).toBeLessThan(0.01);

    const back = convertShape(circle, 'polygon')!;
    expect(back.areaType).toBe('polygon');
    expect(back.path!.length).toBe(16);
  });

  it('starts over for corridors, and leaves an unchanged type alone', () => {
    expect(convertShape(SQUARE, 'corridor')).toBeNull();
    expect(convertShape({ areaType: 'corridor', path: SQUARE.path!.slice(0, 2), corridorWidthMeters: 100 }, 'polygon')).toBeNull();
    expect(convertShape(SQUARE, 'polygon')).toBe(SQUARE);
    expect(convertShape(null, 'circle')).toBeNull();
  });

  it('drops a starting shape at a searched place', () => {
    expect(shapeAt({ lat: 31.5, lng: 74.3 }, 'circle')).toEqual({ areaType: 'circle', center: { lat: 31.5, lng: 74.3 }, radiusMeters: 250 });
    const square = shapeAt({ lat: 31.5, lng: 74.3 }, 'polygon')!;
    expect(square.path!.length).toBe(4);
    expect(shapeMetrics(square).perimeterMeters).toBeGreaterThan(1150);
    expect(shapeMetrics(square).perimeterMeters).toBeLessThan(1250);
    expect(shapeAt({ lat: 31.5, lng: 74.3 }, 'corridor')).toBeNull();
  });
});

describe('geofence editor place search', () => {
  const LAHORE: PlaceSuggestion = {
    label: 'Lahore, Punjab, Pakistan',
    lat: 31.5204,
    lng: 74.3587,
    bbox: { lon1: 74.1, lat1: 31.3, lon2: 74.6, lat2: 31.7 }
  };

  function typing(search: (text: string) => Observable<PlaceSuggestion[]>) {
    const query$ = new Subject<string>();
    const shown: (PlaceSuggestion[] | null)[] = [];
    const sub = placeSuggestions(query$, search).subscribe(r => shown.push(r));
    return { query$, shown, sub };
  }

  it('searches once, 350 ms after typing stops, and not again for the same text', fakeAsync(() => {
    const searched: string[] = [];
    const { query$, shown, sub } = typing(text => {
      searched.push(text);
      return of([LAHORE]);
    });

    query$.next('Lah');
    tick(100);
    query$.next('Laho');
    tick(PLACE_SEARCH_DEBOUNCE_MS - 1);
    expect(searched).toEqual([]);
    tick(1);
    expect(searched).toEqual(['Laho']);
    expect(shown).toEqual([[LAHORE]]);

    query$.next('Laho ');
    tick(PLACE_SEARCH_DEBOUNCE_MS);
    expect(searched).toEqual(['Laho']);
    sub.unsubscribe();
  }));

  it('searches nothing under three characters or for a pasted "lat, lng"', fakeAsync(() => {
    const searched: string[] = [];
    const { query$, shown, sub } = typing(text => {
      searched.push(text);
      return of([LAHORE]);
    });

    query$.next('La');
    tick(PLACE_SEARCH_DEBOUNCE_MS);
    query$.next('31.52, 74.35');
    tick(PLACE_SEARCH_DEBOUNCE_MS);

    expect(searched).toEqual([]);
    expect(shown).toEqual([null, null]);
    sub.unsubscribe();
  }));

  it('drops an older search when newer text arrives, and shows no places when the search fails', fakeAsync(() => {
    const slow = new Subject<PlaceSuggestion[]>();
    const { query$, shown, sub } = typing(text =>
      text === 'Gulberg' ? slow : text === 'Broken' ? throwError(() => new Error('429')) : of([LAHORE]));

    query$.next('Gulberg');
    tick(PLACE_SEARCH_DEBOUNCE_MS);
    query$.next('Lahore');
    tick(PLACE_SEARCH_DEBOUNCE_MS);
    slow.next([{ label: 'Gulberg, Lahore', lat: 31.51, lng: 74.35 }]);
    expect(shown).toEqual([[LAHORE]]);

    query$.next('Broken');
    tick(PLACE_SEARCH_DEBOUNCE_MS);
    expect(shown).toEqual([[LAHORE], []]);
    sub.unsubscribe();
  }));

  it('fits the map to a town, but shows a shop or a short street like a point', () => {
    expect(placeBounds(LAHORE)).toEqual({ minLat: 31.3, maxLat: 31.7, minLng: 74.1, maxLng: 74.6 });
    // Boxes as Geoapify sends them: about 10 m for a shop, about 250 m for a short street.
    expect(placeBounds({ label: 'Shop', lat: 31.5113, lng: 74.345, bbox: { lon1: 74.34496, lat1: 31.51123, lon2: 74.34506, lat2: 31.51133 } })).toBeNull();
    expect(placeBounds({ label: 'Street', lat: 31.51, lng: 74.3445, bbox: { lon1: 74.3433, lat1: 31.5089, lon2: 74.3458, lat2: 31.5112 } })).toBeNull();
    expect(placeBounds({ label: 'No box', lat: 31.5, lng: 74.3 })).toBeNull();
  });
});
