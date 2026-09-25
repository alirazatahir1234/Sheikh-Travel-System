import type * as Leaflet from 'leaflet';

declare global {
  // Geoman / Leaflet drawing attaches to window.L
  // eslint-disable-next-line no-var, @typescript-eslint/no-explicit-any
  var L: typeof Leaflet & Record<string, any>;
}

export {};
