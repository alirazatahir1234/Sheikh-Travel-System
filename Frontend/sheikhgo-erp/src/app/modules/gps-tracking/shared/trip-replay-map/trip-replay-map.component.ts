import {
  Component, Input, OnChanges, OnDestroy, AfterViewInit, ElementRef, ViewChild, SimpleChanges, Output, EventEmitter
} from '@angular/core';
import { TripEvent, TripReplayPosition, TripStop } from '../../../../core/models/gps-tracking.model';
import { MAP_TILE_STACKS } from '../../../../core/leaflet/leaflet-map-tiles';
import { L } from '../../../../core/leaflet/leaflet-cluster';
import { SPEED_HEATMAP_LEGEND, speedToColor } from '../../utils/speed-heatmap.util';
import type * as LeafletTypes from 'leaflet';
import { SharedModule } from '../../../../shared/shared.module';

@Component({
    selector: 'app-trip-replay-map',
    templateUrl: './trip-replay-map.component.html',
    styleUrls: ['./trip-replay-map.component.scss'],
    standalone: true,
    imports: [SharedModule]
})
export class TripReplayMapComponent implements AfterViewInit, OnChanges, OnDestroy {
  @ViewChild('mapEl', { static: false }) mapEl!: ElementRef<HTMLDivElement>;
  @Input() routePoints: TripReplayPosition[] = [];
  @Input() positions: TripReplayPosition[] = [];
  @Input() stops: TripStop[] = [];
  @Input() events: TripEvent[] = [];
  @Input() loading = false;
  @Output() positionSelected = new EventEmitter<TripReplayPosition>();

  replayPlaying = false;
  replaySpeed = 1;
  readonly speedOptions = [1, 2, 4, 8, 16];
  readonly heatmapLegend = SPEED_HEATMAP_LEGEND;
  replayIndex = 0;

  private map: LeafletTypes.Map | null = null;
  private routeLayer: LeafletTypes.LayerGroup | null = null;
  private replayMarker: LeafletTypes.CircleMarker | null = null;
  private replayTimer?: ReturnType<typeof setInterval>;

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

  get routeSummary() {
    if (!this.positions.length) return null;
    const speeds = this.positions.map(p => Number(p.speedKmh) || 0);
    const maxSpeed = Math.max(...speeds, 0);
    const avgSpeed = speeds.length ? speeds.reduce((a, b) => a + b, 0) / speeds.length : 0;
    const first = new Date(this.positions[0].timestamp).getTime();
    const last = new Date(this.positions[this.positions.length - 1].timestamp).getTime();
    let distanceKm = 0;
    for (let i = 1; i < this.positions.length; i++) {
      const a = this.positions[i - 1];
      const b = this.positions[i];
      distanceKm += this.haversineKm(a.latitude, a.longitude, b.latitude, b.longitude);
    }
    return {
      distanceKm: Math.round(distanceKm * 10) / 10,
      durationMinutes: Math.max(1, Math.round((last - first) / 60000)),
      avgSpeed: Math.round(avgSpeed),
      maxSpeed: Math.round(maxSpeed),
      pointCount: this.positions.length
    };
  }

  ngAfterViewInit(): void {
    setTimeout(() => this.initMap(), 50);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if ((changes['positions'] || changes['routePoints'] || changes['stops'] || changes['events']) && this.map) {
      this.stopReplay();
      this.replayIndex = 0;
      requestAnimationFrame(() => this.renderRoute());
    }
  }

  ngOnDestroy(): void {
    this.stopReplay();
    this.map?.remove();
    this.map = null;
  }

  toggleReplay(): void {
    if (this.replayPlaying) {
      this.stopReplay();
      return;
    }
    if (!this.positions.length) return;
    this.replayPlaying = true;
    const stepMs = 350 / this.replaySpeed;
    this.replayTimer = setInterval(() => this.advanceReplay(), stepMs);
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
    if (!this.positions.length) return;
    this.replayIndex = Math.min(
      this.positions.length - 1,
      Math.floor((val / 100) * (this.positions.length - 1))
    );
    this.updateReplayMarker();
  }

  private initMap(): void {
    if (!this.mapEl?.nativeElement || this.map) return;
    const tiles = MAP_TILE_STACKS['light'][0];
    this.map = L.map(this.mapEl.nativeElement, { zoomControl: true }).setView([31.52, 74.35], 11);
    L.tileLayer(tiles.url, {
      attribution: tiles.attribution,
      subdomains: (tiles.subdomains ?? 'abc') as string,
      maxZoom: tiles.maxZoom ?? 19
    }).addTo(this.map);
    this.routeLayer = L.layerGroup().addTo(this.map);
    this.renderRoute();
  }

  private renderRoute(): void {
    if (!this.routeLayer || !this.map) return;
    const layer = this.routeLayer;
    layer.clearLayers();
    this.replayMarker = null;

    const pathPoints = this.routePoints.length ? this.routePoints : this.positions;
    if (!pathPoints.length) return;

    const drawPoints = this.downsampleForDraw(pathPoints, 800);

    if (drawPoints.length <= 150) {
      for (let i = 1; i < drawPoints.length; i++) {
        const a = drawPoints[i - 1];
        const b = drawPoints[i];
        L.polyline(
          [[a.latitude, a.longitude], [b.latitude, b.longitude]],
          { color: speedToColor(Number(b.speedKmh) || 0), weight: 4, opacity: 0.85 }
        ).addTo(layer);
      }
    } else {
      L.polyline(
        drawPoints.map(p => [p.latitude, p.longitude] as [number, number]),
        { color: '#059669', weight: 4, opacity: 0.85 }
      ).addTo(layer);
    }

    const start = pathPoints[0];
    const end = pathPoints[pathPoints.length - 1];
    L.circleMarker([start.latitude, start.longitude], {
      radius: 8, color: '#059669', fillColor: '#059669', fillOpacity: 1
    }).bindPopup(this.popupHtml(start, 'Start')).addTo(layer);

    L.circleMarker([end.latitude, end.longitude], {
      radius: 8, color: '#dc2626', fillColor: '#dc2626', fillOpacity: 1
    }).bindPopup(this.popupHtml(end, 'Finish')).addTo(layer);

    this.events.slice(0, 50).forEach(evt => {
      if (evt.latitude == null || evt.longitude == null) return;
      L.circleMarker([evt.latitude, evt.longitude], {
        radius: 6, color: '#f59e0b', fillColor: '#fbbf24', fillOpacity: 0.9
      }).bindPopup(`<strong>${evt.type}</strong><br>${new Date(evt.time).toLocaleString()}`).addTo(layer);
    });

    this.stops.slice(0, 30).forEach(stop => {
      L.circleMarker([stop.latitude, stop.longitude], {
        radius: 7, color: '#7c3aed', fillColor: '#a78bfa', fillOpacity: 0.95
      }).bindPopup(
        `<strong>Stop</strong><br>${stop.durationMinutes} min<br>${stop.address ?? ''}`
      ).addTo(layer);
    });

    const playback = this.positions.length ? this.positions : pathPoints;
    this.replayMarker = L.circleMarker([playback[0].latitude, playback[0].longitude], {
      radius: 10, color: '#0f766e', fillColor: '#14b8a6', fillOpacity: 1
    }).addTo(layer);

    const bounds = L.latLngBounds(drawPoints.map(p => [p.latitude, p.longitude] as [number, number]));
    this.map.fitBounds(bounds, { padding: [40, 40], maxZoom: 16 });
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
      this.stopReplay();
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
    this.replayMarker.bindPopup(this.popupHtml(row, 'Current'));
  }

  private stopReplay(): void {
    this.replayPlaying = false;
    if (this.replayTimer) {
      clearInterval(this.replayTimer);
      this.replayTimer = undefined;
    }
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
