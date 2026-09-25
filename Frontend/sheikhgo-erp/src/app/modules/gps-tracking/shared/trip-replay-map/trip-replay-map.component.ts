import {
  Component,
  Input,
  OnChanges,
  OnDestroy,
  AfterViewInit,
  ElementRef,
  ViewChild,
  SimpleChanges,
  Output,
  EventEmitter,
  ChangeDetectorRef
} from '@angular/core';
import { environment } from '../../../../../environments/environment';
import { TripEvent, TripReplayPosition, TripStop } from '../../../../core/models/gps-tracking.model';
import {
  MAP_THEME_OPTIONS,
  MapTheme,
  readStoredMapTheme,
  storeMapTheme,
  applyGmapTheme,
  triggerGmapResize,
  type GmapThemeHandle
} from '../../../../core/google-maps/gmap-theme';
import {
  createFleetMarkerClusterer,
  type FleetMarkerClusterer
} from '../../../../core/google-maps/gmap-cluster';
import {
  buildFleetVehiclePopup,
  createFleetVehicleMarkerElement,
  resolveReplayStatus
} from '../../../../core/google-maps/fleet-vehicle-marker.gmap';
import { GoogleMapsLoaderService } from '../../../../core/services/google-maps-loader.service';
import { GpsTrackingService } from '../../../../core/services/gps-tracking.service';
import { splitDisplayAddress } from '../../utils/gps-address.util';

@Component({
  standalone: false,
  selector: 'app-trip-replay-map',
  templateUrl: './trip-replay-map.component.html',
  styleUrls: ['./trip-replay-map.component.scss']
})
export class TripReplayMapComponent implements AfterViewInit, OnChanges, OnDestroy {
  @ViewChild('mapEl', { static: false }) mapEl!: ElementRef<HTMLDivElement>;
  @Input() routePoints: TripReplayPosition[] = [];
  @Input() positions: TripReplayPosition[] = [];
  @Input() rawPoints: TripReplayPosition[] = [];
  @Input() stops: TripStop[] = [];
  @Input() events: TripEvent[] = [];
  @Input() loading = false;
  @Input() loadingProgress = 0;
  @Input() loadingTimedOut = false;
  @Input() noData = false;
  @Input() driverName = '';
  @Input() showLayerControls = true;
  @Input() showLegend = false;
  @Input() historyMode = false;
  @Input() mapHeight = '360px';
  @Input() vehicleName = '';
  @Input() plateNumber = '';
  @Input() vehicleType = '';
  @Input() showGpsPointsLayer = false;
  @Input() showStopsLayer = true;
  @Input() showParkingLayer = true;
  @Input() showGeofencesLayer = true;
  @Input() showHeatmap = false;
  @Input() showRouteLayer = true;
  /** Optional Roads API snap overlay (display only — never replaces Traccar coords). */
  @Input() showSnappedLayer = false;
  @Output() positionSelected = new EventEmitter<TripReplayPosition>();
  @Output() retryRequested = new EventEmitter<void>();

  replayPlaying = false;
  replaySpeed = 1;
  followVehicle = true;
  showGpsPoints = false;
  mapTheme: MapTheme = readStoredMapTheme();
  mapThemeMenuOpen = false;
  readonly mapThemeOptions = MAP_THEME_OPTIONS;
  readonly speedOptions = [0.5, 1, 2, 4, 8, 16];
  readonly historySpeedOptions = [0.5, 1, 2, 4, 8, 16];
  replayIndex = 0;

  private map: google.maps.Map | null = null;
  private themeHandle: GmapThemeHandle | null = null;
  private infoWindow: google.maps.InfoWindow | null = null;
  private plannedPolyline: google.maps.Polyline | null = null;
  private actualPolyline: google.maps.Polyline | null = null;
  private snappedPolyline: google.maps.Polyline | null = null;
  private heatmapPolylines: google.maps.Polyline[] = [];
  private overlayMarkers: google.maps.marker.AdvancedMarkerElement[] = [];
  private overlayListeners: google.maps.MapsEventListener[] = [];
  private pointsCluster: FleetMarkerClusterer | null = null;
  private gpsPointMarkers: google.maps.marker.AdvancedMarkerElement[] = [];
  private gpsPointListeners: google.maps.MapsEventListener[] = [];
  private replayMarker: google.maps.marker.AdvancedMarkerElement | null = null;
  private replayClickListener: google.maps.MapsEventListener | null = null;
  private replayTimer?: ReturnType<typeof setInterval>;
  private segmentDistances: number[] = [];
  private scrubDebounce?: ReturnType<typeof setTimeout>;
  private mapReady = false;
  private pendingRender = false;

  constructor(
    private googleMapsLoader: GoogleMapsLoaderService,
    private gpsTracking: GpsTrackingService,
    private cdr: ChangeDetectorRef
  ) {}

  get replayProgress(): number {
    if (!this.positions.length) return 0;
    return Math.round((this.replayIndex / Math.max(1, this.positions.length - 1)) * 100);
  }

  get currentPosition(): TripReplayPosition | null {
    return this.positions[this.replayIndex] ?? null;
  }

  addressPrimary(address?: string | null): string {
    return splitDisplayAddress(address).primary || address?.trim() || 'Address unavailable';
  }

  addressSecondary(address?: string | null): string | null {
    return splitDisplayAddress(address).secondary;
  }

  get startTimeLabel(): string {
    return this.positions[0]
      ? new Date(this.positions[0].timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
      : '—';
  }

  get endTimeLabel(): string {
    const last = this.positions[this.positions.length - 1];
    return last
      ? new Date(last.timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
      : '—';
  }

  get midTimeLabel(): string {
    if (this.positions.length < 2) return '—';
    const mid = this.positions[Math.floor(this.positions.length / 2)];
    return new Date(mid.timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }

  get totalDistanceKm(): number {
    return this.segmentDistances.reduce((a, b) => a + b, 0);
  }

  get distanceTravelledKm(): number {
    return this.segmentDistances.slice(0, this.replayIndex).reduce((a, b) => a + b, 0);
  }

  get elapsedLabel(): string {
    if (!this.positions.length) return '0:00';
    const start = new Date(this.positions[0].timestamp).getTime();
    const cur = new Date(this.positions[this.replayIndex].timestamp).getTime();
    return this.formatMs(cur - start);
  }

  get totalDurationLabel(): string {
    if (this.positions.length < 2) return '0:00';
    const start = new Date(this.positions[0].timestamp).getTime();
    const end = new Date(this.positions[this.positions.length - 1].timestamp).getTime();
    return this.formatMs(end - start);
  }

  get jumpInputValue(): string {
    const p = this.currentPosition;
    if (!p) return '';
    const d = new Date(p.timestamp);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  get jumpDateValue(): string {
    const p = this.currentPosition;
    if (!p) return '';
    const d = new Date(p.timestamp);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  }

  get jumpTimeValue(): string {
    const p = this.currentPosition;
    if (!p) return '';
    const d = new Date(p.timestamp);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
  }

  get routeSummary() {
    if (!this.positions.length) return null;
    const speeds = this.positions.map(p => Number(p.speedKmh) || 0);
    const maxSpeed = Math.max(...speeds, 0);
    const avgSpeed = speeds.length ? speeds.reduce((a, b) => a + b, 0) / speeds.length : 0;
    const first = new Date(this.positions[0].timestamp).getTime();
    const last = new Date(this.positions[this.positions.length - 1].timestamp).getTime();
    return {
      distanceKm: Math.round(this.totalDistanceKm * 10) / 10,
      durationMinutes: Math.max(1, Math.round((last - first) / 60000)),
      avgSpeed: Math.round(avgSpeed),
      maxSpeed: Math.round(maxSpeed),
      pointCount: this.positions.length
    };
  }

  get activeSpeedOptions(): number[] {
    return this.historyMode ? this.historySpeedOptions : this.speedOptions;
  }

  get timelineTicks(): string[] {
    if (this.positions.length < 2) return [];
    const n = Math.min(5, this.positions.length);
    const ticks: string[] = [];
    for (let i = 0; i < n; i++) {
      const idx = Math.floor((i / (n - 1)) * (this.positions.length - 1));
      ticks.push(
        new Date(this.positions[idx].timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
      );
    }
    return ticks;
  }

  get parkingStops(): TripStop[] {
    return this.stops.filter(s => s.durationMinutes >= 120);
  }

  ngAfterViewInit(): void {
    setTimeout(() => void this.initMap(), 50);
  }

  ngOnChanges(changes: SimpleChanges): void {
    const dataChange =
      changes['positions'] ||
      changes['routePoints'] ||
      changes['stops'] ||
      changes['events'] ||
      changes['rawPoints'] ||
      changes['showGpsPointsLayer'] ||
      changes['showStopsLayer'] ||
      changes['showParkingLayer'] ||
      changes['showGeofencesLayer'] ||
      changes['showHeatmap'] ||
      changes['showRouteLayer'] ||
      changes['showSnappedLayer'];
    if (dataChange && this.mapReady) {
      this.stopAndReset();
      requestAnimationFrame(() => this.renderRoute());
    } else if (dataChange && this.map) {
      this.pendingRender = true;
    }
  }

  requestRetry(): void {
    this.retryRequested.emit();
  }

  ngOnDestroy(): void {
    this.stopReplayTimer();
    if (this.scrubDebounce) clearTimeout(this.scrubDebounce);
    this.teardownMap();
  }

  toggleReplay(): void {
    if (this.replayPlaying) {
      this.stopReplayTimer();
      return;
    }
    if (!this.positions.length) return;
    if (this.replayIndex >= this.positions.length - 1) {
      this.replayIndex = 0;
    }
    this.replayPlaying = true;
    const stepMs = 350 / this.replaySpeed;
    this.replayTimer = setInterval(() => this.advanceReplay(), stepMs);
  }

  stopAndReset(): void {
    this.stopReplayTimer();
    this.replayIndex = 0;
    this.updateReplayMarker();
  }

  jumpToStart(): void {
    this.replayIndex = 0;
    this.updateReplayMarker();
  }

  jumpToEnd(): void {
    if (!this.positions.length) return;
    this.replayIndex = this.positions.length - 1;
    this.updateReplayMarker();
  }

  /** Seek playback to the nearest point for a stop and center the map. */
  focusStop(lat: number, lng: number, at?: string | Date): void {
    if (!this.positions.length) return;
    this.stopReplayTimer();
    if (at) {
      this.jumpToTimestamp(new Date(at).getTime());
    } else {
      let best = 0;
      let bestDist = Number.MAX_SAFE_INTEGER;
      for (let i = 0; i < this.positions.length; i++) {
        const p = this.positions[i];
        const dLat = p.latitude - lat;
        const dLng = p.longitude - lng;
        const dist = dLat * dLat + dLng * dLng;
        if (dist < bestDist) {
          bestDist = dist;
          best = i;
        }
      }
      this.replayIndex = best;
      this.updateReplayMarker();
    }
    if (this.map) {
      this.map.panTo({ lat, lng });
      const z = this.map.getZoom() ?? 11;
      if (z < 15) this.map.setZoom(15);
    }
  }

  stepBack(): void {
    if (this.replayIndex > 0) {
      this.replayIndex--;
      this.updateReplayMarker();
    }
  }

  stepForward(): void {
    if (this.replayIndex < this.positions.length - 1) {
      this.replayIndex++;
      this.updateReplayMarker();
    }
  }

  setReplaySpeed(mult: number): void {
    this.replaySpeed = mult;
    if (this.replayPlaying) {
      this.stopReplayTimer();
      this.toggleReplay();
    }
  }

  onReplayScrub(event: Event): void {
    const val = Number((event.target as HTMLInputElement).value);
    if (!this.positions.length) return;
    const idx = Math.min(
      this.positions.length - 1,
      Math.floor((val / 100) * (this.positions.length - 1))
    );
    if (this.scrubDebounce) clearTimeout(this.scrubDebounce);
    this.scrubDebounce = setTimeout(() => {
      this.replayIndex = idx;
      this.updateReplayMarker();
    }, 50);
  }

  onJumpToTime(event: Event): void {
    const raw = (event.target as HTMLInputElement).value;
    if (!raw || !this.positions.length) return;
    this.jumpToTimestamp(new Date(raw).getTime());
  }

  onJumpDate(event: Event): void {
    const datePart = (event.target as HTMLInputElement).value;
    if (!datePart || !this.positions.length) return;
    const timePart = this.jumpTimeValue || '00:00:00';
    this.jumpToTimestamp(new Date(`${datePart}T${timePart}`).getTime());
  }

  onJumpTime(event: Event): void {
    const timePart = (event.target as HTMLInputElement).value;
    if (!timePart || !this.positions.length) return;
    const datePart = this.jumpDateValue || this.jumpInputValue.slice(0, 10);
    this.jumpToTimestamp(new Date(`${datePart}T${timePart}`).getTime());
  }

  private jumpToTimestamp(target: number): void {
    if (!Number.isFinite(target) || !this.positions.length) return;
    let best = 0;
    let bestDiff = Number.MAX_SAFE_INTEGER;
    for (let i = 0; i < this.positions.length; i++) {
      const diff = Math.abs(new Date(this.positions[i].timestamp).getTime() - target);
      if (diff < bestDiff) {
        bestDiff = diff;
        best = i;
      }
    }
    this.replayIndex = best;
    this.updateReplayMarker();
  }

  toggleGpsPoints(): void {
    this.showGpsPoints = !this.showGpsPoints;
    this.renderGpsPointsLayer();
  }

  toggleMapThemeMenu(): void {
    this.mapThemeMenuOpen = !this.mapThemeMenuOpen;
  }

  selectMapTheme(theme: MapTheme): void {
    this.mapThemeMenuOpen = false;
    void this.setMapTheme(theme);
  }

  async setMapTheme(theme: MapTheme): Promise<void> {
    this.mapTheme = theme;
    storeMapTheme(theme);
    if (!this.map) return;
    this.themeHandle = applyGmapTheme(this.map, theme, this.themeHandle);
    triggerGmapResize(this.map);
  }

  private async initMap(): Promise<void> {
    if (!this.mapEl?.nativeElement || this.map) return;

    try {
      const bootstrapped = await this.googleMapsLoader.load();
      if (!bootstrapped) return;

      await this.googleMapsLoader.importLibrary('maps');
      await this.googleMapsLoader.importLibrary('marker');

      if (this.googleMapsLoader.authFailed) return;

      const mapOptions: google.maps.MapOptions = {
        center: { lat: 31.52, lng: 74.35 },
        zoom: 11,
        maxZoom: 20,
        mapTypeControl: false,
        streetViewControl: false,
        fullscreenControl: false,
        zoomControl: true,
        mapId: (environment as { googleMapsMapId?: string }).googleMapsMapId?.trim() || 'DEMO_MAP_ID'
      };

      this.map = new google.maps.Map(this.mapEl.nativeElement, mapOptions);
      this.infoWindow = new google.maps.InfoWindow();
      this.pointsCluster = createFleetMarkerClusterer(this.map, []);
      this.themeHandle = applyGmapTheme(this.map, this.mapTheme, this.themeHandle);
      this.mapReady = true;
      this.rebuildSegmentDistances();
      this.renderRoute();
      if (this.pendingRender) {
        this.pendingRender = false;
        this.renderRoute();
      }
      requestAnimationFrame(() => triggerGmapResize(this.map));
    } catch {
      this.map = null;
      this.mapReady = false;
    }
  }

  private teardownMap(): void {
    this.clearRouteOverlays();
    this.clearGpsPoints();
    this.clearReplayMarker();
    this.infoWindow?.close();
    this.infoWindow = null;
    this.themeHandle?.trafficLayer?.setMap(null);
    this.themeHandle = null;
    this.pointsCluster?.clearMarkers();
    this.pointsCluster = null;
    this.map = null;
    this.mapReady = false;
  }

  private clearRouteOverlays(): void {
    this.plannedPolyline?.setMap(null);
    this.plannedPolyline = null;
    this.actualPolyline?.setMap(null);
    this.actualPolyline = null;
    this.snappedPolyline?.setMap(null);
    this.snappedPolyline = null;
    this.heatmapPolylines.forEach(p => p.setMap(null));
    this.heatmapPolylines = [];
    this.overlayListeners.forEach(l => l.remove());
    this.overlayListeners = [];
    this.overlayMarkers.forEach(m => {
      m.map = null;
    });
    this.overlayMarkers = [];
  }

  private clearGpsPoints(): void {
    this.gpsPointListeners.forEach(l => l.remove());
    this.gpsPointListeners = [];
    if (this.pointsCluster && this.gpsPointMarkers.length) {
      this.pointsCluster.removeMarkers(this.gpsPointMarkers);
    }
    this.gpsPointMarkers.forEach(m => {
      m.map = null;
    });
    this.gpsPointMarkers = [];
  }

  private clearReplayMarker(): void {
    this.replayClickListener?.remove();
    this.replayClickListener = null;
    if (this.replayMarker) {
      this.replayMarker.map = null;
      this.replayMarker = null;
    }
  }

  private rebuildSegmentDistances(): void {
    this.segmentDistances = [];
    for (let i = 1; i < this.positions.length; i++) {
      const a = this.positions[i - 1];
      const b = this.positions[i];
      this.segmentDistances.push(this.haversineKm(a.latitude, a.longitude, b.latitude, b.longitude));
    }
  }

  private toLatLng(p: TripReplayPosition): google.maps.LatLngLiteral {
    return { lat: p.latitude, lng: p.longitude };
  }

  /** Roads snap overlay — display only; raw Traccar positions stay the source of truth. */
  private loadSnappedOverlay(points: TripReplayPosition[]): void {
    if (!this.map || points.length < 5) return;
    const sample = this.downsampleForDraw(points, 100);
    this.gpsTracking
      .snapGpsPath(sample.map(p => ({ lat: p.latitude, lng: p.longitude })))
      .subscribe({
        next: snapped => {
          if (!this.map || !this.showSnappedLayer || !snapped.length) return;
          this.snappedPolyline?.setMap(null);
          this.snappedPolyline = new google.maps.Polyline({
            map: this.map,
            path: snapped.map(p => ({ lat: p.lat, lng: p.lng })),
            strokeColor: '#f59e0b',
            strokeOpacity: 0.85,
            strokeWeight: 4,
            geodesic: true,
            zIndex: 4
          });
        },
        error: () => {
          /* Roads optional — ignore failures */
        }
      });
  }

  private renderRoute(): void {
    if (!this.map || !this.mapReady) return;

    this.clearRouteOverlays();
    this.clearReplayMarker();
    this.clearGpsPoints();
    this.rebuildSegmentDistances();

    const planned = this.routePoints;
    const actual = this.positions;
    const hasPlanned = planned.length > 0;
    const hasActual = actual.length > 0;
    if (!hasPlanned && !hasActual) return;

    const drawPlanned = this.downsampleForDraw(planned, 800);
    const drawActual = this.downsampleForDraw(actual.length ? actual : planned, 800);

    if (this.showRouteLayer) {
      // Planned route — dashed slate, visually distinct from the live GPS trail.
      if (hasPlanned && hasActual) {
        // Dashed via icons; solid stroke hidden so planned stays distinct from GPS trail.
        this.plannedPolyline = new google.maps.Polyline({
          map: this.map,
          path: drawPlanned.map(p => this.toLatLng(p)),
          strokeColor: '#64748b',
          strokeOpacity: 0,
          strokeWeight: 4,
          geodesic: true,
          zIndex: 1,
          icons: [
            {
              icon: {
                path: 'M 0,-1 0,1',
                strokeOpacity: 1,
                strokeColor: '#64748b',
                scale: 3
              },
              offset: '0',
              repeat: '12px'
            }
          ]
        });
      } else if (hasPlanned && !hasActual) {
        this.plannedPolyline = new google.maps.Polyline({
          map: this.map,
          path: drawPlanned.map(p => this.toLatLng(p)),
          strokeColor: '#1d4ed8',
          strokeOpacity: 1,
          strokeWeight: 7,
          geodesic: true,
          zIndex: 2
        });
      }

      // Actual GPS track (or sole path when no separate planned route).
      if (hasActual) {
        if (this.showHeatmap) {
          this.drawSpeedHeatmap(drawActual);
        } else {
          this.actualPolyline = new google.maps.Polyline({
            map: this.map,
            path: drawActual.map(p => this.toLatLng(p)),
            strokeColor: '#1d4ed8',
            strokeOpacity: 1,
            strokeWeight: 7,
            geodesic: true,
            zIndex: 3
          });
        }
        if (this.showSnappedLayer) {
          this.loadSnappedOverlay(drawActual);
        }
      }

      const endpointSource = hasActual ? actual : planned;
      const start = endpointSource[0];
      const end = endpointSource[endpointSource.length - 1];
      this.addEndpointMarker(start, 'Start', '#059669', 'flag-start');
      this.addEndpointMarker(end, 'End', '#dc2626', 'flag-end');
    }

    if (this.showGeofencesLayer) {
      this.events.slice(0, 40).forEach(evt => {
        if (evt.latitude == null || evt.longitude == null) return;
        const isGeofence = evt.type.toLowerCase().includes('geofence');
        this.addDotMarker(
          evt.latitude,
          evt.longitude,
          isGeofence ? 7 : 6,
          isGeofence ? '#7c3aed' : '#f59e0b',
          isGeofence ? '#a78bfa' : '#fbbf24',
          `<strong>${evt.label ?? evt.type}</strong><br>${new Date(evt.time).toLocaleString()}`
        );
      });
    }

    this.stops.slice(0, 40).forEach(stop => {
      const isParking = stop.durationMinutes >= 120;
      if (isParking && !this.showParkingLayer) return;
      if (!isParking && !this.showStopsLayer) return;
      const kind = isParking ? 'Parking' : 'Stop';
      const lines = splitDisplayAddress(stop.address);
      const primary = lines.primary || stop.address?.trim() || 'Address unavailable';
      const secondary = lines.secondary
        ? `<br><span style="color:#64748b">${lines.secondary}</span>`
        : '';
      const when = new Date(stop.startTime).toLocaleTimeString(undefined, {
        hour: 'numeric',
        minute: '2-digit'
      });
      this.addDotMarker(
        stop.latitude,
        stop.longitude,
        isParking ? 8 : 7,
        isParking ? '#1d4ed8' : '#ca8a04',
        isParking ? '#93c5fd' : '#fde047',
        `<strong>${kind} — ${primary}</strong>${secondary}<br>${when} · ${stop.durationMinutes} min`
      );
    });

    const playback = this.positions.length ? this.positions : planned;
    if (playback.length) {
      this.replayMarker = this.createVehicleMarker(playback[0]);
    }

    this.renderGpsPointsLayer();

    const fitPts = drawActual.length ? drawActual : drawPlanned;
    if (fitPts.length) {
      const bounds = new google.maps.LatLngBounds();
      fitPts.forEach(p => bounds.extend(this.toLatLng(p)));
      this.map.fitBounds(bounds, { top: 48, right: 48, bottom: 48, left: 48 });
      const z = this.map.getZoom();
      if (z != null && z > 15) this.map.setZoom(15);
    }
  }

  private drawSpeedHeatmap(points: TripReplayPosition[]): void {
    if (!this.map || points.length < 2) return;
    let batch: google.maps.LatLngLiteral[] = [this.toLatLng(points[0])];
    let color = this.speedColor(Number(points[1].speedKmh) || 0);

    for (let i = 1; i < points.length; i++) {
      const nextColor = this.speedColor(Number(points[i].speedKmh) || 0);
      batch.push(this.toLatLng(points[i]));
      if (nextColor !== color || i === points.length - 1) {
        if (batch.length >= 2) {
          const line = new google.maps.Polyline({
            map: this.map,
            path: batch,
            strokeColor: color,
            strokeOpacity: 0.9,
            strokeWeight: 5,
            geodesic: true,
            zIndex: 3
          });
          this.heatmapPolylines.push(line);
        }
        batch = [this.toLatLng(points[i])];
        color = nextColor;
      }
    }
  }

  private speedColor(speedKmh: number): string {
    if (speedKmh < 30) return '#22c55e';
    if (speedKmh <= 60) return '#eab308';
    return '#ef4444';
  }

  private addEndpointMarker(
    p: TripReplayPosition,
    label: string,
    color: string,
    kind: 'flag-start' | 'flag-end' = 'flag-start'
  ): void {
    if (!this.map) return;
    const time = new Date(p.timestamp).toLocaleString(undefined, {
      day: '2-digit',
      month: 'short',
      hour: '2-digit',
      minute: '2-digit'
    });
    const address = p.address?.trim() || `${p.latitude.toFixed(5)}, ${p.longitude.toFixed(5)}`;
    const lines = splitDisplayAddress(p.address);
    const primary = lines.primary || address;
    const secondary = lines.secondary
      ? `<br><span style="color:#64748b">${lines.secondary}</span>`
      : '';
    const glyph = kind === 'flag-start' ? '▶' : '🏁';
    const el = document.createElement('div');
    el.className = 'replay-endpoint-wrap';
    el.innerHTML = `<div class="replay-flag" style="--flag:${color}">
        <span class="replay-flag__pin">${glyph}</span>
        <span class="replay-flag__meta">
          <strong>${label}</strong>
          <small>${time}</small>
        </span>
      </div>`;

    const marker = new google.maps.marker.AdvancedMarkerElement({
      map: this.map,
      position: this.toLatLng(p),
      content: el,
      zIndex: 1200,
      gmpClickable: true,
      title: label
    });
    const popup =
      `<strong>${label}</strong><br>${time}<br><strong>${primary}</strong>${secondary}<br>` +
      `<span style="color:#94a3b8;font-size:11px">${p.latitude.toFixed(5)}, ${p.longitude.toFixed(5)}</span><br>` +
      `${Number(p.speedKmh ?? 0).toFixed(0)} km/h`;
    const listener = marker.addListener('click', () => this.openInfo(popup, marker));
    this.overlayListeners.push(listener);
    this.overlayMarkers.push(marker);
  }

  private addDotMarker(
    lat: number,
    lng: number,
    radius: number,
    stroke: string,
    fill: string,
    popupHtml: string
  ): void {
    if (!this.map) return;
    const size = radius * 2;
    const el = document.createElement('div');
    el.style.cssText =
      `width:${size}px;height:${size}px;border-radius:50%;` +
      `background:${fill};border:2px solid ${stroke};box-sizing:border-box;`;
    const marker = new google.maps.marker.AdvancedMarkerElement({
      map: this.map,
      position: { lat, lng },
      content: el,
      gmpClickable: true,
      zIndex: 500
    });
    const listener = marker.addListener('click', () => this.openInfo(popupHtml, marker));
    this.overlayListeners.push(listener);
    this.overlayMarkers.push(marker);
  }

  private openInfo(html: string, anchor: google.maps.marker.AdvancedMarkerElement): void {
    if (!this.infoWindow || !this.map) return;
    this.infoWindow.setContent(html);
    this.infoWindow.open({ map: this.map, anchor });
  }

  private renderGpsPointsLayer(): void {
    if (!this.map || !this.pointsCluster) return;
    this.clearGpsPoints();

    const showPts = this.historyMode ? this.showGpsPointsLayer : this.showGpsPoints;
    if (!showPts) return;

    const pts = this.rawPoints.length
      ? this.rawPoints
      : this.routePoints.length
        ? this.routePoints
        : this.positions;
    const sample = pts.length > 500 ? this.downsampleForDraw(pts, 1200) : pts;

    sample.forEach(p => {
      const el = document.createElement('div');
      el.style.cssText =
        'width:6px;height:6px;border-radius:50%;background:#14b8a6;border:1px solid #0f766e;opacity:0.85;';
      const marker = new google.maps.marker.AdvancedMarkerElement({
        position: this.toLatLng(p),
        content: el,
        gmpClickable: true,
        title: 'GPS point'
      });
      const listener = marker.addListener('click', () => {
        this.openInfo(this.popupHtml(p, 'GPS point'), marker);
        this.positionSelected.emit(p);
      });
      this.gpsPointListeners.push(listener);
      this.gpsPointMarkers.push(marker);
    });

    if (this.gpsPointMarkers.length) {
      this.pointsCluster.addMarkers(this.gpsPointMarkers);
    }
  }

  private createVehicleMarker(p: TripReplayPosition): google.maps.marker.AdvancedMarkerElement {
    const content = this.buildVehicleContent(p);
    const marker = new google.maps.marker.AdvancedMarkerElement({
      map: this.map,
      position: this.toLatLng(p),
      content,
      zIndex: 1000,
      gmpClickable: true,
      title: this.vehicleName || this.driverName || 'Vehicle'
    });
    this.replayClickListener = marker.addListener('click', () => {
      const row = this.currentPosition ?? p;
      this.openInfo(this.vehiclePopupHtml(row), marker);
      this.positionSelected.emit(row);
    });
    return marker;
  }

  private buildVehicleContent(p: TripReplayPosition): HTMLElement {
    const status = resolveReplayStatus(p.speedKmh, p.ignition);
    return createFleetVehicleMarkerElement({
      status,
      heading: p.heading ?? 0,
      vehicleType: this.vehicleType,
      size: 32,
      selected: true,
      pulse: false
    });
  }

  private vehiclePopupHtml(p: TripReplayPosition): string {
    const status = resolveReplayStatus(p.speedKmh, p.ignition);
    const statusLabels: Record<string, string> = {
      moving: 'Moving',
      idle: 'Idle',
      parked: 'Parked',
      offline: 'Offline',
      never_seen: 'No GPS',
      sos: 'SOS',
      scheduled: 'Scheduled',
      delayed: 'Delayed'
    };
    return buildFleetVehiclePopup({
      name: this.vehicleName || this.driverName || 'Vehicle',
      plate: this.plateNumber || null,
      driver: this.driverName || null,
      ignition: p.ignition,
      speedKmh: p.speedKmh,
      headingLabel: p.heading != null ? `${Math.round(p.heading)}°` : null,
      address: p.address,
      lastPing: new Date(p.timestamp).toLocaleString(),
      statusLabel: statusLabels[status] ?? status
    });
  }

  private downsampleForDraw(positions: TripReplayPosition[], maxPoints: number): TripReplayPosition[] {
    if (positions.length <= maxPoints) return positions;
    const step = Math.ceil(positions.length / maxPoints);
    const result: TripReplayPosition[] = [];
    for (let i = 0; i < positions.length; i += step) {
      result.push(positions[i]);
    }
    const last = positions[positions.length - 1];
    if (result[result.length - 1] !== last) result.push(last);
    return result;
  }

  private popupHtml(p: TripReplayPosition, label: string): string {
    return `<strong>${label}</strong><br>
      ${new Date(p.timestamp).toLocaleString()}<br>
      ${p.speedKmh} km/h · ${p.latitude.toFixed(5)}, ${p.longitude.toFixed(5)}<br>
      ${p.address ?? ''}`;
  }

  private advanceReplay(): void {
    if (this.replayIndex >= this.positions.length - 1) {
      this.stopReplayTimer();
      this.cdr.markForCheck();
      return;
    }
    this.replayIndex++;
    this.updateReplayMarker();
    const pos = this.currentPosition;
    if (pos) this.positionSelected.emit(pos);
    this.cdr.markForCheck();
  }

  private updateReplayMarker(): void {
    const row = this.currentPosition;
    if (!row || !this.map) return;
    if (!this.replayMarker) {
      this.replayMarker = this.createVehicleMarker(row);
    } else {
      this.replayMarker.position = this.toLatLng(row);
      this.replayMarker.content = this.buildVehicleContent(row);
    }
    if (this.followVehicle) {
      this.map.panTo(this.toLatLng(row));
    }
    this.positionSelected.emit(row);
  }

  private stopReplayTimer(): void {
    this.replayPlaying = false;
    if (this.replayTimer) {
      clearInterval(this.replayTimer);
      this.replayTimer = undefined;
    }
  }

  private formatMs(ms: number): string {
    const totalSec = Math.max(0, Math.floor(ms / 1000));
    const h = Math.floor(totalSec / 3600);
    const m = Math.floor((totalSec % 3600) / 60);
    const s = totalSec % 60;
    if (h > 0) {
      return m > 0 ? `${h} hr ${m} min` : `${h} hr`;
    }
    if (m > 0) {
      return s > 0 ? `${m} min ${s} sec` : `${m} min`;
    }
    return `${s} sec`;
  }

  private haversineKm(lat1: number, lon1: number, lat2: number, lon2: number): number {
    const R = 6371;
    const dLat = ((lat2 - lat1) * Math.PI) / 180;
    const dLon = ((lon2 - lon1) * Math.PI) / 180;
    const a =
      Math.sin(dLat / 2) ** 2 +
      Math.cos((lat1 * Math.PI) / 180) * Math.cos((lat2 * Math.PI) / 180) * Math.sin(dLon / 2) ** 2;
    return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  }
}
