import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HttpErrorResponse } from '@angular/common/http';
import { VehicleService } from '../../../core/services/vehicle.service';
import {
  CreateVehicleDto, FuelType, FuelTypeLabels,
  UpdateVehicleDto, VehicleStatus, VehicleStatusLabels
} from '../../../core/models/vehicle.model';

@Component({
  selector: 'app-vehicle-form',
  templateUrl: './vehicle-form.component.html',
  styleUrls: ['./vehicle-form.component.scss']
})
export class VehicleFormComponent implements OnInit {
  form: FormGroup;
  loading = false;
  isEdit = false;
  vehicleId: number | null = null;

  readonly fuelTypes = [FuelType.Petrol, FuelType.Diesel, FuelType.CNG];
  readonly vehicleStatuses = [
    VehicleStatus.Available, VehicleStatus.OnTrip,
    VehicleStatus.Maintenance, VehicleStatus.Retired
  ];

  fuelTypeLabel(ft: FuelType): string { return FuelTypeLabels[ft] ?? 'Unknown'; }
  statusLabel(s: VehicleStatus): string { return VehicleStatusLabels[s] ?? 'Unknown'; }

  constructor(
    private fb: FormBuilder,
    private vehicleService: VehicleService,
    private router: Router,
    private route: ActivatedRoute,
    private snackBar: MatSnackBar
  ) {
    this.form = this.fb.group({
      name:                [''],
      registrationNumber:  [''],
      model:               [null],
      year:                [null],
      seatingCapacity:     [null, [Validators.required, Validators.min(1)]],
      fuelAverage:         [null, [Validators.required, Validators.min(0.1)]],
      fuelType:            [FuelType.Petrol, Validators.required],
      currentMileage:      [0, [Validators.min(0)]],
      insuranceExpiryDate: [null],
      status:              [VehicleStatus.Available]
    });

    this.form.controls['name'].setValidators([Validators.required, Validators.maxLength(100)]);
    this.form.controls['registrationNumber'].setValidators([Validators.required, Validators.maxLength(20)]);
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit = true;
      this.vehicleId = +id;
      this.vehicleService.getById(this.vehicleId).subscribe(v => {
        this.form.patchValue({
          ...v,
          insuranceExpiryDate: v.insuranceExpiryDate ? new Date(v.insuranceExpiryDate) : null
        });
      });
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading = true;
    const f = this.form.value;
    const fuelType = this.normalizeEnumValue<FuelType>(f.fuelType, FuelType, FuelType.Petrol);

    const baseDto: CreateVehicleDto = {
      name:                f.name.trim(),
      registrationNumber:  f.registrationNumber.trim(),
      model:               f.model || null,
      year:                this.toNullableNumber(f.year),
      seatingCapacity:     Number(f.seatingCapacity),
      fuelAverage:         Number(f.fuelAverage),
      fuelType,
      currentMileage:      Number(f.currentMileage ?? 0),
      insuranceExpiryDate: f.insuranceExpiryDate ? new Date(f.insuranceExpiryDate).toISOString() : null
    };

    const status = this.normalizeEnumValue<VehicleStatus>(f.status, VehicleStatus, VehicleStatus.Available);

    const obs = this.isEdit
      ? this.vehicleService.update({
          id: this.vehicleId!,
          vehicle: { ...baseDto, status } as UpdateVehicleDto
        })
      : this.vehicleService.create({ vehicle: baseDto });

    obs.subscribe({
      next: () => {
        this.snackBar.open(`Vehicle ${this.isEdit ? 'updated' : 'created'}`, 'Close', { duration: 2000 });
        this.router.navigate(['/vehicles']);
      },
      error: (err: HttpErrorResponse) => {
        this.loading = false;
        this.snackBar.open(this.extractError(err), 'Close', { duration: 5000 });
      }
    });
  }

  /** Pulls the readable message out of either an ApiResponse envelope or an ASP.NET validation problem. */
  private extractError(err: HttpErrorResponse): string {
    const payload = err?.error;
    if (payload?.message) return payload.message;
    if (payload?.errors) {
      const first = Object.values(payload.errors)[0] as string[] | string;
      return Array.isArray(first) ? first[0] : String(first);
    }
    return payload?.title || err.message || 'Operation failed';
  }

  private toNullableNumber(value: unknown): number | null {
    const n = Number(value);
    return Number.isFinite(n) ? n : null;
  }

  private normalizeEnumValue<T extends number>(
    value: unknown,
    enumType: Record<string, string | number>,
    fallback: T
  ): T {
    if (typeof value === 'number' && Number.isFinite(value)) {
      return value as T;
    }
    if (typeof value === 'string') {
      const asNumber = Number(value);
      if (Number.isFinite(asNumber)) return asNumber as T;
      if (value in enumType) return enumType[value] as T;
    }
    return fallback;
  }
}
