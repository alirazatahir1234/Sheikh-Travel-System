import type * as Leaflet from 'leaflet';

declare global {
  // Optional: set when leaflet.markercluster registers on globalThis
  // eslint-disable-next-line @typescript-eslint/no-namespace
  var L: typeof Leaflet & {
    MarkerClusterGroup: new (options?: object) => Leaflet.LayerGroup;
    markerClusterGroup: (options?: object) => Leaflet.LayerGroup;
  };
}

export {};
