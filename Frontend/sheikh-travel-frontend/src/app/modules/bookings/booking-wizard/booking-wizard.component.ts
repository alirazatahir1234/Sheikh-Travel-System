import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, Subject, forkJoin, of } from 'rxjs';
import { map, startWith, switchMap, takeUntil } from 'rxjs/operators';

import { BookingService } from '../../../core/services/booking.service';
import { RouteService } from '../../../core/services/route.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { DriverService } from '../../../core/services/driver.service';
import { CustomerService } from '../../../core/services/customer.service';
import { DriverAllowanceRuleService } from '../../../core/services/driver-allowance-rule.service';

import { Route } from '../../../core/models/route.model';
import { Vehicle, VehicleStatusLabels } from '../../../core/models/vehicle.model';
import { Driver } from '../../../core/models/driver.model';
import { Customer, CreateCustomerDto } from '../../../core/models/customer.model';
import { PriceBreakdown } from '../../../core/models/pricing.model';
import { Booking } from '../../../core/models/booking.model';
import { CalculateDriverAllowanceResponse } from '../../../core/models/driver-allowance-rule.model';

type CustomerMode = 'EXISTING' | 'NEW';

@Component({
  selector: 'app-booking-wizard',
  templateUrl: './booking-wizard.component.html',
  styleUrls: ['./booking-wizard.component.scss']
})
export class BookingWizardComponent implements OnInit, OnDestroy {
  step1Form: FormGroup;
  step2Form: FormGroup;

  routes: Route[]        = [];
  vehicles: Vehicle[]    = [];
  drivers: Driver[]      = [];
  customers: Customer[]  = [];

  filteredRoutes$!:    Observable<Route[]>;
  filteredVehicles$!:  Observable<Vehicle[]>;
  filteredCustomers$!: Observable<Customer[]>;

  priceBreakdown: PriceBreakdown | null = null;
  createdBooking: Booking | null = null;

  loading = false;
  calculating = false;
  loadingData = true;
  creatingCustomer = false;
  error: string | null = null;

  customerMode: CustomerMode = 'EXISTING';

  allowanceCalculating = false;
  allowanceResult: CalculateDriverAllowanceResponse | null = null;
  allowanceOverridden = false;

  private readonly destroy$ = new Subject<void>();

  constructor(
    private fb: FormBuilder,
    private bookingService: BookingService,
    private routeService: RouteService,
    private vehicleService: VehicleService,
    private driverService: DriverService,
    private customerService: CustomerService,
    private allowanceRuleService: DriverAllowanceRuleService,
    private router: Router,
    private snackBar: MatSnackBar
  ) {
    this.step1Form = this.fb.group({
      customerId:        [null, [Validators.required, Validators.min(1)]],
      customerSearch:    [''],
      routeId:           [null, Validators.required],
      routeSearch:       [''],
      vehicleId:         [null, Validators.required],
      vehicleSearch:     [''],
      pickupTime:        [null, Validators.required],
      pickupTimeClock:   [this.getDefaultPickupClock(), Validators.required],
      passengerCount:    [1, [Validators.required, Validators.min(1)]],
      fuelPricePerLiter: [270, [Validators.required, Validators.min(0)]],
      driverAllowance:   [0],
      tollCharges:       [0],
      otherCharges:      [0],
      notes:             [''],

      newFullName: [''],
      newPhone:    [''],
      newEmail:    [''],
      newCnic:     [''],
      newAddress:  ['']
    });

    this.step2Form = this.fb.group({
      driverId: [null]
    });
  }

  // ---------- Lifecycle ------------------------------------------------------

  ngOnInit(): void {
    forkJoin({
      routes:    this.routeService.getAll(1, 200),
      vehicles:  this.vehicleService.getAll(1, 500),
      drivers:   this.driverService.getAll(1, 200),
      customers: this.customerService.getAll(1, 500)
    }).subscribe({
      next: ({ routes, vehicles, drivers, customers }) => {
        this.routes    = routes.items;
        this.vehicles  = vehicles.items;
        this.drivers   = drivers.items.filter(d => d.isActive !== false);
        this.customers = customers.items;
        this.wireAutocompleteStreams();
        this.loadingData = false;
      },
      error: () => {
        this.loadingData = false;
        this.error = 'Failed to load booking data. Please try again.';
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ---------- Autocomplete wiring -------------------------------------------

  private wireAutocompleteStreams(): void {
    this.filteredCustomers$ = this.step1Form.get('customerSearch')!.valueChanges.pipe(
      startWith(''),
      map(value => this.filterCustomers(typeof value === 'string' ? value : ''))
    );

    this.filteredRoutes$ = this.step1Form.get('routeSearch')!.valueChanges.pipe(
      startWith(''),
      map(value => this.filterRoutes(typeof value === 'string' ? value : ''))
    );

    this.filteredVehicles$ = this.step1Form.get('vehicleSearch')!.valueChanges.pipe(
      startWith(''),
      map(value => this.filterVehicles(typeof value === 'string' ? value : ''))
    );
  }

  private filterCustomers(term: string): Customer[] {
    const q = term.trim().toLowerCase();
    if (!q) return this.customers.slice(0, 25);
    return this.customers.filter(c =>
      (c.fullName ?? '').toLowerCase().includes(q) ||
      (c.phone    ?? '').toLowerCase().includes(q) ||
      (c.cnic     ?? '').toLowerCase().includes(q) ||
      (c.email    ?? '').toLowerCase().includes(q)
    ).slice(0, 25);
  }

  private filterRoutes(term: string): Route[] {
    const q = term.trim().toLowerCase();
    if (!q) return this.routes.slice(0, 25);
    return this.routes.filter(r =>
      (r.name        ?? '').toLowerCase().includes(q) ||
      (r.source      ?? '').toLowerCase().includes(q) ||
      (r.destination ?? '').toLowerCase().includes(q)
    ).slice(0, 25);
  }

  private filterVehicles(term: string): Vehicle[] {
    const q = term.trim().toLowerCase();
    if (!q) return this.vehicles.slice(0, 25);
    return this.vehicles.filter(v =>
      (v.name               ?? '').toLowerCase().includes(q) ||
      (v.registrationNumber ?? '').toLowerCase().includes(q) ||
      (v.model              ?? '').toLowerCase().includes(q) ||
      this.vehicleStatusLabel(v.status).toLowerCase().includes(q)
    ).slice(0, 25);
  }

  // ---------- Display helpers for autocomplete ------------------------------

  customerDisplay = (c?: Customer | null): string => {
    if (!c) return '';
    return c.phone ? `${c.fullName} — ${c.phone}` : c.fullName;
  };

  routeDisplay = (r?: Route | null): string => {
    if (!r) return '';
    return r.name || `${r.source} → ${r.destination}`;
  };

  vehicleDisplay = (v?: Vehicle | null): string => {
    if (!v) return '';
    return v.registrationNumber ? `${v.name} (${v.registrationNumber})` : v.name;
  };

  vehicleStatusLabel(status: number): string {
    return VehicleStatusLabels[status as keyof typeof VehicleStatusLabels] ?? 'Unknown';
  }

  // ---------- Option selection ----------------------------------------------

  onCustomerSelected(customer: Customer): void {
    this.step1Form.patchValue({
      customerId:     customer.id,
      customerSearch: customer
    });
  }

  onRouteSelected(route: Route): void {
    this.step1Form.patchValue({ routeId: route.id, routeSearch: route });
    this.priceBreakdown = null;
    this.refreshDriverAllowance();
  }

  onVehicleSelected(vehicle: Vehicle): void {
    this.step1Form.patchValue({ vehicleId: vehicle.id, vehicleSearch: vehicle });
    this.priceBreakdown = null;
    this.refreshDriverAllowance();
  }

  /**
   * Asks the backend to evaluate the configured driver allowance rules for the
   * current route + vehicle. If a rule matches and the user hasn't manually
   * overridden the allowance field, the returned amount is pre-filled. The
   * applied rule metadata is always stored so the UI can explain it.
   */
  refreshDriverAllowance(): void {
    const f = this.step1Form.value;
    if (!f.routeId || !f.vehicleId) {
      this.allowanceResult = null;
      return;
    }

    this.allowanceCalculating = true;
    this.allowanceRuleService.calculate({
      routeId:   f.routeId,
      vehicleId: f.vehicleId,
      tripDays:  1
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: result => {
        this.allowanceResult = result;
        this.allowanceCalculating = false;

        if (result.matchedAnyRule && !this.allowanceOverridden) {
          this.step1Form.patchValue({ driverAllowance: result.amount });
        }
      },
      error: () => {
        this.allowanceCalculating = false;
      }
    });
  }

  /** Marks the field as manually overridden so the next rule eval leaves it alone. */
  onDriverAllowanceManualInput(): void {
    this.allowanceOverridden = true;
  }

  /** Re-applies the rule result, clearing the override flag. */
  useSuggestedAllowance(): void {
    if (!this.allowanceResult?.matchedAnyRule) return;
    this.allowanceOverridden = false;
    this.step1Form.patchValue({ driverAllowance: this.allowanceResult.amount });
  }

  clearCustomer(): void {
    this.step1Form.patchValue({ customerId: null, customerSearch: '' });
  }

  // ---------- Customer mode toggle ------------------------------------------

  setCustomerMode(mode: CustomerMode): void {
    this.customerMode = mode;
    this.updateCustomerValidators();

    if (mode === 'EXISTING') {
      this.step1Form.patchValue({
        newFullName: '', newPhone: '', newEmail: '', newCnic: '', newAddress: ''
      });
    } else {
      this.clearCustomer();
    }
  }

  private updateCustomerValidators(): void {
    const customerId = this.step1Form.get('customerId')!;
    const newFullName = this.step1Form.get('newFullName')!;
    const newPhone    = this.step1Form.get('newPhone')!;

    if (this.customerMode === 'EXISTING') {
      customerId.setValidators([Validators.required, Validators.min(1)]);
      newFullName.clearValidators();
      newPhone.clearValidators();
    } else {
      customerId.clearValidators();
      newFullName.setValidators([Validators.required, Validators.maxLength(100)]);
      newPhone.setValidators([Validators.required, Validators.maxLength(20)]);
    }

    customerId.updateValueAndValidity();
    newFullName.updateValueAndValidity();
    newPhone.updateValueAndValidity();
  }

  // ---------- Price calculation ---------------------------------------------

  calculatePrice(): void {
    const f = this.step1Form.value;
    if (!f.routeId || !f.vehicleId) return;
    this.calculating = true;
    this.bookingService.calculatePrice({
      routeId:            f.routeId,
      vehicleId:          f.vehicleId,
      passengerCount:     f.passengerCount,
      fuelPricePerLiter:  f.fuelPricePerLiter,
      driverAllowance:    f.driverAllowance,
      tollCharges:        f.tollCharges,
      otherCharges:       f.otherCharges
    }).subscribe({
      next: p => { this.priceBreakdown = p; this.calculating = false; },
      error: () => {
        this.calculating = false;
        this.snackBar.open('Failed to calculate price.', 'Close', { duration: 3000 });
      }
    });
  }

  // ---------- Submission ----------------------------------------------------

  createBooking(): void {
    if (!this.validateStep1()) return;

    this.loading = true;

    this.ensureCustomerId().pipe(
      takeUntil(this.destroy$),
      switchMap(customerId => {
        const f = this.step1Form.value;
        const pickupIso = this.combineDateAndTime(f.pickupTime, f.pickupTimeClock);
        return this.bookingService.create({
          booking: {
            customerId,
            routeId:        f.routeId,
            pickupTime:     pickupIso,
            passengerCount: f.passengerCount,
            totalAmount:    this.priceBreakdown?.totalAmount ?? 0,
            notes:          f.notes || null
          }
        });
      })
    ).subscribe({
      next: booking => {
        this.createdBooking = booking;
        const vehicleId = this.step1Form.value.vehicleId;
        if (vehicleId) {
          this.bookingService.assignVehicle({ bookingId: booking.id, vehicleId }).subscribe();
        }
        this.loading = false;
      },
      error: (err: HttpErrorResponse) => {
        this.loading = false;
        this.creatingCustomer = false;
        this.snackBar.open(this.extractError(err) || 'Failed to create booking', 'Close', { duration: 4000 });
      }
    });
  }

  /**
   * Returns an observable that resolves to a customer id. If the wizard is in
   * 'NEW' mode, the new customer is created first and the returned id is
   * pushed into the form so the rest of the flow (and snapshots) has it.
   */
  private ensureCustomerId(): Observable<number> {
    if (this.customerMode === 'EXISTING') {
      const id = this.step1Form.value.customerId as number;
      return of(id);
    }

    const f = this.step1Form.value;
    const dto: CreateCustomerDto = {
      fullName: (f.newFullName ?? '').trim(),
      phone:    (f.newPhone    ?? '').trim(),
      email:    f.newEmail?.trim()   ? f.newEmail.trim()   : null,
      cnic:     f.newCnic?.trim()    ? f.newCnic.trim()    : null,
      address:  f.newAddress?.trim() ? f.newAddress.trim() : null
    };

    this.creatingCustomer = true;
    return this.customerService.create({ customer: dto }).pipe(
      map(newId => {
        const newCustomer: Customer = {
          id: newId, fullName: dto.fullName, phone: dto.phone,
          email: dto.email ?? null, address: dto.address ?? null,
          cnic: dto.cnic ?? null, isActive: true, createdAt: new Date().toISOString()
        };
        this.customers = [newCustomer, ...this.customers];
        this.step1Form.patchValue({ customerId: newId, customerSearch: newCustomer });
        this.creatingCustomer = false;
        this.snackBar.open('Customer created.', 'Close', { duration: 2000 });
        return newId;
      })
    );
  }

  private validateStep1(): boolean {
    this.updateCustomerValidators();
    this.step1Form.markAllAsTouched();

    if (this.step1Form.invalid) {
      this.snackBar.open('Please fill all required fields.', 'Close', { duration: 3000 });
      return false;
    }

    if (!this.priceBreakdown) {
      this.snackBar.open('Calculate the price before continuing.', 'Close', { duration: 3000 });
      return false;
    }

    if (this.customerMode === 'EXISTING' && !this.step1Form.value.customerId) {
      this.snackBar.open('Pick an existing customer or switch to New.', 'Close', { duration: 3000 });
      return false;
    }

    if (this.customerMode === 'NEW') {
      const f = this.step1Form.value;
      if (!f.newFullName?.trim() || !f.newPhone?.trim()) {
        this.snackBar.open('New customer needs a name and phone.', 'Close', { duration: 3000 });
        return false;
      }
    }
    return true;
  }

  // ---------- Step 2 --------------------------------------------------------

  assignAndFinish(): void {
    if (!this.createdBooking) return;
    const driverId = this.step2Form.value.driverId;

    const after = () => {
      this.snackBar.open('Booking created successfully!', 'Close', { duration: 3000 });
      this.router.navigate(['/bookings']);
    };

    if (driverId) {
      this.bookingService.assignDriver({ bookingId: this.createdBooking.id, driverId })
        .subscribe({ next: after, error: () => after() });
    } else {
      after();
    }
  }

  // ---------- Misc ----------------------------------------------------------

  trackById(_index: number, item: { id: number }): number { return item.id; }

  private getDefaultPickupClock(): string {
    const now = new Date();
    const hh = String(now.getHours()).padStart(2, '0');
    const mm = String(now.getMinutes()).padStart(2, '0');
    return `${hh}:${mm}`;
  }

  private combineDateAndTime(dateValue: Date | string | null, timeValue: string | null): string {
    const date = dateValue ? new Date(dateValue) : new Date();
    const [hours, minutes] = (timeValue || '09:00').split(':').map(v => Number(v));

    const merged = new Date(date);
    merged.setHours(Number.isFinite(hours) ? hours : 9, Number.isFinite(minutes) ? minutes : 0, 0, 0);
    return merged.toISOString();
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
    return `Operation failed (${err?.status || 'network'}).`;
  }
}
