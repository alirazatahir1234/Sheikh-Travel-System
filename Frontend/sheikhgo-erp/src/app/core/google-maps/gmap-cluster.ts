import {
  MarkerClusterer,
  SuperClusterAlgorithm,
  type Renderer,
  type Cluster,
  type Marker
} from '@googlemaps/markerclusterer';

/** Cluster bubble matching `.fv-cluster` from live-map SCSS. */
const fleetClusterRenderer: Renderer = {
  render({ count, position }: Cluster): google.maps.marker.AdvancedMarkerElement {
    const size = count < 10 ? 36 : count < 50 ? 42 : 48;
    const el = document.createElement('div');
    el.className = 'fv-marker-host';
    el.innerHTML = `<div class="fv-cluster" style="width:${size}px;height:${size}px"><span>${count}</span></div>`;
    return new google.maps.marker.AdvancedMarkerElement({
      position,
      content: el,
      zIndex: Number(google.maps.Marker.MAX_ZINDEX) + count,
      gmpClickable: true
    });
  }
};

export type FleetMarkerClusterer = MarkerClusterer;

/**
 * MarkerClusterer tuned to former Leaflet settings:
 * disableClusteringAtZoom: 14 → algorithm maxZoom 13.
 */
export function createFleetMarkerClusterer(
  map: google.maps.Map,
  markers: Marker[] = []
): MarkerClusterer {
  return new MarkerClusterer({
    map,
    markers,
    renderer: fleetClusterRenderer,
    algorithm: new SuperClusterAlgorithm({
      radius: 55,
      maxZoom: 13
    })
  });
}
