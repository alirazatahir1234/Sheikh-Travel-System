import * as Leaflet from 'leaflet';
import type * as LeafletType from 'leaflet';

/** Shared Leaflet instance for geofence drawing (Geoman). Clustering lives on Google Maps now. */
export const L = Leaflet as typeof LeafletType;
