import * as Leaflet from 'leaflet';
import 'leaflet.markercluster';
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

// The markercluster plugin may attach to window.L (global) rather than the module
// in some ESBuild configurations — check both.
function getLeafletWithCluster(): LeafletWithCluster {
  const mod = Leaflet as LeafletWithCluster;
  if (typeof mod.MarkerClusterGroup === 'function') return mod;
  const win = (typeof window !== 'undefined' ? window : {}) as { L?: LeafletWithCluster };
  if (win.L && typeof win.L.MarkerClusterGroup === 'function') return win.L;
  return mod;
}

/** Shared Leaflet instance. */
export const L = Leaflet as LeafletWithCluster;

export function loadMarkerClusterPlugin(): Promise<void> {
  return Promise.resolve();
}

export function createMarkerClusterGroup(options?: object): MarkerClusterGroupLayer {
  const l = getLeafletWithCluster();

  if (typeof l.MarkerClusterGroup === 'function') {
    return new l.MarkerClusterGroup(options);
  }

  if (typeof l.markerClusterGroup === 'function') {
    return l.markerClusterGroup(options);
  }

  // Fallback: plain LayerGroup — map initialises and shows tiles/markers without clustering.
  return Leaflet.layerGroup() as unknown as MarkerClusterGroupLayer;
}
