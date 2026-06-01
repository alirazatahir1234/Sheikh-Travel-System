import type * as LeafletType from 'leaflet';

export type MarkerClusterGroupLayer = LeafletType.LayerGroup & {
  addLayer(layer: LeafletType.Layer): MarkerClusterGroupLayer;
  removeLayer(layer: LeafletType.Layer): MarkerClusterGroupLayer;
  clearLayers(): MarkerClusterGroupLayer;
};

type LeafletWithCluster = typeof LeafletType & {
  MarkerClusterGroup: new (options?: object) => MarkerClusterGroupLayer;
  markerClusterGroup: (options?: object) => MarkerClusterGroupLayer;
};

function resolveLeaflet(): LeafletWithCluster {
  const leaflet = (globalThis as { L?: LeafletWithCluster }).L;
  if (!leaflet) {
    throw new Error(
      'Leaflet is not on globalThis.L. Add leaflet scripts to angular.json.'
    );
  }
  return leaflet;
}

/** Always uses globalThis.L (loaded via angular.json scripts). */
export const L = new Proxy({} as LeafletWithCluster, {
  get(_target, prop) {
    const leaflet = resolveLeaflet();
    const value = Reflect.get(leaflet, prop, leaflet) as unknown;
    return typeof value === 'function'
      ? (value as (...args: unknown[]) => unknown).bind(leaflet)
      : value;
  }
});

export function loadMarkerClusterPlugin(): Promise<void> {
  const leaflet = resolveLeaflet();
  if (
    typeof leaflet.markerClusterGroup !== 'function' &&
    typeof leaflet.MarkerClusterGroup !== 'function'
  ) {
    return Promise.reject(
      new Error(
        'leaflet.markercluster is not loaded. Add its script to angular.json after leaflet.js.'
      )
    );
  }
  return Promise.resolve();
}

export function createMarkerClusterGroup(options?: object): MarkerClusterGroupLayer {
  const leaflet = resolveLeaflet();

  if (typeof leaflet.MarkerClusterGroup === 'function') {
    return new leaflet.MarkerClusterGroup(options);
  }

  if (typeof leaflet.markerClusterGroup === 'function') {
    return leaflet.markerClusterGroup(options);
  }

  throw new Error(
    'Leaflet.markercluster is not loaded. Call loadMarkerClusterPlugin() before creating a cluster group.'
  );
}
