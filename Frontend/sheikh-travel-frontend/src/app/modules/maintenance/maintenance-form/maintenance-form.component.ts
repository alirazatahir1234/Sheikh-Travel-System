import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { MaintenanceService } from '../../../core/services/maintenance.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { CreateMaintenanceDto, Maintenance } from '../../../core/models/maintenance.model';
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

  editMode = false;
  editId: number | null = null;
  pageTitle = 'Schedule Maintenance';

  constructor(
    private fb: FormBuilder,
    private maintenanceService: MaintenanceService,
    private vehicleService: VehicleService,
    private router: Router,
    private route: ActivatedRoute,
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
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.editMode = true;
      this.editId = +idParam;
      this.pageTitle = 'Edit Maintenance Record';
    }

    this.vehicleService.getAll(1, 500).subscribe({
      next: res => {
        this.vehicles = res.items;
        if (this.editMode && this.editId) {
          this.loadExisting(this.editId);
        } else {
          this.loading = false;
        }
      },
      error: () => {
        this.loading = false;
        this.snackBar.open('Failed to load vehicles.', 'Close', { duration: 3000 });
      }
    });
  }

  private loadExisting(id: number): void {
    this.maintenanceService.getById(id).subscribe({
      next: (record: Maintenance) => {
        this.form.patchValue({
          vehicleId:       record.vehicleId,
          description:     record.description,
          cost:            record.cost,
          maintenanceDate: new Date(record.maintenanceDate),
          nextDueDate:     record.nextDueDate ? new Date(record.nextDueDate) : null,
          serviceProvider: record.serviceProvider ?? ''
        });
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.snackBar.open('Failed to load maintenance record.', 'Close', { duration: 3000 });
        this.router.navigate(['/maintenance']);
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

    const request$: Observable<unknown> = this.editMode && this.editId
      ? this.maintenanceService.update(this.editId, { maintenance: payload })
      : this.maintenanceService.create({ maintenance: payload });

    request$.subscribe({
      next: () => {
        this.submitting = false;
        const msg = this.editMode ? 'Maintenance record updated.' : 'Maintenance record created.';
        this.snackBar.open(msg, 'Close', { duration: 2000 });
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
