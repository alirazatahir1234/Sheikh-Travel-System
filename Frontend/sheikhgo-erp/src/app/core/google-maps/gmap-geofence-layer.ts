import type { Geofence } from '../models/gps-tracking.model';

export type GmapGeofenceOverlay = google.maps.Circle | google.maps.Data;

export interface GmapGeofenceLayerHandle {
  overlays: GmapGeofenceOverlay[];
  dataLayer: google.maps.Data | null;
}

export function createGmapGeofenceLayer(): GmapGeofenceLayerHandle {
  return { overlays: [], dataLayer: null };
}

/** Clear all geofence overlays from the map. */
export function clearGmapGeofences(handle: GmapGeofenceLayerHandle | null | undefined): void {
  if (!handle) return;
  for (const o of handle.overlays) {
    if (o instanceof google.maps.Circle) {
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
 * Matches colour/opacity of the Leaflet geofence-layer helper.
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
