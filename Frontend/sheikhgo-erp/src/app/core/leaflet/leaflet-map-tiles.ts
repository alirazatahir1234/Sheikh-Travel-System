export type MapTheme = 'dark' | 'street' | 'satellite' | 'traffic';

/** Default basemap — OpenStreetMap street tiles (no API key). */
export const DEFAULT_MAP_THEME: MapTheme = 'street';

/** @deprecated Use `street` — kept as alias for older call sites. */
export type LegacyMapTheme = MapTheme | 'light';

export interface LeafletTileConfig {
  url: string;
  attribution: string;
  subdomains?: string;
  maxZoom?: number;
}

export const MAP_THEME_OPTIONS: { id: MapTheme; label: string; icon: string }[] = [
  { id: 'street', label: 'Street', icon: 'map' },
  { id: 'satellite', label: 'Satellite', icon: 'satellite_alt' },
  { id: 'dark', label: 'Dark', icon: 'dark_mode' },
  { id: 'traffic', label: 'Traffic', icon: 'traffic' }
];

export const MAP_THEME_STORAGE_KEY = 'stb_live_map_theme';

/** One-time migration: Esri satellite placeholders → default Street (OSM). */
const MAP_THEME_SATELLITE_MIGRATION_KEY = 'stb_live_map_osm_default_v1';

/** Ordered fallbacks — first provider is preferred; later entries used if tiles fail. */
export const MAP_TILE_STACKS: Record<Exclude<MapTheme, 'traffic'>, LeafletTileConfig[]> = {
  street: [
    {
      url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
      attribution: '&copy; OpenStreetMap contributors',
      subdomains: 'abc',
      maxZoom: 19
    },
    {
      url: 'https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png',
      attribution: '&copy; OpenStreetMap &copy; CARTO',
      subdomains: 'abcd',
      maxZoom: 20
    },
    {
      url: 'https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png',
      attribution: '&copy; OpenStreetMap &copy; CARTO',
      subdomains: 'abcd',
      maxZoom: 20
    }
  ],
  dark: [
    {
      url: 'https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png',
      attribution: '&copy; OpenStreetMap &copy; CARTO',
      subdomains: 'abcd',
      maxZoom: 20
    },
    {
      url: 'https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png',
      attribution: '&copy; OpenStreetMap &copy; CARTO',
      subdomains: 'abcd',
      maxZoom: 20
    }
  ],
  satellite: [
    {
      url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
      attribution: '&copy; Esri',
      maxZoom: 19
    },
    {
      url: 'https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
      attribution: '&copy; Esri',
      maxZoom: 19
    },
    {
      url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
      attribution: '&copy; OpenStreetMap contributors',
      subdomains: 'abc',
      maxZoom: 19
    },
    {
      url: 'https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png',
      attribution: '&copy; OpenStreetMap &copy; CARTO',
      subdomains: 'abcd',
      maxZoom: 20
    }
  ]
};

export function normalizeMapTheme(raw: string | null | undefined): MapTheme {
  if (raw === 'light') return DEFAULT_MAP_THEME;
  if (raw === 'street' || raw === 'dark' || raw === 'satellite' || raw === 'traffic') return raw;
  return DEFAULT_MAP_THEME;
}

export function readStoredMapTheme(): MapTheme {
  try {
    if (!localStorage.getItem(MAP_THEME_SATELLITE_MIGRATION_KEY)) {
      localStorage.setItem(MAP_THEME_SATELLITE_MIGRATION_KEY, '1');
      const stored = localStorage.getItem(MAP_THEME_STORAGE_KEY);
      if (!stored || stored === 'satellite') {
        localStorage.setItem(MAP_THEME_STORAGE_KEY, DEFAULT_MAP_THEME);
        return DEFAULT_MAP_THEME;
      }
    }
    return normalizeMapTheme(localStorage.getItem(MAP_THEME_STORAGE_KEY));
  } catch {
    return DEFAULT_MAP_THEME;
  }
}

export function storeMapTheme(theme: MapTheme): void {
  try {
    localStorage.setItem(MAP_THEME_STORAGE_KEY, theme);
  } catch {
    /* ignore quota / private mode */
  }
}
