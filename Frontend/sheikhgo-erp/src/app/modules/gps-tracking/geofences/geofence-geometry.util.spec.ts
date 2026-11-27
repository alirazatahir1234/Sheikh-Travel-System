import {
  circlePath,
  completeShape,
  distanceToLineMeters,
  formatArea,
  formatCoordinate,
  formatDistance,
  haversineMeters,
  isInsideShape,
  isShapeComplete,
  lineFromGeoJson,
  overlapFraction,
  parseCoordinateList,
  parseLatLng,
  polygonAreaSqMeters,
  rectanglePath,
  ringFromGeoJson,
  shapeFromApi,
  shapeMetrics,
  shapeToApi
} from './geofence-geometry.util';
import { GeofenceShape } from './geofence.models';

/** A square of about 200 m × 200 m in Pasrur. */
const SQUARE: GeofenceShape = {
  areaType: 'polygon',
  path: [
    { lat: 32.2600, lng: 74.6600 },
    { lat: 32.2600, lng: 74.662125 },
    { lat: 32.258203, lng: 74.662125 },
    { lat: 32.258203, lng: 74.6600 }
  ]
};

describe('geofence geometry', () => {
  it('measures a 200 m square like the server does (spherical excess, haversine perimeter)', () => {
    const m = shapeMetrics(SQUARE);
    expect(m.points).toBe(4);
    expect(m.areaSqMeters).toBeGreaterThan(39_500);
    expect(m.areaSqMeters).toBeLessThan(40_500);
    expect(m.perimeterMeters).toBeGreaterThan(795);
    expect(m.perimeterMeters).toBeLessThan(805);
  });

  it('measures a circle and a corridor with round ends', () => {
    const circle = shapeMetrics({ areaType: 'circle', center: { lat: 31.5, lng: 74.3 }, radiusMeters: 100 });
    expect(circle.points).toBe(0);
    expect(circle.areaSqMeters).toBeCloseTo(Math.PI * 100 * 100, 3);

    const line = [{ lat: 31.5, lng: 74.3 }, { lat: 31.5, lng: 74.31 }];
    const length = haversineMeters(line[0], line[1]);
    const corridor = shapeMetrics({ areaType: 'corridor', path: line, corridorWidthMeters: 200 });
    expect(corridor.areaSqMeters).toBeCloseTo(length * 200 + Math.PI * 100 * 100, 3);
    expect(corridor.perimeterMeters).toBeCloseTo(2 * length + 2 * Math.PI * 100, 3);
  });

  it('knows when a shape is complete enough to save', () => {
    expect(isShapeComplete(null)).toBeFalse();
    expect(isShapeComplete({ areaType: 'circle', center: { lat: 1, lng: 1 }, radiusMeters: 0 })).toBeFalse();
    expect(isShapeComplete({ areaType: 'polygon', path: SQUARE.path!.slice(0, 2) })).toBeFalse();
    expect(isShapeComplete({ areaType: 'corridor', path: SQUARE.path!.slice(0, 2) })).toBeFalse();
    expect(isShapeComplete({ areaType: 'corridor', path: SQUARE.path!.slice(0, 2), corridorWidthMeters: 50 })).toBeTrue();
    expect(completeShape(SQUARE)).toBe(SQUARE);
    expect(completeShape({ areaType: 'polygon', path: [] })).toBeNull();
  });

  it('tells inside from outside for every shape type', () => {
    expect(isInsideShape({ lat: 32.2591, lng: 74.6610 }, SQUARE)).toBeTrue();
    expect(isInsideShape({ lat: 32.2610, lng: 74.6610 }, SQUARE)).toBeFalse();

    const circle: GeofenceShape = { areaType: 'circle', center: { lat: 31.5, lng: 74.3 }, radiusMeters: 100 };
    expect(isInsideShape({ lat: 31.5005, lng: 74.3 }, circle)).toBeTrue(); // ~56 m
    expect(isInsideShape({ lat: 31.5015, lng: 74.3 }, circle)).toBeFalse(); // ~167 m

    const corridor: GeofenceShape = { areaType: 'corridor', path: [{ lat: 31.5, lng: 74.3 }, { lat: 31.5, lng: 74.32 }], corridorWidthMeters: 200 };
    expect(isInsideShape({ lat: 31.5008, lng: 74.31 }, corridor)).toBeTrue(); // ~89 m from the line
    expect(isInsideShape({ lat: 31.5012, lng: 74.31 }, corridor)).toBeFalse(); // ~134 m
    expect(distanceToLineMeters({ lat: 31.5, lng: 74.31 }, corridor.path!)).toBeLessThan(0.5);
  });

  it('estimates how much of one shape lies inside another', () => {
    expect(overlapFraction(SQUARE, SQUARE)).toBeCloseTo(1, 2);
    const shifted: GeofenceShape = { areaType: 'polygon', path: SQUARE.path!.map(p => ({ lat: p.lat, lng: p.lng + 0.0010625 })) };
    const half = overlapFraction(SQUARE, shifted);
    expect(half).toBeGreaterThan(0.45);
    expect(half).toBeLessThan(0.55);
    const far: GeofenceShape = { areaType: 'circle', center: { lat: 33, lng: 75 }, radiusMeters: 100 };
    expect(overlapFraction(SQUARE, far)).toBe(0);
  });

  it('writes API GeoJSON with closed rings, [lng, lat] order and 6 decimals, and reads it back', () => {
    const api = shapeToApi(SQUARE);
    expect(api.areaType).toBe('polygon');
    expect(api.radiusMeters).toBe(0);
    const geo = JSON.parse(api.geoJson!);
    const ring: number[][] = geo.geometry.coordinates[0];
    expect(ring.length).toBe(5);
    expect(ring[0]).toEqual(ring[4]);
    expect(ring[1]).toEqual([74.662125, 32.26]);
    expect(ringFromGeoJson(api.geoJson)!.length).toBe(4);

    const corridor = shapeToApi({ areaType: 'corridor', path: [{ lat: 31.5, lng: 74.3 }, { lat: 31.6, lng: 74.4 }], corridorWidthMeters: 199.6 });
    expect(corridor.corridorWidthMeters).toBe(200);
    expect(lineFromGeoJson(corridor.geoJson)).toEqual([{ lat: 31.5, lng: 74.3 }, { lat: 31.6, lng: 74.4 }]);

    const circle = shapeToApi({ areaType: 'circle', center: { lat: 31.1234567, lng: 74.7654321 }, radiusMeters: 249.6 });
    expect(circle).toEqual(jasmine.objectContaining({ centerLat: 31.123457, centerLng: 74.765432, radiusMeters: 250, geoJson: null }));
  });

  it('reads the shapes the previous editor saved (Feature, FeatureCollection, bare Polygon)', () => {
    const ring = [[74.3, 31.5], [74.31, 31.5], [74.31, 31.51], [74.3, 31.5]];
    expect(ringFromGeoJson(JSON.stringify({ type: 'Polygon', coordinates: [ring] }))!.length).toBe(3);
    expect(ringFromGeoJson(JSON.stringify({ type: 'FeatureCollection', features: [{ type: 'Feature', geometry: { type: 'Polygon', coordinates: [ring] } }] }))!.length).toBe(3);
    expect(ringFromGeoJson('not json')).toBeNull();
    expect(shapeFromApi({ areaType: 'rectangle', centerLat: 0, centerLng: 0, radiusMeters: 0, geoJson: JSON.stringify({ type: 'Polygon', coordinates: [ring] }) })!.areaType)
      .toBe('rectangle');
    expect(shapeFromApi({ areaType: 'circle', centerLat: 31.5, centerLng: 74.3, radiusMeters: 0 })).toBeNull();
  });

  it('parses pasted coordinates and reports the lines it cannot read', () => {
    expect(parseLatLng('31.520412, 74.358749')).toEqual({ lat: 31.520412, lng: 74.358749 });
    expect(parseLatLng('(31.52 74.35)')).toEqual({ lat: 31.52, lng: 74.35 });
    expect(parseLatLng('95, 74')).toBeNull();
    expect(parseLatLng('Lahore')).toBeNull();
    const parsed = parseCoordinateList('31.5, 74.3\n\n31.6, 74.4; nonsense\n31.7, 74.5');
    expect(parsed.points.length).toBe(3);
    expect(parsed.badLines).toEqual([4]);
  });

  it('builds rectangle and circle outlines', () => {
    expect(rectanglePath({ lat: 1, lng: 2 }, { lat: 3, lng: 4 })).toEqual([
      { lat: 3, lng: 2 }, { lat: 3, lng: 4 }, { lat: 1, lng: 4 }, { lat: 1, lng: 2 }
    ]);
    const ring = circlePath({ lat: 31.5, lng: 74.3 }, 500, 32);
    expect(ring.length).toBe(32);
    for (const p of ring) expect(Math.abs(haversineMeters(p, { lat: 31.5, lng: 74.3 }) - 500)).toBeLessThan(5);
    expect(polygonAreaSqMeters(ring)).toBeGreaterThan(Math.PI * 500 * 500 * 0.98);
  });

  it('formats areas, distances and coordinates for people', () => {
    expect(formatArea(42_300)).toBe('42,300 m²');
    expect(formatArea(2_500_000)).toBe('2.5 km²');
    expect(formatDistance(846)).toBe('846 m');
    expect(formatDistance(1_234)).toBe('1.23 km');
    expect(formatDistance(25_400)).toBe('25.4 km');
    expect(formatCoordinate(31.5)).toBe('31.500000');
    expect(formatCoordinate(null)).toBe('—');
  });
});
