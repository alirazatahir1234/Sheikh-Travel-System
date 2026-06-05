import { Component, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MatSnackBar } from '@angular/material/snack-bar';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { Geofence } from '../../../core/models/gps-tracking.model';

@Component({
  selector: 'app-gps-geofences',
  templateUrl: './gps-geofences.component.html',
  styleUrls: ['./gps-geofences.component.scss']
})
export class GpsGeofencesComponent implements OnInit {
  geofences: Geofence[] = [];
  loading = false;

  form!: ReturnType<FormBuilder['group']>;

  constructor(
    private gps: GpsTrackingService,
    private fb: FormBuilder,
    private snack: MatSnackBar
  ) {
    this.form = this.fb.group({
      name: ['', Validators.required],
      centerLat: [31.52, Validators.required],
      centerLng: [74.35, Validators.required],
      radiusMeters: [500, [Validators.required, Validators.min(50)]]
    });
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.gps.getGeofences().subscribe({
      next: rows => {
        this.geofences = rows;
        this.loading = false;
      },
      error: () => { this.loading = false; }
    });
  }

  create(): void {
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    this.gps.createGeofence({
      name: v.name!,
      centerLat: Number(v.centerLat),
      centerLng: Number(v.centerLng),
      radiusMeters: Number(v.radiusMeters)
    }).subscribe({
      next: () => {
        this.snack.open('Geofence created', 'OK', { duration: 2500 });
        this.form.reset({ centerLat: 31.52, centerLng: 74.35, radiusMeters: 500 });
        this.load();
      },
      error: () => this.snack.open('Failed to create geofence', 'Dismiss', { duration: 3000 })
    });
  }

  toggleActive(g: Geofence): void {
    this.gps.updateGeofence(g.id, {
      name: g.name,
      centerLat: g.centerLat,
      centerLng: g.centerLng,
      radiusMeters: g.radiusMeters,
      geoJson: g.geoJson,
      isActive: !g.isActive
    }).subscribe({ next: () => this.load() });
  }

  delete(g: Geofence): void {
    if (!confirm(`Delete geofence "${g.name}"?`)) return;
    this.gps.deleteGeofence(g.id).subscribe({ next: () => this.load() });
  }
}
