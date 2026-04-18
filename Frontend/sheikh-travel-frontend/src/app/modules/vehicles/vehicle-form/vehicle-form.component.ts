import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { VehicleService } from '../../../core/services/vehicle.service';

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

  constructor(
    private fb: FormBuilder,
    private vehicleService: VehicleService,
    private router: Router,
    private route: ActivatedRoute,
    private snackBar: MatSnackBar
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(100)]],
      registrationNumber: ['', [Validators.required, Validators.maxLength(20)]],
      seatingCapacity: [null, [Validators.required, Validators.min(1)]],
      fuelAverage: [null, [Validators.required, Validators.min(0.1)]]
    });
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit = true;
      this.vehicleId = +id;
      this.vehicleService.getById(this.vehicleId).subscribe(v => this.form.patchValue(v));
    }
  }

  submit(): void {
    if (this.form.invalid) return;
    this.loading = true;
    const req = this.form.value;
    const obs = this.isEdit
      ? this.vehicleService.update({ ...req, id: this.vehicleId! })
      : this.vehicleService.create(req);

    obs.subscribe({
      next: () => {
        this.snackBar.open(`Vehicle ${this.isEdit ? 'updated' : 'created'}`, 'Close', { duration: 2000 });
        this.router.navigate(['/vehicles']);
      },
      error: () => { this.loading = false; this.snackBar.open('Operation failed', 'Close', { duration: 3000 }); }
    });
  }
}
