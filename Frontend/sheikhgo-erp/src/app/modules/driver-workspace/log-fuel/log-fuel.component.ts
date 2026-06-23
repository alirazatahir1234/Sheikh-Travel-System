import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { DriverAppService } from '../../../core/services/driver-app.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { DriverTrip } from '../../../core/models/driver-trip.model';
import { FuelType } from '../../../core/models/fuel-log.model';
import { dateInputToIso, toDateInputValue } from '../../../core/utils/date-input.util';

interface VehicleOption {
  id: number;
  label: string;
}

@Component({
  selector: 'app-log-fuel',
  templateUrl: './log-fuel.component.html',
  styleUrls: ['./log-fuel.component.scss']
})
export class LogFuelComponent implements OnInit {
  form: FormGroup;
  submitting = false;
  loadingTrips = true;
  vehicles: VehicleOption[] = [];
  readonly fuelTypes = [FuelType.Petrol, FuelType.Diesel, FuelType.CNG];

  constructor(
    private fb: FormBuilder,
    private driverApp: DriverAppService,
    private toast: UiToastService) {
    this.form = this.fb.group({
      vehicleId: [null, Validators.required],
      fuelType: [FuelType.Petrol, Validators.required],
      liters: [null, [Validators.required, Validators.min(0.1)]],
      pricePerLiter: [null, [Validators.required, Validators.min(0)]],
      odometerReading: [null, [Validators.required, Validators.min(0)]],
      fuelDate: [toDateInputValue(new Date()), Validators.required],
      station: ['']
    });
  }

  ngOnInit(): void {
    this.driverApp.getTrips().subscribe({
      next: trips => {
        this.vehicles = this.vehiclesFromTrips(trips);
        this.loadingTrips = false;
        if (this.vehicles.length === 1) {
          this.form.patchValue({ vehicleId: this.vehicles[0].id });
        }
      },
      error: () => {
        this.loadingTrips = false;
        this.toast.error('Could not load your vehicles');
      }
    });
  }

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    const raw = this.form.getRawValue();
    const fuelDate = dateInputToIso(raw.fuelDate)!;

    this.submitting = true;
    this.driverApp.submitFuelReceipt({
      vehicleId: raw.vehicleId,
      driverId: null,
      liters: +raw.liters,
      pricePerLiter: +raw.pricePerLiter,
      odometerReading: +raw.odometerReading,
      fuelType: raw.fuelType,
      fuelDate,
      station: raw.station || null
    }).subscribe({
      next: () => {
        this.submitting = false;
        this.toast.success('Fuel receipt logged');
        this.form.reset({
          vehicleId: this.vehicles.length === 1 ? this.vehicles[0].id : null,
          fuelType: FuelType.Petrol,
          fuelDate: toDateInputValue(new Date()),
          station: ''
        });
      },
      error: err => {
        this.submitting = false;
        this.toast.error(err?.error?.message || 'Could not save fuel log');
      }
    });
  }

  private vehiclesFromTrips(trips: DriverTrip[]): VehicleOption[] {
    const map = new Map<number, string>();
    for (const t of trips) {
      if (t.vehicleId && !map.has(t.vehicleId)) {
        map.set(t.vehicleId, t.vehicleName || `Vehicle #${t.vehicleId}`);
      }
    }
    return [...map.entries()].map(([id, label]) => ({ id, label }));
  }
}
