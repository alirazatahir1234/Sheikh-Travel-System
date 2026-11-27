/// <reference types="google.maps" />
import { GmapShapeDrawer } from '../../../core/google-maps/gmap-shape-drawer';
import { GeofenceAreaType, GeofenceShape } from './geofence.models';
import {
  LatLng,
  MIN_CIRCLE_RADIUS_M,
  circlePath,
  completeShape,
  formatDistance,
  haversineMeters,
  isShapeComplete,
  metersPerPixel,
  rectanglePath,
  shapeBounds
} from './geofence-geometry.util';

/**
 * Google Maps drawing for the geofences screens: saved shapes (restricted dashed, corridors as a band with a dashed
 * centre line), pill labels, live vehicle dots, the drawing tools, measure, and the editable shape with its GPS
 * tolerance band. Everything here is created after the Maps API has loaded; callers wrap callbacks in NgZone.run.
 */

/** Darker shade of a fence colour for its label, as on the design (teal #0f766e → label #0b5d56). */
export function shade(hex: string, amount = 0.22): string {
  const m = /^#([0-9a-f]{6})$/i.exec(hex);
  if (!m) return '#0f172a';
  const n = parseInt(m[1], 16);
  const ch = (v: number) => Math.max(0, Math.round(v * (1 - amount))).toString(16).padStart(2, '0');
  return `#${ch((n >> 16) & 255)}${ch((n >> 8) & 255)}${ch(n & 255)}`;
}

// ---- HTML labels --------------------------------------------------------------------------------------------------

export interface GmapHtmlLabel {
  setMap(map: google.maps.Map | null): void;
  update(text: string, color: string): void;
  setPosition(position: LatLng): void;
}

type LabelCtor = new (position: LatLng, text: string, color: string, onClick?: () => void, variant?: 'pill' | 'tip') => GmapHtmlLabel;

let labelCtor: LabelCtor | null = null;

/** The OverlayView subclass is created on first use: google.maps must exist when the class is defined. */
function htmlLabelClass(): LabelCtor {
  if (labelCtor) return labelCtor;
  class HtmlLabel extends google.maps.OverlayView implements GmapHtmlLabel {
    private div: HTMLDivElement | null = null;

    constructor(
      private position: LatLng,
      private text: string,
      private color: string,
      private readonly onClick?: () => void,
      private readonly variant: 'pill' | 'tip' = 'pill'
    ) {
      super();
    }

    override onAdd(): void {
      const div = document.createElement('div');
      div.style.cssText = [
        'position:absolute',
        'transform:translate(-50%,-50%)',
        'white-space:nowrap',
        'font:600 13px/1.2 Inter,system-ui,sans-serif',
        'color:#fff',
        this.variant === 'pill' ? 'padding:5px 12px;border-radius:12px' : 'padding:5px 10px;border-radius:6px',
        'box-shadow:0 1px 4px rgba(15,23,42,.28)',
        this.onClick ? 'cursor:pointer' : 'pointer-events:none'
      ].join(';');
      if (this.onClick) {
        div.addEventListener('click', e => {
          e.stopPropagation();
          this.onClick?.();
        });
      }
      this.div = div;
      this.paint();
      this.getPanes()?.overlayMouseTarget.appendChild(div);
    }

    override draw(): void {
      const projection = this.getProjection();
      if (!projection || !this.div) return;
      const point = projection.fromLatLngToDivPixel(new google.maps.LatLng(this.position.lat, this.position.lng));
      if (!point) return;
      this.div.style.left = `${point.x}px`;
      this.div.style.top = `${point.y}px`;
    }

    override onRemove(): void {
      this.div?.remove();
      this.div = null;
    }

    update(text: string, color: string): void {
      this.text = text;
      this.color = color;
      this.paint();
    }

    setPosition(position: LatLng): void {
      this.position = position;
      this.draw();
    }

    private paint(): void {
      if (!this.div) return;
      // textContent, never innerHTML: geofence names are user input.
      this.div.textContent = this.text;
      this.div.style.background = this.color;
    }
  }
  labelCtor = HtmlLabel;
  return labelCtor;
}

export function createHtmlLabel(
  map: google.maps.Map,
  position: LatLng,
  text: string,
  color: string,
  onClick?: () => void,
  variant: 'pill' | 'tip' = 'pill'
): GmapHtmlLabel {
  const Ctor = htmlLabelClass();
  const label = new Ctor(position, text, color, onClick, variant);
  label.setMap(map);
  return label;
}

// ---- Strokes that follow ground distance ---------------------------------------------------------------------------

/** Stroke width in pixels for a band that is `meters` wide on the ground at the map's zoom. */
export function bandPixels(map: google.maps.Map, lat: number, meters: number): number {
  const zoom = map.getZoom() ?? 12;
  return Math.min(600, Math.max(2, meters / metersPerPixel(lat, zoom)));
}

function dashIcons(color: string, weight: number, opacity = 1): google.maps.IconSequence[] {
  return [{
    icon: { path: 'M 0,-1 0,1', strokeOpacity: opacity, strokeColor: color, strokeWeight: weight, scale: Math.max(2, weight * 1.6) },
    offset: '0',
    repeat: `${Math.max(8, weight * 6)}px`
  }];
}

function dashedLine(map: google.maps.Map, path: LatLng[], color: string, weight: number, closed: boolean, zIndex = 2, opacity = 1): google.maps.Polyline {
  return new google.maps.Polyline({
    map,
    path: closed && path.length ? [...path, path[0]] : path,
    strokeOpacity: 0,
    icons: dashIcons(color, weight, opacity),
    clickable: false,
    zIndex
  });
}

// ---- Saved fences (main map and the editor's reference layer) -----------------------------------------------------

export interface FenceStyle {
  color: string;
  restricted: boolean;
  active: boolean;
  /** Reference outlines on the editor: dashed, faint, not clickable. */
  reference?: boolean;
  selected?: boolean;
}

export interface RenderedFence {
  remove(): void;
  bounds(): google.maps.LatLngBounds | null;
}

/** Draws one saved geofence. Returns a handle that redraws corridor bands on zoom and removes everything. */
export function renderFence(map: google.maps.Map, shape: GeofenceShape, style: FenceStyle, onClick?: () => void): RenderedFence {
  const overlays: (google.maps.MVCObject & { setMap(m: google.maps.Map | null): void })[] = [];
  const listeners: google.maps.MapsEventListener[] = [];
  const fillOpacity = style.reference ? 0.06 : style.active ? (style.restricted ? 0.16 : 0.2) : 0.07;
  const strokeOpacity = style.active ? 1 : 0.55;
  const weight = style.selected ? 3.5 : 2.5;
  const dashed = style.restricted || !!style.reference;
  const clickable = !!onClick && !style.reference;
  const zIndex = style.selected ? 5 : 3;

  const listen = (o: google.maps.MVCObject) => {
    if (clickable) listeners.push(o.addListener('click', () => onClick!()));
  };

  if (shape.areaType === 'corridor') {
    const path = shape.path ?? [];
    const lat = path[0]?.lat ?? 0;
    const band = new google.maps.Polyline({
      map,
      path,
      strokeColor: style.color,
      strokeOpacity: style.reference ? 0.12 : style.active ? 0.28 : 0.12,
      strokeWeight: bandPixels(map, lat, shape.corridorWidthMeters ?? 0),
      clickable,
      zIndex: zIndex - 1
    });
    listen(band);
    overlays.push(band, dashedLine(map, path, shade(style.color, 0.1), style.selected ? 3 : 2, false, zIndex, strokeOpacity));
    listeners.push(map.addListener('zoom_changed', () =>
      band.setOptions({ strokeWeight: bandPixels(map, lat, shape.corridorWidthMeters ?? 0) })));
  } else if (shape.areaType === 'circle' && shape.center) {
    const circle = new google.maps.Circle({
      map,
      center: shape.center,
      radius: shape.radiusMeters ?? 0,
      fillColor: style.color,
      fillOpacity,
      strokeColor: style.color,
      strokeOpacity: dashed ? 0 : strokeOpacity,
      strokeWeight: weight,
      clickable,
      zIndex
    });
    listen(circle);
    overlays.push(circle);
    if (dashed) overlays.push(dashedLine(map, circlePath(shape.center, shape.radiusMeters ?? 0), style.color, weight, true, zIndex + 1, strokeOpacity));
  } else {
    const path = shape.path ?? [];
    const polygon = new google.maps.Polygon({
      map,
      paths: path,
      fillColor: style.color,
      fillOpacity,
      strokeColor: style.color,
      strokeOpacity: dashed ? 0 : strokeOpacity,
      strokeWeight: weight,
      clickable,
      zIndex
    });
    listen(polygon);
    overlays.push(polygon);
    if (dashed) overlays.push(dashedLine(map, path, style.color, weight, true, zIndex + 1, strokeOpacity));
    if (style.selected && shape.areaType === 'polygon') {
      for (const p of path) {
        overlays.push(new google.maps.Marker({
          map,
          position: p,
          clickable: false,
          zIndex: zIndex + 2,
          icon: { path: google.maps.SymbolPath.CIRCLE, scale: 5, fillColor: '#fff', fillOpacity: 1, strokeColor: style.color, strokeWeight: 2 }
        }));
      }
    }
  }

  return {
    remove(): void {
      for (const l of listeners) l.remove();
      for (const o of overlays) o.setMap(null);
    },
    bounds(): google.maps.LatLngBounds | null {
      const b = shapeBounds(shape);
      return b ? new google.maps.LatLngBounds({ lat: b.minLat, lng: b.minLng }, { lat: b.maxLat, lng: b.maxLng }) : null;
    }
  };
}

/** Where a fence's label sits: the circle centre, the ring's centre, or the corridor's middle point. */
export function labelAnchor(shape: GeofenceShape): LatLng | null {
  if (shape.areaType === 'circle') return shape.center ?? null;
  const path = shape.path ?? [];
  if (!path.length) return null;
  if (shape.areaType === 'corridor') return path[Math.floor(path.length / 2)];
  const b = shapeBounds(shape);
  return b ? { lat: (b.minLat + b.maxLat) / 2, lng: (b.minLng + b.maxLng) / 2 } : null;
}

// ---- Live vehicles -------------------------------------------------------------------------------------------------

export type VehicleTone = 'inside' | 'outside' | 'violation';

export const VEHICLE_TONE_COLORS: Record<VehicleTone, string> = {
  inside: '#0f172a',
  outside: '#475569',
  violation: '#dc2626'
};

export function vehicleMarker(map: google.maps.Map, position: LatLng, tone: VehicleTone, title: string): google.maps.Marker {
  return new google.maps.Marker({
    map,
    position,
    title,
    zIndex: tone === 'violation' ? 30 : 20,
    icon: {
      path: google.maps.SymbolPath.CIRCLE,
      scale: tone === 'violation' ? 8 : 7,
      fillColor: VEHICLE_TONE_COLORS[tone],
      fillOpacity: 1,
      strokeColor: '#ffffff',
      strokeWeight: 3
    }
  });
}

// ---- Drawing tools -------------------------------------------------------------------------------------------------

export interface DrawToolOptions {
  map: google.maps.Map;
  color: () => string;
  corridorWidth: () => number;
  onComplete: (shape: GeofenceShape) => void;
  onHint?: (text: string) => void;
}

/** Default circle radius when the map is clicked without dragging. */
export const DEFAULT_DRAWN_RADIUS_M = 250;

/**
 * Click-to-draw for all four shapes. Polygon and rectangle use the shared GmapShapeDrawer; a circle is a click (or a
 * press-and-drag to size it); a corridor is clicks along the route, finished with a double-click or Enter.
 */
export class GeofenceDrawTool {
  private mode: GeofenceAreaType | null = null;
  private readonly drawer: GmapShapeDrawer;
  private listeners: google.maps.MapsEventListener[] = [];
  private preview: google.maps.Polyline | google.maps.Circle | null = null;
  private band: google.maps.Polyline | null = null;
  private line: LatLng[] = [];
  private readonly keyHandler = (e: KeyboardEvent) => {
    if (this.mode !== 'corridor') return;
    if (e.key === 'Enter') this.finishCorridor();
    if (e.key === 'Escape') this.cancel();
  };

  constructor(private readonly opts: DrawToolOptions) {
    this.drawer = new GmapShapeDrawer({
      map: opts.map,
      getColor: opts.color,
      isLocked: () => false,
      onComplete: overlay => {
        const shape = overlayToShape(overlay);
        overlay.setMap(null);
        this.mode = null;
        if (shape) opts.onComplete(shape);
      }
    });
  }

  get active(): GeofenceAreaType | null {
    return this.mode;
  }

  start(mode: GeofenceAreaType): void {
    this.cancel();
    this.mode = mode;
    if (mode === 'polygon' || mode === 'rectangle') {
      this.drawer.setMode(mode);
      this.opts.onHint?.(mode === 'polygon'
        ? 'Click the map to add corners; click the first corner or double-click to finish.'
        : 'Press and drag on the map to draw the rectangle.');
      return;
    }
    this.opts.map.setOptions({ draggableCursor: 'crosshair', disableDoubleClickZoom: true });
    if (mode === 'circle') this.bindCircle();
    else this.bindCorridor();
  }

  cancel(): void {
    this.drawer.cancel();
    this.clear();
    this.mode = null;
  }

  destroy(): void {
    this.cancel();
    this.drawer.destroy();
  }

  private clear(): void {
    for (const l of this.listeners) l.remove();
    this.listeners = [];
    this.preview?.setMap(null);
    this.band?.setMap(null);
    this.preview = null;
    this.band = null;
    this.line = [];
    document.removeEventListener('keydown', this.keyHandler);
    this.opts.map.setOptions({ draggableCursor: undefined, disableDoubleClickZoom: false, draggable: true, gestureHandling: 'greedy' });
  }

  private bindCircle(): void {
    const map = this.opts.map;
    this.opts.onHint?.('Click the map to place the circle, or press and drag to size it.');
    let center: google.maps.LatLng | null = null;
    this.listeners.push(map.addListener('mousedown', (e: google.maps.MapMouseEvent) => {
      if (!e.latLng) return;
      center = e.latLng;
      map.setOptions({ draggable: false, gestureHandling: 'none' });
      this.preview = new google.maps.Circle({
        map, center, radius: 1, fillColor: this.opts.color(), fillOpacity: 0.18, strokeColor: this.opts.color(), strokeWeight: 2, clickable: false
      });
    }));
    this.listeners.push(map.addListener('mousemove', (e: google.maps.MapMouseEvent) => {
      if (!center || !e.latLng || !(this.preview instanceof google.maps.Circle)) return;
      const r = haversineMeters(ll(center), ll(e.latLng));
      this.preview.setRadius(r);
      this.opts.onHint?.(`Radius ${formatDistance(r)}`);
    }));
    this.listeners.push(map.addListener('mouseup', (e: google.maps.MapMouseEvent) => {
      if (!center) return;
      const dragged = e.latLng ? haversineMeters(ll(center), ll(e.latLng)) : 0;
      const radius = dragged >= MIN_CIRCLE_RADIUS_M ? Math.round(dragged) : DEFAULT_DRAWN_RADIUS_M;
      const shape: GeofenceShape = { areaType: 'circle', center: ll(center), radiusMeters: radius };
      this.clear();
      this.mode = null;
      this.opts.onComplete(shape);
    }));
  }

  private bindCorridor(): void {
    const map = this.opts.map;
    this.opts.onHint?.('Click along the route; double-click or press Enter to finish.');
    this.preview = new google.maps.Polyline({ map, path: [], strokeColor: shade(this.opts.color(), 0.1), strokeWeight: 2, clickable: false, zIndex: 10 });
    this.band = new google.maps.Polyline({ map, path: [], strokeColor: this.opts.color(), strokeOpacity: 0.25, strokeWeight: 2, clickable: false, zIndex: 9 });
    const redraw = () => {
      (this.preview as google.maps.Polyline | null)?.setPath(this.line);
      if (this.band && this.line.length) {
        this.band.setPath(this.line);
        this.band.setOptions({ strokeWeight: bandPixels(map, this.line[0].lat, this.opts.corridorWidth()) });
      }
    };
    this.listeners.push(map.addListener('click', (e: google.maps.MapMouseEvent) => {
      if (!e.latLng) return;
      this.line.push(ll(e.latLng));
      redraw();
    }));
    this.listeners.push(map.addListener('dblclick', (e: google.maps.MapMouseEvent) => {
      e.stop?.();
      this.finishCorridor();
    }));
    this.listeners.push(map.addListener('zoom_changed', redraw));
    document.addEventListener('keydown', this.keyHandler);
  }

  private finishCorridor(): void {
    // A double-click also fires two clicks; drop the repeated last point.
    const line = this.line.filter((p, i, all) => i === 0 || haversineMeters(p, all[i - 1]) > 1);
    if (line.length < 2) {
      this.opts.onHint?.('Add at least two points along the route.');
      return;
    }
    const shape: GeofenceShape = { areaType: 'corridor', path: line, corridorWidthMeters: this.opts.corridorWidth() };
    this.clear();
    this.mode = null;
    this.opts.onComplete(shape);
  }
}

const ll = (p: google.maps.LatLng): LatLng => ({ lat: p.lat(), lng: p.lng() });

function overlayToShape(overlay: google.maps.Circle | google.maps.Polygon | google.maps.Rectangle): GeofenceShape | null {
  if (overlay instanceof google.maps.Rectangle) {
    const b = overlay.getBounds();
    return b ? { areaType: 'rectangle', path: rectanglePath(ll(b.getNorthEast()), ll(b.getSouthWest())) } : null;
  }
  if (overlay instanceof google.maps.Polygon) {
    const path = overlay.getPath().getArray().map(ll);
    return path.length >= 3 ? { areaType: 'polygon', path } : null;
  }
  const c = overlay.getCenter();
  return c ? { areaType: 'circle', center: ll(c), radiusMeters: Math.round(overlay.getRadius()) } : null;
}

// ---- Measure -------------------------------------------------------------------------------------------------------

/** Click points to measure a distance along them; a label shows the running total. */
export class MeasureTool {
  private points: LatLng[] = [];
  private line: google.maps.Polyline | null = null;
  private label: GmapHtmlLabel | null = null;
  private listeners: google.maps.MapsEventListener[] = [];

  constructor(private readonly map: google.maps.Map, private readonly onChange: (meters: number) => void) {}

  get active(): boolean {
    return this.listeners.length > 0;
  }

  start(): void {
    this.stop();
    this.map.setOptions({ draggableCursor: 'crosshair', disableDoubleClickZoom: true });
    this.line = new google.maps.Polyline({ map: this.map, path: [], strokeColor: '#0f172a', strokeWeight: 2, clickable: false, zIndex: 40 });
    this.listeners.push(this.map.addListener('click', (e: google.maps.MapMouseEvent) => {
      if (!e.latLng) return;
      this.points.push(ll(e.latLng));
      this.line?.setPath(this.points);
      const meters = this.points.reduce((sum, p, i) => (i ? sum + haversineMeters(this.points[i - 1], p) : 0), 0);
      const text = this.points.length > 1 ? formatDistance(meters) : 'Click the next point';
      if (this.label) {
        this.label.update(text, '#0f172a');
        this.label.setPosition(this.points[this.points.length - 1]);
      } else {
        this.label = createHtmlLabel(this.map, this.points[0], text, '#0f172a', undefined, 'tip');
      }
      this.onChange(meters);
    }));
  }

  stop(): void {
    for (const l of this.listeners) l.remove();
    this.listeners = [];
    this.line?.setMap(null);
    this.label?.setMap(null);
    this.line = null;
    this.label = null;
    this.points = [];
    this.map.setOptions({ draggableCursor: undefined, disableDoubleClickZoom: false });
  }
}

// ---- The editor's editable shape -----------------------------------------------------------------------------------

export interface EditableShapeOptions {
  map: google.maps.Map;
  /** User edits only; programmatic setShape never calls back. */
  onChange: (shape: GeofenceShape) => void;
  /** "Moved 183 m", "Radius 250 m" — shown briefly near the edit. */
  onMeasure?: (text: string, at: LatLng) => void;
}

/**
 * The shape being edited: corner points and midpoints come from Google's editable overlays (drag a midpoint to add a
 * point, click a point to delete it), with a light band showing the GPS tolerance buffer on both sides of the line.
 */
export class EditableShapeLayer {
  private shape: GeofenceShape | null = null;
  private color = '#0f766e';
  private buffer = 25;
  private main: google.maps.Circle | google.maps.Polygon | google.maps.Rectangle | google.maps.Polyline | null = null;
  private corridorBand: google.maps.Polyline | null = null;
  private bufferBand: google.maps.Circle | google.maps.Polygon | google.maps.Polyline | null = null;
  private listeners: google.maps.MapsEventListener[] = [];
  private readonly zoomListener: google.maps.MapsEventListener;
  private applying = false;

  constructor(private readonly opts: EditableShapeOptions) {
    this.zoomListener = opts.map.addListener('zoom_changed', () => this.refreshBands());
  }

  setStyle(color: string, bufferMeters: number): void {
    this.color = color;
    this.buffer = Math.max(0, bufferMeters || 0);
    if (!this.main) return;
    if (this.main instanceof google.maps.Polyline) this.main.setOptions({ strokeColor: shade(color, 0.1) });
    else this.main.setOptions({ fillColor: color, strokeColor: color });
    this.corridorBand?.setOptions({ strokeColor: color });
    this.bufferBand?.setOptions({ strokeColor: color });
    this.refreshBands();
  }

  /** Redraws from data (load, undo, paste, shape switch). */
  setShape(shape: GeofenceShape | null): void {
    this.applying = true;
    this.teardown();
    this.shape = shape ? JSON.parse(JSON.stringify(shape)) as GeofenceShape : null;
    const complete = completeShape(this.shape);
    if (complete) this.build(complete);
    this.applying = false;
  }

  fit(padding = 64): void {
    const b = shapeBounds(this.shape, this.buffer);
    if (b) this.opts.map.fitBounds(new google.maps.LatLngBounds({ lat: b.minLat, lng: b.minLng }, { lat: b.maxLat, lng: b.maxLng }), padding);
  }

  destroy(): void {
    this.teardown();
    this.zoomListener.remove();
  }

  private teardown(): void {
    for (const l of this.listeners) l.remove();
    this.listeners = [];
    this.main?.setMap(null);
    this.corridorBand?.setMap(null);
    this.bufferBand?.setMap(null);
    this.main = this.corridorBand = this.bufferBand = null;
  }

  private emit(next: GeofenceShape): void {
    this.shape = next;
    this.refreshBands();
    if (!this.applying) this.opts.onChange(JSON.parse(JSON.stringify(next)) as GeofenceShape);
  }

  private build(shape: GeofenceShape): void {
    const map = this.opts.map;
    const style = { fillColor: this.color, fillOpacity: 0.2, strokeColor: this.color, strokeWeight: 3, editable: true, draggable: true, zIndex: 10 };

    if (shape.areaType === 'circle') {
      const circle = new google.maps.Circle({ map, center: shape.center!, radius: shape.radiusMeters ?? DEFAULT_DRAWN_RADIUS_M, ...style });
      this.main = circle;
      const changed = (showRadius: boolean) => {
        const c = circle.getCenter();
        if (!c) return;
        const radius = Math.round(circle.getRadius());
        this.emit({ areaType: 'circle', center: ll(c), radiusMeters: radius });
        if (showRadius) this.opts.onMeasure?.(`Radius ${formatDistance(radius)}`, ll(c));
      };
      this.listeners.push(
        circle.addListener('radius_changed', () => changed(true)),
        circle.addListener('center_changed', () => changed(false))
      );
    } else if (shape.areaType === 'rectangle') {
      const path = shape.path ?? [];
      const bounds = new google.maps.LatLngBounds();
      path.forEach(p => bounds.extend(p));
      const rect = new google.maps.Rectangle({ map, bounds, ...style });
      this.main = rect;
      this.listeners.push(rect.addListener('bounds_changed', () => {
        const b = rect.getBounds();
        if (b) this.emit({ areaType: 'rectangle', path: rectanglePath(ll(b.getNorthEast()), ll(b.getSouthWest())) });
      }));
    } else if (shape.areaType === 'polygon') {
      const polygon = new google.maps.Polygon({ map, paths: shape.path ?? [], ...style });
      this.main = polygon;
      this.bindPath(polygon.getPath(), p => ({ areaType: 'polygon', path: p }));
      this.listeners.push(polygon.addListener('click', (e: google.maps.PolyMouseEvent) => this.deleteVertex(polygon.getPath(), e, 3)));
    } else {
      const line = new google.maps.Polyline({
        map, path: shape.path ?? [], strokeColor: shade(this.color, 0.1), strokeWeight: 3, editable: true, draggable: true, zIndex: 12
      });
      this.main = line;
      this.corridorBand = new google.maps.Polyline({
        map, path: shape.path ?? [], strokeColor: this.color, strokeOpacity: 0.3, strokeWeight: 2, clickable: false, zIndex: 9
      });
      this.bindPath(line.getPath(), p => ({ areaType: 'corridor', path: p, corridorWidthMeters: this.shape?.corridorWidthMeters ?? null }));
      this.listeners.push(line.addListener('click', (e: google.maps.PolyMouseEvent) => this.deleteVertex(line.getPath(), e, 2)));
    }
    this.buildBufferBand(shape);
    this.refreshBands();
  }

  private bindPath(path: google.maps.MVCArray<google.maps.LatLng>, make: (p: LatLng[]) => GeofenceShape): void {
    const changed = () => this.emit(make(path.getArray().map(ll)));
    this.listeners.push(
      path.addListener('set_at', (i: number, previous: google.maps.LatLng) => {
        changed();
        const now = path.getAt(i);
        if (now && previous) this.opts.onMeasure?.(`Moved ${formatDistance(haversineMeters(ll(previous), ll(now)))}`, ll(now));
      }),
      path.addListener('insert_at', changed),
      path.addListener('remove_at', changed)
    );
  }

  /** Clicking a corner deletes it (never below the minimum the shape needs). */
  private deleteVertex(path: google.maps.MVCArray<google.maps.LatLng>, e: google.maps.PolyMouseEvent, min: number): void {
    if (e.vertex == null || path.getLength() <= min) return;
    path.removeAt(e.vertex);
  }

  private buildBufferBand(shape: GeofenceShape): void {
    const map = this.opts.map;
    const band = { strokeColor: this.color, strokeOpacity: 0.22, strokeWeight: 2, fillOpacity: 0, clickable: false, zIndex: 8 };
    if (shape.areaType === 'circle') {
      this.bufferBand = new google.maps.Circle({ map, center: shape.center!, radius: shape.radiusMeters ?? 0, ...band });
    } else if (shape.areaType === 'corridor') {
      this.bufferBand = new google.maps.Polyline({ map, path: shape.path ?? [], ...band, strokeOpacity: 0.12 });
    } else {
      this.bufferBand = new google.maps.Polygon({ map, paths: shape.path ?? [], ...band });
    }
  }

  /** Keeps the corridor band and the buffer band their real ground width as the zoom changes. */
  private refreshBands(): void {
    const s = this.shape;
    if (!s || !isShapeComplete(s)) return;
    const map = this.opts.map;
    const lat = s.areaType === 'circle' ? s.center!.lat : (s.path ?? [])[0]?.lat ?? 0;
    if (s.areaType === 'corridor') {
      const width = s.corridorWidthMeters ?? 0;
      this.corridorBand?.setOptions({ path: s.path ?? [], strokeWeight: bandPixels(map, lat, width) });
      (this.bufferBand as google.maps.Polyline | null)?.setOptions({ path: s.path ?? [], strokeWeight: bandPixels(map, lat, width + 2 * this.buffer) });
      return;
    }
    const weight = this.buffer > 0 ? bandPixels(map, lat, 2 * this.buffer) : 0;
    if (this.bufferBand instanceof google.maps.Circle) {
      this.bufferBand.setOptions({ center: s.center!, radius: s.radiusMeters ?? 0, strokeWeight: weight, strokeOpacity: weight ? 0.22 : 0 });
    } else if (this.bufferBand instanceof google.maps.Polygon) {
      this.bufferBand.setOptions({ paths: s.path ?? [], strokeWeight: weight, strokeOpacity: weight ? 0.22 : 0 });
    }
  }
}
