import { environment } from '../../../environments/environment';

export type RouteEndpoint = google.maps.LatLngLiteral | string;

export interface ComputeDrivingRouteParams {
  origin: RouteEndpoint;
  destination: RouteEndpoint;
  waypoints?: RouteEndpoint[];
  travelMode?: 'DRIVING' | google.maps.TravelMode;
  avoidTolls?: boolean;
  computeAlternativeRoutes?: boolean;
  optimizeWaypointOrder?: boolean;
  routingPreference?: 'TRAFFIC_AWARE' | 'TRAFFIC_AWARE_OPTIMAL' | 'TRAFFIC_UNAWARE';
}

export interface ComputedDrivingRoute {
  distanceMeters: number;
  durationSeconds: number;
  durationInTrafficSeconds?: number;
  path: google.maps.LatLngLiteral[];
  raw?: unknown;
}

interface RoutesLibrary {
  Route?: {
    computeRoutes: (request: JsComputeRoutesRequest) => Promise<JsComputeRoutesResponse>;
  };
}

interface JsComputeRoutesRequest {
  origin: RouteEndpoint;
  destination: RouteEndpoint;
  intermediates?: RouteEndpoint[];
  travelMode?: string;
  fields: string[];
  computeAlternativeRoutes?: boolean;
  optimizeWaypointOrder?: boolean;
  routingPreference?: string;
  routeModifiers?: { avoidTolls?: boolean };
}

interface JsComputeRoutesResponse {
  routes?: JsRoute[];
}

interface JsRoute {
  distanceMeters?: number;
  durationMillis?: number;
  durationInTrafficMillis?: number;
  path?: google.maps.LatLngLiteral[];
}

interface RestComputeRoutesResponse {
  routes?: Array<{
    distanceMeters?: number;
    duration?: string;
    staticDuration?: string;
    polyline?: { encodedPolyline?: string };
  }>;
}

const ROUTES_FIELD_MASK =
  'routes.distanceMeters,routes.duration,routes.staticDuration,routes.polyline.encodedPolyline';

/** Compute driving route(s) via Routes API (JS library preferred, REST fallback). */
export async function computeDrivingRoute(
  params: ComputeDrivingRouteParams,
  mapsLoader?: { importLibrary: (name: string) => Promise<unknown> }
): Promise<ComputedDrivingRoute[]> {
  if (mapsLoader) {
    try {
      const routesLib = (await mapsLoader.importLibrary('routes')) as RoutesLibrary;
      if (routesLib.Route?.computeRoutes) {
        return computeViaJsLibrary(routesLib.Route, params);
      }
    } catch (err) {
      console.warn('Routes JS library unavailable; falling back to Routes REST.', err);
    }
  }

  return [await computeViaRest(params)];
}

export function fitMapBoundsToPath(
  map: google.maps.Map,
  path: google.maps.LatLngLiteral[],
  padding: number | google.maps.Padding = 56
): void {
  if (!path.length) return;
  const bounds = new google.maps.LatLngBounds();
  path.forEach(point => bounds.extend(point));
  map.fitBounds(bounds, padding);
}

export function pathCenter(path: google.maps.LatLngLiteral[]): google.maps.LatLngLiteral | null {
  if (!path.length) return null;
  let lat = 0;
  let lng = 0;
  path.forEach(p => {
    lat += p.lat;
    lng += p.lng;
  });
  return { lat: lat / path.length, lng: lng / path.length };
}

async function computeViaJsLibrary(
  Route: NonNullable<RoutesLibrary['Route']>,
  params: ComputeDrivingRouteParams
): Promise<ComputedDrivingRoute[]> {
  const request: JsComputeRoutesRequest = {
    origin: params.origin,
    destination: params.destination,
    intermediates: params.waypoints?.length ? params.waypoints : undefined,
    travelMode: normalizeTravelMode(params.travelMode),
    fields: ['path', 'distanceMeters', 'durationMillis', 'durationInTrafficMillis'],
    computeAlternativeRoutes: params.computeAlternativeRoutes,
    optimizeWaypointOrder: params.optimizeWaypointOrder,
    routingPreference: params.routingPreference ?? 'TRAFFIC_AWARE',
    routeModifiers: params.avoidTolls ? { avoidTolls: true } : undefined
  };

  const { routes } = await Route.computeRoutes(request);
  const mapped = (routes ?? [])
    .filter(route => route.path?.length)
    .map(route => ({
      distanceMeters: route.distanceMeters ?? 0,
      durationSeconds: millisToSeconds(route.durationMillis),
      durationInTrafficSeconds: route.durationInTrafficMillis != null
        ? millisToSeconds(route.durationInTrafficMillis)
        : undefined,
      path: route.path!,
      raw: route
    }));

  if (!mapped.length) {
    throw new Error('No route returned from Routes API.');
  }
  return mapped;
}

async function computeViaRest(params: ComputeDrivingRouteParams): Promise<ComputedDrivingRoute> {
  const key = environment.googleMapsApiKey?.trim();
  if (!key) {
    throw new Error('Google Maps API key is not configured.');
  }

  const body = {
    origin: toRestWaypoint(params.origin),
    destination: toRestWaypoint(params.destination),
    intermediates: params.waypoints?.map(toRestWaypoint),
    travelMode: 'DRIVE',
    routingPreference: params.routingPreference ?? 'TRAFFIC_AWARE',
    computeAlternativeRoutes: !!params.computeAlternativeRoutes,
    optimizeWaypointOrder: !!params.optimizeWaypointOrder,
    routeModifiers: params.avoidTolls ? { avoidTolls: true } : undefined,
    polylineEncoding: 'ENCODED_POLYLINE',
    polylineQuality: 'OVERVIEW'
  };

  const response = await fetch('https://routes.googleapis.com/directions/v2:computeRoutes', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Goog-Api-Key': key,
      'X-Goog-FieldMask': ROUTES_FIELD_MASK
    },
    body: JSON.stringify(body)
  });

  if (!response.ok) {
    const detail = await response.text().catch(() => '');
    throw new Error(`Routes API failed (${response.status})${detail ? `: ${detail}` : ''}`);
  }

  const payload = (await response.json()) as RestComputeRoutesResponse;
  const route = payload.routes?.[0];
  const encoded = route?.polyline?.encodedPolyline;
  if (!encoded) {
    throw new Error('Routes API returned no polyline.');
  }

  const durationSeconds = parseDurationSeconds(route.duration ?? route.staticDuration);
  return {
    distanceMeters: route.distanceMeters ?? 0,
    durationSeconds,
    path: decodeEncodedPolyline(encoded),
    raw: route
  };
}

function normalizeTravelMode(mode?: 'DRIVING' | google.maps.TravelMode): string {
  if (mode == null) return 'DRIVING';
  if (typeof mode === 'string') return mode.toUpperCase();
  return google.maps.TravelMode[mode] ?? 'DRIVING';
}

function toRestWaypoint(point: RouteEndpoint): object {
  if (typeof point === 'string') {
    return { address: point };
  }
  return {
    location: {
      latLng: {
        latitude: point.lat,
        longitude: point.lng
      }
    }
  };
}

function millisToSeconds(value: number | undefined): number {
  if (value == null || !Number.isFinite(value)) return 0;
  return Math.max(0, Math.round(value / 1000));
}

function parseDurationSeconds(value: string | undefined): number {
  if (!value) return 0;
  const match = /^(\d+(?:\.\d+)?)s$/.exec(value.trim());
  if (!match) return 0;
  return Math.max(0, Math.round(Number(match[1])));
}

/** Decode Google's encoded polyline (precision 5). */
export function decodeEncodedPolyline(encoded: string): google.maps.LatLngLiteral[] {
  const path: google.maps.LatLngLiteral[] = [];
  let index = 0;
  let lat = 0;
  let lng = 0;

  while (index < encoded.length) {
    let shift = 0;
    let result = 0;
    let byte: number;
    do {
      byte = encoded.charCodeAt(index++) - 63;
      result |= (byte & 0x1f) << shift;
      shift += 5;
    } while (byte >= 0x20);
    const deltaLat = result & 1 ? ~(result >> 1) : result >> 1;
    lat += deltaLat;

    shift = 0;
    result = 0;
    do {
      byte = encoded.charCodeAt(index++) - 63;
      result |= (byte & 0x1f) << shift;
      shift += 5;
    } while (byte >= 0x20);
    const deltaLng = result & 1 ? ~(result >> 1) : result >> 1;
    lng += deltaLng;

    path.push({ lat: lat / 1e5, lng: lng / 1e5 });
  }

  return path;
}
