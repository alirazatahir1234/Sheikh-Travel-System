import { Component, OnInit, OnDestroy, AfterViewInit, ElementRef, ViewChild } from '@angular/core';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { Vehicle } from '../../../core/models/vehicle.model';
import { PositionDto } from '../../../core/models/gps-tracking.model';
import { MAP_TILE_STACKS } from '../../../core/leaflet/leaflet-map-tiles';
import { L } from '../../../core/leaflet/leaflet-cluster';
import type * as LeafletTypes from 'leaflet';

@Component({
  selector: 'app-gps-history',
  templateUrl: './gps-history.component.html',
  styleUrls: ['./gps-history.component.scss']
})
export class GpsHistoryComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('mapEl', { static: false }) mapEl!: ElementRef<HTMLDivElement>;

  vehicles: Vehicle[] = [];
  vehicleId: number | null = null;
  from = '';
  to = '';
  rows: PositionDto[] = [];
  loading = false;
  error = '';

  get summary() {
    if (!this.rows.length) return null;
    const maxSpeed = Math.max(...this.rows.map(r => Number(r.speed) || 0));
    const avgSpeed = this.rows.reduce((s, r) => s + (Number(r.speed) || 0), 0) / this.rows.length;
    const firstTs = new Date(this.rows[0].timestamp).getTime();
    const lastTs = new Date(this.rows[this.rows.length - 1].timestamp).getTime();
    const durationMin = Math.round((lastTs - firstTs) / 60000);
    return { points: this.rows.length, maxSpeed: Math.round(maxSpeed), avgSpeed: Math.round(avgSpeed), durationMin };
  }

  private map: LeafletTypes.Map | null = null;
  private routeLayer: LeafletTypes.LayerGroup | null = null;

  constructor(
    private gps: GpsTrackingService,
    private vehicleService: VehicleService
  ) {}

  ngOnInit(): void {
    const now = new Date();
    const from = new Date(now.getTime() - 24 * 60 * 60 * 1000);
    this.from = this.toInput(from);
    this.to = this.toInput(now);

    this.vehicleService.getAll(1, 500).subscribe({
      next: r => {
        this.vehicles = r.items;
        if (r.items.length) { this.vehicleId = r.items[0].id; }
      }
    });
  }

  ngAfterViewInit(): void {
    setTimeout(() => this.initMap(), 100);
  }

  ngOnDestroy(): void {
    this.map?.remove();
    this.map = null;
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
  }

  private renderRoute(): void {
    if (!this.routeLayer || !this.rows.length) return;
    this.routeLayer.clearLayers();
    const latlngs: [number, number][] = this.rows.map(r => [r.latitude, r.longitude]);
    L.polyline(latlngs, { color: '#0f766e', weight: 3, opacity: 0.8 }).addTo(this.routeLayer!);

    // Start marker
    const start = this.rows[0];
    L.circleMarker([start.latitude, start.longitude], { radius: 8, color: '#059669', fillColor: '#059669', fillOpacity: 1 })
      .bindPopup(`Start: ${new Date(start.timestamp).toLocaleTimeString()}`)
      .addTo(this.routeLayer!);

    // End marker
    const end = this.rows[this.rows.length - 1];
    L.circleMarker([end.latitude, end.longitude], { radius: 8, color: '#dc2626', fillColor: '#dc2626', fillOpacity: 1 })
      .bindPopup(`End: ${new Date(end.timestamp).toLocaleTimeString()}`)
      .addTo(this.routeLayer!);

    if (this.map) {
      const bounds = L.latLngBounds(latlngs as LeafletTypes.LatLngTuple[]);
      this.map.fitBounds(bounds, { padding: [32, 32] });
    }
  }

  load(): void {
    if (!this.vehicleId) return;
    const fromDate = new Date(this.from);
    const toDate = new Date(this.to);
    if (fromDate > toDate) { this.error = 'Start date must be before end date.'; return; }
    if (toDate.getTime() - fromDate.getTime() > 30 * 24 * 60 * 60 * 1000) { this.error = 'Date range cannot exceed 30 days.'; return; }

    this.loading = true;
    this.error = '';
    this.gps.getHistory(this.vehicleId, fromDate, toDate).subscribe({
      next: rows => {
        this.rows = rows;
        this.loading = false;
        this.renderRoute();
      },
      error: err => {
        this.error = err?.error?.message ?? 'Failed to load history.';
        this.loading = false;
      }
    });
  }

  private toInput(d: Date): string {
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }
}
