import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { RentalService } from '../../../core/services/rental.service';
import { RentalBookingDetail, RentalPayment } from '../../../core/models/rental.model';
import { AuthService } from '../../../core/services/auth.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';

@Component({
  standalone: false,
  selector: 'app-rental-detail',
  templateUrl: './rental-detail.component.html',
  styleUrls: ['./rental-detail.component.scss']
})
export class RentalDetailComponent implements OnInit {
  booking: RentalBookingDetail | null = null;
  payments: RentalPayment[] = [];
  loading = true;
  error: string | null = null;
  pickupForm: FormGroup;
  returnForm: FormGroup;
  paymentForm: FormGroup;
  signForm: FormGroup;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private rentalService: RentalService,
    private fb: FormBuilder,
    private toast: UiToastService,
    public auth: AuthService
  ) {
    this.pickupForm = this.fb.group({
      odometer: [null],
      fuelLevel: [''],
      notes: [''],
      holdDeposit: [true]
    });
    this.returnForm = this.fb.group({
      odometer: [null],
      fuelLevel: [''],
      notes: [''],
      damageCharges: [0],
      damageNotes: [''],
      depositRefund: [null]
    });
    this.paymentForm = this.fb.group({
      amount: [null, [Validators.required, Validators.min(0.01)]],
      paymentMethod: ['Cash', Validators.required],
      transactionReference: [''],
      notes: ['']
    });
    this.signForm = this.fb.group({
      signedBy: ['', Validators.required]
    });
  }

  get canManage(): boolean {
    return this.auth.hasPermission('Rental.Manage');
  }

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!id) {
      void this.router.navigate(['/rental']);
      return;
    }
    this.reload(id);
  }

  reload(id = this.booking?.id): void {
    if (!id) return;
    this.loading = true;
    this.error = null;
    this.rentalService.getBooking(id).subscribe({
      next: b => {
        this.booking = b;
        this.loading = false;
        this.paymentForm.patchValue({ amount: Math.max(0, (b.totalAmount + b.depositAmount) - b.paidAmount) });
      },
      error: () => { this.loading = false; this.error = 'Failed to load rental.'; }
    });
    this.rentalService.getPayments(id).subscribe({
      next: p => { this.payments = p; },
      error: () => { this.payments = []; }
    });
  }

  confirm(): void {
    if (!this.booking) return;
    this.rentalService.confirm(this.booking.id).subscribe({
      next: () => { this.toast.success('Confirmed'); this.reload(); },
      error: e => this.toast.error(e?.error?.message || 'Confirm failed')
    });
  }

  cancel(): void {
    if (!this.booking) return;
    this.rentalService.cancel(this.booking.id).subscribe({
      next: () => { this.toast.success('Cancelled'); this.reload(); },
      error: e => this.toast.error(e?.error?.message || 'Cancel failed')
    });
  }

  pickup(): void {
    if (!this.booking) return;
    const v = this.pickupForm.value;
    this.rentalService.pickup(this.booking.id, {
      odometer: v.odometer,
      fuelLevel: v.fuelLevel || null,
      notes: v.notes || null,
      holdDeposit: !!v.holdDeposit
    }).subscribe({
      next: () => { this.toast.success('Picked up'); this.reload(); },
      error: e => this.toast.error(e?.error?.message || 'Pickup failed')
    });
  }

  returnVehicle(): void {
    if (!this.booking) return;
    const v = this.returnForm.value;
    this.rentalService.returnVehicle(this.booking.id, {
      odometer: v.odometer,
      fuelLevel: v.fuelLevel || null,
      notes: v.notes || null,
      damageCharges: +v.damageCharges || 0,
      damageNotes: v.damageNotes || null,
      depositRefund: v.depositRefund
    }).subscribe({
      next: () => { this.toast.success('Returned'); this.reload(); },
      error: e => this.toast.error(e?.error?.message || 'Return failed')
    });
  }

  close(): void {
    if (!this.booking) return;
    this.rentalService.close(this.booking.id).subscribe({
      next: () => { this.toast.success('Closed'); this.reload(); },
      error: e => this.toast.error(e?.error?.message || 'Close failed')
    });
  }

  sign(): void {
    if (!this.booking || this.signForm.invalid) return;
    this.rentalService.signAgreement(this.booking.id, { signedBy: this.signForm.value.signedBy }).subscribe({
      next: () => { this.toast.success('Agreement signed'); this.reload(); },
      error: e => this.toast.error(e?.error?.message || 'Sign failed')
    });
  }

  recordPayment(): void {
    if (!this.booking || this.paymentForm.invalid) return;
    const v = this.paymentForm.value;
    this.rentalService.recordPayment(this.booking.id, {
      amount: +v.amount,
      paymentMethod: v.paymentMethod,
      transactionReference: v.transactionReference || null,
      notes: v.notes || null
    }).subscribe({
      next: () => { this.toast.success('Payment recorded'); this.reload(); },
      error: e => this.toast.error(e?.error?.message || 'Payment failed')
    });
  }
}
