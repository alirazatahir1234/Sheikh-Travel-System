import { Component, OnInit, AfterViewInit, ViewChild, ElementRef, OnDestroy, NgZone } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HttpErrorResponse } from '@angular/common/http';
import { forkJoin } from 'rxjs';
import { FuelLogService } from '../../../core/services/fuel-log.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { DriverService } from '../../../core/services/driver.service';
import { GoogleMapsLoaderService } from '../../../core/services/google-maps-loader.service';
import { FuelType, FuelTypeLabels, CreateFuelLogDto } from '../../../core/models/fuel-log.model';
import { Vehicle } from '../../../core/models/vehicle.model';
import { Driver } from '../../../core/models/driver.model';

@Component({
  selector: 'app-fuel-log-form',
  templateUrl: './fuel-log-form.component.html',
  styleUrls: ['./fuel-log-form.component.scss']
})
export class FuelLogFormComponent implements OnInit, AfterViewInit, OnDestroy {
  form: FormGroup;
  loading = true;
  submitting = false;

  vehicles: Vehicle[] = [];
  drivers: Driver[] = [];

  readonly fuelTypes = [FuelType.Petrol, FuelType.Diesel, FuelType.CNG];

  @ViewChild('stationInput') stationInput!: ElementRef<HTMLInputElement>;
  private autocomplete: google.maps.places.Autocomplete | null = null;
  private placeChangedListener: google.maps.MapsEventListener | null = null;
  mapsConfigured = false;

  constructor(
    private fb: FormBuilder,
    private fuelLogService: FuelLogService,
    private vehicleService: VehicleService,
    private driverService: DriverService,
    private mapsLoader: GoogleMapsLoaderService,
    private router: Router,
    private snackBar: MatSnackBar,
    private ngZone: NgZone
  ) {
    this.form = this.fb.group({
      vehicleId:      [null, Validators.required],
      driverId:       [null],
      fuelType:       [FuelType.Petrol, Validators.required],
      liters:         [null, [Validators.required, Validators.min(0.1)]],
      pricePerLiter:  [null, [Validators.required, Validators.min(0)]],
      odometerReading:[null, [Validators.required, Validators.min(0)]],
      fuelDate:       [new Date(), Validators.required],
      station:        ['']
    });
    this.mapsConfigured = this.mapsLoader.isConfigured;
  }

  ngOnInit(): void {
    forkJoin({
      vehicles: this.vehicleService.getAll(1, 500),
      drivers: this.driverService.getAll(1, 500)
    }).subscribe({
      next: ({ vehicles, drivers }) => {
        this.vehicles = vehicles.items;
        this.drivers = drivers.items.filter(d => d.isActive !== false);
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.snackBar.open('Failed to load data.', 'Close', { duration: 3000 });
      }
    });
  }

  ngAfterViewInit(): void {
    if (this.mapsConfigured) {
      this.initPlacesAutocomplete();
    }
  }

  ngOnDestroy(): void {
    if (this.placeChangedListener) {
      google.maps.event.removeListener(this.placeChangedListener);
    }
  }

  private async initPlacesAutocomplete(): Promise<void> {
    try {
      const placesLib = await this.mapsLoader.importLibrary<typeof google.maps.places>('places');
      if (!placesLib || !this.stationInput?.nativeElement) return;

      this.autocomplete = new placesLib.Autocomplete(this.stationInput.nativeElement, {
        types: ['establishment'],
        fields: ['name', 'formatted_address'],
        componentRestrictions: { country: 'pk' }
      });

      this.placeChangedListener = this.autocomplete.addListener('place_changed', () => {
        this.ngZone.run(() => {
          const place = this.autocomplete?.getPlace();
          if (place?.name) {
            const stationName = place.formatted_address
              ? `${place.name}, ${place.formatted_address}`
              : place.name;
            this.form.patchValue({ station: stationName });
          }
        });
      });
    } catch (err) {
      console.warn('Places autocomplete init failed:', err);
    }
  }

  get totalCost(): number {
    const liters = this.form.value.liters ?? 0;
    const price = this.form.value.pricePerLiter ?? 0;
    return liters * price;
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting = true;
    const f = this.form.value;

    const payload: CreateFuelLogDto = {
      vehicleId:       f.vehicleId,
      driverId:        f.driverId || null,
      fuelType:        Number(f.fuelType),
      liters:          Number(f.liters),
      pricePerLiter:   Number(f.pricePerLiter),
      odometerReading: Number(f.odometerReading),
      fuelDate:        f.fuelDate instanceof Date ? f.fuelDate.toISOString() : f.fuelDate,
      station:         (f.station || '').trim() || null
    };

    this.fuelLogService.create({ fuelLog: payload }).subscribe({
      next: () => {
        this.submitting = false;
        this.snackBar.open('Fuel log recorded successfully.', 'Close', { duration: 2000 });
        this.router.navigate(['/fuel-logs']);
      },
      error: (err: HttpErrorResponse) => {
        this.submitting = false;
        this.snackBar.open(this.extractError(err), 'Close', { duration: 4000 });
      }
    });
  }

  fuelTypeLabel(f: FuelType): string {
    return FuelTypeLabels[f] ?? 'Unknown';
  }

  private extractError(err: HttpErrorResponse): string {
    const body: any = err?.error;
    if (body?.errors) {
      const flat = Object.values(body.errors).flat();
      if (flat.length) return String(flat[0]);
    }
    if (body?.message) return String(body.message);
    return `Operation failed (${err?.status || 'network'}).`;
  }
}
