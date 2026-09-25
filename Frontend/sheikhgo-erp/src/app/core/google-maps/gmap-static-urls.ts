import { environment } from '../../../environments/environment';

/**
 * Street View Static + Maps Static URL builders (browser key, HTTP-referrer restricted).
 * Do not poll these for moving vehicles — call only for selected pickup/destination/snapshot.
 */
export function buildStreetViewStaticUrl(opts: {
  lat: number;
  lng: number;
  width?: number;
  height?: number;
  heading?: number;
  pitch?: number;
  fov?: number;
  /** When true, Google returns HTTP 404 instead of a placeholder image (so UI can hide). */
  returnErrorCode?: boolean;
}): string | null {
  const key = environment.googleMapsApiKey?.trim();
  if (!key || !Number.isFinite(opts.lat) || !Number.isFinite(opts.lng)) return null;
  const w = opts.width ?? 400;
  const h = opts.height ?? 200;
  const params = new URLSearchParams({
    size: `${w}x${h}`,
    location: `${opts.lat},${opts.lng}`,
    key
  });
  if (opts.heading != null) params.set('heading', String(opts.heading));
  if (opts.pitch != null) params.set('pitch', String(opts.pitch));
  if (opts.fov != null) params.set('fov', String(opts.fov));
  if (opts.returnErrorCode !== false) params.set('return_error_code', 'true');
  return `https://maps.googleapis.com/maps/api/streetview?${params.toString()}`;
}

export function buildStaticMapUrl(opts: {
  width?: number;
  height?: number;
  zoom?: number;
  markers?: { lat: number; lng: number; label?: string; color?: string }[];
  path?: { lat: number; lng: number }[];
  pathColor?: string;
  maptype?: 'roadmap' | 'satellite' | 'hybrid' | 'terrain';
}): string | null {
  const key = environment.googleMapsApiKey?.trim();
  if (!key) return null;
  const w = opts.width ?? 640;
  const h = opts.height ?? 360;
  const params = new URLSearchParams({
    size: `${w}x${h}`,
    key,
    maptype: opts.maptype ?? 'roadmap'
  });
  if (opts.zoom != null) params.set('zoom', String(opts.zoom));
  for (const m of opts.markers ?? []) {
    const color = m.color ?? '0x0f766e';
    const label = m.label ? `label:${m.label}|` : '';
    params.append('markers', `color:${color}|${label}${m.lat},${m.lng}`);
  }
  if (opts.path && opts.path.length >= 2) {
    const color = opts.pathColor ?? '0x2563EB';
    const pts = opts.path.map(p => `${p.lat},${p.lng}`).join('|');
    params.append('path', `color:${color}|weight:3|${pts}`);
  }
  return `https://maps.googleapis.com/maps/api/staticmap?${params.toString()}`;
}
