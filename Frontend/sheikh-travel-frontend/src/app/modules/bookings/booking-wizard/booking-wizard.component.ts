import { Component, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatStepper } from '@angular/material/stepper';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, Subject, forkJoin, of } from 'rxjs';
import { finalize, map, startWith, switchMap, takeUntil, tap } from 'rxjs/operators';

import { BookingService } from '../../../core/services/booking.service';
import { PaymentService } from '../../../core/services/payment.service';
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
import { PaymentMethod } from '../../../core/models/payment.model';
import { CalculateDriverAllowanceResponse } from '../../../core/models/driver-allowance-rule.model';
import { PaymentPlan } from '../../../shared/utils/booking-payment-plan.util';
import {
  extractTransactionReferenceFromFileName,
  extractTransactionReferenceFromReceiptOcrText
} from '../../../shared/utils/transaction-reference-extract.util';

type CustomerMode = 'EXISTING' | 'NEW';

@Component({
  selector: 'app-booking-wizard',
  templateUrl: './booking-wizard.component.html',
  styleUrls: ['./booking-wizard.component.scss']
})
export class BookingWizardComponent implements OnInit, OnDestroy {
  @ViewChild('stepper') stepper?: MatStepper;

  step1Form!: FormGroup;
  /** Driver assignment only. */
  step2Form!: FormGroup;
  /** Payment plan and payment fields. */
  step3Form!: FormGroup;
  minPickupDateTime = '';

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
  /** True while assign-driver + payment API calls run from Confirm. */
  finishing = false;
  calculating = false;
  loadingData = true;
  creatingCustomer = false;
  error: string | null = null;

  customerMode: CustomerMode = 'EXISTING';
  readonly paymentMethods: PaymentMethod[] = ['Cash', 'Card', 'BankTransfer'];

  /** Bank transfer: optional proof image (JPEG data URL after compression). */
  bankTransferReceiptDataUrl: string | null = null;
  bankTransferReceiptFileName: string | null = null;
  bankTransferReceiptCompressing = false;
  /** True while Tesseract is reading the slip to suggest a reference. */
  bankTransferReceiptOcrBusy = false;
  /** User picked a PDF (reference from file name only; no image payload). */
  bankTransferReceiptIsPdf = false;
  private readonly bankTransferReceiptMaxBytes = 15 * 1024 * 1024;

  allowanceCalculating = false;
  allowanceResult: CalculateDriverAllowanceResponse | null = null;
  allowanceOverridden = false;
  cnicOcrBusy = false;
  cnicUploadBusy = false;
  cnicPreviewDataUrl: string | null = null;
  cnicFileName: string | null = null;

  selectedVehicle: Vehicle | null = null;

  private readonly destroy$ = new Subject<void>();

  constructor(
    private fb: FormBuilder,
    private bookingService: BookingService,
    private paymentService: PaymentService,
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
      driverRequired:    [true],
      pickupTime:        [this.getDefaultPickupDateTime(), Validators.required],
      pickupTimeClock:   [''],  // kept for backward compat, not actively used
      passengerCount:    [1, [Validators.required, Validators.min(1)]],
      fuelPricePerLiter: [270, [Validators.required, Validators.min(0)]],
      driverAllowance:   [0],
      tollCharges:       [0],
      otherCharges:      [0],
      isRoundTrip:       [false],
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

    this.step3Form = this.fb.group({
      paymentPlan: ['FULL' as PaymentPlan, Validators.required],
      partialAmount: [null],
      paymentMethod: ['Cash' as PaymentMethod, Validators.required],
      paymentMethodName: [''],
      transactionReference: [''],
      paymentNotes: ['']
    });

    this.step3Form.get('paymentMethod')!.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(m => {
        if (m !== 'BankTransfer') this.clearBankTransferReceiptPreview();
      });
    this.step3Form.get('paymentPlan')!.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(p => {
        if (p === 'PAY_LATER') this.clearBankTransferReceiptPreview();
      });
  }

  // ---------- Lifecycle ------------------------------------------------------

  ngOnInit(): void {
    this.minPickupDateTime = this.getMinPickupDateTime();
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
      error: (err: unknown) => {
        this.loadingData = false;
        this.error = this.describeBookingWizardLoadError(err);
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private describeBookingWizardLoadError(err: unknown): string {
    if (err instanceof HttpErrorResponse && err.status === 0) {
      return 'Cannot reach the API (connection refused). Start the backend from Backend/SheikhTravelSystem.API with: dotnet run --launch-profile http — then refresh this page.';
    }
    return 'Failed to load booking data. Please try again.';
  }

  // ---------- Autocomplete wiring -------------------------------------------

  private wireAutocompleteStreams(): void {
    this.filteredCustomers$ = this.step1Form.get('customerSearch')!.valueChanges.pipe(
      tap(value => {
        if (typeof value === 'string') {
          this.step1Form.patchValue({ customerId: null }, { emitEvent: false });
        }
      }),
      startWith(''),
      map(value => this.filterCustomers(typeof value === 'string' ? value : ''))
    );

    this.filteredRoutes$ = this.step1Form.get('routeSearch')!.valueChanges.pipe(
      tap(value => {
        if (typeof value === 'string') {
          this.step1Form.patchValue({ routeId: null }, { emitEvent: false });
          this.priceBreakdown = null;
          this.allowanceResult = null;
        }
      }),
      startWith(''),
      map(value => this.filterRoutes(typeof value === 'string' ? value : ''))
    );

    this.filteredVehicles$ = this.step1Form.get('vehicleSearch')!.valueChanges.pipe(
      tap(value => {
        if (typeof value === 'string') {
          this.step1Form.patchValue({ vehicleId: null }, { emitEvent: false });
          this.selectedVehicle = null;
          this.priceBreakdown = null;
          this.allowanceResult = null;
        }
      }),
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
    this.selectedVehicle = vehicle;
    this.step1Form.patchValue({ vehicleId: vehicle.id, vehicleSearch: vehicle });
    this.priceBreakdown = null;
    this.refreshDriverAllowance();
  }

  setDriverRequired(required: boolean): void {
    this.step1Form.patchValue({ driverRequired: required });
    if (!required) {
      this.allowanceOverridden = false;
      this.allowanceResult = null;
      this.step1Form.patchValue({ driverAllowance: 0 });
    } else {
      this.refreshDriverAllowance();
    }
  }

  /**
   * Asks the backend to evaluate the configured driver allowance rules for the
   * current route + vehicle. If a rule matches and the user hasn't manually
   * overridden the allowance field, the returned amount is pre-filled. The
   * applied rule metadata is always stored so the UI can explain it.
   */
  refreshDriverAllowance(): void {
    if (!this.step1Form.value.driverRequired) {
      this.allowanceResult = null;
      this.allowanceOverridden = false;
      this.step1Form.patchValue({ driverAllowance: 0 });
      return;
    }

    const f = this.step1Form.value;
    const routeId = this.resolveRouteId();
    const vehicleId = this.resolveVehicleId();
    if (!routeId || !vehicleId) {
      this.allowanceResult = null;
      return;
    }

    this.allowanceCalculating = true;
    this.allowanceRuleService.calculate({
      routeId,
      vehicleId,
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
    const routeId = this.resolveRouteId();
    const vehicleId = this.resolveVehicleId();
    if (!routeId || !vehicleId) {
      this.priceBreakdown = null;
      this.snackBar.open('Please select route and vehicle from the suggestions.', 'Close', { duration: 3200 });
      return;
    }
    const routeExists = this.routes.some(r => r.id === routeId);
    const vehicleExists = this.vehicles.some(v => v.id === vehicleId);
    if (!routeExists || !vehicleExists) {
      this.priceBreakdown = null;
      this.snackBar.open('Please select route and vehicle from the suggestions.', 'Close', { duration: 3200 });
      return;
    }
    const driverAllowanceAmount = this.step1Form.value.driverRequired
      ? (f.driverAllowance ?? 0)
      : 0;
    if (!this.step1Form.value.driverRequired && f.driverAllowance !== 0) {
      this.step1Form.patchValue({ driverAllowance: 0 });
    }

    this.calculating = true;
    this.bookingService.calculatePrice({
      routeId,
      vehicleId,
      fuelPricePerLiter: f.fuelPricePerLiter ?? 270,
      driverAllowance:   driverAllowanceAmount,
      tollCharges:       f.tollCharges        ?? 0,
      otherCharges:      f.otherCharges       ?? 0,
      isRoundTrip:       f.isRoundTrip        ?? false
    }).subscribe({
      next: p => { this.priceBreakdown = p; this.calculating = false; },
      error: (err: HttpErrorResponse) => {
        this.calculating = false;
        const fallback = this.buildLocalPriceBreakdown(routeId, vehicleId);
        if (fallback) {
          this.priceBreakdown = fallback;
          this.snackBar.open('Calculated locally (server unavailable).', 'Close', { duration: 2800 });
          return;
        }
        this.snackBar.open(this.extractError(err) || 'Failed to calculate price.', 'Close', { duration: 3500 });
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
    ).pipe(
      takeUntil(this.destroy$),
      switchMap((newId: number) => {
        const vehicleId = this.step1Form.value.vehicleId;
        const assign$ = vehicleId
          ? this.bookingService.assignVehicle({ bookingId: newId, vehicleId })
          : of(true as unknown as boolean);
        return assign$.pipe(
          switchMap(() => this.bookingService.getById(newId))
        );
      })
    ).subscribe({
      next: fullBooking => {
        this.createdBooking = fullBooking;
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

    const pickupIso = this.combineDateAndTime(this.step1Form.value.pickupTime, this.step1Form.value.pickupTimeClock);
    if (!this.isPickupInFuture(pickupIso)) {
      this.snackBar.open('Pickup time must be in the future.', 'Close', { duration: 3200 });
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

  // ---------- Step 2 / 3: Driver & payment ------------------------------------

  /**
   * True when payment choices allow leaving the Payment step or tapping Finish.
   * Partial plan requires a positive amount strictly below the booking total.
   */
  canProceedToConfirm(): boolean {
    const plan = this.step3Form.value.paymentPlan as PaymentPlan;
    if (plan === 'PAY_LATER') return true;
    if (plan === 'PARTIAL') {
      const total = this.totalBookingAmount;
      const amt = Number(this.step3Form.value.partialAmount);
      if (!Number.isFinite(amt) || amt <= 0 || amt >= total) return false;
    }
    return true;
  }

  assignAndFinish(): void {
    if (!this.createdBooking) {
      this.snackBar.open('Create the booking from Trip Details first (use Next: Assign Driver).', 'Close', { duration: 4000 });
      return;
    }
    if (!this.canProceedToConfirm()) {
      this.snackBar.open(
        'Partial payment requires an amount greater than 0 and less than the total. Go back to Payment to fix it.',
        'Close',
        { duration: 5000 }
      );
      this.goToPaymentStep();
      return;
    }

    const driverId = this.step2Form.value.driverId;
    const paymentPlan = this.step3Form.value.paymentPlan as PaymentPlan;

    const afterSuccess = () => {
      this.snackBar.open('Booking saved. Payment and driver updates are complete.', 'Close', { duration: 3200 });
      this.router.navigate(['/bookings']);
    };

    const assignment$ = driverId
      ? this.bookingService.assignDriver({ bookingId: this.createdBooking.id, driverId })
      : of(true as unknown as boolean);

    this.finishing = true;
    assignment$
      .pipe(
        takeUntil(this.destroy$),
        switchMap(() => {
          if (paymentPlan === 'PAY_LATER') return of(null);

          const totalAmount = this.totalBookingAmount;
          const paymentMethod = this.step3Form.value.paymentMethod as PaymentMethod;
          const amount = paymentPlan === 'FULL'
            ? totalAmount
            : Number(this.step3Form.value.partialAmount ?? 0);
          const paymentMethodName = (this.step3Form.value.paymentMethodName || '').trim();
          const paymentNotes = (this.step3Form.value.paymentNotes || '').trim();
          const composedNotes = [paymentMethodName ? `Method Name: ${paymentMethodName}` : '', paymentNotes]
            .filter(Boolean)
            .join(' | ');

          if (paymentPlan === 'PARTIAL') {
            if (!Number.isFinite(amount) || amount <= 0) {
              this.snackBar.open('Enter a valid partial payment amount.', 'Close', { duration: 3200 });
              return of('BLOCKED' as const);
            }
            if (amount >= totalAmount) {
              this.snackBar.open('Partial payment must be less than total amount.', 'Close', { duration: 3200 });
              return of('BLOCKED' as const);
            }
          }

          const receiptImageData =
            paymentMethod === 'BankTransfer' && this.bankTransferReceiptDataUrl
              ? this.bankTransferReceiptDataUrl
              : undefined;
          return this.paymentService.create({
            bookingId: this.createdBooking!.id,
            amount,
            paymentMethod,
            transactionReference: (this.step3Form.value.transactionReference || '').trim() || undefined,
            notes: composedNotes || undefined,
            receiptImageData
          });
        }),
        finalize(() => {
          this.finishing = false;
        })
      )
      .subscribe({
        next: result => {
          if (result === 'BLOCKED') {
            this.goToPaymentStep();
            return;
          }
          afterSuccess();
        },
        error: (err: unknown) => {
          const msg = err instanceof HttpErrorResponse
            ? this.extractError(err)
            : 'Could not complete driver assignment or payment. Please try again.';
          this.snackBar.open(msg, 'Close', { duration: 5500 });
        }
      });
  }

  /** Payment step index in the linear stepper (Trip=0, Driver=1, Payment=2, Confirm=3). */
  private goToPaymentStep(): void {
    const s = this.stepper;
    if (!s) return;
    Promise.resolve().then(() => {
      s.selectedIndex = 2;
    });
  }

  selectPaymentPlan(plan: PaymentPlan): void {
    this.step3Form.patchValue({ paymentPlan: plan });
    if (plan !== 'PARTIAL') {
      this.step3Form.patchValue({ partialAmount: null });
    }
    if (plan === 'PAY_LATER') this.clearBankTransferReceiptPreview();
  }

  clearBankTransferReceiptPreview(): void {
    this.bankTransferReceiptDataUrl = null;
    this.bankTransferReceiptFileName = null;
    this.bankTransferReceiptIsPdf = false;
  }

  onBankTransferReceiptDrop(ev: DragEvent): void {
    ev.preventDefault();
    const file = ev.dataTransfer?.files?.item(0);
    if (file) void this.ingestBankTransferReceiptFile(file);
  }

  onBankTransferReceiptFileSelected(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.item(0);
    input.value = '';
    if (file) void this.ingestBankTransferReceiptFile(file);
  }

  onCnicFileSelected(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.item(0);
    input.value = '';
    if (file) void this.ingestCnicFile(file);
  }

  onCnicDrop(ev: DragEvent): void {
    ev.preventDefault();
    const file = ev.dataTransfer?.files?.item(0);
    if (file) void this.ingestCnicFile(file);
  }

  private async ingestCnicFile(file: File): Promise<void> {
    if (file.size > this.bankTransferReceiptMaxBytes) {
      this.snackBar.open('CNIC file must be 15 MB or smaller.', 'Close', { duration: 3500 });
      return;
    }

    // OCR path currently supports images. PDFs are accepted for future backend OCR flow.
    if (file.type === 'application/pdf') {
      this.cnicFileName = file.name;
      this.cnicPreviewDataUrl = null;
      this.snackBar.open('PDF uploaded. OCR is currently supported for image CNIC files.', 'Close', { duration: 4500 });
      return;
    }

    if (!file.type.startsWith('image/')) {
      this.snackBar.open('Please upload CNIC as JPG, PNG, WebP, or PDF.', 'Close', { duration: 3500 });
      return;
    }

    this.customerMode = 'NEW';
    this.updateCustomerValidators();
    this.cnicUploadBusy = true;
    try {
      const imageDataUrl = await this.compressImageToJpegDataUrl(file, 1600, 0.9);
      this.cnicPreviewDataUrl = imageDataUrl;
      this.cnicFileName = file.name;
      await this.extractAndApplyCnicData(imageDataUrl);
    } catch {
      this.snackBar.open('Could not process CNIC image. Try a clearer file.', 'Close', { duration: 3500 });
    } finally {
      this.cnicUploadBusy = false;
    }
  }

  private async extractAndApplyCnicData(imageDataUrl: string): Promise<void> {
    this.cnicOcrBusy = true;
    try {
      const { createWorker } = await import('tesseract.js');
      const worker = await createWorker('eng');
      const { data } = await worker.recognize(imageDataUrl);
      await worker.terminate();

      const extracted = this.parseCnicOcrText(data.text || '');
      this.applyCnicExtraction(extracted);
    } catch {
      this.snackBar.open('OCR failed. You can still fill fields manually.', 'Close', { duration: 3500 });
    } finally {
      this.cnicOcrBusy = false;
    }
  }

  private parseCnicOcrText(text: string): { fullName?: string; cnic?: string; address?: string } {
    const normalized = text.replace(/\r/g, '\n');
    const lines = normalized.split('\n').map(l => l.trim()).filter(Boolean);

    const cnicMatch =
      normalized.match(/\b\d{5}-\d{7}-\d\b/) ||
      normalized.match(/\b\d{13}\b/);
    const cnic = cnicMatch?.[0]
      ? (cnicMatch[0].includes('-')
        ? cnicMatch[0]
        : `${cnicMatch[0].slice(0, 5)}-${cnicMatch[0].slice(5, 12)}-${cnicMatch[0].slice(12)}`)
      : undefined;

    const ignoreTokens = ['islamic republic', 'national identity', 'pakistan', 'card', 'father', 'husband', 'date of birth', 'gender'];
    let fullName: string | undefined;
    for (const line of lines) {
      const lower = line.toLowerCase();
      if (line.length < 3 || line.length > 50) continue;
      if (/\d/.test(line)) continue;
      if (ignoreTokens.some(t => lower.includes(t))) continue;
      if (/^[A-Z][A-Z\s.]+$/.test(line) || /^[A-Z][a-z]+(?:\s+[A-Z][a-z]+)+$/.test(line)) {
        fullName = line.replace(/\s+/g, ' ').trim();
        break;
      }
    }

    let address: string | undefined;
    const addressLine = lines.find(l => /address|addr/i.test(l));
    if (addressLine) {
      address = addressLine.replace(/address[:\s-]*/i, '').trim();
    }

    return { fullName, cnic, address };
  }

  private applyCnicExtraction(extracted: { fullName?: string; cnic?: string; address?: string }): void {
    const patch: Record<string, string> = {};
    if (extracted.fullName) patch['newFullName'] = extracted.fullName;
    if (extracted.cnic) patch['newCnic'] = extracted.cnic;
    if (extracted.address) patch['newAddress'] = extracted.address;

    if (Object.keys(patch).length > 0) {
      this.step1Form.patchValue(patch);
    }

    if (extracted.cnic) {
      const existing = this.customers.find(c => (c.cnic || '').trim() === extracted.cnic);
      if (existing) {
        this.setCustomerMode('EXISTING');
        this.onCustomerSelected(existing);
        this.snackBar.open('Existing customer found by CNIC and loaded.', 'Close', { duration: 3500 });
        return;
      }
    }

    const filled = [
      extracted.fullName ? 'name' : '',
      extracted.cnic ? 'CNIC' : '',
      extracted.address ? 'address' : ''
    ].filter(Boolean);
    if (filled.length > 0) {
      this.snackBar.open(`CNIC OCR filled: ${filled.join(', ')}. Please verify before saving.`, 'Close', { duration: 4000 });
    } else {
      this.snackBar.open('No reliable CNIC fields found. Please fill manually.', 'Close', { duration: 3500 });
    }
  }

  private async ingestBankTransferReceiptFile(file: File): Promise<void> {
    if (file.size > this.bankTransferReceiptMaxBytes) {
      this.snackBar.open('File must be 15 MB or smaller.', 'Close', { duration: 4000 });
      return;
    }

    if (file.type === 'application/pdf') {
      this.bankTransferReceiptDataUrl = null;
      this.bankTransferReceiptIsPdf = true;
      this.bankTransferReceiptFileName = file.name;
      this.applyTransactionReferenceFromFileNameOnly(file.name, 'PDF');
      return;
    }

    if (!file.type.startsWith('image/')) {
      this.snackBar.open('Please choose an image (JPEG, PNG, WebP) or a PDF.', 'Close', { duration: 3500 });
      return;
    }

    this.bankTransferReceiptIsPdf = false;
    this.bankTransferReceiptCompressing = true;
    try {
      this.bankTransferReceiptDataUrl = await this.compressImageToJpegDataUrl(file);
      this.bankTransferReceiptFileName = file.name;
      this.applyTransactionReferenceFromUpload(file, this.bankTransferReceiptDataUrl);
    } catch {
      this.snackBar.open('Could not process that image.', 'Close', { duration: 3500 });
      this.clearBankTransferReceiptPreview();
    } finally {
      this.bankTransferReceiptCompressing = false;
    }
  }

  private getTransactionReferenceTrimmed(): string {
    return String(this.step3Form.get('transactionReference')?.value ?? '').trim();
  }

  /** Fill reference from slip file name (works for e.g. TRANSACTION_RECEIPT_ABC123.pdf). */
  private applyTransactionReferenceFromFileNameOnly(fileName: string, sourceLabel: string): void {
    const fromName = extractTransactionReferenceFromFileName(fileName);
    if (!fromName) {
      this.snackBar.open(
        'Could not detect a reference from the file name. Enter it manually or upload a clear screenshot.',
        'Close',
        { duration: 4500 }
      );
      return;
    }
    if (!this.getTransactionReferenceTrimmed()) {
      this.step3Form.patchValue({ transactionReference: fromName });
      this.snackBar.open(`Transaction reference filled from ${sourceLabel} file name.`, 'Close', { duration: 3000 });
    }
  }

  /** File name first, then optional OCR on the image data URL. */
  private applyTransactionReferenceFromUpload(file: File, imageDataUrl: string): void {
    const fromName = extractTransactionReferenceFromFileName(file.name);
    if (fromName && !this.getTransactionReferenceTrimmed()) {
      this.step3Form.patchValue({ transactionReference: fromName });
      this.snackBar.open('Transaction reference filled from file name.', 'Close', { duration: 2500 });
    }
    void this.maybeFillReferenceFromReceiptOcr(imageDataUrl, fromName);
  }

  private async maybeFillReferenceFromReceiptOcr(
    imageDataUrl: string,
    fromFileName: string | null
  ): Promise<void> {
    this.bankTransferReceiptOcrBusy = true;
    try {
      const { createWorker } = await import('tesseract.js');
      const worker = await createWorker('eng');
      const { data } = await worker.recognize(imageDataUrl);
      await worker.terminate();
      const ocrRef = extractTransactionReferenceFromReceiptOcrText(data.text);
      const current = this.getTransactionReferenceTrimmed();
      if (ocrRef && !current) {
        this.step3Form.patchValue({ transactionReference: ocrRef });
        this.snackBar.open('Transaction reference filled from receipt scan.', 'Close', { duration: 2800 });
        return;
      }
      if (
        ocrRef &&
        fromFileName &&
        current.toUpperCase() === fromFileName.toUpperCase() &&
        ocrRef.toUpperCase() !== current.toUpperCase()
      ) {
        this.step3Form.patchValue({ transactionReference: ocrRef });
        this.snackBar.open('Transaction reference updated from receipt scan.', 'Close', { duration: 2800 });
      }
    } catch {
      /* OCR is best-effort; filename hint may still be enough */
    } finally {
      this.bankTransferReceiptOcrBusy = false;
    }
  }

  private compressImageToJpegDataUrl(file: File, maxDim = 1280, quality = 0.82): Promise<string> {
    return new Promise((resolve, reject) => {
      const url = URL.createObjectURL(file);
      const img = new Image();
      img.onload = () => {
        URL.revokeObjectURL(url);
        try {
          let { width, height } = img;
          if (width > maxDim || height > maxDim) {
            if (width >= height) {
              height = Math.round((height * maxDim) / width);
              width = maxDim;
            } else {
              width = Math.round((width * maxDim) / height);
              height = maxDim;
            }
          }
          const canvas = document.createElement('canvas');
          canvas.width = width;
          canvas.height = height;
          const ctx = canvas.getContext('2d');
          if (!ctx) {
            reject(new Error('Canvas unsupported'));
            return;
          }
          ctx.drawImage(img, 0, 0, width, height);
          resolve(canvas.toDataURL('image/jpeg', quality));
        } catch (e) {
          reject(e);
        }
      };
      img.onerror = () => {
        URL.revokeObjectURL(url);
        reject(new Error('Image load failed'));
      };
      img.src = url;
    });
  }

  /** Display text for payment method radios (API value stays Cash | Card | BankTransfer). */
  paymentMethodLabel(method: PaymentMethod): string {
    switch (method) {
      case 'BankTransfer':
        return 'Bank transfer';
      default:
        return method;
    }
  }

  paymentDescription(plan: PaymentPlan): string {
    switch (plan) {
      case 'FULL':
        return 'Collect the complete booking amount now and close payment.';
      case 'PARTIAL':
        return 'Collect part of the amount now and settle the remaining balance later.';
      default:
        return 'Skip payment for now. Booking will be created without an initial payment.';
    }
  }

  get totalBookingAmount(): number {
    return Number(this.createdBooking?.totalAmount ?? this.priceBreakdown?.totalAmount ?? 0);
  }

  /** Confirm step: server name if already assigned, else driver chosen in step 2. */
  getConfirmDriverDisplay(): string {
    const name = this.createdBooking?.driverName?.trim();
    if (name) return name;
    const id = this.step2Form?.value?.driverId as number | null | undefined;
    if (!id) return '— Not assigned yet —';
    return this.drivers.find(d => d.id === id)?.fullName ?? '—';
  }

  // ---------- Misc ----------------------------------------------------------

  trackById(_index: number, item: { id: number }): number { return item.id; }

  absVal(n: number): number { return Math.abs(n ?? 0); }

  openDateTimePicker(input: HTMLInputElement): void {
    if (input.showPicker) {
      input.showPicker();
    } else {
      input.focus();
    }
  }

  private getDefaultPickupDateTime(): string {
    const now = new Date();
    // Keep default safely in the future to satisfy backend validation.
    now.setMinutes(now.getMinutes() + 30, 0, 0);
    // Format as 'YYYY-MM-DDTHH:mm' (datetime-local input value)
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}T${pad(now.getHours())}:${pad(now.getMinutes())}`;
  }

  private getMinPickupDateTime(): string {
    const now = new Date();
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}T${pad(now.getHours())}:${pad(now.getMinutes())}`;
  }

  private combineDateAndTime(dateValue: Date | string | null, _timeValue: string | null): string {
    // datetime-local produces 'YYYY-MM-DDTHH:mm' — parse as local time
    if (typeof dateValue === 'string' && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/.test(dateValue)) {
      return new Date(dateValue).toISOString();
    }
    // Fallback: legacy Date object path
    const date = dateValue ? new Date(dateValue) : new Date();
    return date.toISOString();
  }

  private isPickupInFuture(pickupIso: string): boolean {
    const pickupTime = new Date(pickupIso).getTime();
    if (!Number.isFinite(pickupTime)) return false;
    // Small buffer avoids race-condition with server-time checks.
    const threshold = Date.now() + 60_000;
    return pickupTime > threshold;
  }

  private buildLocalPriceBreakdown(routeId: number, vehicleId: number): PriceBreakdown | null {
    const route = this.routes.find(r => r.id === routeId);
    const vehicle = this.vehicles.find(v => v.id === vehicleId);
    if (!route || !vehicle || !vehicle.fuelAverage || vehicle.fuelAverage <= 0) return null;

    const fuelPricePerLiter = Number(this.step1Form.value.fuelPricePerLiter ?? 270);
    const driverAllowance = this.step1Form.value.driverRequired
      ? Number(this.step1Form.value.driverAllowance ?? 0)
      : 0;
    const tollCharges = Number(this.step1Form.value.tollCharges ?? 0);
    const otherCharges = Number(this.step1Form.value.otherCharges ?? 0);
    const isRoundTrip = !!this.step1Form.value.isRoundTrip;
    const tripMultiplier = isRoundTrip ? 2 : 1;

    const fuelCost = (route.distance / vehicle.fuelAverage) * fuelPricePerLiter * tripMultiplier;
    const totalAmount = fuelCost + driverAllowance + tollCharges + otherCharges;
    return {
      distance: route.distance,
      fuelAverage: vehicle.fuelAverage,
      fuelPricePerLiter,
      fuelCost: Number(fuelCost.toFixed(2)),
      driverAllowance,
      tollCharges,
      otherCharges,
      totalAmount: Number(totalAmount.toFixed(2)),
      isRoundTrip
    };
  }

  private resolveRouteId(): number | null {
    const rawId = this.step1Form.value.routeId;
    const fromControl = Number(rawId);
    if (Number.isInteger(fromControl) && fromControl > 0) return fromControl;

    const selectedRoute = this.step1Form.value.routeSearch as Route | string | null | undefined;
    if (selectedRoute && typeof selectedRoute === 'object' && Number.isInteger(selectedRoute.id) && selectedRoute.id > 0) {
      this.step1Form.patchValue({ routeId: selectedRoute.id }, { emitEvent: false });
      return selectedRoute.id;
    }
    return null;
  }

  private resolveVehicleId(): number | null {
    const rawId = this.step1Form.value.vehicleId;
    const fromControl = Number(rawId);
    if (Number.isInteger(fromControl) && fromControl > 0) return fromControl;

    const selectedVehicle = this.step1Form.value.vehicleSearch as Vehicle | string | null | undefined;
    if (selectedVehicle && typeof selectedVehicle === 'object' && Number.isInteger(selectedVehicle.id) && selectedVehicle.id > 0) {
      this.step1Form.patchValue({ vehicleId: selectedVehicle.id }, { emitEvent: false });
      return selectedVehicle.id;
    }
    return null;
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
