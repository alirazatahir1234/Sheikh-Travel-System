/**
 * Custom geofence drawing helpers — replaces deprecated Maps DrawingManager (removed in v3.65).
 * Produces the same Circle / Polygon / Rectangle overlays the geofences editor already edits.
 */

export type GmapDrawMode = '' | 'circle' | 'polygon' | 'rectangle';

export interface GmapShapeDrawerOptions {
  map: google.maps.Map;
  getColor: () => string;
  isLocked: () => boolean;
  onComplete: (overlay: google.maps.Circle | google.maps.Polygon | google.maps.Rectangle) => void;
  onLockedAttempt?: () => void;
}

const STYLE = (color: string) => ({
  fillColor: color,
  fillOpacity: 0.18,
  strokeColor: color,
  strokeWeight: 2,
  editable: true,
  draggable: true,
  clickable: true
});

function haversineMeters(a: google.maps.LatLng, b: google.maps.LatLng): number {
  const toRad = (d: number) => (d * Math.PI) / 180;
  const dLat = toRad(b.lat() - a.lat());
  const dLng = toRad(b.lng() - a.lng());
  const lat1 = toRad(a.lat());
  const lat2 = toRad(b.lat());
  const h =
    Math.sin(dLat / 2) ** 2 +
    Math.cos(lat1) * Math.cos(lat2) * Math.sin(dLng / 2) ** 2;
  return 2 * 6371000 * Math.asin(Math.min(1, Math.sqrt(h)));
}

/**
 * Interactive polygon (click vertices) + rectangle (click-drag) drawer.
 * Circle drafts are created by the host via ensureDefaultDraftCircle — not here.
 */
export class GmapShapeDrawer {
  private mode: GmapDrawMode = '';
  private listeners: google.maps.MapsEventListener[] = [];
  private polyPath: google.maps.MVCArray<google.maps.LatLng> | null = null;
  private polyPreview: google.maps.Polygon | null = null;
  private polyMarkers: google.maps.Marker[] = [];
  private rectStart: google.maps.LatLng | null = null;
  private rectPreview: google.maps.Rectangle | null = null;
  private drawing = false;

  constructor(private readonly opts: GmapShapeDrawerOptions) {}

  get currentMode(): GmapDrawMode {
    return this.mode;
  }

  setMode(mode: GmapDrawMode): void {
    this.clearInteraction(false);
    this.mode = mode;
    if (!mode || mode === 'circle') {
      this.opts.map.setOptions({ draggableCursor: undefined });
      return;
    }
    if (this.opts.isLocked()) {
      this.mode = '';
      this.opts.onLockedAttempt?.();
      return;
    }
    this.opts.map.setOptions({ draggableCursor: 'crosshair' });
    if (mode === 'polygon') this.bindPolygon();
    else if (mode === 'rectangle') this.bindRectangle();
  }

  /** Cancel in-progress draw without completing. */
  cancel(): void {
    this.clearInteraction(true);
    this.mode = '';
    this.opts.map.setOptions({ draggableCursor: undefined });
  }

  destroy(): void {
    this.cancel();
  }

  private bindPolygon(): void {
    this.polyPath = new google.maps.MVCArray<google.maps.LatLng>();
    this.polyPreview = new google.maps.Polygon({
      map: this.opts.map,
      paths: this.polyPath,
      ...STYLE(this.opts.getColor()),
      editable: false,
      draggable: false
    });

    this.listeners.push(
      this.opts.map.addListener('click', (e: google.maps.MapMouseEvent) => {
        if (!e.latLng || this.opts.isLocked()) return;
        this.addPolygonVertex(e.latLng);
      })
    );
    this.listeners.push(
      this.opts.map.addListener('dblclick', (e: google.maps.MapMouseEvent) => {
        e.stop?.();
        this.finishPolygon();
      })
    );
  }

  private addPolygonVertex(latLng: google.maps.LatLng): void {
    if (!this.polyPath) return;

    // Close when clicking near the first vertex (≥3 points).
    if (this.polyPath.getLength() >= 3) {
      const first = this.polyPath.getAt(0);
      if (haversineMeters(first, latLng) < 25) {
        this.finishPolygon();
        return;
      }
    }

    this.polyPath.push(latLng);
    const marker = new google.maps.Marker({
      map: this.opts.map,
      position: latLng,
      clickable: this.polyPath.getLength() === 1,
      icon: {
        path: google.maps.SymbolPath.CIRCLE,
        scale: this.polyPath.getLength() === 1 ? 7 : 4,
        fillColor: '#fff',
        fillOpacity: 1,
        strokeColor: this.opts.getColor(),
        strokeWeight: 2
      },
      title: this.polyPath.getLength() === 1 ? 'Click to finish polygon' : undefined,
      zIndex: 999
    });
    if (this.polyPath.getLength() === 1) {
      marker.addListener('click', () => {
        if ((this.polyPath?.getLength() ?? 0) >= 3) this.finishPolygon();
      });
    }
    this.polyMarkers.push(marker);
  }

  private finishPolygon(): void {
    if (!this.polyPath || this.polyPath.getLength() < 3) return;
    const path = this.polyPath.getArray().map(ll => ({ lat: ll.lat(), lng: ll.lng() }));
    this.clearInteraction(true);
    this.mode = '';
    this.opts.map.setOptions({ draggableCursor: undefined });
    const polygon = new google.maps.Polygon({
      map: this.opts.map,
      paths: path,
      ...STYLE(this.opts.getColor())
    });
    this.opts.onComplete(polygon);
  }

  private bindRectangle(): void {
    this.listeners.push(
      this.opts.map.addListener('mousedown', (e: google.maps.MapMouseEvent) => {
        if (!e.latLng || this.opts.isLocked() || this.drawing) return;
        this.drawing = true;
        this.rectStart = e.latLng;
        this.opts.map.setOptions({ draggable: false, gestureHandling: 'none' });
        this.rectPreview = new google.maps.Rectangle({
          map: this.opts.map,
          bounds: new google.maps.LatLngBounds(e.latLng, e.latLng),
          ...STYLE(this.opts.getColor()),
          editable: false,
          draggable: false
        });
      })
    );
    this.listeners.push(
      this.opts.map.addListener('mousemove', (e: google.maps.MapMouseEvent) => {
        if (!this.drawing || !this.rectStart || !e.latLng || !this.rectPreview) return;
        this.rectPreview.setBounds(new google.maps.LatLngBounds(this.rectStart, e.latLng));
      })
    );
    const end = (e: google.maps.MapMouseEvent) => {
      if (!this.drawing || !this.rectStart) return;
      const endLl = e.latLng ?? this.rectPreview?.getBounds()?.getNorthEast() ?? null;
      this.drawing = false;
      this.opts.map.setOptions({ draggable: true, gestureHandling: 'greedy' });
      if (!endLl) {
        this.clearInteraction(true);
        return;
      }
      const bounds = new google.maps.LatLngBounds(this.rectStart, endLl);
      const ne = bounds.getNorthEast();
      const sw = bounds.getSouthWest();
      const tooSmall =
        Math.abs(ne.lat() - sw.lat()) < 1e-6 && Math.abs(ne.lng() - sw.lng()) < 1e-6;
      this.clearInteraction(true);
      this.mode = '';
      this.opts.map.setOptions({ draggableCursor: undefined });
      if (tooSmall) return;
      const rect = new google.maps.Rectangle({
        map: this.opts.map,
        bounds,
        ...STYLE(this.opts.getColor())
      });
      this.opts.onComplete(rect);
    };
    this.listeners.push(this.opts.map.addListener('mouseup', end));
  }

  private clearInteraction(removePreview: boolean): void {
    for (const l of this.listeners) l.remove();
    this.listeners = [];
    for (const m of this.polyMarkers) m.setMap(null);
    this.polyMarkers = [];
    if (removePreview) {
      this.polyPreview?.setMap(null);
      this.rectPreview?.setMap(null);
    }
    this.polyPreview = null;
    this.polyPath = null;
    this.rectPreview = null;
    this.rectStart = null;
    this.drawing = false;
    this.opts.map.setOptions({ draggable: true, gestureHandling: 'greedy' });
  }
}
