import type * as LeafletTypes from 'leaflet';
import { L } from './leaflet-cluster';
import type { Geofence } from '../models/gps-tracking.model';

/** Render a geofence boundary into a Leaflet layer group. Returns the added layer. */
export function addGeofenceBoundary(
  layerGroup: LeafletTypes.LayerGroup,
  fence: Geofence,
  options?: { interactive?: boolean; weight?: number; fillOpacity?: number }
): LeafletTypes.Layer | null {
  const color = fence.color || '#0f766e';
  const weight = options?.weight ?? 2;
  const fillOpacity = options?.fillOpacity ?? 0.12;
  const interactive = options?.interactive ?? true;
  const type = (fence.areaType || 'circle').toLowerCase();

  try {
    if ((type === 'polygon' || type === 'rectangle') && fence.geoJson) {
      const geo = JSON.parse(fence.geoJson) as object;
      const layer = L.geoJSON(geo as GeoJSON.GeoJsonObject, {
        style: () => ({ color, weight, fillColor: color, fillOpacity }),
        interactive
      });
      layerGroup.addLayer(layer);
      return layer;
    }

    if (fence.centerLat != null && fence.centerLng != null && fence.radiusMeters > 0) {
      const circle = L.circle([fence.centerLat, fence.centerLng], {
        radius: fence.radiusMeters,
        color,
        fillColor: color,
        fillOpacity,
        weight,
        interactive
      });
      layerGroup.addLayer(circle);
      return circle;
    }
  } catch {
    /* invalid geojson — skip */
  }

  return null;
}

export function clearLayerGroup(layerGroup: LeafletTypes.LayerGroup | null | undefined): void {
  layerGroup?.clearLayers();
}

export function fitGeofencesBounds(
  map: LeafletTypes.Map,
  layerGroup: LeafletTypes.LayerGroup
): void {
  const layers = layerGroup.getLayers();
  if (!layers.length) return;
  const group = L.featureGroup(layers as LeafletTypes.Layer[]);
  try {
    map.fitBounds(group.getBounds().pad(0.15), { maxZoom: 15 });
  } catch {
    /* empty or invalid bounds */
  }
}

/** Convert a drawn Geoman layer into payload fields for create/update. */
export function extractGeofenceGeometry(layer: LeafletTypes.Layer): {
  areaType: string;
  centerLat: number;
  centerLng: number;
  radiusMeters: number;
  geoJson: string | null;
} | null {
  const anyLayer = layer as LeafletTypes.Circle & LeafletTypes.Polygon & {
    pm?: { getShape?: () => string };
  };

  // Circle
  if (typeof anyLayer.getRadius === 'function' && typeof anyLayer.getLatLng === 'function') {
    const ll = anyLayer.getLatLng();
    return {
      areaType: 'circle',
      centerLat: +ll.lat.toFixed(6),
      centerLng: +ll.lng.toFixed(6),
      radiusMeters: Math.round(anyLayer.getRadius()),
      geoJson: null
    };
  }

  // Polygon / rectangle
  if (typeof anyLayer.toGeoJSON === 'function') {
    const geo = anyLayer.toGeoJSON() as GeoJSON.Feature;
    const shape = anyLayer.pm?.getShape?.()?.toLowerCase() ?? '';
    const areaType = shape === 'rectangle' || shape.includes('rect') ? 'rectangle' : 'polygon';
    const ring = (geo.geometry as GeoJSON.Polygon)?.coordinates?.[0];
    let centerLat = 0;
    let centerLng = 0;
    if (ring?.length) {
      let sumLat = 0;
      let sumLng = 0;
      const n = ring.length - (ring.length > 1 ? 1 : 0);
      for (let i = 0; i < n; i++) {
        sumLng += ring[i][0];
        sumLat += ring[i][1];
      }
      centerLat = +(sumLat / Math.max(n, 1)).toFixed(6);
      centerLng = +(sumLng / Math.max(n, 1)).toFixed(6);
    }
    return {
      areaType,
      centerLat,
      centerLng,
      radiusMeters: 0,
      geoJson: JSON.stringify(geo)
    };
  }

  return null;
}
