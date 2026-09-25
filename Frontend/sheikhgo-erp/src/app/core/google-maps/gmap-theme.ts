/**
 * Map theme helpers for Google Maps JS (live fleet + history/replay).
 * Keeps the same MapTheme union and localStorage key as the former Leaflet tiles.
 */
export type MapTheme = 'dark' | 'street' | 'satellite' | 'traffic';

/** Default basemap. */
export const DEFAULT_MAP_THEME: MapTheme = 'street';

export const MAP_THEME_OPTIONS: { id: MapTheme; label: string; icon: string }[] = [
  { id: 'street', label: 'Map', icon: 'map' },
  { id: 'satellite', label: 'Satellite', icon: 'satellite_alt' },
  { id: 'traffic', label: 'Traffic', icon: 'traffic' },
  { id: 'dark', label: 'Dark', icon: 'dark_mode' }
];

export const MAP_THEME_STORAGE_KEY = 'stb_live_map_theme';

const MAP_THEME_SATELLITE_MIGRATION_KEY = 'stb_live_map_osm_default_v1';

/** Dark roadmap styles (Google Maps styled map). */
export const GMAP_DARK_STYLES: google.maps.MapTypeStyle[] = [
  { elementType: 'geometry', stylers: [{ color: '#1d2c4d' }] },
  { elementType: 'labels.text.fill', stylers: [{ color: '#8ec3b9' }] },
  { elementType: 'labels.text.stroke', stylers: [{ color: '#1a3646' }] },
  { featureType: 'administrative.country', elementType: 'geometry.stroke', stylers: [{ color: '#4b6878' }] },
  { featureType: 'administrative.land_parcel', elementType: 'labels.text.fill', stylers: [{ color: '#64779e' }] },
  { featureType: 'administrative.province', elementType: 'geometry.stroke', stylers: [{ color: '#4b6878' }] },
  { featureType: 'landscape.man_made', elementType: 'geometry.stroke', stylers: [{ color: '#334e87' }] },
  { featureType: 'landscape.natural', elementType: 'geometry', stylers: [{ color: '#023e58' }] },
  { featureType: 'poi', elementType: 'geometry', stylers: [{ color: '#283d6a' }] },
  { featureType: 'poi', elementType: 'labels.text.fill', stylers: [{ color: '#6f9ba5' }] },
  { featureType: 'poi', elementType: 'labels.text.stroke', stylers: [{ color: '#1d2c4d' }] },
  { featureType: 'poi.park', elementType: 'geometry.fill', stylers: [{ color: '#023e58' }] },
  { featureType: 'poi.park', elementType: 'labels.text.fill', stylers: [{ color: '#3C7680' }] },
  { featureType: 'road', elementType: 'geometry', stylers: [{ color: '#304a7d' }] },
  { featureType: 'road', elementType: 'labels.text.fill', stylers: [{ color: '#98a5be' }] },
  { featureType: 'road', elementType: 'labels.text.stroke', stylers: [{ color: '#1d2c4d' }] },
  { featureType: 'road.highway', elementType: 'geometry', stylers: [{ color: '#2c6675' }] },
  { featureType: 'road.highway', elementType: 'geometry.stroke', stylers: [{ color: '#255763' }] },
  { featureType: 'road.highway', elementType: 'labels.text.fill', stylers: [{ color: '#b0d5ce' }] },
  { featureType: 'road.highway', elementType: 'labels.text.stroke', stylers: [{ color: '#023e58' }] },
  { featureType: 'transit', elementType: 'labels.text.fill', stylers: [{ color: '#98a5be' }] },
  { featureType: 'transit', elementType: 'labels.text.stroke', stylers: [{ color: '#1d2c4d' }] },
  { featureType: 'transit.line', elementType: 'geometry.fill', stylers: [{ color: '#283d6a' }] },
  { featureType: 'transit.station', elementType: 'geometry', stylers: [{ color: '#3a4762' }] },
  { featureType: 'water', elementType: 'geometry', stylers: [{ color: '#0e1626' }] },
  { featureType: 'water', elementType: 'labels.text.fill', stylers: [{ color: '#4e6d70' }] }
];

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

export interface GmapThemeHandle {
  trafficLayer: google.maps.TrafficLayer | null;
}

/**
 * Apply theme to an existing google.maps.Map. Creates/destroys TrafficLayer as needed.
 * Returns the traffic layer handle (may be null).
 */
export function applyGmapTheme(
  map: google.maps.Map,
  theme: MapTheme,
  previous?: GmapThemeHandle | null
): GmapThemeHandle {
  previous?.trafficLayer?.setMap(null);

  if (theme === 'traffic') {
    map.setMapTypeId('roadmap');
    map.setOptions({ styles: null });
    const traffic = new google.maps.TrafficLayer();
    traffic.setMap(map);
    return { trafficLayer: traffic };
  }

  if (theme === 'satellite') {
    map.setMapTypeId('hybrid');
    map.setOptions({ styles: null });
    return { trafficLayer: null };
  }

  if (theme === 'dark') {
    map.setMapTypeId('roadmap');
    map.setOptions({ styles: GMAP_DARK_STYLES });
    return { trafficLayer: null };
  }

  // street
  map.setMapTypeId('roadmap');
  map.setOptions({ styles: null });
  return { trafficLayer: null };
}

/** Trigger a resize after layout changes (fullscreen, panel toggle). */
export function triggerGmapResize(map: google.maps.Map | null | undefined): void {
  if (!map) return;
  google.maps.event.trigger(map, 'resize');
}
