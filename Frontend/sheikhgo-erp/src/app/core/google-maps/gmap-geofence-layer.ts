import type { Geofence } from '../models/gps-tracking.model';

export type GmapGeofenceOverlay = google.maps.Circle | google.maps.Data | google.maps.Polygon | google.maps.Rectangle;

export type GmapEditableShape = google.maps.Circle | google.maps.Polygon | google.maps.Rectangle;

export interface GmapGeofenceLayerHandle {
  overlays: GmapGeofenceOverlay[];
  dataLayer: google.maps.Data | null;
}

export interface GmapGeofenceGeometry {
  areaType: string;
  centerLat: number;
  centerLng: number;
  radiusMeters: number;
  geoJson: string | null;
}

export function createGmapGeofenceLayer(): GmapGeofenceLayerHandle {
  return { overlays: [], dataLayer: null };
}

/** Clear all geofence overlays from the map. */
export function clearGmapGeofences(handle: GmapGeofenceLayerHandle | null | undefined): void {
  if (!handle) return;
  for (const o of handle.overlays) {
    if (o instanceof google.maps.Circle || o instanceof google.maps.Polygon || o instanceof google.maps.Rectangle) {
      o.setMap(null);
    }
  }
  handle.overlays = [];
  if (handle.dataLayer) {
    handle.dataLayer.setMap(null);
    handle.dataLayer = null;
  }
}

/**
 * Render geofence boundaries onto a Google Map (circle + GeoJSON polygon/rectangle).
 * Used by Live Map (view-only). Editor page prefers {@link createGmapEditableFenceOverlay}.
 */
export function addGmapGeofenceBoundary(
  map: google.maps.Map,
  handle: GmapGeofenceLayerHandle,
  fence: Geofence,
  options?: { weight?: number; fillOpacity?: number }
): GmapGeofenceOverlay | null {
  const color = fence.color || '#0f766e';
  const weight = options?.weight ?? 2;
  const fillOpacity = options?.fillOpacity ?? 0.12;
  const type = (fence.areaType || 'circle').toLowerCase();

  try {
    if ((type === 'polygon' || type === 'rectangle') && fence.geoJson) {
      if (!handle.dataLayer) {
        handle.dataLayer = new google.maps.Data({ map });
        handle.dataLayer.setStyle({
          strokeColor: color,
          strokeWeight: weight,
          fillColor: color,
          fillOpacity
        });
      }
      const geo = JSON.parse(fence.geoJson) as object;
      handle.dataLayer.addGeoJson(geo);
      // Per-feature style (data layer is shared; set style fn once covering all)
      handle.dataLayer.setStyle(feature => {
        const c = (feature.getProperty('color') as string) || color;
        return {
          strokeColor: c,
          strokeWeight: weight,
          fillColor: c,
          fillOpacity
        };
      });
      handle.overlays.push(handle.dataLayer);
      return handle.dataLayer;
    }

    if (fence.centerLat != null && fence.centerLng != null && fence.radiusMeters > 0) {
      const circle = new google.maps.Circle({
        map,
        center: { lat: fence.centerLat, lng: fence.centerLng },
        radius: fence.radiusMeters,
        strokeColor: color,
        strokeWeight: weight,
        fillColor: color,
        fillOpacity,
        clickable: true
      });
      handle.overlays.push(circle);
      return circle;
    }
  } catch {
    /* invalid geojson — skip */
  }

  return null;
}

/** Style options shared by draft + saved fence shapes. */
export function gmapFenceStyle(color: string, fillOpacity = 0.18): google.maps.CircleOptions {
  return {
    strokeColor: color,
    strokeOpacity: 1,
    strokeWeight: 2,
    fillColor: color,
    fillOpacity,
    clickable: true
  };
}

/** Parse GeoJSON Feature / FeatureCollection / Polygon into a path (lat/lng). */
export function pathFromGeofenceGeoJson(geoJson: string): google.maps.LatLngLiteral[] | null {
  try {
    const root = JSON.parse(geoJson) as {
      type?: string;
      geometry?: { type?: string; coordinates?: number[][][] };
      features?: Array<{ geometry?: { type?: string; coordinates?: number[][][] } }>;
      coordinates?: number[][][];
    };
    let geometry = root as { type?: string; coordinates?: number[][][] };
    if (root.type === 'Feature' && root.geometry) {
      geometry = root.geometry;
    } else if (root.type === 'FeatureCollection' && root.features?.[0]?.geometry) {
      geometry = root.features[0].geometry;
    }
    if (geometry.type !== 'Polygon' || !geometry.coordinates?.[0]?.length) return null;
    const ring = geometry.coordinates[0];
    const path: google.maps.LatLngLiteral[] = [];
    for (let i = 0; i < ring.length; i++) {
      const [lng, lat] = ring[i];
      if (!Number.isFinite(lat) || !Number.isFinite(lng)) continue;
      // Skip duplicate closing vertex for Google paths
      if (i === ring.length - 1 && path.length && path[0].lat === lat && path[0].lng === lng) break;
      path.push({ lat, lng });
    }
    return path.length >= 3 ? path : null;
  } catch {
    return null;
  }
}

/** Build API GeoJSON Feature (Polygon) from a Google path. Coordinates are [lng, lat]. */
export function geoJsonFeatureFromPath(path: google.maps.LatLngLiteral[]): string {
  const ring = path.map(p => [p.lng, p.lat]);
  if (ring.length) {
    const first = ring[0];
    const last = ring[ring.length - 1];
    if (first[0] !== last[0] || first[1] !== last[1]) {
      ring.push([first[0], first[1]]);
    }
  }
  return JSON.stringify({
    type: 'Feature',
    properties: {},
    geometry: {
      type: 'Polygon',
      coordinates: [ring]
    }
  });
}

export function geoJsonFeatureFromRectangle(bounds: google.maps.LatLngBounds): string {
  const ne = bounds.getNorthEast();
  const sw = bounds.getSouthWest();
  const path: google.maps.LatLngLiteral[] = [
    { lat: ne.lat(), lng: sw.lng() },
    { lat: ne.lat(), lng: ne.lng() },
    { lat: sw.lat(), lng: ne.lng() },
    { lat: sw.lat(), lng: sw.lng() }
  ];
  return geoJsonFeatureFromPath(path);
}

/** Convert a drawn/edited Google shape into the geofence API payload fields. */
export function extractGmapGeofenceGeometry(shape: GmapEditableShape): GmapGeofenceGeometry | null {
  if (shape instanceof google.maps.Circle) {
    const c = shape.getCenter();
    if (!c) return null;
    return {
      areaType: 'circle',
      centerLat: +c.lat().toFixed(6),
      centerLng: +c.lng().toFixed(6),
      radiusMeters: Math.round(shape.getRadius()),
      geoJson: null
    };
  }

  if (shape instanceof google.maps.Rectangle) {
    const bounds = shape.getBounds();
    if (!bounds) return null;
    const ne = bounds.getNorthEast();
    const sw = bounds.getSouthWest();
    const centerLat = +((ne.lat() + sw.lat()) / 2).toFixed(6);
    const centerLng = +((ne.lng() + sw.lng()) / 2).toFixed(6);
    return {
      areaType: 'rectangle',
      centerLat,
      centerLng,
      radiusMeters: 0,
      geoJson: geoJsonFeatureFromRectangle(bounds)
    };
  }

  if (shape instanceof google.maps.Polygon) {
    const path = shape.getPath().getArray().map(ll => ({ lat: ll.lat(), lng: ll.lng() }));
    if (path.length < 3) return null;
    let sumLat = 0;
    let sumLng = 0;
    for (const p of path) {
      sumLat += p.lat;
      sumLng += p.lng;
    }
    return {
      areaType: 'polygon',
      centerLat: +(sumLat / path.length).toFixed(6),
      centerLng: +(sumLng / path.length).toFixed(6),
      radiusMeters: 0,
      geoJson: geoJsonFeatureFromPath(path)
    };
  }

  return null;
}

/**
 * Create a map overlay for a saved fence (circle / polygon / rectangle-as-polygon).
 * Prefer this on the geofences editor so each fence is clickable and independently styled.
 */
export function createGmapFenceOverlay(
  map: google.maps.Map,
  fence: Geofence,
  options?: { editable?: boolean; draggable?: boolean; fillOpacity?: number; weight?: number }
): GmapEditableShape | null {
  const color = fence.color || '#0f766e';
  const style = gmapFenceStyle(color, options?.fillOpacity ?? 0.12);
  const editable = !!options?.editable;
  const draggable = options?.draggable ?? editable;
  const type = (fence.areaType || 'circle').toLowerCase();

  try {
    if (type === 'circle' && fence.centerLat != null && fence.centerLng != null && fence.radiusMeters > 0) {
      return new google.maps.Circle({
        map,
        center: { lat: fence.centerLat, lng: fence.centerLng },
        radius: fence.radiusMeters,
        ...style,
        strokeWeight: options?.weight ?? 2,
        editable,
        draggable
      });
    }

    if ((type === 'polygon' || type === 'rectangle') && fence.geoJson) {
      if (type === 'rectangle') {
        const path = pathFromGeofenceGeoJson(fence.geoJson);
        if (path?.length) {
          const bounds = new google.maps.LatLngBounds();
          path.forEach(p => bounds.extend(p));
          return new google.maps.Rectangle({
            map,
            bounds,
            ...style,
            strokeWeight: options?.weight ?? 2,
            editable,
            draggable
          });
        }
      }
      const path = pathFromGeofenceGeoJson(fence.geoJson);
      if (path?.length) {
        return new google.maps.Polygon({
          map,
          paths: path,
          ...style,
          strokeWeight: options?.weight ?? 2,
          editable,
          draggable
        });
      }
    }

    // Fallback: circle from center if geoJson missing
    if (fence.centerLat != null && fence.centerLng != null && fence.radiusMeters > 0) {
      return new google.maps.Circle({
        map,
        center: { lat: fence.centerLat, lng: fence.centerLng },
        radius: fence.radiusMeters,
        ...style,
        editable,
        draggable
      });
    }
  } catch {
    return null;
  }

  return null;
}

export function applyGmapShapeStyle(shape: GmapEditableShape, color: string, fillOpacity = 0.18): void {
  shape.setOptions(gmapFenceStyle(color, fillOpacity));
}

export function setGmapShapeLocked(shape: GmapEditableShape | null, locked: boolean): void {
  if (!shape) return;
  shape.setOptions({ editable: !locked, draggable: !locked });
}

export function fitGmapOverlays(
  map: google.maps.Map,
  overlays: Array<GmapEditableShape | null | undefined>,
  padding = 48
): void {
  const bounds = new google.maps.LatLngBounds();
  let has = false;
  for (const o of overlays) {
    if (!o) continue;
    if (o instanceof google.maps.Circle) {
      const b = o.getBounds();
      if (b) {
        bounds.union(b);
        has = true;
      }
    } else if (o instanceof google.maps.Rectangle) {
      const b = o.getBounds();
      if (b) {
        bounds.union(b);
        has = true;
      }
    } else if (o instanceof google.maps.Polygon) {
      o.getPath().forEach(ll => {
        bounds.extend(ll);
        has = true;
      });
    }
  }
  if (has) {
    map.fitBounds(bounds, padding);
  }
}
