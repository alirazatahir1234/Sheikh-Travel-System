import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';

import { CustomerService } from '../../../core/services/customer.service';
import {
  Customer,
  CreateCustomerDto,
  UpdateCustomerDto
} from '../../../core/models/customer.model';

@Component({
  selector: 'app-customer-form',
  templateUrl: './customer-form.component.html',
  styleUrls: ['./customer-form.component.scss']
})
export class CustomerFormComponent implements OnInit, OnDestroy {
  form: FormGroup;
  /** True while saving (Create / Update). */
  loading = false;
  /** True while loading an existing customer for edit. */
  loadingCustomer = false;
  isEdit = false;
  customerId: number | null = null;

  private readonly destroy$ = new Subject<void>();

  constructor(
    private fb: FormBuilder,
    private customerService: CustomerService,
    private router: Router,
    private route: ActivatedRoute,
    private snackBar: MatSnackBar
  ) {
    this.form = this.fb.group({
      fullName: ['', [Validators.required, Validators.maxLength(100)]],
      phone:    ['', [Validators.required, Validators.maxLength(20)]],
      email:    ['', [Validators.email, Validators.maxLength(120)]],
      cnic:     [''],
      address:  ['']
    });
  }

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe(pm => {
      const idStr = pm.get('id');
      if (!idStr) {
        this.isEdit = false;
        this.customerId = null;
        this.resetFormForCreate();
        return;
      }
      const id = +idStr;
      if (!Number.isFinite(id) || id <= 0) return;

      this.isEdit = true;
      this.customerId = id;
      this.loadCustomer(id);
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private resetFormForCreate(): void {
    this.form.reset({ fullName: '', phone: '', email: '', cnic: '', address: '' });
  }

  private loadCustomer(id: number): void {
    this.loadingCustomer = true;
    this.customerService.getById(id).subscribe({
      next: (c) => {
        this.applyCustomerToForm(c);
        this.loadingCustomer = false;
      },
      error: (err: HttpErrorResponse) => {
        this.loadingCustomer = false;
        this.snackBar.open(this.extractError(err) || 'Could not load customer.', 'Close', { duration: 5000 });
      }
    });
  }

  private applyCustomerToForm(c: Customer | Record<string, unknown>): void {
    const r = c as Record<string, unknown>;
    this.form.patchValue({
      fullName: this.readStr(r, 'fullName', 'FullName'),
      phone:    this.readStr(r, 'phone', 'Phone'),
      email:    this.readStrOrEmpty(r, 'email', 'Email'),
      cnic:     this.readStrOrEmpty(r, 'cnic', 'cNIC', 'CNIC'),
      address:  this.readStrOrEmpty(r, 'address', 'Address')
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

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading = true;
    const f = this.form.value;

    const baseDto: CreateCustomerDto = {
      fullName: f.fullName.trim(),
      phone:    f.phone.trim(),
      email:    f.email?.trim()   ? f.email.trim()   : null,
      cnic:     f.cnic?.trim()    ? f.cnic.trim()    : null,
      address:  f.address?.trim() ? f.address.trim() : null
    };

    const obs: Observable<unknown> = this.isEdit
      ? this.customerService.update({
          id: this.customerId!,
          customer: { ...baseDto } as UpdateCustomerDto
        })
      : this.customerService.create({ customer: baseDto });

    obs.subscribe({
      next: () => {
        this.snackBar.open(`Customer ${this.isEdit ? 'updated' : 'created'}`, 'Close', { duration: 2500 });
        this.router.navigate(['/customers']);
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
    if (err.status === 404) return 'Customer not found.';
    if (err.status === 401 || err.status === 403) return 'Not authorized. Sign in again.';
    return `Operation failed (${err.status || 'network'}).`;
  }
}
