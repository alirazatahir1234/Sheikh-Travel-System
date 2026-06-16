import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { VehicleService } from '../../../core/services/vehicle.service';
import {
  CreateVehicleDto, FuelType, FuelTypeLabels,
  UpdateVehicleDto, VehicleStatus, VehicleStatusLabels
} from '../../../core/models/vehicle.model';
import { UiSelectOption } from '../../../shared/components/ui/types/ui.types';
import { dateInputToIso, toDateInputValue } from '../../../core/utils/date-input.util';

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

  readonly fuelTypeOptions: UiSelectOption[] = [FuelType.Petrol, FuelType.Diesel, FuelType.CNG].map(ft => ({
    value: String(ft),
    label: FuelTypeLabels[ft] ?? 'Unknown'
  }));

  readonly statusOptions: UiSelectOption[] = [
    VehicleStatus.Available, VehicleStatus.OnTrip,
    VehicleStatus.Maintenance, VehicleStatus.Retired
  ].map(s => ({
    value: String(s),
    label: VehicleStatusLabels[s] ?? 'Unknown'
  }));

  constructor(
    private fb: FormBuilder,
    private vehicleService: VehicleService,
    public router: Router,
    private route: ActivatedRoute,
    private snackBar: MatSnackBar
  ) {
    this.form = this.fb.group({
      name:                [''],
      registrationNumber:  [''],
      vehicleCode:         [null],
      vin:                 [null],
      make:                [null],
      model:               [null],
      year:                [null],
      color:               [null],
      vehicleType:         [null],
      seatingCapacity:     [null, [Validators.required, Validators.min(1)]],
      fuelAverage:         [null, [Validators.required, Validators.min(0.1)]],
      fuelType:            [String(FuelType.Petrol), Validators.required],
      engineNo:            [null],
      chassisNo:           [null],
      currentMileage:      [0, [Validators.min(0)]],
      insuranceExpiryDate: [''],
      purchaseDate:        [''],
      purchasePrice:       [null],
      branchId:            [null],
      departmentId:        [null],
      status:              [String(VehicleStatus.Available)]
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
          fuelType: String(v.fuelType),
          status: String(v.status),
          insuranceExpiryDate: toDateInputValue(v.insuranceExpiryDate),
          purchaseDate: toDateInputValue(v.purchaseDate)
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
      vehicleCode:         f.vehicleCode?.trim() || null,
      vin:                 f.vin?.trim() || null,
      make:                f.make?.trim() || null,
      model:               f.model || null,
      year:                this.toNullableNumber(f.year),
      color:               f.color?.trim() || null,
      vehicleType:         f.vehicleType?.trim() || null,
      seatingCapacity:     Number(f.seatingCapacity),
      fuelAverage:         Number(f.fuelAverage),
      fuelType,
      engineNo:            f.engineNo?.trim() || null,
      chassisNo:           f.chassisNo?.trim() || null,
      currentMileage:      Number(f.currentMileage ?? 0),
      insuranceExpiryDate: dateInputToIso(f.insuranceExpiryDate),
      purchaseDate:        dateInputToIso(f.purchaseDate),
      purchasePrice:       this.toNullableNumber(f.purchasePrice),
      branchId:            this.toNullableNumber(f.branchId),
      departmentId:        this.toNullableNumber(f.departmentId)
    };

    const status = this.normalizeEnumValue<VehicleStatus>(f.status, VehicleStatus, VehicleStatus.Available);

    const obs = this.isEdit
      ? this.vehicleService.update({
          id: this.vehicleId!,
          vehicle: { ...baseDto, status } as UpdateVehicleDto
        })
      : this.vehicleService.create({ vehicle: baseDto });

    (obs as Observable<unknown>).subscribe({
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
