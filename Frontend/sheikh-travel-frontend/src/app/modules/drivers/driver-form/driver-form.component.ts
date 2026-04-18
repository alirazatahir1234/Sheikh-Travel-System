import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DriverService } from '../../../core/services/driver.service';

@Component({
  selector: 'app-driver-form',
  templateUrl: './driver-form.component.html',
  styleUrls: ['./driver-form.component.scss']
})
export class DriverFormComponent implements OnInit {
  form: FormGroup;
  loading = false;
  isEdit = false;
  driverId: number | null = null;

  constructor(
    private fb: FormBuilder,
    private driverService: DriverService,
    private router: Router,
    private route: ActivatedRoute,
    private snackBar: MatSnackBar
  ) {
    this.form = this.fb.group({
      fullName: ['', [Validators.required, Validators.maxLength(100)]],
      phone: ['', [Validators.required, Validators.maxLength(20)]],
      licenseNumber: ['', [Validators.required, Validators.maxLength(50)]],
      licenseExpiryDate: [null, Validators.required],
      cnic: [''],
      address: ['']
    });
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit = true;
      this.driverId = +id;
      this.driverService.getById(this.driverId).subscribe(d => this.form.patchValue(d));
    }
  }

  submit(): void {
    if (this.form.invalid) return;
    this.loading = true;
    const req = this.form.value;
    const obs = this.isEdit
      ? this.driverService.update({ ...req, id: this.driverId! })
      : this.driverService.create(req);

    obs.subscribe({
      next: () => {
        this.snackBar.open(`Driver ${this.isEdit ? 'updated' : 'created'}`, 'Close', { duration: 2000 });
        this.router.navigate(['/drivers']);
      },
      error: () => { this.loading = false; this.snackBar.open('Operation failed', 'Close', { duration: 3000 }); }
    });
  }
}
