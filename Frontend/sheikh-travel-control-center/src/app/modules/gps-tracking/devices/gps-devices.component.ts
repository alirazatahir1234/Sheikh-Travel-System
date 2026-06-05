import { Component, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MatSnackBar } from '@angular/material/snack-bar';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { GpsDevice } from '../../../core/models/gps-tracking.model';
import { Vehicle } from '../../../core/models/vehicle.model';

@Component({
  selector: 'app-gps-devices',
  templateUrl: './gps-devices.component.html',
  styleUrls: ['./gps-devices.component.scss']
})
export class GpsDevicesComponent implements OnInit {
  devices: GpsDevice[] = [];
  vehicles: Vehicle[] = [];
  loading = false;

  form!: ReturnType<FormBuilder['group']>;

  constructor(
    private gps: GpsTrackingService,
    private vehicleService: VehicleService,
    private fb: FormBuilder,
    private snack: MatSnackBar
  ) {
    this.form = this.fb.group({
      uniqueId: ['', Validators.required],
      name: ['', Validators.required],
      vehicleId: [null as number | null],
      protocol: [''],
      supportsEngineCutoff: [false]
    });
  }

  ngOnInit(): void {
    this.vehicleService.getAll(1, 500).subscribe({ next: r => { this.vehicles = r.items; } });
    this.load();
  }

  load(): void {
    this.loading = true;
    this.gps.getDevices().subscribe({
      next: d => { this.devices = d; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  create(): void {
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    this.gps.createDevice({
      uniqueId: v.uniqueId!,
      name: v.name!,
      vehicleId: v.vehicleId ?? undefined,
      protocol: v.protocol || undefined,
      supportsEngineCutoff: !!v.supportsEngineCutoff
    }).subscribe({
      next: () => {
        this.snack.open('Device registered', 'OK', { duration: 2500 });
        this.form.reset({ supportsEngineCutoff: false });
        this.load();
      }
    });
  }

  ignitionLabel(d: GpsDevice): string {
    if (d.lastIgnition == null) return 'Unknown';
    return d.lastIgnition ? 'Engine ON' : 'Engine OFF';
  }
}
