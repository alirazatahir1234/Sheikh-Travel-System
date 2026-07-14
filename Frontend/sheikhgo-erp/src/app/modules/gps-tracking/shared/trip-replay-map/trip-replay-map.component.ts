import {
  Component, Input, OnChanges, OnDestroy, AfterViewInit, ElementRef, ViewChild, SimpleChanges, Output, EventEmitter
} from '@angular/core';
import { TripEvent, TripReplayPosition, TripStop } from '../../../../core/models/gps-tracking.model';
import {
  MAP_TILE_STACKS,
  MAP_THEME_OPTIONS,
  MapTheme,
  readStoredMapTheme,
  storeMapTheme
} from '../../../../core/leaflet/leaflet-map-tiles';
import { createMarkerClusterGroup, L } from '../../../../core/leaflet/leaflet-cluster';
import { GoogleTrafficBasemap } from '../../../../core/leaflet/google-traffic-basemap';
import { GoogleMapsLoaderService } from '../../../../core/services/google-maps-loader.service';
import {
  createFleetVehicleDivIcon,
  buildFleetVehiclePopup,
  resolveReplayStatus
} from '../../../../core/leaflet/fleet-vehicle-marker';
import type * as LeafletTypes from 'leaflet';
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
  @Input() showHeatmap = true;
  @Input() showRouteLayer = true;
  @Output() positionSelected = new EventEmitter<TripReplayPosition>();
  @Output() retryRequested = new EventEmitter<void>();

  replayPlaying = false;
  replaySpeed = 1;
  followVehicle = true;
  showGpsPoints = false;
  mapTheme: MapTheme = readStoredMapTheme();
  mapThemeMenuOpen = false;
  readonly mapThemeOptions = MAP_THEME_OPTIONS;
  readonly speedOptions = [0.5, 1, 2, 4, 5, 8, 10, 16];
  readonly historySpeedOptions = [1, 2, 5, 10];
  replayIndex = 0;

  private map: LeafletTypes.Map | null = null;
  private routeLayer: LeafletTypes.LayerGroup | null = null;
  private pointsCluster: ReturnType<typeof createMarkerClusterGroup> | null = null;
  private replayMarker: LeafletTypes.Marker | null = null;
  private replayTimer?: ReturnType<typeof setInterval>;
  private segmentDistances: number[] = [];
  private tileLayer?: LeafletTypes.TileLayer;
  private tileFallbackIndex = 0;
  private readonly trafficBasemap = new GoogleTrafficBasemap();
  private scrubDebounce?: ReturnType<typeof setTimeout>;

  constructor(private googleMapsLoader: GoogleMapsLoaderService) {}

  get replayProgress(): number {
    if (!this.positions.length) return 0;
    return Math.round((this.replayIndex / Math.max(1, this.positions.length - 1)) * 100);
  }

  get currentPosition(): TripReplayPosition | null {
    return this.positions[this.replayIndex] ?? null;
  }

  get startTimeLabel(): string {
    return this.positions[0] ? new Date(this.positions[0].timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '—';
  }

  get endTimeLabel(): string {
    const last = this.positions[this.positions.length - 1];
    return last ? new Date(last.timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '—';
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
    setTimeout(() => this.initMap(), 50);
  }

  ngOnChanges(changes: SimpleChanges): void {
    const dataChange =
      changes['positions'] || changes['routePoints'] || changes['stops'] || changes['events'] ||
      changes['rawPoints'] || changes['showGpsPointsLayer'] || changes['showStopsLayer'] ||
      changes['showParkingLayer'] || changes['showGeofencesLayer'] || changes['showHeatmap'] ||
      changes['showRouteLayer'];
    if (dataChange && this.map) {
      this.stopAndReset();
      requestAnimationFrame(() => this.renderRoute());
    }
  }

  requestRetry(): void {
    this.retryRequested.emit();
  }

  ngOnDestroy(): void {
    this.stopReplayTimer();
    this.trafficBasemap.detach();
    this.map?.remove();
    this.map = null;
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
    this.tileFallbackIndex = 0;
    await this.applyTileLayer(theme);
  }

  private async initMap(): Promise<void> {
    if (!this.mapEl?.nativeElement || this.map) return;
    this.map = L.map(this.mapEl.nativeElement, { zoomControl: true }).setView([31.52, 74.35], 11);
    this.routeLayer = L.layerGroup().addTo(this.map);
    this.pointsCluster = createMarkerClusterGroup({
      maxClusterRadius: 48,
      showCoverageOnHover: false,
      spiderfyOnMaxZoom: true,
      disableClusteringAtZoom: 17,
      animate: true
    });
    await this.applyTileLayer(this.mapTheme);
    this.rebuildSegmentDistances();
    this.renderRoute();
  }

  private async applyTileLayer(theme: MapTheme): Promise<void> {
    if (!this.map) return;

    this.trafficBasemap.detach();

    if (this.tileLayer) {
      this.tileLayer.off();
      this.map.removeLayer(this.tileLayer);
      this.tileLayer = undefined;
    }

    if (theme === 'traffic') {
      const ok = await this.trafficBasemap.attach(this.map, this.googleMapsLoader);
      if (!ok) {
        this.mapTheme = 'street';
        storeMapTheme('street');
        await this.applyTileLayer('street');
      }
      return;
    }

    const stack = MAP_TILE_STACKS[theme];
    const cfg = stack[this.tileFallbackIndex] ?? stack[0];
    this.tileLayer = L.tileLayer(cfg.url, {
      attribution: cfg.attribution,
      subdomains: (cfg.subdomains ?? 'abc') as string,
      maxZoom: cfg.maxZoom ?? 19
    }).addTo(this.map);
  }

  private rebuildSegmentDistances(): void {
    this.segmentDistances = [];
    for (let i = 1; i < this.positions.length; i++) {
      const a = this.positions[i - 1];
      const b = this.positions[i];
      this.segmentDistances.push(this.haversineKm(a.latitude, a.longitude, b.latitude, b.longitude));
    }
  }

  private renderRoute(): void {
    if (!this.routeLayer || !this.map) return;
    const layer = this.routeLayer;
    layer.clearLayers();
    this.replayMarker = null;
    this.rebuildSegmentDistances();

    // Clear GPS clusters before redraw so they never outlive a toggle-off.
    if (this.pointsCluster && this.map.hasLayer(this.pointsCluster)) {
      this.map.removeLayer(this.pointsCluster);
    }
    this.pointsCluster?.clearLayers();

    const pathPoints = this.routePoints.length ? this.routePoints : this.positions;
    if (!pathPoints.length) return;

    // Keep draw layer lean so the polyline stays visible and responsive.
    const drawPoints = this.downsampleForDraw(pathPoints, 800);

    if (this.showRouteLayer) {
      if (this.showHeatmap) {
        this.drawSpeedHeatmap(layer, drawPoints);
      } else {
        L.polyline(
          drawPoints.map(p => [p.latitude, p.longitude] as [number, number]),
          { color: '#2563eb', weight: 5, opacity: 0.9 }
        ).addTo(layer);
      }

      const start = pathPoints[0];
      const end = pathPoints[pathPoints.length - 1];
      this.addEndpointMarker(layer, start, 'START', '#059669');
      this.addEndpointMarker(layer, end, 'END', '#dc2626');
    }

    if (this.showGeofencesLayer) {
      this.events.slice(0, 40).forEach(evt => {
        if (evt.latitude == null || evt.longitude == null) return;
        const isGeofence = evt.type.toLowerCase().includes('geofence');
        L.circleMarker([evt.latitude, evt.longitude], {
          radius: isGeofence ? 7 : 6,
          color: isGeofence ? '#7c3aed' : '#f59e0b',
          fillColor: isGeofence ? '#a78bfa' : '#fbbf24',
          fillOpacity: 0.9
        }).bindPopup(
          `<strong>${evt.label ?? evt.type}</strong><br>${new Date(evt.time).toLocaleString()}`
        ).addTo(layer);
      });
    }

    this.stops.slice(0, 40).forEach(stop => {
      const isParking = stop.durationMinutes >= 120;
      if (isParking && !this.showParkingLayer) return;
      if (!isParking && !this.showStopsLayer) return;
      L.circleMarker([stop.latitude, stop.longitude], {
        radius: isParking ? 8 : 7,
        color: isParking ? '#1d4ed8' : '#ca8a04',
        fillColor: isParking ? '#93c5fd' : '#fde047',
        fillOpacity: 0.95
      }).bindPopup(
        `<strong>${isParking ? 'Parking' : 'Stop'}</strong><br>${stop.durationMinutes} min<br>${stop.address ?? ''}`
      ).addTo(layer);
    });

    const playback = this.positions.length ? this.positions : pathPoints;
    if (playback.length) {
      this.replayMarker = this.createVehicleMarker(playback[0]);
      this.replayMarker.addTo(layer);
    }

    this.renderGpsPointsLayer();

    if (drawPoints.length) {
      const bounds = L.latLngBounds(drawPoints.map(p => [p.latitude, p.longitude] as [number, number]));
      this.map.fitBounds(bounds, { padding: [48, 48], maxZoom: 15 });
    }
  }

  private drawSpeedHeatmap(layer: LeafletTypes.LayerGroup, points: TripReplayPosition[]): void {
    if (points.length < 2) return;
    let batch: [number, number][] = [[points[0].latitude, points[0].longitude]];
    let color = this.speedColor(Number(points[1].speedKmh) || 0);

    for (let i = 1; i < points.length; i++) {
      const nextColor = this.speedColor(Number(points[i].speedKmh) || 0);
      batch.push([points[i].latitude, points[i].longitude]);
      if (nextColor !== color || i === points.length - 1) {
        if (batch.length >= 2) {
          L.polyline(batch, { color, weight: 5, opacity: 0.9 }).addTo(layer);
        }
        // Start next batch from current point so segments stay continuous.
        batch = [[points[i].latitude, points[i].longitude]];
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
    layer: LeafletTypes.LayerGroup,
    p: TripReplayPosition,
    label: string,
    color: string
  ): void {
    const icon = L.divIcon({
      className: 'replay-endpoint-wrap',
      html: `<div class="replay-endpoint" style="background:${color}">${label}</div>`,
      iconSize: [48, 24],
      iconAnchor: [24, 12]
    });
    L.marker([p.latitude, p.longitude], { icon, zIndexOffset: 1200 })
      .bindPopup(this.popupHtml(p, label))
      .addTo(layer);
  }

  private renderGpsPointsLayer(): void {
    if (!this.map || !this.pointsCluster) return;
    if (this.map.hasLayer(this.pointsCluster)) {
      this.map.removeLayer(this.pointsCluster);
    }
    this.pointsCluster.clearLayers();
    const showPts = this.historyMode ? this.showGpsPointsLayer : this.showGpsPoints;
    if (!showPts) return;

    const pts = this.rawPoints.length ? this.rawPoints : this.routePoints.length ? this.routePoints : this.positions;
    const sample = pts.length > 500 ? this.downsampleForDraw(pts, 1200) : pts;
    sample.forEach(p => {
      const marker = L.circleMarker([p.latitude, p.longitude], {
        radius: 3,
        color: '#0f766e',
        fillColor: '#14b8a6',
        fillOpacity: 0.7,
        weight: 1
      });
      marker.bindPopup(this.popupHtml(p, 'GPS point'));
      marker.on('click', () => this.positionSelected.emit(p));
      this.pointsCluster!.addLayer(marker);
    });
    this.map.addLayer(this.pointsCluster);
  }

  private createVehicleMarker(p: TripReplayPosition): LeafletTypes.Marker {
    const status = resolveReplayStatus(p.speedKmh, p.ignition);
    const badge =
      status === 'sos' ? 'sos' :
      p.ignition === true && status !== 'moving' ? 'ignition' :
      status === 'parked' ? 'parked' :
      null;
    const icon = createFleetVehicleDivIcon({
      status,
      heading: p.heading,
      vehicleType: this.vehicleType,
      badge,
      size: 32
    });
    const marker = L.marker([p.latitude, p.longitude], { icon, zIndexOffset: 1000 });
    marker.bindPopup(this.vehiclePopupHtml(p));
    marker.on('click', () => {
      const idx = this.positions.findIndex(x => x.timestamp === p.timestamp);
      if (idx >= 0) {
        this.replayIndex = idx;
        this.updateReplayMarker();
      }
      this.positionSelected.emit(p);
    });
    return marker;
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
      return;
    }
    this.replayIndex++;
    this.updateReplayMarker();
    const pos = this.currentPosition;
    if (pos) this.positionSelected.emit(pos);
  }

  private updateReplayMarker(): void {
    const row = this.currentPosition;
    if (!row || !this.replayMarker) return;
    this.replayMarker.setLatLng([row.latitude, row.longitude]);
    const status = resolveReplayStatus(row.speedKmh, row.ignition);
    const badge =
      row.ignition === true && status !== 'moving' ? 'ignition' :
      status === 'parked' ? 'parked' :
      null;
    this.replayMarker.setIcon(createFleetVehicleDivIcon({
      status,
      heading: row.heading,
      vehicleType: this.vehicleType,
      badge,
      size: 32
    }));
    this.replayMarker.bindPopup(this.vehiclePopupHtml(row));
    if (this.followVehicle && this.map) {
      this.map.panTo([row.latitude, row.longitude], { animate: true, duration: 0.25 });
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
    const m = Math.floor(totalSec / 60);
    const s = totalSec % 60;
    return `${m}:${String(s).padStart(2, '0')}`;
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
