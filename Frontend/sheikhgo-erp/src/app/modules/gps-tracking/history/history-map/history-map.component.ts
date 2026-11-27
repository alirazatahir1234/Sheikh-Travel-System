import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  NgZone,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
  ViewChild
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { environment } from '../../../../../environments/environment';
import {
  Geofence,
  HistoryTimeline,
  HistoryTimelineSegment,
  TripReplayPosition,
  TripStop
} from '../../../../core/models/gps-tracking.model';
import { applyGmapTheme, triggerGmapResize } from '../../../../core/google-maps/gmap-theme';
import { pathFromGeofenceGeoJson } from '../../../../core/google-maps/gmap-geofence-layer';
import { GoogleMapsLoaderService } from '../../../../core/services/google-maps-loader.service';
import { AppBrandLoaderComponent } from '../../../../shared/components/app-brand-loader/app-brand-loader.component';
import { toDate } from '../../../../core/utils/zoned-time.util';
import { EventKind, EventTick, indexAtOrBefore, timesOf } from '../../utils/history-playback.util';
import {
  segmentEndMs,
  segmentStartMs,
  standstillTitle,
  standstillWhere
} from '../../utils/history-timeline.util';

export interface HistoryMapLayers {
  route: boolean;
  points: boolean;
  speed: boolean;
  stops: boolean;
  parking: boolean;
  geofences: boolean;
  events: boolean;
}

export type HistoryMapType = 'street' | 'satellite';

/** The vehicle at the playhead. */
export interface HistoryMapVehicle {
  latitude: number;
  longitude: number;
  heading: number | null;
}

/** Another vehicle's route drawn over this one (compare). */
export interface HistoryMapCompareRoute {
  label: string;
  color: string;
  route: TripReplayPosition[];
}

interface Vertex {
  lat: number;
  lng: number;
  t: number;
  speed: number;
}

interface TripGeometry {
  /** Stretches drawn as a solid line. */
  solid: Vertex[][];
  /** Stretches driven without a fix: straight and dashed. */
  dashed: google.maps.LatLngLiteral[][];
}

type Tier = 'selected' | 'day' | 'other';

const TEAL = '#0d9488';
const TEAL_DARK = '#134e4a';
const TEAL_LIGHT = '#5db5ab';
const SLATE = '#64748b';
const FENCE_BLUE = '#1d4ed8';
const SPEED_STOPS: [number, number, number][] = [[56, 189, 248], [245, 158, 11], [220, 38, 38]];
const SPEED_STEPS = 12;
const MAX_GPS_POINT_MARKERS = 600;
/** Event dots by kind: fill, border. */
const EVENT_COLOURS: Record<EventKind, [string, string]> = {
  geofence: ['#a78bfa', '#7c3aed'],
  overspeed: ['#fbbf24', '#b45309'],
  alarm: ['#f87171', '#b91c1c']
};
const MAX_GEOFENCES = 30;
/** Room for the layer chips (top), zoom buttons (right) and legend (bottom) over the map. */
const FIT_PADDING: google.maps.Padding = { top: 64, right: 64, bottom: 56, left: 48 };
/** Follow mode pans once the vehicle is this close to an edge (share of the map's size). */
const FOLLOW_EDGE = 0.05;

function speedColor(speed: number, max: number): string {
  const ratio = max > 0 ? Math.min(1, Math.max(0, speed / max)) : 0;
  const step = Math.round(ratio * SPEED_STEPS) / SPEED_STEPS;
  const [from, to, f] = step <= 0.5
    ? [SPEED_STOPS[0], SPEED_STOPS[1], step * 2]
    : [SPEED_STOPS[1], SPEED_STOPS[2], (step - 0.5) * 2];
  const mix = (i: number) => Math.round(from[i] + (to[i] - from[i]) * f);
  return `rgb(${mix(0)},${mix(1)},${mix(2)})`;
}

function dashIcons(color: string, scale: number): google.maps.IconSequence[] {
  return [{
    icon: { path: 'M 0,-1 0,1', strokeOpacity: 1, strokeColor: color, scale },
    offset: '0',
    repeat: `${Math.round(scale * 4.5)}px`
  }];
}

/** Marker content is anchored at its bottom centre; this puts its centre on the point. */
function centred(css: string): string {
  return `${css}transform:translateY(50%);`;
}

function standstillElement(kind: 'stop' | 'parking', selected: boolean, faded: boolean): HTMLElement {
  const wrap = document.createElement('div');
  wrap.style.cssText = centred('position:relative;display:grid;place-items:center;width:38px;height:38px;cursor:pointer;');
  if (selected) {
    const ring = document.createElement('div');
    ring.style.cssText =
      'position:absolute;inset:2px;border:2.5px solid #0f172a;border-radius:50%;box-sizing:border-box;';
    wrap.appendChild(ring);
  }
  const dot = document.createElement('div');
  if (kind === 'parking') {
    dot.style.cssText =
      'width:20px;height:20px;border-radius:6px;background:#1d4ed8;border:2px solid #fff;box-sizing:border-box;' +
      'color:#fff;font:700 12px/16px Inter,system-ui,sans-serif;text-align:center;box-shadow:0 1px 2px rgba(15,23,42,.3);';
    dot.textContent = 'P';
  } else {
    dot.style.cssText =
      'width:16px;height:16px;border-radius:50%;background:#f59e0b;border:2.5px solid #fff;box-sizing:border-box;' +
      'box-shadow:0 1px 2px rgba(15,23,42,.3);';
  }
  if (faded && !selected) dot.style.opacity = '0.6';
  wrap.appendChild(dot);
  return wrap;
}

function dotElement(size: number, fill: string, border: string, clickable: boolean): HTMLElement {
  const el = document.createElement('div');
  el.style.cssText = centred(
    `width:${size}px;height:${size}px;border-radius:50%;background:${fill};border:1.5px solid ${border};` +
    `box-sizing:border-box;${clickable ? 'cursor:pointer;' : ''}`
  );
  return el;
}

/**
 * The History workspace map. It only draws what it is given: the page owns the selection and the
 * playhead. The route is drawn by day and by trip — the selected trip dark and thick (or coloured
 * by speed), the rest of that day teal, other days lighter, stretches without a fix dashed.
 */
@Component({
  selector: 'app-history-map',
  standalone: true,
  imports: [CommonModule, AppBrandLoaderComponent],
  templateUrl: './history-map.component.html',
  styleUrls: ['./history-map.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HistoryMapComponent implements AfterViewInit, OnChanges, OnDestroy {
  @ViewChild('mapEl', { static: true }) mapEl!: ElementRef<HTMLDivElement>;

  /** Route for drawing (thinned by the API), in time order. */
  @Input() route: TripReplayPosition[] = [];
  @Input() timeline: HistoryTimeline | null = null;
  /** Stops to mark when the API sent no timeline. */
  @Input() legacyStops: TripStop[] = [];
  @Input() selectedId: string | null = null;
  /** The local day drawn in full colour. */
  @Input() activeDate: string | null = null;
  @Input() layers: HistoryMapLayers = { route: true, points: false, speed: false, stops: true, parking: true, geofences: true, events: true };
  /** Every fix, for the GPS points layer. Falls back to the route. */
  @Input() gpsPoints: TripReplayPosition[] = [];
  /** The events the dock shows (its day, week or trip), already capped. */
  @Input() events: EventTick[] = [];
  @Input() geofences: Geofence[] = [];
  @Input() compareRoutes: HistoryMapCompareRoute[] = [];
  @Input() vehicle: HistoryMapVehicle | null = null;
  @Input() follow = true;
  @Input() mapType: HistoryMapType = 'street';
  /** Stops shorter than this are left off the map, like the list. */
  @Input() minStopMinutes = 0;
  /** Top of the speed colour scale. */
  @Input() maxSpeedKmh = 0;
  @Input() loading = false;
  @Input() loadingTimedOut = false;
  @Input() noData = false;
  /** Shown over an empty map. */
  @Input() emptyHint = '';

  @Output() segmentSelected = new EventEmitter<string>();
  /** A point on the route or a GPS fix was clicked: seek there. */
  @Output() timeSelected = new EventEmitter<number>();
  @Output() retryRequested = new EventEmitter<void>();

  mapsUnavailable: string | null = null;

  private map: google.maps.Map | null = null;
  private resizeObserver: ResizeObserver | null = null;
  private times: number[] = [];
  private geometry = new Map<string, TripGeometry>();

  private routeLines: google.maps.Polyline[] = [];
  private compareLines: google.maps.Polyline[] = [];
  private fenceShapes: Array<google.maps.Polyline | google.maps.Polygon | google.maps.Circle> = [];
  private standstillMarkers: google.maps.marker.AdvancedMarkerElement[] = [];
  private pointMarkers: google.maps.marker.AdvancedMarkerElement[] = [];
  private eventMarkers: google.maps.marker.AdvancedMarkerElement[] = [];
  /** Click listeners per layer, dropped whenever that layer is redrawn. */
  private listeners: Record<'route' | 'standstill' | 'point' | 'event', google.maps.MapsEventListener[]> = {
    route: [],
    standstill: [],
    point: [],
    event: []
  };
  private vehicleMarker: google.maps.marker.AdvancedMarkerElement | null = null;
  private vehicleArrow: SVGElement | null = null;
  private fitPending = false;

  constructor(
    private readonly googleMapsLoader: GoogleMapsLoaderService,
    private readonly zone: NgZone,
    private readonly cdr: ChangeDetectorRef
  ) {}

  get hasRoute(): boolean {
    return this.route.length > 0;
  }

  ngAfterViewInit(): void {
    void this.initMap();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['route']) {
      this.times = timesOf(this.route);
      this.geometry.clear();
      this.fitPending = true;
    }
    if (changes['timeline']) this.geometry.clear();
    if (!this.map) return;

    const changed = (...names: string[]) => names.some(n => !!changes[n]);

    if (changed('mapType')) this.applyMapType();
    if (changed('route', 'timeline', 'selectedId', 'activeDate', 'layers', 'maxSpeedKmh')) this.renderRoute();
    if (changed('timeline', 'legacyStops', 'selectedId', 'activeDate', 'layers', 'minStopMinutes')) this.renderStandstills();
    if (changed('route', 'gpsPoints', 'timeline', 'selectedId', 'activeDate', 'layers')) this.renderGpsPoints();
    if (changed('geofences', 'layers', 'route')) this.renderGeofences();
    if (changed('events', 'layers')) this.renderEvents();
    if (changed('compareRoutes')) this.renderCompare();
    if (changed('vehicle', 'follow')) this.renderVehicle();
    if (this.fitPending) {
      this.fitPending = false;
      this.fitRoute();
    }
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    this.resizeObserver = null;
    this.clearAll();
    this.map = null;
  }

  requestRetry(): void {
    this.retryRequested.emit();
  }

  // ---- commands from the page ---------------------------------------------------------------

  zoomIn(): void {
    if (this.map) this.map.setZoom((this.map.getZoom() ?? 11) + 1);
  }

  zoomOut(): void {
    if (this.map) this.map.setZoom((this.map.getZoom() ?? 11) - 1);
  }

  /** Fit the whole loaded route, with every standstill, into view. */
  fitRoute(): void {
    if (!this.map) return;
    const bounds = new google.maps.LatLngBounds();
    let any = false;
    for (const p of this.route) {
      bounds.extend({ lat: p.latitude, lng: p.longitude });
      any = true;
    }
    for (const s of this.timeline?.segments ?? []) {
      bounds.extend({ lat: s.startLatitude, lng: s.startLongitude });
      bounds.extend({ lat: s.endLatitude, lng: s.endLongitude });
      any = true;
    }
    if (any) this.fit(bounds);
  }

  /** Bring one segment into view: a trip is fitted, a standstill is centred. */
  focusSegment(id: string | null): void {
    const segment = id ? this.timeline?.segments.find(s => s.id === id) : null;
    if (!this.map || !segment) return;
    if (segment.kind !== 'trip') {
      this.map.panTo({ lat: segment.startLatitude, lng: segment.startLongitude });
      if ((this.map.getZoom() ?? 0) < 15) this.map.setZoom(15);
      return;
    }
    const bounds = new google.maps.LatLngBounds();
    const { solid, dashed } = this.tripGeometry(segment);
    solid.forEach(path => path.forEach(v => bounds.extend(v)));
    dashed.forEach(path => path.forEach(v => bounds.extend(v)));
    this.fit(bounds);
  }

  // ---- map ------------------------------------------------------------------------------------

  private async initMap(): Promise<void> {
    try {
      const bootstrapped = await this.googleMapsLoader.load();
      if (bootstrapped) {
        await this.googleMapsLoader.importLibrary('maps');
        await this.googleMapsLoader.importLibrary('marker');
      }
      if (!bootstrapped || this.googleMapsLoader.authFailed) {
        this.showUnavailable();
        return;
      }

      // Created outside Angular: the map's own pointer events must not run change detection.
      this.zone.runOutsideAngular(() => {
        this.map = new google.maps.Map(this.mapEl.nativeElement, {
          center: { lat: 31.52, lng: 74.35 },
          zoom: 11,
          maxZoom: 20,
          disableDefaultUI: true,
          clickableIcons: false,
          mapId: (environment as { googleMapsMapId?: string }).googleMapsMapId?.trim() || 'DEMO_MAP_ID'
        });
        this.resizeObserver = new ResizeObserver(() => triggerGmapResize(this.map));
        this.resizeObserver.observe(this.mapEl.nativeElement);
      });

      this.applyMapType();
      this.renderRoute();
      this.renderStandstills();
      this.renderGpsPoints();
      this.renderGeofences();
      this.renderEvents();
      this.renderCompare();
      this.renderVehicle();
      this.fitPending = false;
      this.fitRoute();
    } catch {
      this.map = null;
      this.showUnavailable();
    }
  }

  private showUnavailable(): void {
    this.mapsUnavailable = 'The map could not be loaded. The timeline and the figures still work.';
    this.cdr.markForCheck();
  }

  private applyMapType(): void {
    if (this.map) applyGmapTheme(this.map, this.mapType === 'satellite' ? 'satellite' : 'street');
  }

  private fit(bounds: google.maps.LatLngBounds): void {
    if (!this.map || bounds.isEmpty()) return;
    this.map.fitBounds(bounds, FIT_PADDING);
    google.maps.event.addListenerOnce(this.map, 'idle', () => {
      if (this.map && (this.map.getZoom() ?? 0) > 16) this.map.setZoom(16);
    });
  }

  private emit(fn: () => void): void {
    this.zone.run(fn);
  }

  // ---- route ------------------------------------------------------------------------------------

  private renderRoute(): void {
    this.dropListeners('route');
    this.routeLines.forEach(l => l.setMap(null));
    this.routeLines = [];
    if (!this.map || !this.layers.route || !this.route.length) return;

    const timeline = this.timeline;
    if (!timeline) {
      // Older API: no cut into trips, so the whole route is one line.
      const path = this.route.map(p => ({ lat: p.latitude, lng: p.longitude }));
      this.addLine(path, '#ffffff', 8, 20);
      this.addLine(path, TEAL, 4, 21);
      return;
    }

    for (const trip of timeline.segments) {
      if (trip.kind !== 'trip') continue;
      const tier: Tier = trip.id === this.selectedId
        ? 'selected'
        : !this.activeDate || trip.date === this.activeDate ? 'day' : 'other';
      this.drawTrip(trip, tier);
    }
  }

  private drawTrip(trip: HistoryTimelineSegment, tier: Tier): void {
    const { solid, dashed } = this.tripGeometry(trip);
    const bySpeed = this.layers.speed && (tier === 'selected' || (tier === 'day' && !this.selectedIsTrip()));
    const select = () => this.emit(() => this.segmentSelected.emit(trip.id));
    const seek = (e: google.maps.MapMouseEvent) => {
      const at = e.latLng ? this.nearestTime(solid, e.latLng.lat(), e.latLng.lng()) : null;
      this.emit(() => {
        this.segmentSelected.emit(trip.id);
        if (at != null) this.timeSelected.emit(at);
      });
    };

    for (const path of solid) {
      if (path.length < 2) continue;
      if (tier === 'other') {
        this.addLine(path, TEAL_LIGHT, 3.5, 10, select);
        continue;
      }
      const casing = tier === 'selected' ? 11 : 8;
      const weight = tier === 'selected' ? 6.5 : 4;
      const z = tier === 'selected' ? 30 : 20;
      this.addLine(path, '#ffffff', casing, z);
      if (bySpeed) {
        this.addSpeedLines(path, weight, z + 1, tier === 'selected' ? seek : select);
      } else {
        this.addLine(path, tier === 'selected' ? TEAL_DARK : TEAL, weight, z + 1, tier === 'selected' ? seek : select);
      }
    }

    for (const path of dashed) {
      const line = new google.maps.Polyline({
        map: this.map,
        path,
        strokeOpacity: 0,
        icons: dashIcons(SLATE, tier === 'selected' ? 3 : 2.5),
        clickable: false,
        zIndex: tier === 'other' ? 9 : 22
      });
      this.routeLines.push(line);
    }
  }

  private selectedIsTrip(): boolean {
    return !!this.selectedId && this.timeline?.segments.some(s => s.id === this.selectedId && s.kind === 'trip') === true;
  }

  private addLine(
    path: google.maps.LatLngLiteral[],
    color: string,
    weight: number,
    zIndex: number,
    onClick?: (e: google.maps.MapMouseEvent) => void
  ): void {
    const line = new google.maps.Polyline({
      map: this.map,
      path,
      strokeColor: color,
      strokeOpacity: 1,
      strokeWeight: weight,
      clickable: !!onClick,
      zIndex
    });
    if (onClick) this.listeners.route.push(line.addListener('click', onClick));
    this.routeLines.push(line);
  }

  /** One line per run of the same colour step, so a long trip is a handful of polylines. */
  private addSpeedLines(
    path: Vertex[],
    weight: number,
    zIndex: number,
    onClick: (e: google.maps.MapMouseEvent) => void
  ): void {
    let run: Vertex[] = [path[0]];
    let color = speedColor(path[1].speed, this.maxSpeedKmh);
    for (let i = 1; i < path.length; i++) {
      const next = speedColor(path[i].speed, this.maxSpeedKmh);
      if (next !== color) {
        run.push(path[i]);
        this.addLine(run, color, weight, zIndex, onClick);
        run = [path[i]];
        color = next;
      } else {
        run.push(path[i]);
      }
    }
    if (run.length >= 2) this.addLine(run, color, weight, zIndex, onClick);
  }

  /**
   * A trip's line: the fixes inside it, tied to where it starts and ends. Between two fixes that
   * a no-signal stretch covers the solid line stops, and the stretch is drawn dashed instead.
   */
  private tripGeometry(trip: HistoryTimelineSegment): TripGeometry {
    const cached = this.geometry.get(trip.id);
    if (cached) return cached;

    const start = segmentStartMs(trip);
    const end = segmentEndMs(trip);
    const vertices: Vertex[] = [{ lat: trip.startLatitude, lng: trip.startLongitude, t: start, speed: 0 }];
    let i = indexAtOrBefore(this.times, start);
    if (i < 0 || this.times[i] < start) i++;
    for (; i < this.route.length && this.times[i] <= end; i++) {
      const p = this.route[i];
      vertices.push({ lat: p.latitude, lng: p.longitude, t: this.times[i], speed: Number(p.speedKmh) || 0 });
    }
    vertices.push({ lat: trip.endLatitude, lng: trip.endLongitude, t: end, speed: 0 });

    const gaps = (trip.noSignal ?? []).map(n => ({
      start: toDate(n.startTime)?.getTime() ?? 0,
      end: toDate(n.endTime)?.getTime() ?? 0,
      path: [
        { lat: n.startLatitude, lng: n.startLongitude },
        { lat: n.endLatitude, lng: n.endLongitude }
      ]
    }));
    const inGap = (a: number, b: number) => gaps.some(g => {
      const overlap = Math.min(b, g.end) - Math.max(a, g.start);
      return overlap > 0 && overlap >= (g.end - g.start) / 2;
    });

    const solid: Vertex[][] = [];
    let current: Vertex[] = [vertices[0]];
    for (let k = 1; k < vertices.length; k++) {
      if (inGap(vertices[k - 1].t, vertices[k].t)) {
        if (current.length >= 2) solid.push(current);
        current = [vertices[k]];
      } else {
        current.push(vertices[k]);
      }
    }
    if (current.length >= 2) solid.push(current);

    const geometry: TripGeometry = { solid, dashed: gaps.map(g => g.path) };
    this.geometry.set(trip.id, geometry);
    return geometry;
  }

  private nearestTime(paths: Vertex[][], lat: number, lng: number): number | null {
    let best: number | null = null;
    let bestDistance = Number.MAX_VALUE;
    for (const path of paths) {
      for (const v of path) {
        const d = (v.lat - lat) ** 2 + (v.lng - lng) ** 2;
        if (d < bestDistance) {
          bestDistance = d;
          best = v.t;
        }
      }
    }
    return best;
  }

  // ---- standstills ------------------------------------------------------------------------------

  private renderStandstills(): void {
    this.dropListeners('standstill');
    this.standstillMarkers.forEach(m => (m.map = null));
    this.standstillMarkers = [];
    if (!this.map) return;

    if (!this.timeline) {
      for (const stop of this.legacyStops) {
        const parking = stop.durationMinutes >= 120;
        if (parking ? !this.layers.parking : !this.layers.stops) continue;
        this.addStandstill(
          { lat: stop.latitude, lng: stop.longitude },
          standstillElement(parking ? 'parking' : 'stop', false, false),
          parking ? 'Parking' : 'Stop',
          null
        );
      }
      return;
    }

    for (const s of this.timeline.segments) {
      if (s.kind === 'trip') continue;
      const selected = s.id === this.selectedId;
      const shown = s.kind === 'parking' ? this.layers.parking : this.layers.stops;
      // The selected standstill stays visible even when its layer is off or the list hides it.
      if (!selected && (!shown || s.durationMinutes < this.minStopMinutes)) continue;
      const faded = !!this.activeDate && s.date !== this.activeDate;
      this.addStandstill(
        { lat: s.startLatitude, lng: s.startLongitude },
        standstillElement(s.kind, selected, faded),
        `${standstillTitle(s)}, ${standstillWhere(s)}`,
        s.id,
        selected ? 600 : s.kind === 'parking' ? 510 : 500
      );
    }
  }

  private addStandstill(
    position: google.maps.LatLngLiteral,
    content: HTMLElement,
    title: string,
    id: string | null,
    zIndex = 500
  ): void {
    const marker = new google.maps.marker.AdvancedMarkerElement({
      map: this.map,
      position,
      content,
      title,
      zIndex,
      gmpClickable: !!id
    });
    if (id) {
      this.listeners.standstill.push(marker.addListener('click', () => this.emit(() => this.segmentSelected.emit(id))));
    }
    this.standstillMarkers.push(marker);
  }

  // ---- GPS points -------------------------------------------------------------------------------

  /** Fixes of the selected trip, or of the day in view when no trip is selected. */
  private renderGpsPoints(): void {
    this.dropListeners('point');
    this.pointMarkers.forEach(m => (m.map = null));
    this.pointMarkers = [];
    if (!this.map || !this.layers.points) return;

    const source = this.gpsPoints.length ? this.gpsPoints : this.route;
    const sourceTimes = this.gpsPoints.length ? timesOf(this.gpsPoints) : this.times;
    const ranges = this.pointRanges();
    const picked: number[] = [];
    for (let i = 0; i < source.length; i++) {
      const t = sourceTimes[i];
      if (!ranges || ranges.some(r => t >= r[0] && t <= r[1])) picked.push(i);
    }
    const stride = Math.max(1, Math.ceil(picked.length / MAX_GPS_POINT_MARKERS));

    for (let k = 0; k < picked.length; k += stride) {
      const p = source[picked[k]];
      const t = sourceTimes[picked[k]];
      const marker = new google.maps.marker.AdvancedMarkerElement({
        map: this.map,
        position: { lat: p.latitude, lng: p.longitude },
        content: dotElement(8, '#ffffff', TEAL_DARK, true),
        title: 'GPS point',
        zIndex: 400,
        gmpClickable: true
      });
      this.listeners.point.push(marker.addListener('click', () => this.emit(() => this.timeSelected.emit(t))));
      this.pointMarkers.push(marker);
    }
  }

  /** Null means every fix (no timeline to narrow by). */
  private pointRanges(): [number, number][] | null {
    const timeline = this.timeline;
    if (!timeline) return null;
    const trips = timeline.segments.filter(s => s.kind === 'trip');
    const selected = trips.find(s => s.id === this.selectedId);
    const chosen = selected
      ? [selected]
      : trips.filter(s => !this.activeDate || s.date === this.activeDate);
    return chosen.map(s => [segmentStartMs(s), segmentEndMs(s)] as [number, number]);
  }

  // ---- geofences and events -----------------------------------------------------------------------

  private renderGeofences(): void {
    this.fenceShapes.forEach(s => s.setMap(null));
    this.fenceShapes = [];
    if (!this.map || !this.layers.geofences) return;

    const near = this.routeBounds(0.03);
    const style = { strokeOpacity: 0, fillColor: FENCE_BLUE, fillOpacity: 0.07, clickable: false, zIndex: 2 };
    let drawn = 0;
    for (const g of this.geofences) {
      if (drawn >= MAX_GEOFENCES) break;
      if (g.isActive === false) continue;
      const polygon = g.geoJson ? pathFromGeofenceGeoJson(g.geoJson) : null;
      if (polygon) {
        if (near && !polygon.some(v => near.contains(v))) continue;
        this.fenceShapes.push(new google.maps.Polygon({ map: this.map, paths: polygon, ...style }));
        this.fenceShapes.push(this.dashedOutline([...polygon, polygon[0]]));
        drawn++;
        continue;
      }
      if (!Number.isFinite(g.centerLat) || !Number.isFinite(g.centerLng) || !(g.radiusMeters > 0)) continue;
      const centre = { lat: g.centerLat, lng: g.centerLng };
      const ring = this.circlePath(centre, g.radiusMeters);
      if (near && !near.contains(centre) && !ring.some(v => near.contains(v))) continue;
      this.fenceShapes.push(new google.maps.Circle({ map: this.map, center: centre, radius: g.radiusMeters, ...style }));
      this.fenceShapes.push(this.dashedOutline(ring));
      drawn++;
    }
  }

  /** Its own layer: shown whatever the Geofences layer says. */
  private renderEvents(): void {
    this.dropListeners('event');
    this.eventMarkers.forEach(m => (m.map = null));
    this.eventMarkers = [];
    if (!this.map || !this.layers.events) return;

    for (const tick of this.events) {
      const { latitude, longitude } = tick.event;
      if (latitude == null || longitude == null) continue;
      const [fill, border] = EVENT_COLOURS[tick.kind];
      const marker = new google.maps.marker.AdvancedMarkerElement({
        map: this.map,
        position: { lat: latitude, lng: longitude },
        content: dotElement(11, fill, border, true),
        title: tick.label,
        zIndex: 450,
        gmpClickable: true
      });
      this.listeners.event.push(marker.addListener('click', () => this.emit(() => this.timeSelected.emit(tick.atMs))));
      this.eventMarkers.push(marker);
    }
  }

  private dashedOutline(path: google.maps.LatLngLiteral[]): google.maps.Polyline {
    return new google.maps.Polyline({
      map: this.map,
      path,
      strokeOpacity: 0,
      icons: dashIcons(FENCE_BLUE, 1.5),
      clickable: false,
      zIndex: 3
    });
  }

  private circlePath(centre: google.maps.LatLngLiteral, radiusMeters: number): google.maps.LatLngLiteral[] {
    const path: google.maps.LatLngLiteral[] = [];
    const dLat = radiusMeters / 111_320;
    const dLng = radiusMeters / (111_320 * Math.max(0.01, Math.cos((centre.lat * Math.PI) / 180)));
    for (let i = 0; i <= 72; i++) {
      const a = (i / 72) * 2 * Math.PI;
      path.push({ lat: centre.lat + dLat * Math.sin(a), lng: centre.lng + dLng * Math.cos(a) });
    }
    return path;
  }

  private routeBounds(padDegrees: number): google.maps.LatLngBounds | null {
    if (!this.route.length) return null;
    let south = 90, north = -90, west = 180, east = -180;
    for (const p of this.route) {
      south = Math.min(south, p.latitude);
      north = Math.max(north, p.latitude);
      west = Math.min(west, p.longitude);
      east = Math.max(east, p.longitude);
    }
    return new google.maps.LatLngBounds(
      { lat: south - padDegrees, lng: west - padDegrees },
      { lat: north + padDegrees, lng: east + padDegrees }
    );
  }

  // ---- compare ------------------------------------------------------------------------------------

  private renderCompare(): void {
    this.compareLines.forEach(l => l.setMap(null));
    this.compareLines = [];
    if (!this.map) return;
    for (const overlay of this.compareRoutes) {
      if (overlay.route.length < 2) continue;
      const stride = Math.max(1, Math.ceil(overlay.route.length / 1200));
      const path: google.maps.LatLngLiteral[] = [];
      for (let i = 0; i < overlay.route.length; i += stride) {
        path.push({ lat: overlay.route[i].latitude, lng: overlay.route[i].longitude });
      }
      const last = overlay.route[overlay.route.length - 1];
      path.push({ lat: last.latitude, lng: last.longitude });
      this.compareLines.push(new google.maps.Polyline({
        map: this.map,
        path,
        strokeColor: overlay.color,
        strokeOpacity: 0.85,
        strokeWeight: 4,
        clickable: false,
        zIndex: 15
      }));
    }
  }

  // ---- vehicle ------------------------------------------------------------------------------------

  private renderVehicle(): void {
    if (!this.map) return;
    const v = this.vehicle;
    if (!v) {
      if (this.vehicleMarker) this.vehicleMarker.map = null;
      return;
    }

    if (!this.vehicleMarker) {
      const root = document.createElement('div');
      root.style.cssText = centred('width:34px;height:34px;pointer-events:none;');
      root.innerHTML =
        '<svg viewBox="-17 -17 34 34" width="34" height="34" aria-hidden="true">' +
        '<circle r="17" fill="#0e766e" opacity=".2"></circle>' +
        '<circle r="11" fill="#ffffff" stroke="#134e4a" stroke-width="2.5"></circle>' +
        '<path data-arrow d="M-4.5 4.5L0 -6.5L4.5 4.5L0 2Z" fill="#134e4a"></path></svg>';
      this.vehicleArrow = root.querySelector('[data-arrow]');
      this.vehicleMarker = new google.maps.marker.AdvancedMarkerElement({
        content: root,
        title: 'Vehicle position',
        zIndex: 1000
      });
    }

    const position = { lat: v.latitude, lng: v.longitude };
    this.vehicleMarker.position = position;
    if (!this.vehicleMarker.map) this.vehicleMarker.map = this.map;
    if (v.heading != null) this.vehicleArrow?.setAttribute('transform', `rotate(${Math.round(v.heading)})`);

    if (this.follow) this.keepInView(position);
  }

  /**
   * Pan only when the vehicle nears the edge, so the map does not slide on every frame. The edge
   * band is narrower than the fit padding, so playing a trip that was just fitted does not jump.
   */
  private keepInView(position: google.maps.LatLngLiteral): void {
    const bounds = this.map?.getBounds();
    if (!this.map || !bounds) return;
    const ne = bounds.getNorthEast();
    const sw = bounds.getSouthWest();
    const padLat = (ne.lat() - sw.lat()) * FOLLOW_EDGE;
    const padLng = (ne.lng() - sw.lng()) * FOLLOW_EDGE;
    const inside =
      position.lat < ne.lat() - padLat &&
      position.lat > sw.lat() + padLat &&
      position.lng < ne.lng() - padLng &&
      position.lng > sw.lng() + padLng;
    if (!inside) this.map.panTo(position);
  }

  // ---- teardown -----------------------------------------------------------------------------------

  private dropListeners(layer: 'route' | 'standstill' | 'point' | 'event'): void {
    this.listeners[layer].forEach(l => l.remove());
    this.listeners[layer] = [];
  }

  private clearAll(): void {
    (['route', 'standstill', 'point', 'event'] as const).forEach(layer => this.dropListeners(layer));
    [...this.routeLines, ...this.compareLines].forEach(l => l.setMap(null));
    this.fenceShapes.forEach(s => s.setMap(null));
    [...this.standstillMarkers, ...this.pointMarkers, ...this.eventMarkers].forEach(m => (m.map = null));
    if (this.vehicleMarker) this.vehicleMarker.map = null;
    this.routeLines = [];
    this.compareLines = [];
    this.fenceShapes = [];
    this.standstillMarkers = [];
    this.pointMarkers = [];
    this.eventMarkers = [];
    this.vehicleMarker = null;
    this.vehicleArrow = null;
  }
}
