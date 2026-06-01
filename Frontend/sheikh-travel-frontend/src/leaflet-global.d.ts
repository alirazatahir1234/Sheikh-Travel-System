import type * as Leaflet from 'leaflet';

declare global {
  // Set by leaflet.js / leaflet.markercluster scripts in angular.json
  // eslint-disable-next-line @typescript-eslint/no-namespace
  var L: typeof Leaflet & {
    MarkerClusterGroup: new (options?: object) => Leaflet.LayerGroup;
    markerClusterGroup: (options?: object) => Leaflet.LayerGroup;
  };
}

export {};
