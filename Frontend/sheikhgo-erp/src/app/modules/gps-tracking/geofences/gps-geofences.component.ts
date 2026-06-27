import { Component, OnInit, OnDestroy, AfterViewInit, ElementRef, ViewChild } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { Geofence } from '../../../core/models/gps-tracking.model';
import { MAP_TILE_STACKS } from '../../../core/leaflet/leaflet-map-tiles';
import { L } from '../../../core/leaflet/leaflet-cluster';
import type * as LeafletTypes from 'leaflet';

@Component({
  selector: 'app-gps-geofences',
  templateUrl: './gps-geofences.component.html',
  styleUrls: ['./gps-geofences.component.scss']
})
export class GpsGeofencesComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('mapEl', { static: false }) mapEl!: ElementRef<HTMLDivElement>;

  geofences: Geofence[] = [];
  loading = false;
  showForm = false;
  editGeofence: Geofence | null = null;

  private map: LeafletTypes.Map | null = null;
  private circleLayer: LeafletTypes.LayerGroup | null = null;

  form!: ReturnType<FormBuilder['group']>;

  constructor(
    private gps: GpsTrackingService,
    private fb: FormBuilder,
    private toast: UiToastService
  ) {
    this.form = this.fb.group({
      name: ['', Validators.required],
      centerLat: [31.52, [Validators.required]],
      centerLng: [74.35, [Validators.required]],
      radiusMeters: [500, [Validators.required, Validators.min(50)]]
    });
  }

  ngOnInit(): void {
    this.load();
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
    this.circleLayer = L.layerGroup().addTo(this.map);
    this.map.on('click', (e: LeafletTypes.LeafletMouseEvent) => {
      this.form.patchValue({ centerLat: +e.latlng.lat.toFixed(6), centerLng: +e.latlng.lng.toFixed(6) });
      if (!this.showForm) {
        this.editGeofence = null;
        this.form.patchValue({ name: '', radiusMeters: 500 });
        this.showForm = true;
      }
    });
    this.renderCircles();
  }

  load(): void {
    this.loading = true;
    this.gps.getGeofences().subscribe({
      next: rows => {
        this.geofences = rows;
        this.loading = false;
        this.renderCircles();
      },
      error: () => { this.loading = false; }
    });
  }

  private renderCircles(): void {
    if (!this.circleLayer) return;
    this.circleLayer.clearLayers();
    for (const g of this.geofences) {
      const color = g.isActive ? '#0f766e' : '#9ca3af';
      const c = L.circle([g.centerLat, g.centerLng], {
        radius: g.radiusMeters, color, fillColor: color, fillOpacity: 0.12, weight: 2
      });
      c.bindPopup(`<strong>${g.name}</strong><br>${g.radiusMeters}m radius`);
      c.addTo(this.circleLayer!);
    }
    if (this.geofences.length && this.map) {
      this.map.setView([this.geofences[0].centerLat, this.geofences[0].centerLng], 12);
    }
  }

  openCreate(): void {
    this.editGeofence = null;
    this.form.reset({ centerLat: 31.52, centerLng: 74.35, radiusMeters: 500 });
    this.showForm = true;
  }

  openEdit(g: Geofence): void {
    this.editGeofence = g;
    this.form.patchValue({ name: g.name, centerLat: g.centerLat, centerLng: g.centerLng, radiusMeters: g.radiusMeters });
    this.showForm = true;
    if (this.map) { this.map.setView([g.centerLat, g.centerLng], 13); }
  }

  save(): void {
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const payload = { name: v.name!, centerLat: +v.centerLat, centerLng: +v.centerLng, radiusMeters: +v.radiusMeters };

    if (this.editGeofence) {
      this.gps.updateGeofence(this.editGeofence.id, { ...payload, geoJson: this.editGeofence.geoJson, isActive: this.editGeofence.isActive }).subscribe({
        next: () => { this.toast.success('Geofence updated'); this.showForm = false; this.load(); },
        error: () => this.toast.error('Update failed')
      });
    } else {
      this.gps.createGeofence(payload).subscribe({
        next: () => { this.toast.success('Geofence created'); this.showForm = false; this.load(); },
        error: () => this.toast.error('Failed to create geofence')
      });
    }
  }

  toggleActive(g: Geofence): void {
    this.gps.updateGeofence(g.id, {
      name: g.name, centerLat: g.centerLat, centerLng: g.centerLng,
      radiusMeters: g.radiusMeters, geoJson: g.geoJson, isActive: !g.isActive
    }).subscribe({ next: () => this.load() });
  }

  delete(g: Geofence): void {
    if (!confirm(`Delete geofence "${g.name}"?`)) return;
    this.gps.deleteGeofence(g.id).subscribe({
      next: () => { this.toast.success('Geofence deleted'); this.load(); },
      error: () => this.toast.error('Delete failed')
    });
  }
}
