import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HttpErrorResponse } from '@angular/common/http';
import { MaintenanceService } from '../../../core/services/maintenance.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { CreateMaintenanceDto } from '../../../core/models/maintenance.model';
import { Vehicle } from '../../../core/models/vehicle.model';

@Component({
  selector: 'app-maintenance-form',
  templateUrl: './maintenance-form.component.html',
  styleUrls: ['./maintenance-form.component.scss']
})
export class MaintenanceFormComponent implements OnInit {
  form: FormGroup;
  loading = true;
  submitting = false;
  vehicles: Vehicle[] = [];

  constructor(
    private fb: FormBuilder,
    private maintenanceService: MaintenanceService,
    private vehicleService: VehicleService,
    private router: Router,
    private snackBar: MatSnackBar
  ) {
    this.form = this.fb.group({
      vehicleId:       [null, Validators.required],
      description:     ['', Validators.required],
      cost:            [0, [Validators.required, Validators.min(0)]],
      maintenanceDate: [new Date(), Validators.required],
      nextDueDate:     [null],
      serviceProvider: ['']
    });
  }

  ngOnInit(): void {
    this.vehicleService.getAll(1, 500).subscribe({
      next: res => {
        this.vehicles = res.items;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.snackBar.open('Failed to load vehicles.', 'Close', { duration: 3000 });
      }
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting = true;
    const f = this.form.value;

    const payload: CreateMaintenanceDto = {
      vehicleId:       f.vehicleId,
      description:     f.description.trim(),
      cost:            Number(f.cost),
      maintenanceDate: f.maintenanceDate instanceof Date ? f.maintenanceDate.toISOString() : f.maintenanceDate,
      nextDueDate:     f.nextDueDate ? (f.nextDueDate instanceof Date ? f.nextDueDate.toISOString() : f.nextDueDate) : null,
      serviceProvider: (f.serviceProvider || '').trim() || null
    };

    this.maintenanceService.create({ maintenance: payload }).subscribe({
      next: () => {
        this.submitting = false;
        this.snackBar.open('Maintenance record created.', 'Close', { duration: 2000 });
        this.router.navigate(['/maintenance']);
      },
      error: (err: HttpErrorResponse) => {
        this.submitting = false;
        this.snackBar.open(this.extractError(err), 'Close', { duration: 4000 });
      }
    });
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
