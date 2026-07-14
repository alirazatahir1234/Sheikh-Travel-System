import type * as LeafletTypes from 'leaflet';
import { GoogleMapsLoaderService } from '../services/google-maps-loader.service';

/**
 * Embeds a Google Maps ROADMAP + TrafficLayer behind Leaflet panes so markers
 * stay on Leaflet while traffic rendering uses the configured Maps API key.
 */
export class GoogleTrafficBasemap {
  private container: HTMLDivElement | null = null;
  private gmap: google.maps.Map | null = null;
  private traffic: google.maps.TrafficLayer | null = null;
  private map: LeafletTypes.Map | null = null;
  private readonly sync = (): void => this.syncView();
  private readonly onResize = (): void => {
    if (this.gmap && this.map) {
      google.maps.event.trigger(this.gmap, 'resize');
      this.syncView();
    }
  };

  async attach(map: LeafletTypes.Map, loader: GoogleMapsLoaderService): Promise<boolean> {
    this.detach();
    if (!loader.isConfigured) return false;

    const bootstrapped = await loader.load();
    if (!bootstrapped) return false;

    try {
      await loader.importLibrary('maps');
    } catch {
      return false;
    }

    const leafletEl = map.getContainer();
    const container = document.createElement('div');
    container.className = 'google-traffic-basemap';
    container.style.cssText =
      'position:absolute;inset:0;z-index:0;pointer-events:none;overflow:hidden;';
    leafletEl.style.background = 'transparent';
    leafletEl.insertBefore(container, leafletEl.firstChild);

    const gmap = new google.maps.Map(container, {
      mapTypeId: google.maps.MapTypeId.ROADMAP,
      disableDefaultUI: true,
      keyboardShortcuts: false,
      draggable: false,
      scrollwheel: false,
      disableDoubleClickZoom: true,
      gestureHandling: 'none',
      clickableIcons: false,
      heading: 0,
      tilt: 0
    });

    const traffic = new google.maps.TrafficLayer();
    traffic.setMap(gmap);

    this.container = container;
    this.gmap = gmap;
    this.traffic = traffic;
    this.map = map;

    map.on('move', this.sync);
    map.on('zoom', this.sync);
    map.on('resize', this.onResize);
    this.syncView();
    requestAnimationFrame(() => this.onResize());
    return true;
  }

  detach(): void {
    if (this.map) {
      this.map.off('move', this.sync);
      this.map.off('zoom', this.sync);
      this.map.off('resize', this.onResize);
      this.map.getContainer().style.background = '';
    }
    this.traffic?.setMap(null);
    this.traffic = null;
    this.gmap = null;
    this.container?.remove();
    this.container = null;
    this.map = null;
  }

  private syncView(): void {
    if (!this.map || !this.gmap) return;
    const c = this.map.getCenter();
    if (!c) return;
    const zoom = this.map.getZoom();
    this.gmap.setCenter({ lat: c.lat, lng: c.lng });
    if (typeof zoom === 'number') {
      this.gmap.setZoom(zoom);
    }
  }
}
