import type * as LeafletTypes from 'leaflet';
import type { LeafletTileConfig } from './leaflet-map-tiles';

export interface TileStackState {
  fallbackIndex: number;
  errorCount: number;
  placeholderCount: number;
  switching: boolean;
}

export function createTileStackState(): TileStackState {
  return {
    fallbackIndex: 0,
    errorCount: 0,
    placeholderCount: 0,
    switching: false
  };
}

export function resetTileStackState(state: TileStackState): void {
  state.fallbackIndex = 0;
  state.errorCount = 0;
  state.placeholderCount = 0;
  state.switching = false;
}

function isEsriTileUrl(url: string): boolean {
  return url.includes('arcgisonline.com') || url.includes('arcgis.com');
}

/** Esri returns a uniform gray PNG with "Map data not yet available" instead of HTTP errors. */
export function isEsriPlaceholderTile(img: HTMLImageElement): boolean {
  try {
    if (!img.complete || img.naturalWidth < 16) return false;

    const canvas = document.createElement('canvas');
    const sample = 16;
    canvas.width = sample;
    canvas.height = sample;
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    if (!ctx) return false;

    ctx.drawImage(img, 0, 0, sample, sample);
    const { data } = ctx.getImageData(0, 0, sample, sample);
    const pixels = sample * sample;
    let sumR = 0;
    let sumG = 0;
    let sumB = 0;

    for (let i = 0; i < data.length; i += 4) {
      sumR += data[i];
      sumG += data[i + 1];
      sumB += data[i + 2];
    }

    const avgR = sumR / pixels;
    const avgG = sumG / pixels;
    const avgB = sumB / pixels;
    let variance = 0;

    for (let i = 0; i < data.length; i += 4) {
      variance +=
        (data[i] - avgR) ** 2 + (data[i + 1] - avgG) ** 2 + (data[i + 2] - avgB) ** 2;
    }
    variance /= pixels * 3;

    return avgR > 195 && avgG > 195 && avgB > 195 && variance < 800;
  } catch {
    return false;
  }
}

export function createStackTileLayer(
  L: typeof import('leaflet'),
  cfg: LeafletTileConfig
): LeafletTypes.TileLayer {
  return L.tileLayer(cfg.url, {
    maxZoom: cfg.maxZoom ?? 19,
    attribution: cfg.attribution,
    ...(cfg.subdomains ? { subdomains: cfg.subdomains } : {})
  });
}

const TILE_ERROR_THRESHOLD = 4;
const ESRI_PLACEHOLDER_THRESHOLD = 4;

export function bindTileLayerFallbackHandlers(
  tileLayer: LeafletTypes.TileLayer,
  cfg: LeafletTileConfig,
  stack: LeafletTileConfig[],
  state: TileStackState,
  onAdvance: () => void | Promise<void>,
  options?: {
    onExhausted?: (message: string) => void;
    onEsriPlaceholderFallback?: () => void;
  }
): void {
  const exhaustedMessage =
    'Map tiles could not be loaded. Check your network or try another map style.';

  const advance = (fromPlaceholder: boolean): void => {
    if (state.switching) return;

    if (state.fallbackIndex < stack.length - 1) {
      state.switching = true;
      state.fallbackIndex += 1;
      state.errorCount = 0;
      state.placeholderCount = 0;
      if (fromPlaceholder) {
        options?.onEsriPlaceholderFallback?.();
      }
      const result = onAdvance();
      if (result && typeof (result as Promise<void>).finally === 'function') {
        (result as Promise<void>).finally(() => {
          state.switching = false;
        });
      } else {
        state.switching = false;
      }
      return;
    }

    options?.onExhausted?.(exhaustedMessage);
  };

  tileLayer.on('tileerror', () => {
    if (state.switching) return;
    state.errorCount += 1;
    if (state.errorCount < TILE_ERROR_THRESHOLD) return;
    advance(false);
  });

  if (isEsriTileUrl(cfg.url)) {
    tileLayer.on('tileload', (ev) => {
      if (state.switching) return;
      const tile = ev.tile as HTMLImageElement;
      if (!isEsriPlaceholderTile(tile)) return;
      state.placeholderCount += 1;
      if (state.placeholderCount < ESRI_PLACEHOLDER_THRESHOLD) return;
      advance(true);
    });
  }
}
