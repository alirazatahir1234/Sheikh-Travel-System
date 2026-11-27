import { GeofenceAreaType, GeofenceShape, SaveGeofencePayload } from './geofence.models';
import {
  LatLng,
  MAX_CIRCLE_RADIUS_M,
  MAX_CORRIDOR_WIDTH_M,
  MIN_CORRIDOR_WIDTH_M,
  circlePath,
  isShapeComplete,
  normalizeAreaType,
  shapeFromApi,
  shapeToApi
} from './geofence-geometry.util';

/**
 * Geofence files in and out: GeoJSON, KML and CSV. Parsing happens in the browser; each parsed row becomes a full
 * save payload that the server validates again on import. Imported geofences apply to all vehicles (so visits are
 * recorded at once) and start with no alerts.
 */

export type GeofenceFileFormat = 'geojson' | 'kml' | 'csv';

export const MAX_IMPORT_ITEMS = 500;
export const DEFAULT_IMPORT_RADIUS_M = 100;
export const DEFAULT_IMPORT_CORRIDOR_WIDTH_M = 100;

export interface ImportCandidate {
  /** Where the row came from, for the preview: "Feature 3", "Placemark 2", "Row 5". */
  source: string;
  payload: SaveGeofencePayload | null;
  warnings: string[];
  error: string | null;
}

export interface ExportableGeofence {
  name: string;
  type?: string | null;
  color?: string | null;
  groupName?: string | null;
  address?: string | null;
  description?: string | null;
  areaType: string;
  centerLat: number;
  centerLng: number;
  radiusMeters: number;
  geoJson?: string | null;
  corridorWidthMeters?: number | null;
  bufferMeters?: number | null;
  isActive?: boolean;
}

export function detectFormat(fileName: string, text: string): GeofenceFileFormat | null {
  const name = fileName.toLowerCase();
  if (name.endsWith('.kml')) return 'kml';
  if (name.endsWith('.geojson') || name.endsWith('.json')) return 'geojson';
  if (name.endsWith('.csv')) return 'csv';
  const head = text.trimStart().slice(0, 200);
  if (head.startsWith('{')) return 'geojson';
  if (head.startsWith('<')) return 'kml';
  return head.includes(',') ? 'csv' : null;
}

export function parseGeofenceFile(fileName: string, text: string): { format: GeofenceFileFormat | null; items: ImportCandidate[]; error: string | null } {
  const format = detectFormat(fileName, text);
  if (!format) return { format, items: [], error: 'Pick a .kml, .geojson or .csv file.' };
  let items: ImportCandidate[];
  try {
    items = format === 'kml' ? parseKml(text) : format === 'geojson' ? parseGeoJson(text) : parseCsv(text);
  } catch (e) {
    return { format, items: [], error: e instanceof Error ? e.message : 'The file could not be read.' };
  }
  if (!items.length) return { format, items, error: 'No geofences were found in the file.' };
  dedupeNames(items);
  if (items.length > MAX_IMPORT_ITEMS) {
    return { format, items, error: `The file has ${items.length} geofences; import at most ${MAX_IMPORT_ITEMS} at a time.` };
  }
  return { format, items, error: null };
}

/** Builds a save payload around a parsed shape, with the import defaults. */
export function importPayload(
  shape: GeofenceShape,
  props: { name?: string | null; type?: string | null; color?: string | null; address?: string | null; description?: string | null }
): SaveGeofencePayload {
  const geometry = shapeToApi(shape);
  return {
    name: (props.name ?? '').trim().slice(0, 100),
    type: props.type?.trim() || null,
    address: props.address?.trim().slice(0, 300) || null,
    description: props.description?.trim().slice(0, 500) || null,
    color: validHex(props.color),
    isActive: true,
    ...geometry,
    bufferMeters: 25,
    alertOnEnter: false,
    alertOnExit: false,
    engineBlockOnExit: false,
    afterHoursExitCritical: false,
    appliesTo: 'all',
    cooldownMinutes: 15
  };
}

function validHex(value: string | null | undefined): string | null {
  const v = (value ?? '').trim();
  if (/^#[0-9a-fA-F]{6}$/.test(v)) return v.toLowerCase();
  if (/^#[0-9a-fA-F]{3}$/.test(v)) return `#${v[1]}${v[1]}${v[2]}${v[2]}${v[3]}${v[3]}`.toLowerCase();
  return null;
}

function candidate(source: string, shape: GeofenceShape | null, props: Record<string, unknown>, warnings: string[]): ImportCandidate {
  const name = str(props['name']) ?? str(props['Name']) ?? str(props['title']);
  if (!shape || !isShapeComplete(shape)) {
    return { source, payload: null, warnings, error: 'No usable shape (needs a polygon, a line or a point).' };
  }
  if (shape.areaType === 'circle' && (shape.radiusMeters ?? 0) > MAX_CIRCLE_RADIUS_M) {
    return { source, payload: null, warnings, error: `Radius is over ${MAX_CIRCLE_RADIUS_M / 1000} km.` };
  }
  if (shape.areaType === 'corridor') {
    const w = shape.corridorWidthMeters ?? 0;
    if (w < MIN_CORRIDOR_WIDTH_M || w > MAX_CORRIDOR_WIDTH_M) {
      return { source, payload: null, warnings, error: `Corridor width must be ${MIN_CORRIDOR_WIDTH_M}–${MAX_CORRIDOR_WIDTH_M} m.` };
    }
  }
  const payload = importPayload(shape, {
    name: name ?? source,
    type: str(props['type']) ?? str(props['category']),
    color: str(props['color']) ?? str(props['stroke']) ?? str(props['fill']),
    address: str(props['address']),
    description: str(props['description'])
  });
  if (!name) warnings.push(`No name in the file; it will be called “${payload.name}”.`);
  return { source, payload, warnings, error: null };
}

function str(v: unknown): string | null {
  if (v === null || v === undefined) return null;
  const s = String(v).trim();
  return s ? s : null;
}

function num(v: unknown): number | null {
  if (v === null || v === undefined || v === '') return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
}

/** Names must be unique per company; within one file a repeated name gets " (2)", " (3)" … */
function dedupeNames(items: ImportCandidate[]): void {
  const seen = new Map<string, number>();
  for (const item of items) {
    if (!item.payload) continue;
    const key = item.payload.name.toLowerCase();
    const count = (seen.get(key) ?? 0) + 1;
    seen.set(key, count);
    if (count > 1) {
      const renamed = `${item.payload.name.slice(0, 94)} (${count})`;
      item.warnings.push(`Name repeated in the file; renamed to “${renamed}”.`);
      item.payload = { ...item.payload, name: renamed };
    }
  }
}

// ---- GeoJSON ------------------------------------------------------------------------------------------------------

interface GeoJsonFeature {
  type?: string;
  geometry?: { type?: string; coordinates?: unknown } | null;
  properties?: Record<string, unknown> | null;
}

export function parseGeoJson(text: string): ImportCandidate[] {
  let root: unknown;
  try {
    root = JSON.parse(text);
  } catch {
    throw new Error('The GeoJSON file is not valid JSON.');
  }
  const r = root as { type?: string; features?: GeoJsonFeature[] };
  const features: GeoJsonFeature[] =
    r.type === 'FeatureCollection' ? r.features ?? []
    : r.type === 'Feature' ? [r as GeoJsonFeature]
    : [{ type: 'Feature', geometry: root as GeoJsonFeature['geometry'], properties: {} }];

  return features.map((f, i) => {
    const props = f.properties ?? {};
    const warnings: string[] = [];
    return candidate(`Feature ${i + 1}`, geoJsonShape(f.geometry ?? null, props, warnings), props, warnings);
  });
}

function geoJsonShape(geometry: GeoJsonFeature['geometry'], props: Record<string, unknown>, warnings: string[]): GeofenceShape | null {
  if (!geometry?.type) return null;
  const declared = str(props['areaType'])?.toLowerCase();
  // Our own exports write circles as polygons with their real centre and radius in the properties.
  if (declared === 'circle') {
    const lat = num(props['centerLat']);
    const lng = num(props['centerLng']);
    const radius = num(props['radiusMeters']);
    if (lat !== null && lng !== null && radius) return { areaType: 'circle', center: { lat, lng }, radiusMeters: radius };
  }
  const coords = geometry.coordinates;
  switch (geometry.type) {
    case 'Point': {
      const p = pointOf(coords);
      if (!p) return null;
      const radius = num(props['radiusMeters']) ?? num(props['radius']) ?? num(props['radius_m']);
      if (!radius) warnings.push(`No radius given; using ${DEFAULT_IMPORT_RADIUS_M} m.`);
      return { areaType: 'circle', center: p, radiusMeters: radius ?? DEFAULT_IMPORT_RADIUS_M };
    }
    case 'LineString':
      return corridorOf(linePoints(coords), props, warnings);
    case 'Polygon':
      return polygonOf(Array.isArray(coords) ? coords[0] : null, declared === 'rectangle' ? 'rectangle' : 'polygon');
    case 'MultiPolygon': {
      const first = Array.isArray(coords) && Array.isArray(coords[0]) ? coords[0][0] : null;
      if (Array.isArray(coords) && coords.length > 1) warnings.push('Only the first part of a multi-part polygon is used.');
      return polygonOf(first, 'polygon');
    }
    default:
      return null;
  }
}

function pointOf(c: unknown): LatLng | null {
  if (!Array.isArray(c) || c.length < 2) return null;
  const lng = Number(c[0]);
  const lat = Number(c[1]);
  return Number.isFinite(lat) && Number.isFinite(lng) && Math.abs(lat) <= 90 && Math.abs(lng) <= 180 ? { lat, lng } : null;
}

function linePoints(c: unknown): LatLng[] {
  return Array.isArray(c) ? c.map(pointOf).filter((p): p is LatLng => !!p) : [];
}

function polygonOf(ring: unknown, areaType: GeofenceAreaType): GeofenceShape | null {
  const points = linePoints(ring);
  if (points.length > 1) {
    const a = points[0];
    const b = points[points.length - 1];
    if (a.lat === b.lat && a.lng === b.lng) points.pop();
  }
  return points.length >= 3 ? { areaType, path: points } : null;
}

function corridorOf(points: LatLng[], props: Record<string, unknown>, warnings: string[]): GeofenceShape | null {
  if (points.length < 2) return null;
  const width = num(props['corridorWidthMeters']) ?? num(props['width']) ?? num(props['width_m']);
  if (!width) warnings.push(`A line becomes a route corridor; no width given, using ${DEFAULT_IMPORT_CORRIDOR_WIDTH_M} m.`);
  return { areaType: 'corridor', path: points, corridorWidthMeters: width ?? DEFAULT_IMPORT_CORRIDOR_WIDTH_M };
}

export function toGeoJson(items: readonly ExportableGeofence[]): string {
  const features = items.map(g => {
    const shape = shapeFromApi(g);
    const props: Record<string, unknown> = {
      name: g.name,
      type: g.type ?? null,
      color: g.color ?? null,
      group: g.groupName ?? null,
      address: g.address ?? null,
      areaType: normalizeAreaType(g.areaType),
      bufferMeters: g.bufferMeters ?? null,
      active: g.isActive ?? true
    };
    let geometry: { type: string; coordinates: unknown } | null = null;
    if (shape?.areaType === 'circle') {
      Object.assign(props, { centerLat: shape.center!.lat, centerLng: shape.center!.lng, radiusMeters: shape.radiusMeters });
      geometry = { type: 'Polygon', coordinates: [closedRing(circlePath(shape.center!, shape.radiusMeters ?? 0))] };
    } else if (shape?.areaType === 'corridor') {
      props['corridorWidthMeters'] = shape.corridorWidthMeters ?? null;
      geometry = { type: 'LineString', coordinates: (shape.path ?? []).map(p => [r6(p.lng), r6(p.lat)]) };
    } else if (shape) {
      geometry = { type: 'Polygon', coordinates: [closedRing(shape.path ?? [])] };
    }
    return { type: 'Feature', properties: props, geometry };
  });
  return JSON.stringify({ type: 'FeatureCollection', features }, null, 2);
}

const r6 = (n: number): number => Math.round(n * 1e6) / 1e6;

function closedRing(path: readonly LatLng[]): number[][] {
  const ring = path.map(p => [r6(p.lng), r6(p.lat)]);
  if (ring.length) ring.push([...ring[0]]);
  return ring;
}

// ---- KML ----------------------------------------------------------------------------------------------------------

export function parseKml(text: string): ImportCandidate[] {
  const doc = new DOMParser().parseFromString(text, 'application/xml');
  if (doc.getElementsByTagName('parsererror').length) throw new Error('The KML file is not valid XML.');
  const placemarks = Array.from(doc.getElementsByTagNameNS('*', 'Placemark'));
  return placemarks.map((pm, i) => {
    const warnings: string[] = [];
    const props: Record<string, unknown> = {
      name: childText(pm, 'name'),
      description: childText(pm, 'description')
    };
    for (const data of Array.from(pm.getElementsByTagNameNS('*', 'Data'))) {
      const key = data.getAttribute('name');
      if (key) props[key] = childText(data, 'value');
    }
    return candidate(`Placemark ${i + 1}`, kmlShape(pm, props, warnings), props, warnings);
  });
}

function childText(el: Element, tag: string): string | null {
  const child = el.getElementsByTagNameNS('*', tag)[0];
  return child?.textContent?.trim() || null;
}

function kmlCoordinates(el: Element | undefined): LatLng[] {
  const raw = el?.getElementsByTagNameNS('*', 'coordinates')[0]?.textContent ?? '';
  return raw
    .trim()
    .split(/\s+/)
    .map(t => t.split(',').map(Number))
    .filter(c => c.length >= 2 && Number.isFinite(c[0]) && Number.isFinite(c[1]))
    .map(c => ({ lat: c[1], lng: c[0] }));
}

function kmlShape(pm: Element, props: Record<string, unknown>, warnings: string[]): GeofenceShape | null {
  if (str(props['areaType'])?.toLowerCase() === 'circle') {
    const lat = num(props['centerLat']);
    const lng = num(props['centerLng']);
    const radius = num(props['radiusMeters']);
    if (lat !== null && lng !== null && radius) return { areaType: 'circle', center: { lat, lng }, radiusMeters: radius };
  }
  const polygon = pm.getElementsByTagNameNS('*', 'Polygon')[0];
  if (polygon) {
    const outer = polygon.getElementsByTagNameNS('*', 'outerBoundaryIs')[0] ?? polygon;
    const ring = kmlCoordinates(outer);
    if (pm.getElementsByTagNameNS('*', 'Polygon').length > 1) warnings.push('Only the first polygon of the placemark is used.');
    const declared = str(props['areaType'])?.toLowerCase() === 'rectangle' ? 'rectangle' : 'polygon';
    return polygonOf(ring.map(p => [p.lng, p.lat]), declared);
  }
  const line = pm.getElementsByTagNameNS('*', 'LineString')[0];
  if (line) return corridorOf(kmlCoordinates(line), props, warnings);
  const point = pm.getElementsByTagNameNS('*', 'Point')[0];
  if (point) {
    const p = kmlCoordinates(point)[0];
    if (!p) return null;
    const radius = num(props['radiusMeters']) ?? num(props['radius']);
    if (!radius) warnings.push(`No radius given; using ${DEFAULT_IMPORT_RADIUS_M} m.`);
    return { areaType: 'circle', center: p, radiusMeters: radius ?? DEFAULT_IMPORT_RADIUS_M };
  }
  return null;
}

const xml = (s: string | null | undefined): string =>
  (s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

export function toKml(items: readonly ExportableGeofence[], documentName = 'Geofences'): string {
  const placemarks = items.map(g => {
    const shape = shapeFromApi(g);
    const data: Record<string, string | number | null | undefined> = {
      type: g.type,
      color: g.color,
      group: g.groupName,
      address: g.address,
      areaType: normalizeAreaType(g.areaType)
    };
    let geometry = '';
    const coords = (path: readonly LatLng[], close: boolean): string => {
      const pts = path.map(p => `${r6(p.lng)},${r6(p.lat)},0`);
      if (close && pts.length) pts.push(pts[0]);
      return pts.join(' ');
    };
    if (shape?.areaType === 'circle') {
      Object.assign(data, { centerLat: shape.center!.lat, centerLng: shape.center!.lng, radiusMeters: shape.radiusMeters });
      geometry = `<Polygon><outerBoundaryIs><LinearRing><coordinates>${coords(circlePath(shape.center!, shape.radiusMeters ?? 0), true)}</coordinates></LinearRing></outerBoundaryIs></Polygon>`;
    } else if (shape?.areaType === 'corridor') {
      data['corridorWidthMeters'] = shape.corridorWidthMeters;
      geometry = `<LineString><coordinates>${coords(shape.path ?? [], false)}</coordinates></LineString>`;
    } else if (shape) {
      geometry = `<Polygon><outerBoundaryIs><LinearRing><coordinates>${coords(shape.path ?? [], true)}</coordinates></LinearRing></outerBoundaryIs></Polygon>`;
    }
    const extended = Object.entries(data)
      .filter(([, v]) => v !== null && v !== undefined && v !== '')
      .map(([k, v]) => `<Data name="${xml(k)}"><value>${xml(String(v))}</value></Data>`)
      .join('');
    return `<Placemark><name>${xml(g.name)}</name>${g.description ? `<description>${xml(g.description)}</description>` : ''}<ExtendedData>${extended}</ExtendedData>${geometry}</Placemark>`;
  });
  return `<?xml version="1.0" encoding="UTF-8"?>\n<kml xmlns="http://www.opengis.net/kml/2.2"><Document><name>${xml(documentName)}</name>${placemarks.join('\n')}</Document></kml>\n`;
}

// ---- CSV ----------------------------------------------------------------------------------------------------------

/** Columns written and read. "points" is "lat,lng;lat,lng;…" for polygons, rectangles and corridors. */
export const CSV_COLUMNS = ['name', 'type', 'shape', 'center_lat', 'center_lng', 'radius_m', 'width_m', 'points', 'address', 'color', 'group'] as const;

/** RFC 4180: quoted fields, doubled quotes, commas and new lines inside quotes. */
export function parseCsvRows(text: string): string[][] {
  const rows: string[][] = [];
  let row: string[] = [];
  let field = '';
  let quoted = false;
  const src = text.replace(/^﻿/, '');
  for (let i = 0; i < src.length; i++) {
    const c = src[i];
    if (quoted) {
      if (c === '"' && src[i + 1] === '"') {
        field += '"';
        i++;
      } else if (c === '"') quoted = false;
      else field += c;
      continue;
    }
    if (c === '"') quoted = true;
    else if (c === ',') {
      row.push(field);
      field = '';
    } else if (c === '\n' || c === '\r') {
      if (c === '\r' && src[i + 1] === '\n') i++;
      row.push(field);
      rows.push(row);
      row = [];
      field = '';
    } else field += c;
  }
  if (field !== '' || row.length) {
    row.push(field);
    rows.push(row);
  }
  return rows.filter(r => r.some(f => f.trim() !== ''));
}

export function parseCsv(text: string): ImportCandidate[] {
  const rows = parseCsvRows(text);
  if (rows.length < 2) return [];
  const header = rows[0].map(h => h.trim().toLowerCase().replace(/\s+/g, '_'));
  const col = (name: string, ...aliases: string[]): number => [name, ...aliases].map(n => header.indexOf(n)).find(i => i >= 0) ?? -1;
  const idx = {
    name: col('name'),
    type: col('type', 'category'),
    shape: col('shape', 'area_type', 'areatype'),
    lat: col('center_lat', 'lat', 'latitude'),
    lng: col('center_lng', 'lng', 'lon', 'longitude'),
    radius: col('radius_m', 'radius', 'radius_meters'),
    width: col('width_m', 'width', 'corridor_width_m'),
    points: col('points', 'coordinates'),
    address: col('address'),
    color: col('color', 'colour'),
    group: col('group')
  };
  return rows.slice(1).map((r, i) => {
    const get = (k: number): string | null => (k >= 0 ? str(r[k]) : null);
    const warnings: string[] = [];
    const props: Record<string, unknown> = {
      name: get(idx.name), type: get(idx.type), address: get(idx.address), color: get(idx.color)
    };
    const declared = get(idx.shape)?.toLowerCase();
    const points = (get(idx.points) ?? '')
      .split(';')
      .map(p => p.trim())
      .filter(Boolean)
      .map(p => {
        const [lat, lng] = p.split(/[\s,]+/).map(Number);
        return Number.isFinite(lat) && Number.isFinite(lng) ? { lat, lng } : null;
      })
      .filter((p): p is LatLng => !!p);
    let shape: GeofenceShape | null = null;
    if (declared === 'corridor' || (declared !== 'polygon' && declared !== 'rectangle' && points.length === 2)) {
      const width = num(get(idx.width));
      if (!width) warnings.push(`No width given; using ${DEFAULT_IMPORT_CORRIDOR_WIDTH_M} m.`);
      shape = points.length >= 2 ? { areaType: 'corridor', path: points, corridorWidthMeters: width ?? DEFAULT_IMPORT_CORRIDOR_WIDTH_M } : null;
    } else if (points.length >= 3) {
      shape = { areaType: declared === 'rectangle' ? 'rectangle' : 'polygon', path: points };
    } else {
      const lat = num(get(idx.lat));
      const lng = num(get(idx.lng));
      const radius = num(get(idx.radius));
      if (lat !== null && lng !== null && Math.abs(lat) <= 90 && Math.abs(lng) <= 180) {
        if (!radius) warnings.push(`No radius given; using ${DEFAULT_IMPORT_RADIUS_M} m.`);
        shape = { areaType: 'circle', center: { lat, lng }, radiusMeters: radius ?? DEFAULT_IMPORT_RADIUS_M };
      }
    }
    return candidate(`Row ${i + 2}`, shape, props, warnings);
  });
}

function csvField(value: string | number | null | undefined): string {
  const s = value === null || value === undefined ? '' : String(value);
  return /[",\r\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
}

export function toCsv(items: readonly ExportableGeofence[]): string {
  const lines = [CSV_COLUMNS.join(',')];
  for (const g of items) {
    const shape = shapeFromApi(g);
    const type = normalizeAreaType(g.areaType);
    const points = shape && shape.areaType !== 'circle'
      ? (shape.path ?? []).map(p => `${r6(p.lat)},${r6(p.lng)}`).join(';')
      : '';
    lines.push([
      g.name,
      g.type ?? '',
      type,
      type === 'circle' ? r6(g.centerLat) : '',
      type === 'circle' ? r6(g.centerLng) : '',
      type === 'circle' ? Math.round(g.radiusMeters) : '',
      type === 'corridor' ? g.corridorWidthMeters ?? '' : '',
      points,
      g.address ?? '',
      g.color ?? '',
      g.groupName ?? ''
    ].map(csvField).join(','));
  }
  return `${lines.join('\r\n')}\r\n`;
}
