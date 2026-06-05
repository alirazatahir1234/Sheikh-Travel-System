import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { DriverService } from '../../../core/services/driver.service';
import {
  CreateDriverDto,
  UpdateDriverDto,
  Driver,
  DriverStatus,
  DriverStatusLabels
} from '../../../core/models/driver.model';

@Component({
  selector: 'app-driver-form',
  templateUrl: './driver-form.component.html',
  styleUrls: ['./driver-form.component.scss']
})
export class DriverFormComponent implements OnInit, OnDestroy {
  form: FormGroup;
  /** True while saving (Create / Update). */
  loading = false;
  /** True while loading an existing driver for edit. */
  loadingDriver = false;
  isEdit = false;
  driverId: number | null = null;

  private readonly destroy$ = new Subject<void>();

  readonly statuses: DriverStatus[] = [
    DriverStatus.Available,
    DriverStatus.OnTrip,
    DriverStatus.OffDuty,
    DriverStatus.Suspended
  ];

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
      licenseNumber: ['', [Validators.required, Validators.maxLength(30)]],
      licenseExpiryDate: [null, Validators.required],
      cnic: [''],
      address: [''],
      status: [DriverStatus.Available, Validators.required],
      isActive: [true]
    });
  }

  ngOnInit(): void {
    // Listen to param changes so navigating e.g. /drivers/1/edit → /drivers/2/edit
    // still reloads (Angular reuses the same component instance).
    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe((pm) => {
      const idStr = pm.get('id');
      if (idStr == null || idStr === '') {
        this.isEdit = false;
        this.driverId = null;
        this.resetFormForCreate();
        return;
      }
      const id = +idStr;
      if (!Number.isFinite(id) || id <= 0) {
        return;
      }
      this.isEdit = true;
      this.driverId = id;
      this.loadDriver(id);
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private resetFormForCreate(): void {
    this.form.reset({
      fullName: '',
      phone: '',
      licenseNumber: '',
      licenseExpiryDate: null,
      cnic: '',
      address: '',
      status: DriverStatus.Available,
      isActive: true
    });
  }

  private loadDriver(id: number): void {
    this.loadingDriver = true;
    this.driverService.getById(id).subscribe({
      next: (d) => {
        this.applyDriverToForm(d);
        this.loadingDriver = false;
      },
      error: (err: HttpErrorResponse) => {
        this.loadingDriver = false;
        this.snackBar.open(
          this.extractError(err) || 'Could not load driver.',
          'Close',
          { duration: 5000 }
        );
      }
    });
  }

  /**
   * Maps API payload to the reactive form. Accepts plain camelCase `Driver` and
   * a few defensive fallbacks if property names differ.
   */
  private applyDriverToForm(d: Driver | Record<string, unknown>): void {
    const r = d as Record<string, unknown>;
    this.form.patchValue({
      fullName: this.readStr(r, 'fullName', 'FullName'),
      phone: this.readStr(r, 'phone', 'Phone'),
      licenseNumber: this.readStr(r, 'licenseNumber', 'LicenseNumber'),
      licenseExpiryDate: this.parseDate(this.readAny(r, 'licenseExpiryDate', 'LicenseExpiryDate')),
      cnic: this.readStr(r, 'cnic', 'cNIC', 'CNIC'),
      address: this.readStrOrEmpty(r, 'address', 'Address'),
      status: this.coerceStatus(this.readAny(r, 'status', 'Status')),
      isActive: Boolean(
        (r as unknown as Driver).isActive ?? r['IsActive'] ?? true
      )
    });
  }

  private readStr(r: Record<string, unknown>, ...keys: string[]): string {
    for (const k of keys) {
      const v = r[k];
      if (v != null && v !== '') return String(v);
    }
    return '';
  }

  private readStrOrEmpty(r: Record<string, unknown>, ...keys: string[]): string {
    for (const k of keys) {
      const v = r[k];
      if (v != null) return v === '' ? '' : String(v);
    }
    return '';
  }

  private readAny(r: Record<string, unknown>, ...keys: string[]): unknown {
    for (const k of keys) {
      if (k in r && r[k] !== undefined) return r[k];
    }
    return undefined;
  }

  private parseDate(v: unknown): Date | null {
    if (v == null) return null;
    if (v instanceof Date) return isNaN(v.getTime()) ? null : v;
    if (typeof v === 'string' || typeof v === 'number') {
      const d = new Date(v);
      return isNaN(d.getTime()) ? null : d;
    }
    return null;
  }

  private coerceStatus(v: unknown): DriverStatus {
    const n = Number(v);
    if (Number.isFinite(n) && n >= 1 && n <= 4) {
      return n as DriverStatus;
    }
    return DriverStatus.Available;
  }

  statusLabel(status: DriverStatus): string {
    return DriverStatusLabels[status];
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading = true;
    const f = this.form.value;

    const expiry: Date | string | null = f.licenseExpiryDate;
    const expiryIso = expiry
      ? (expiry instanceof Date ? expiry : new Date(expiry)).toISOString()
      : null;

    const baseDto: CreateDriverDto = {
      fullName: f.fullName,
      phone: f.phone,
      licenseNumber: f.licenseNumber,
      licenseExpiryDate: expiryIso!,
      cnic: f.cnic?.trim() ? f.cnic.trim() : null,
      address: f.address?.trim() ? f.address.trim() : null
    };

    const obs: Observable<unknown> = this.isEdit
      ? this.driverService.update({
          id: this.driverId!,
          driver: {
            ...baseDto,
            status: Number(f.status) as DriverStatus,
            isActive: !!f.isActive
          } as UpdateDriverDto
        })
      : this.driverService.create({ driver: baseDto });

    obs.subscribe({
      next: () => {
        this.snackBar.open(`Driver ${this.isEdit ? 'updated' : 'created'}`, 'Close', { duration: 2500 });
        this.router.navigate(['/drivers']);
      },
      error: (err: HttpErrorResponse) => {
        this.loading = false;
        this.snackBar.open(this.extractError(err), 'Close', { duration: 4500 });
      }
    });
  }

  private extractError(err: HttpErrorResponse): string {
    const body: any = err?.error;
    if (body?.errors) {
      const flat = Object.values(body.errors).flat();
      if (flat.length) return String(flat[0]);
    }
    if (body?.error?.message) return String(body.error.message);
    if (body?.message) return String(body.message);
    if (typeof body === 'string' && body) return body;
    if (err.status === 404) {
      return 'Driver not found. Try refreshing the list.';
    }
    if (err.status === 401 || err.status === 403) {
      return 'Not authorized. Sign in again.';
    }
    if (err.status === 405) {
      return 'API is out of date (missing GET /drivers/{id}). Restart the backend after pulling latest code.';
    }
    return `Operation failed (${err.status || 'network'}).`;
  }
}
