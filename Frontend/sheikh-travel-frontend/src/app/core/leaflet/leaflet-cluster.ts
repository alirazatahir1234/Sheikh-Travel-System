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

let leafletApi: LeafletWithCluster | null = null;
let loadPromise: Promise<LeafletWithCluster> | null = null;

function readGlobalLeaflet(): LeafletWithCluster | undefined {
  const scope = globalThis as typeof globalThis & { L?: LeafletWithCluster };
  return scope.L ?? (typeof window !== 'undefined' ? window.L : undefined);
}

function copyClusterExtensions(
  target: LeafletWithCluster,
  source: LeafletWithCluster
): void {
  if (typeof source.MarkerClusterGroup === 'function') {
    target.MarkerClusterGroup = source.MarkerClusterGroup;
  }
  if (typeof source.markerClusterGroup === 'function') {
    target.markerClusterGroup = source.markerClusterGroup;
  }
}

async function importLeafletWithCluster(): Promise<LeafletWithCluster> {
  const leafletModule = (await import('leaflet')) as unknown as LeafletWithCluster & {
    default?: LeafletWithCluster;
  };
  const leaflet = leafletModule.default ?? leafletModule;

  const scope = globalThis as typeof globalThis & { L?: LeafletWithCluster };
  scope.L = leaflet;

  await import('leaflet.markercluster');

  const globalLeaflet = readGlobalLeaflet();
  if (globalLeaflet && globalLeaflet !== leaflet) {
    copyClusterExtensions(leaflet, globalLeaflet);
  }

  if (
    typeof leaflet.markerClusterGroup !== 'function' &&
    typeof leaflet.MarkerClusterGroup !== 'function'
  ) {
    throw new Error(
      'leaflet.markercluster failed to register on Leaflet. Check the package install.'
    );
  }

  leafletApi = leaflet;
  return leaflet;
}

function resolveLeaflet(): LeafletWithCluster {
  if (leafletApi) {
    return leafletApi;
  }

  const globalLeaflet = readGlobalLeaflet();
  if (globalLeaflet) {
    leafletApi = globalLeaflet;
    return globalLeaflet;
  }

  throw new Error(
    'Leaflet is not loaded yet. Call loadMarkerClusterPlugin() before using the map.'
  );
}

/** Resolves to the shared Leaflet instance after loadMarkerClusterPlugin(). */
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
  if (leafletApi) {
    return Promise.resolve();
  }

  const globalLeaflet = readGlobalLeaflet();
  if (
    globalLeaflet &&
    (typeof globalLeaflet.markerClusterGroup === 'function' ||
      typeof globalLeaflet.MarkerClusterGroup === 'function')
  ) {
    leafletApi = globalLeaflet;
    return Promise.resolve();
  }

  if (!loadPromise) {
    loadPromise = importLeafletWithCluster();
  }

  return loadPromise.then(() => undefined);
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
    'Leaflet.markercluster is not loaded. Call loadMarkerClusterPlugin() first.'
  );
}
