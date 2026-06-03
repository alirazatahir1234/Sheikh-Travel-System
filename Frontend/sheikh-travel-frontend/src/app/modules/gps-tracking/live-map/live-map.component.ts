import {
  Component,
  OnInit,
  OnDestroy,
  AfterViewInit,
  ElementRef,
  ViewChild,
  HostListener
} from '@angular/core';
import { Router } from '@angular/router';
import type * as LeafletTypes from 'leaflet';
import {
  createMarkerClusterGroup,
  L,
  loadMarkerClusterPlugin
} from '../../../core/leaflet/leaflet-cluster';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { GpsRealtimeService } from '../../../core/services/gps-realtime.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import {
  VehicleLocation,
  PositionDto,
  TrackingDto,
  FleetTrackStatus
} from '../../../core/models/gps-tracking.model';
import { Vehicle } from '../../../core/models/vehicle.model';

type StatusFilter = 'all' | FleetTrackStatus;
type MapTheme = 'dark' | 'light' | 'satellite';
type TimePreset = 'today' | '24h' | '7d' | 'custom';

interface TrackEvent {
  time: Date;
  message: string;
  type: 'info' | 'alert' | 'success' | 'warning';
  icon: string;
}

interface TripSummary {
  distanceKm: number;
  avgSpeed: number;
  stopMinutes: number;
  durationMinutes: number;
  pointCount: number;
}

const MAP_TILES: Record<MapTheme, { url: string; attribution: string }> = {
  dark: {
    url: 'https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png',
    attribution: '&copy; OpenStreetMap &copy; CARTO'
  },
  light: {
    url: 'https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png',
    attribution: '&copy; OpenStreetMap &copy; CARTO'
  },
  satellite: {
    url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
    attribution: '&copy; Esri'
  }
};

const TRAIL_COLORS: Record<FleetTrackStatus, string> = {
  moving: '#10B981',
  idle: '#F59E0B',
  delayed: '#EF4444',
  offline: '#64748B',
  scheduled: '#3B82F6'
};

@Component({
  selector: 'app-live-map',
  templateUrl: './live-map.component.html',
  styleUrls: ['./live-map.component.scss']
})
export class LiveMapComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('mapHost') mapHost?: ElementRef<HTMLElement>;

  private map!: LeafletTypes.Map;
  private tileLayer?: LeafletTypes.TileLayer;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private markerCluster!: any;
  private markers = new Map<number, LeafletTypes.Marker>();
  private trailLayers = new Map<number, LeafletTypes.Polyline>();
  private prevPositions = new Map<number, { lat: number; lng: number }>();
  private positionTrails = new Map<number, [number, number][]>();
  private historyPolyline?: LeafletTypes.Polyline;
  private historyMarker?: LeafletTypes.Marker;
  private refreshInterval?: ReturnType<typeof setInterval>;
  private replayTimer?: ReturnType<typeof setInterval>;
  private replayIndex = 0;
  private readonly maxTrailPoints = 14;

  locations: VehicleLocation[] = [];
  loading = true;
  error: string | null = null;
  searchQuery = '';
  statusFilter: StatusFilter = 'all';
  timePreset: TimePreset = 'today';
  mapTheme: MapTheme = 'dark';
  liveTracking = true;
  selectedVehicleId: number | null = null;
  lastSyncAt: Date | null = null;
  secondsSinceSync = 0;
  isMapFullscreen = false;
  tripSummary: TripSummary | null = null;
  private syncTick?: ReturnType<typeof setInterval>;

  vehicles: Vehicle[] = [];
  historyFrom = '';
  historyTo = '';
  historyRows: TrackingDto[] = [];
  loadingHistory = false;
  historyError = '';
  showHistory = false;
  replayPlaying = false;
  replaySpeed = 1;
  events: TrackEvent[] = [];

  readonly statusFilters: { id: StatusFilter; label: string }[] = [
    { id: 'all', label: 'All' },
    { id: 'moving', label: 'Active' },
    { id: 'idle', label: 'Idle' },
    { id: 'offline', label: 'Offline' },
    { id: 'delayed', label: 'Delayed' },
    { id: 'scheduled', label: 'Scheduled' }
  ];

  readonly timePresets: { id: TimePreset; label: string }[] = [
    { id: 'today', label: 'Today' },
    { id: '24h', label: 'Last 24h' },
    { id: '7d', label: 'Last 7d' },
    { id: 'custom', label: 'Custom' }
  ];

  geofenceBreachCount = 0;
  private realtimeSub?: { unsubscribe(): void };

  constructor(
    private gpsService: GpsTrackingService,
    private realtime: GpsRealtimeService,
    private vehicleService: VehicleService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.applyTimePreset('today');
    this.vehicleService.getAll(1, 500).subscribe({
      next: r => { this.vehicles = r.items; },
      error: () => {}
    });
    this.gpsService.getGeofenceBreachCount().subscribe({
      next: c => { this.geofenceBreachCount = c; },
      error: () => {}
    });
    void this.realtime.connect().catch(() => {
      this.pushEvent('Realtime unavailable — using polling', 'warning', 'wifi_off');
    });
    this.realtimeSub = this.realtime.locationUpdates$.subscribe(update => {
      this.applyRealtimeUpdate(update);
    });
    this.syncTick = setInterval(() => {
      if (this.lastSyncAt) {
        this.secondsSinceSync = Math.floor((Date.now() - this.lastSyncAt.getTime()) / 1000);
      }
    }, 1000);
    this.pushEvent('Tracking console ready', 'info', 'gps_fixed');
  }

  ngAfterViewInit(): void {
    setTimeout(() => {
      void loadMarkerClusterPlugin()
        .then(() => {
          this.initMap();
          this.loadLocations();
          this.refreshInterval = setInterval(() => {
            if (this.liveTracking) this.loadLocations(true);
          }, 30000);
        })
        .catch(() => {
          this.error = 'Map clustering failed to load. Refresh the page.';
        });
    }, 0);
  }

  ngOnDestroy(): void {
    if (this.refreshInterval) clearInterval(this.refreshInterval);
    if (this.syncTick) clearInterval(this.syncTick);
    this.realtimeSub?.unsubscribe();
    void this.realtime.disconnect();
    this.stopReplay();
    if (this.map) this.map.remove();
  }

  @HostListener('document:fullscreenchange')
  onFullscreenChange(): void {
    this.isMapFullscreen = !!document.fullscreenElement;
    setTimeout(() => this.map?.invalidateSize(), 200);
  }

  get filteredLocations(): VehicleLocation[] {
    const q = this.searchQuery.trim().toLowerCase();
    return this.locations.filter(loc => {
      if (this.statusFilter !== 'all' && loc.status !== this.statusFilter) return false;
      if (!q) return true;
      return (
        loc.vehicleName.toLowerCase().includes(q) ||
        loc.registrationNumber.toLowerCase().includes(q) ||
        (loc.driverName?.toLowerCase().includes(q) ?? false)
      );
    });
  }

  get statusCounts(): Record<FleetTrackStatus | 'all', number> {
    const gps = this.locations.filter(l => l.hasGps);
    return {
      all: this.locations.length,
      moving: gps.filter(l => l.status === 'moving').length,
      idle: gps.filter(l => l.status === 'idle').length,
      offline: this.locations.filter(l => l.status === 'offline').length,
      delayed: gps.filter(l => l.status === 'delayed').length,
      scheduled: this.locations.filter(l => l.status === 'scheduled').length
    };
  }

  get liveStats() {
    const gps = this.locations.filter(l => l.hasGps);
    const moving = gps.filter(l => l.status === 'moving');
    const speeds = moving.map(l => l.speed).filter(s => s > 0);
    const avg = speeds.length ? speeds.reduce((a, b) => a + b, 0) / speeds.length : 0;
    return {
      online: gps.filter(l => l.status !== 'offline').length,
      tripsActive: moving.length,
      avgSpeed: Math.round(avg),
      fuelAlerts: 0,
      geofence: this.geofenceBreachCount
    };
  }

  get trackingActive(): boolean {
    return !this.error && this.liveTracking;
  }

  get gpsHealthy(): boolean {
    return !this.error && this.locations.some(l => l.hasGps);
  }

  get replayProgress(): number {
    if (!this.historyRows.length) return 0;
    return Math.round((this.replayIndex / this.historyRows.length) * 100);
  }

  statusLabel(status: FleetTrackStatus): string {
    const labels: Record<FleetTrackStatus, string> = {
      moving: 'Moving',
      idle: 'Idle',
      offline: 'Offline',
      scheduled: 'Scheduled',
      delayed: 'Delayed'
    };
    return labels[status];
  }

  statusIcon(status: FleetTrackStatus): string {
    const icons: Record<FleetTrackStatus, string> = {
      moving: 'directions_bus',
      idle: 'local_shipping',
      offline: 'signal_wifi_off',
      scheduled: 'schedule',
      delayed: 'warning'
    };
    return icons[status];
  }

  signalBars(loc: VehicleLocation): number {
    if (!loc.hasGps) return 0;
    if (!loc.lastUpdated) return 1;
    const ageMin = (Date.now() - new Date(loc.lastUpdated).getTime()) / 60000;
    if (ageMin < 2 && loc.status === 'moving') return 4;
    if (ageMin < 10) return 3;
    if (ageMin < 30) return 2;
    return 1;
  }

  formatLastPing(loc: VehicleLocation): string {
    if (!loc.hasGps || !loc.lastUpdated) return 'No live GPS';
    const sec = Math.floor((Date.now() - new Date(loc.lastUpdated).getTime()) / 1000);
    if (sec < 60) return `Last ping: ${sec}s ago`;
    const min = Math.floor(sec / 60);
    if (min < 60) return `Last ping: ${min}m ago`;
    const hr = Math.floor(min / 60);
    return `Last ping: ${hr}h ago`;
  }

  speedLabel(loc: VehicleLocation): string {
    if (!loc.hasGps) return '—';
    if (loc.speed > 0) return `${Math.round(loc.speed)} km/h`;
    return loc.status === 'idle' ? '0 km/h · idle' : 'Stationary';
  }

  setStatusFilter(id: StatusFilter): void {
    this.statusFilter = id;
  }

  setTimePreset(id: TimePreset): void {
    this.timePreset = id;
    if (id !== 'custom') this.applyTimePreset(id);
  }

  private applyTimePreset(preset: TimePreset): void {
    const now = new Date();
    const to = new Date(now);
    let from = new Date(now);
    if (preset === 'today') {
      from.setHours(0, 0, 0, 0);
    } else if (preset === '24h') {
      from = new Date(now.getTime() - 24 * 60 * 60 * 1000);
    } else if (preset === '7d') {
      from = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
    }
    this.historyFrom = this.toLocalInput(from);
    this.historyTo = this.toLocalInput(to);
  }

  private toLocalInput(d: Date): string {
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  refreshNow(): void {
    this.loadLocations(true);
  }

  toggleLiveTracking(): void {
    this.liveTracking = !this.liveTracking;
    if (this.liveTracking) this.refreshNow();
    this.pushEvent(
      this.liveTracking ? 'Live tracking resumed' : 'Live tracking paused',
      'info',
      this.liveTracking ? 'play_circle' : 'pause_circle'
    );
  }

  cycleMapTheme(): void {
    const order: MapTheme[] = ['dark', 'light', 'satellite'];
    const i = order.indexOf(this.mapTheme);
    this.setMapTheme(order[(i + 1) % order.length]);
  }

  setMapTheme(theme: MapTheme): void {
    this.mapTheme = theme;
    if (!this.map) return;
    if (this.tileLayer) this.map.removeLayer(this.tileLayer);
    const cfg = MAP_TILES[theme];
    this.tileLayer = L.tileLayer(cfg.url, { maxZoom: 19, attribution: cfg.attribution }).addTo(this.map);
  }

  centerMap(): void {
    const gps = this.locations.filter(l => l.hasGps && l.latitude && l.longitude);
    if (!gps.length) {
      this.map.setView([30.3753, 69.3451], 6);
      return;
    }
    const bounds = L.latLngBounds(gps.map(l => [l.latitude, l.longitude] as [number, number]));
    this.map.fitBounds(bounds, { padding: [48, 48], maxZoom: 12 });
  }

  resetZoom(): void {
    this.map.setZoom(6);
    this.centerMap();
  }

  toggleFullscreen(): void {
    const el = this.mapHost?.nativeElement;
    if (!el) return;
    if (!document.fullscreenElement) {
      el.requestFullscreen?.();
    } else {
      document.exitFullscreen?.();
    }
  }

  selectVehicle(loc: VehicleLocation): void {
    this.selectedVehicleId = loc.vehicleId;
    if (loc.hasGps && loc.latitude && loc.longitude) {
      this.focusVehicle(loc);
    }
  }

  onVehicleCardEnter(loc: VehicleLocation): void {
    if (!loc.hasGps) return;
    const marker = this.markers.get(loc.vehicleId);
    marker?.setZIndexOffset(2000);
    this.trailLayers.get(loc.vehicleId)?.setStyle({ weight: 5, opacity: 1 });
  }

  onVehicleCardLeave(loc: VehicleLocation): void {
    const marker = this.markers.get(loc.vehicleId);
    if (this.selectedVehicleId !== loc.vehicleId) {
      marker?.setZIndexOffset(0);
    }
    const line = this.trailLayers.get(loc.vehicleId);
    if (line) line.setStyle({ weight: 3, opacity: 0.75 });
  }

  private initMap(): void {
    this.map = L.map('tracking-map', {
      center: [30.3753, 69.3451],
      zoom: 6,
      zoomControl: false
    });
    L.control.zoom({ position: 'bottomright' }).addTo(this.map);
    this.markerCluster = createMarkerClusterGroup({
      maxClusterRadius: 55,
      disableClusteringAtZoom: 14,
      spiderfyOnMaxZoom: true,
      showCoverageOnHover: false,
      animateAddingMarkers: true,
      iconCreateFunction: (cluster: { getChildCount: () => number }) => {
        const count = cluster.getChildCount();
        return L.divIcon({
          html: `<div class="fleet-cluster"><span>${count}</span></div>`,
          className: 'fleet-cluster-host',
          iconSize: [44, 44],
          iconAnchor: [22, 22]
        });
      }
    });
    this.map.addLayer(this.markerCluster);
    this.setMapTheme(this.mapTheme);
  }

  private loadLocations(silent = false): void {
    if (!silent) this.loading = true;
    this.gpsService.getAllVehicleLocations().subscribe({
      next: locs => {
        const prevMoving = new Set(
          this.locations.filter(l => l.status === 'moving').map(l => l.vehicleId)
        );
        this.locations = locs;
        this.loading = false;
        this.error = null;
        this.lastSyncAt = new Date();
        this.secondsSinceSync = 0;
        const gpsLocs = locs.filter(l => l.hasGps);
        this.updateMarkers(gpsLocs);
        this.emitTelemetryEvents(gpsLocs, prevMoving);
        if (!silent) this.centerMap();
        this.pushEvent('Fleet positions synced', 'success', 'sync');
      },
      error: () => {
        this.loading = false;
        this.error = 'Could not reach tracking service. Showing fleet registry as offline.';
        this.locations = [];
        this.pushEvent('Tracking sync failed', 'alert', 'cloud_off');
      }
    });
  }

  private emitTelemetryEvents(gpsLocs: VehicleLocation[], prevMoving: Set<number>): void {
    gpsLocs
      .filter(l => l.status === 'moving' && l.speed > 5)
      .slice(0, 2)
      .forEach(l => {
        if (!prevMoving.has(l.vehicleId)) {
          this.pushEvent(`${l.vehicleName} is now en route (${Math.round(l.speed)} km/h)`, 'success', 'directions_bus');
        }
      });
    gpsLocs
      .filter(l => l.status === 'delayed')
      .slice(0, 1)
      .forEach(l => this.pushEvent(`${l.vehicleName} — delayed telemetry`, 'warning', 'warning'));
  }

  private pushEvent(message: string, type: TrackEvent['type'], icon: string): void {
    this.events = [{ time: new Date(), message, type, icon }, ...this.events].slice(0, 30);
  }

  private bearingFrom(prev: { lat: number; lng: number }, lat: number, lng: number): number {
    const dLng = ((lng - prev.lng) * Math.PI) / 180;
    const lat1 = (prev.lat * Math.PI) / 180;
    const lat2 = (lat * Math.PI) / 180;
    const y = Math.sin(dLng) * Math.cos(lat2);
    const x =
      Math.cos(lat1) * Math.sin(lat2) -
      Math.sin(lat1) * Math.cos(lat2) * Math.cos(dLng);
    return ((Math.atan2(y, x) * 180) / Math.PI + 360) % 360;
  }

  private createMarkerIcon(status: FleetTrackStatus, bearing = 0): LeafletTypes.DivIcon {
    const showArrow = status === 'moving' || status === 'delayed';
    const arrow = showArrow
      ? `<span class="fleet-marker-arrow" style="transform:rotate(${bearing}deg)"></span>`
      : '';
    const vehicleGlyph =
      status === 'moving'
        ? '&#128652;'
        : status === 'idle'
          ? '&#128666;'
          : status === 'delayed'
            ? '&#9888;'
            : '&#9679;';
    return L.divIcon({
      className: 'fleet-marker-host',
      html: `
        <div class="fleet-marker fleet-marker--${status}">
          <span class="fleet-marker-ring"></span>
          <span class="fleet-marker-pulse"></span>
          ${arrow}
          <span class="fleet-marker-glyph">${vehicleGlyph}</span>
        </div>`,
      iconSize: [40, 40],
      iconAnchor: [20, 20],
      popupAnchor: [0, -22]
    });
  }

  private updateTrail(loc: VehicleLocation): void {
    const pts = [...(this.positionTrails.get(loc.vehicleId) ?? []), [loc.latitude, loc.longitude] as [number, number]]
      .slice(-this.maxTrailPoints);
    this.positionTrails.set(loc.vehicleId, pts);
    if (pts.length < 2) return;

    const color = TRAIL_COLORS[loc.status];
    const existing = this.trailLayers.get(loc.vehicleId);
    if (existing) {
      existing.setLatLngs(pts);
      existing.setStyle({ color });
    } else {
      const line = L.polyline(pts, {
        color,
        weight: 3,
        opacity: 0.8,
        className: `live-trail live-trail--${loc.status}`
      }).addTo(this.map);
      this.trailLayers.set(loc.vehicleId, line);
    }
  }

  private updateMarkers(locs: VehicleLocation[]): void {
    const currentIds = new Set(locs.map(l => l.vehicleId));

    this.markers.forEach((marker, vehicleId) => {
      if (!currentIds.has(vehicleId)) {
        this.markerCluster.removeLayer(marker);
        marker.remove();
        this.markers.delete(vehicleId);
        this.trailLayers.get(vehicleId)?.remove();
        this.trailLayers.delete(vehicleId);
        this.positionTrails.delete(vehicleId);
        this.prevPositions.delete(vehicleId);
      }
    });

    locs.forEach(loc => {
      const prev = this.prevPositions.get(loc.vehicleId);
      let bearing = 0;
      if (prev) {
        bearing = this.bearingFrom(prev, loc.latitude, loc.longitude);
      }
      this.prevPositions.set(loc.vehicleId, { lat: loc.latitude, lng: loc.longitude });
      this.updateTrail(loc);

      const popupContent = `
        <div class="map-popup">
          <strong>${loc.vehicleName}</strong>
          <span class="map-popup-reg">${loc.registrationNumber}</span>
          ${loc.driverName ? `<span>Driver: ${loc.driverName}</span>` : ''}
          <span>${this.statusLabel(loc.status)} · ${this.speedLabel(loc)}</span>
          <small>${this.formatLastPing(loc)}</small>
          <a href="#" class="map-popup-link" data-vid="${loc.vehicleId}">View vehicle →</a>
        </div>
      `;
      const icon = this.createMarkerIcon(loc.status, bearing);

      if (this.markers.has(loc.vehicleId)) {
        const m = this.markers.get(loc.vehicleId)!;
        m.setLatLng([loc.latitude, loc.longitude]);
        m.setIcon(icon);
        m.setPopupContent(popupContent);
      } else {
        const marker = L.marker([loc.latitude, loc.longitude], { icon })
          .bindPopup(popupContent);
        marker.on('popupopen', () => {
          document.querySelector(`a[data-vid="${loc.vehicleId}"]`)?.addEventListener('click', e => {
            e.preventDefault();
            this.goToVehicleProfile(loc.vehicleId);
          });
        });
        this.markerCluster.addLayer(marker);
        this.markers.set(loc.vehicleId, marker);
      }
      if (this.selectedVehicleId === loc.vehicleId) {
        this.markers.get(loc.vehicleId)?.setZIndexOffset(1500);
      }
    });
  }

  focusVehicle(loc: VehicleLocation): void {
    if (!loc.hasGps) return;
    this.map.setView([loc.latitude, loc.longitude], 14, { animate: true });
    this.markers.get(loc.vehicleId)?.openPopup();
  }

  goToVehicleProfile(vehicleId: number): void {
    this.router.navigate(['/vehicles', vehicleId]);
  }

  goToVehicles(): void {
    this.router.navigate(['/vehicles']);
  }

  loadHistory(): void {
    if (!this.selectedVehicleId) return;
    this.loadingHistory = true;
    this.historyError = '';
    this.historyRows = [];
    this.tripSummary = null;
    this.showHistory = true;
    this.clearHistoryOverlay();
    this.gpsService
      .getHistory(this.selectedVehicleId, new Date(this.historyFrom), new Date(this.historyTo))
      .subscribe({
        next: rows => {
          this.historyRows = [...rows].sort(
            (a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime()
          );
          this.loadingHistory = false;
          this.tripSummary = this.computeTripSummary(this.historyRows);
          this.drawHistoryRoute(this.historyRows);
          this.buildHistoryEvents(this.historyRows);
          if (this.historyRows.length) {
            this.pushEvent(`Route loaded — ${this.tripSummary?.distanceKm} km`, 'info', 'route');
          }
        },
        error: () => {
          this.historyError = 'Could not load tracking history.';
          this.loadingHistory = false;
        }
      });
  }

  private computeTripSummary(rows: TrackingDto[]): TripSummary | null {
    if (!rows.length) return null;
    let distanceKm = 0;
    let stopMs = 0;
    const speeds: number[] = [];
    for (let i = 1; i < rows.length; i++) {
      const a = rows[i - 1];
      const b = rows[i];
      distanceKm += this.haversineKm(a.latitude, a.longitude, b.latitude, b.longitude);
      const dt = new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime();
      const sp = Number(b.speed) || 0;
      speeds.push(sp);
      if (sp < 2 && dt > 0) stopMs += dt;
    }
    const t0 = new Date(rows[0].timestamp).getTime();
    const t1 = new Date(rows[rows.length - 1].timestamp).getTime();
    const durationMinutes = Math.max(1, Math.round((t1 - t0) / 60000));
    const avgSpeed =
      speeds.length > 0
        ? Math.round(speeds.reduce((s, v) => s + v, 0) / speeds.length)
        : 0;
    return {
      distanceKm: Math.round(distanceKm * 10) / 10,
      avgSpeed,
      stopMinutes: Math.round(stopMs / 60000),
      durationMinutes,
      pointCount: rows.length
    };
  }

  private haversineKm(lat1: number, lon1: number, lat2: number, lon2: number): number {
    const R = 6371;
    const dLat = ((lat2 - lat1) * Math.PI) / 180;
    const dLon = ((lon2 - lon1) * Math.PI) / 180;
    const a =
      Math.sin(dLat / 2) ** 2 +
      Math.cos((lat1 * Math.PI) / 180) *
        Math.cos((lat2 * Math.PI) / 180) *
        Math.sin(dLon / 2) ** 2;
    return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  }

  private buildHistoryEvents(rows: TrackingDto[]): void {
    rows.forEach((r, i) => {
      const speed = Number(r.speed) || 0;
      if (speed > 80) {
        this.pushEvent(`Speed alert — ${Math.round(speed)} km/h`, 'alert', 'speed');
      }
      if (i > 0 && speed < 2 && (Number(rows[i - 1].speed) || 0) > 10) {
        this.pushEvent('Vehicle stopped', 'info', 'pause_circle');
      }
      if (i > 0 && speed > 10 && (Number(rows[i - 1].speed) || 0) < 2) {
        this.pushEvent('Vehicle departed', 'success', 'play_circle');
      }
    });
  }

  private drawHistoryRoute(rows: TrackingDto[]): void {
    if (!rows.length) return;
    const latlngs: LeafletTypes.LatLngExpression[] = rows.map(r => [r.latitude, r.longitude]);
    this.historyPolyline = L.polyline(latlngs, {
      color: '#2DD4BF',
      weight: 5,
      opacity: 0.9,
      className: 'history-route-glow'
    }).addTo(this.map);
    const last = rows[rows.length - 1];
    let bearing = 0;
    if (rows.length > 1) {
      const p = rows[rows.length - 2];
      bearing = this.bearingFrom(
        { lat: p.latitude, lng: p.longitude },
        last.latitude,
        last.longitude
      );
    }
    this.historyMarker = L.marker([last.latitude, last.longitude], {
      icon: this.createMarkerIcon('moving', bearing)
    }).addTo(this.map);
    this.map.fitBounds(this.historyPolyline.getBounds(), { padding: [48, 48] });
  }

  private clearHistoryOverlay(): void {
    this.stopReplay();
    this.replayIndex = 0;
    if (this.historyPolyline) {
      this.map.removeLayer(this.historyPolyline);
      this.historyPolyline = undefined;
    }
    if (this.historyMarker) {
      this.map.removeLayer(this.historyMarker);
      this.historyMarker = undefined;
    }
  }

  toggleReplay(): void {
    if (this.replayPlaying) {
      this.stopReplay();
      return;
    }
    if (!this.historyRows.length) return;
    this.replayPlaying = true;
    this.replayIndex = 0;
    const stepMs = 350 / this.replaySpeed;
    this.replayTimer = setInterval(() => this.advanceReplay(), stepMs);
    this.pushEvent('Route playback started', 'info', 'play_arrow');
  }

  setReplaySpeed(mult: number): void {
    this.replaySpeed = mult;
    if (this.replayPlaying) {
      this.stopReplay();
      this.toggleReplay();
    }
  }

  onReplayScrub(event: Event): void {
    const val = Number((event.target as HTMLInputElement).value);
    if (!this.historyRows.length) return;
    this.replayIndex = Math.min(
      this.historyRows.length - 1,
      Math.floor((val / 100) * this.historyRows.length)
    );
    const row = this.historyRows[this.replayIndex];
    if (row && this.historyMarker) {
      let bearing = 0;
      if (this.replayIndex > 0) {
        const p = this.historyRows[this.replayIndex - 1];
        bearing = this.bearingFrom(
          { lat: p.latitude, lng: p.longitude },
          row.latitude,
          row.longitude
        );
      }
      this.historyMarker.setLatLng([row.latitude, row.longitude]);
      this.historyMarker.setIcon(this.createMarkerIcon('moving', bearing));
    }
  }

  private advanceReplay(): void {
    if (this.replayIndex >= this.historyRows.length) {
      this.stopReplay();
      this.pushEvent('Route playback complete', 'success', 'flag');
      return;
    }
    const row = this.historyRows[this.replayIndex];
    let bearing = 0;
    if (this.replayIndex > 0) {
      const p = this.historyRows[this.replayIndex - 1];
      bearing = this.bearingFrom(
        { lat: p.latitude, lng: p.longitude },
        row.latitude,
        row.longitude
      );
    }
    this.historyMarker?.setLatLng([row.latitude, row.longitude]);
    this.historyMarker?.setIcon(this.createMarkerIcon('moving', bearing));
    this.map.setView([row.latitude, row.longitude], Math.max(this.map.getZoom(), 12), {
      animate: true
    });
    this.replayIndex++;
  }

  stopReplay(): void {
    this.replayPlaying = false;
    if (this.replayTimer) {
      clearInterval(this.replayTimer);
      this.replayTimer = undefined;
    }
  }

  formatSyncAgo(): string {
    if (!this.lastSyncAt) return '—';
    if (this.secondsSinceSync < 60) return `${this.secondsSinceSync} sec ago`;
    return `${Math.floor(this.secondsSinceSync / 60)} min ago`;
  }

  private applyRealtimeUpdate(update: TrackingDto): void {
    const idx = this.locations.findIndex(l => l.vehicleId === update.vehicleId);
    const status = update.speed > 5 ? 'moving' as FleetTrackStatus : 'idle';
    if (idx >= 0) {
      this.locations[idx] = {
        ...this.locations[idx],
        latitude: update.latitude,
        longitude: update.longitude,
        speed: Number(update.speed) || 0,
        lastUpdated: update.timestamp,
        status,
        hasGps: true,
        ignition: update.ignition,
        routeHint: `${Math.round(Number(update.speed) || 0)} km/h`
      };
      this.updateMarkers(this.locations.filter(l => l.hasGps && l.latitude !== 0));
    }
    this.lastSyncAt = new Date();
    this.secondsSinceSync = 0;
  }
}
