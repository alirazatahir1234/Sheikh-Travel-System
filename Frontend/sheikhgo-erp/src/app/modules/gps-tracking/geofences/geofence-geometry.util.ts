import { GeofenceAreaType, GeofenceShape } from './geofence.models';

/**
 * Geofence geometry without Google Maps: area, perimeter, inside tests, overlap, GeoJSON and coordinate parsing.
 * The maths matches GpsGeoHelper.cs (spherical-excess area, planar distance-to-line, corridor = band with round
 * ends), so the editor shows the numbers the server stores.
 */

export interface LatLng {
  lat: number;
  lng: number;
}

export interface LatLngBounds {
  minLat: number;
  maxLat: number;
  minLng: number;
  maxLng: number;
}

const EARTH_RADIUS_M = 6_371_000;
const METERS_PER_DEG_LAT = 111_320;

export const MIN_CIRCLE_RADIUS_M = 50;
/** GpsGeoHelper.MaxCircleRadiusMeters. */
export const MAX_CIRCLE_RADIUS_M = 50_000;
export const MIN_CORRIDOR_WIDTH_M = 10;
export const MAX_CORRIDOR_WIDTH_M = 5_000;
export const MAX_BUFFER_M = 500;

const toRad = (d: number): number => (d * Math.PI) / 180;

export function haversineMeters(a: LatLng, b: LatLng): number {
  const dLat = toRad(b.lat - a.lat);
  const dLng = toRad(b.lng - a.lng);
  const h = Math.sin(dLat / 2) ** 2 + Math.cos(toRad(a.lat)) * Math.cos(toRad(b.lat)) * Math.sin(dLng / 2) ** 2;
  return 2 * EARTH_RADIUS_M * Math.atan2(Math.sqrt(h), Math.sqrt(1 - h));
}

export function pathLengthMeters(path: readonly LatLng[], closed: boolean): number {
  let total = 0;
  for (let i = 1; i < path.length; i++) total += haversineMeters(path[i - 1], path[i]);
  if (closed && path.length > 2) total += haversineMeters(path[path.length - 1], path[0]);
  return total;
}

/** Spherical-excess area of a ring (open or closed), as in GpsGeoHelper.ComputeAreaAndPerimeter. */
export function polygonAreaSqMeters(ring: readonly LatLng[]): number {
  if (ring.length < 3) return 0;
  let sum = 0;
  for (let i = 0, j = ring.length - 1; i < ring.length; j = i++) {
    sum += toRad(ring[i].lng - ring[j].lng) * (2 + Math.sin(toRad(ring[j].lat)) + Math.sin(toRad(ring[i].lat)));
  }
  return Math.abs((sum * EARTH_RADIUS_M * EARTH_RADIUS_M) / 2);
}

/** A shape has enough to be saved: a circle with a centre and radius, a ring of 3+, a line of 2+ with a width. */
/** The shape when it is complete, else null — use it to narrow before reading the shape's fields. */
export function completeShape(shape: GeofenceShape | null | undefined): GeofenceShape | null {
  return shape && isShapeComplete(shape) ? shape : null;
}

export function isShapeComplete(shape: GeofenceShape | null | undefined): boolean {
  if (!shape) return false;
  switch (shape.areaType) {
    case 'circle':
      return !!shape.center && (shape.radiusMeters ?? 0) > 0;
    case 'corridor':
      return distinctCount(shape.path ?? []) >= 2 && (shape.corridorWidthMeters ?? 0) > 0;
    default:
      return distinctCount(shape.path ?? []) >= 3;
  }
}

function distinctCount(path: readonly LatLng[]): number {
  return new Set(path.map(p => `${p.lat.toFixed(7)},${p.lng.toFixed(7)}`)).size;
}

export interface ShapeMetrics {
  /** Corner points; 0 for a circle. */
  points: number;
  areaSqMeters: number;
  perimeterMeters: number;
}

export function shapeMetrics(shape: GeofenceShape | null | undefined): ShapeMetrics {
  const s = completeShape(shape);
  if (!s) return { points: shape?.path?.length ?? 0, areaSqMeters: 0, perimeterMeters: 0 };
  if (s.areaType === 'circle') {
    const r = s.radiusMeters ?? 0;
    return { points: 0, areaSqMeters: Math.PI * r * r, perimeterMeters: 2 * Math.PI * r };
  }
  const path = s.path ?? [];
  if (s.areaType === 'corridor') {
    const length = pathLengthMeters(path, false);
    const half = (s.corridorWidthMeters ?? 0) / 2;
    return {
      points: path.length,
      areaSqMeters: length * half * 2 + Math.PI * half * half,
      perimeterMeters: 2 * length + 2 * Math.PI * half
    };
  }
  return { points: path.length, areaSqMeters: polygonAreaSqMeters(path), perimeterMeters: pathLengthMeters(path, true) };
}

/** The circle's centre, or the average of the points (GpsGeoHelper.CentroidOfRing). */
export function shapeCenter(shape: GeofenceShape | null | undefined): LatLng | null {
  if (!shape) return null;
  if (shape.areaType === 'circle') return shape.center ?? null;
  const path = shape.path ?? [];
  if (!path.length) return null;
  const sum = path.reduce((acc, p) => ({ lat: acc.lat + p.lat, lng: acc.lng + p.lng }), { lat: 0, lng: 0 });
  return { lat: sum.lat / path.length, lng: sum.lng / path.length };
}

export function shapeBounds(shape: GeofenceShape | null | undefined, marginMeters = 0): LatLngBounds | null {
  const s = completeShape(shape);
  if (!s) return null;
  let points: LatLng[];
  let grow = marginMeters;
  if (s.areaType === 'circle') {
    points = [s.center!];
    grow += s.radiusMeters ?? 0;
  } else {
    points = s.path ?? [];
    if (s.areaType === 'corridor') grow += (s.corridorWidthMeters ?? 0) / 2;
  }
  const lats = points.map(p => p.lat);
  const lngs = points.map(p => p.lng);
  const minLat = Math.min(...lats);
  const maxLat = Math.max(...lats);
  const dLat = grow / METERS_PER_DEG_LAT;
  const dLng = grow / (METERS_PER_DEG_LAT * Math.max(Math.cos(toRad((minLat + maxLat) / 2)), 0.01));
  return { minLat: minLat - dLat, maxLat: maxLat + dLat, minLng: Math.min(...lngs) - dLng, maxLng: Math.max(...lngs) + dLng };
}

/** Ray casting on lng/lat, as GpsGeoHelper.IsInsidePolygon. */
export function pointInRing(point: LatLng, ring: readonly LatLng[]): boolean {
  let inside = false;
  for (let i = 0, j = ring.length - 1; i < ring.length; j = i++) {
    const a = ring[i];
    const b = ring[j];
    const crosses = a.lat > point.lat !== b.lat > point.lat
      && point.lng < ((b.lng - a.lng) * (point.lat - a.lat)) / (b.lat - a.lat) + a.lng;
    if (crosses) inside = !inside;
  }
  return inside;
}

/** Planar distance (metres) from a point to an open line, as GpsGeoHelper.DistanceToPolylineMeters. */
export function distanceToLineMeters(point: LatLng, line: readonly LatLng[]): number {
  const mPerDegLng = METERS_PER_DEG_LAT * Math.cos(toRad(point.lat));
  let best = Number.POSITIVE_INFINITY;
  for (let i = 1; i < line.length; i++) {
    const ax = (line[i - 1].lng - point.lng) * mPerDegLng;
    const ay = (line[i - 1].lat - point.lat) * METERS_PER_DEG_LAT;
    const bx = (line[i].lng - point.lng) * mPerDegLng;
    const by = (line[i].lat - point.lat) * METERS_PER_DEG_LAT;
    const dx = bx - ax;
    const dy = by - ay;
    const lenSq = dx * dx + dy * dy;
    const t = lenSq <= 0 ? 0 : Math.min(1, Math.max(0, -(ax * dx + ay * dy) / lenSq));
    best = Math.min(best, Math.hypot(ax + t * dx, ay + t * dy));
  }
  return best;
}

export function isInsideShape(point: LatLng, shape: GeofenceShape): boolean {
  const s = completeShape(shape);
  if (!s) return false;
  switch (s.areaType) {
    case 'circle':
      return haversineMeters(point, s.center!) <= (s.radiusMeters ?? 0);
    case 'corridor':
      return distanceToLineMeters(point, s.path ?? []) <= (s.corridorWidthMeters ?? 0) / 2;
    default:
      return pointInRing(point, s.path ?? []);
  }
}

/**
 * Share (0–1) of shape A's area that lies inside shape B, sampled on a grid over A's bounding box. Good enough to
 * warn about overlaps while drawing; the server never relies on it.
 */
export function overlapFraction(a: GeofenceShape, b: GeofenceShape, grid = 40): number {
  const ab = shapeBounds(a);
  const bb = shapeBounds(b);
  if (!ab || !bb) return 0;
  if (ab.maxLat < bb.minLat || bb.maxLat < ab.minLat || ab.maxLng < bb.minLng || bb.maxLng < ab.minLng) return 0;
  let insideA = 0;
  let insideBoth = 0;
  for (let i = 0; i < grid; i++) {
    const lat = ab.minLat + ((i + 0.5) / grid) * (ab.maxLat - ab.minLat);
    for (let j = 0; j < grid; j++) {
      const lng = ab.minLng + ((j + 0.5) / grid) * (ab.maxLng - ab.minLng);
      const p = { lat, lng };
      if (!isInsideShape(p, a)) continue;
      insideA++;
      if (isInsideShape(p, b)) insideBoth++;
    }
  }
  return insideA === 0 ? 0 : insideBoth / insideA;
}

/** Points around a circle, for dashed outlines and exports. */
export function circlePath(center: LatLng, radiusMeters: number, segments = 64): LatLng[] {
  const path: LatLng[] = [];
  const dLat = radiusMeters / METERS_PER_DEG_LAT;
  const dLng = radiusMeters / (METERS_PER_DEG_LAT * Math.max(Math.cos(toRad(center.lat)), 0.01));
  for (let i = 0; i < segments; i++) {
    const a = (2 * Math.PI * i) / segments;
    path.push({ lat: center.lat + dLat * Math.sin(a), lng: center.lng + dLng * Math.cos(a) });
  }
  return path;
}

/** Rectangle ring NW, NE, SE, SW (the order the previous editor saved). */
export function rectanglePath(a: LatLng, b: LatLng): LatLng[] {
  const north = Math.max(a.lat, b.lat);
  const south = Math.min(a.lat, b.lat);
  const east = Math.max(a.lng, b.lng);
  const west = Math.min(a.lng, b.lng);
  return [
    { lat: north, lng: west },
    { lat: north, lng: east },
    { lat: south, lng: east },
    { lat: south, lng: west }
  ];
}

const round6 = (n: number): number => Math.round(n * 1e6) / 1e6;

/** GeoJSON text of a Polygon Feature (closed ring, [lng, lat]). */
export function polygonGeoJson(ring: readonly LatLng[]): string {
  const coords = ring.map(p => [round6(p.lng), round6(p.lat)]);
  if (coords.length && (coords[0][0] !== coords[coords.length - 1][0] || coords[0][1] !== coords[coords.length - 1][1])) {
    coords.push([...coords[0]]);
  }
  return JSON.stringify({ type: 'Feature', properties: {}, geometry: { type: 'Polygon', coordinates: [coords] } });
}

/** GeoJSON text of a LineString Feature ([lng, lat]). */
export function lineGeoJson(line: readonly LatLng[]): string {
  const coords = line.map(p => [round6(p.lng), round6(p.lat)]);
  return JSON.stringify({ type: 'Feature', properties: {}, geometry: { type: 'LineString', coordinates: coords } });
}

interface GeoJsonGeometry {
  type?: string;
  coordinates?: unknown;
}

function unwrapGeometry(root: unknown): GeoJsonGeometry | null {
  if (!root || typeof root !== 'object') return null;
  const r = root as { type?: string; geometry?: GeoJsonGeometry; features?: { geometry?: GeoJsonGeometry }[] };
  if (r.type === 'Feature') return r.geometry ?? null;
  if (r.type === 'FeatureCollection') return r.features?.[0]?.geometry ?? null;
  return r as GeoJsonGeometry;
}

function toLatLngs(coords: unknown): LatLng[] {
  if (!Array.isArray(coords)) return [];
  const out: LatLng[] = [];
  for (const c of coords) {
    if (!Array.isArray(c) || c.length < 2) continue;
    const lng = Number(c[0]);
    const lat = Number(c[1]);
    if (Number.isFinite(lat) && Number.isFinite(lng)) out.push({ lat, lng });
  }
  return out;
}

/** A polygon's outer ring from GeoJSON text (Feature, FeatureCollection or bare Polygon), without the closing point. */
export function ringFromGeoJson(geoJson: string | null | undefined): LatLng[] | null {
  if (!geoJson) return null;
  try {
    const g = unwrapGeometry(JSON.parse(geoJson));
    if (!g) return null;
    let coords: unknown = null;
    if (g.type === 'Polygon' && Array.isArray(g.coordinates)) coords = g.coordinates[0];
    else if (g.type === 'MultiPolygon' && Array.isArray(g.coordinates) && Array.isArray(g.coordinates[0])) coords = g.coordinates[0][0];
    const ring = toLatLngs(coords);
    if (ring.length > 1) {
      const first = ring[0];
      const last = ring[ring.length - 1];
      if (first.lat === last.lat && first.lng === last.lng) ring.pop();
    }
    return ring.length >= 3 ? ring : null;
  } catch {
    return null;
  }
}

/** A line from GeoJSON text (Feature, FeatureCollection or bare LineString). */
export function lineFromGeoJson(geoJson: string | null | undefined): LatLng[] | null {
  if (!geoJson) return null;
  try {
    const g = unwrapGeometry(JSON.parse(geoJson));
    if (!g || g.type !== 'LineString') return null;
    const line = toLatLngs(g.coordinates);
    return line.length >= 2 ? line : null;
  } catch {
    return null;
  }
}

export function normalizeAreaType(raw: string | null | undefined): GeofenceAreaType {
  const t = (raw ?? '').trim().toLowerCase();
  return t === 'polygon' || t === 'rectangle' || t === 'corridor' ? t : 'circle';
}

/** The editor's shape from API fields; null when the stored geometry cannot be read. */
export function shapeFromApi(fence: {
  areaType: string;
  centerLat: number;
  centerLng: number;
  radiusMeters: number;
  geoJson?: string | null;
  corridorWidthMeters?: number | null;
}): GeofenceShape | null {
  const areaType = normalizeAreaType(fence.areaType);
  if (areaType === 'circle') {
    if (!Number.isFinite(fence.centerLat) || !Number.isFinite(fence.centerLng) || !(fence.radiusMeters > 0)) return null;
    return { areaType, center: { lat: fence.centerLat, lng: fence.centerLng }, radiusMeters: fence.radiusMeters };
  }
  if (areaType === 'corridor') {
    const line = lineFromGeoJson(fence.geoJson);
    return line ? { areaType, path: line, corridorWidthMeters: fence.corridorWidthMeters ?? null } : null;
  }
  const ring = ringFromGeoJson(fence.geoJson);
  return ring ? { areaType, path: ring } : null;
}

export interface ApiGeometry {
  areaType: GeofenceAreaType;
  centerLat: number;
  centerLng: number;
  radiusMeters: number;
  geoJson: string | null;
  corridorWidthMeters: number | null;
}

/** API fields of a complete shape (6-decimal coordinates). */
export function shapeToApi(shape: GeofenceShape): ApiGeometry {
  const center = shapeCenter(shape) ?? { lat: 0, lng: 0 };
  const base = { areaType: shape.areaType, centerLat: round6(center.lat), centerLng: round6(center.lng) };
  switch (shape.areaType) {
    case 'circle':
      return { ...base, radiusMeters: Math.round(shape.radiusMeters ?? 0), geoJson: null, corridorWidthMeters: null };
    case 'corridor':
      return {
        ...base,
        radiusMeters: 0,
        geoJson: lineGeoJson(shape.path ?? []),
        corridorWidthMeters: Math.round(shape.corridorWidthMeters ?? 0)
      };
    default:
      return { ...base, radiusMeters: 0, geoJson: polygonGeoJson(shape.path ?? []), corridorWidthMeters: null };
  }
}

/** "31.520412, 74.358749", "31.52 74.35" or "(31.52, 74.35)" → lat/lng; null when it is not a valid pair. */
export function parseLatLng(text: string | null | undefined): LatLng | null {
  const m = /^\s*\(?\s*(-?\d+(?:\.\d+)?)\s*(?:,|\s)\s*(-?\d+(?:\.\d+)?)\s*\)?\s*$/.exec(text ?? '');
  if (!m) return null;
  const lat = Number(m[1]);
  const lng = Number(m[2]);
  if (!Number.isFinite(lat) || !Number.isFinite(lng) || Math.abs(lat) > 90 || Math.abs(lng) > 180) return null;
  return { lat, lng };
}

/** One "lat, lng" per line (or separated by ";"). Lines that are not a pair are reported by their 1-based number. */
export function parseCoordinateList(text: string | null | undefined): { points: LatLng[]; badLines: number[] } {
  const points: LatLng[] = [];
  const badLines: number[] = [];
  (text ?? '').split(/\r?\n|;/).forEach((line, i) => {
    if (!line.trim()) return;
    const p = parseLatLng(line);
    if (p) points.push(p);
    else badLines.push(i + 1);
  });
  return { points, badLines };
}

/** Ground metres per screen pixel at a latitude and zoom (Web Mercator). */
export function metersPerPixel(lat: number, zoom: number): number {
  return (156_543.033_92 * Math.cos(toRad(lat))) / 2 ** zoom;
}

export function formatArea(sqMeters: number | null | undefined): string {
  const v = Number(sqMeters) || 0;
  if (v >= 1_000_000) return `${(v / 1_000_000).toLocaleString('en-US', { maximumFractionDigits: 2 })} km²`;
  return `${Math.round(v).toLocaleString('en-US')} m²`;
}

export function formatDistance(meters: number | null | undefined): string {
  const v = Number(meters) || 0;
  if (v < 1000) return `${Math.round(v)} m`;
  return `${(v / 1000).toLocaleString('en-US', { maximumFractionDigits: v < 10_000 ? 2 : 1 })} km`;
}

/** Coordinates always show 6 decimals (about 0.1 m). */
export function formatCoordinate(value: number | null | undefined): string {
  return Number.isFinite(value) ? (value as number).toFixed(6) : '—';
}
