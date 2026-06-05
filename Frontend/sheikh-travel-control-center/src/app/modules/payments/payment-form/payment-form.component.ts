import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PaymentService } from '../../../core/services/payment.service';
import { PaymentMethod } from '../../../core/models/payment.model';

@Component({
  selector: 'app-payment-form',
  templateUrl: './payment-form.component.html',
  styleUrls: ['./payment-form.component.scss']
})
export class PaymentFormComponent implements OnInit {
  form: FormGroup;
  loading = false;
  methods: PaymentMethod[] = ['Cash', 'Card', 'BankTransfer'];
  /** When opened from booking detail / “pay remaining”, return there after save. */
  private returnToBookingId: number | null = null;

  constructor(
    private fb: FormBuilder,
    private paymentService: PaymentService,
    private router: Router,
    private route: ActivatedRoute,
    private snackBar: MatSnackBar
  ) {
    this.form = this.fb.group({
      bookingId: [null, [Validators.required, Validators.min(1)]],
      amount: [null, [Validators.required, Validators.min(1)]],
      paymentMethod: ['Cash', Validators.required],
      transactionReference: ['']
    });
  }

  ngOnInit(): void {
    const q = this.route.snapshot.queryParamMap;
    const bidRaw = q.get('bookingId');
    const amtRaw = q.get('amount');
    const patch: { bookingId?: number; amount?: number } = {};

    if (bidRaw != null && bidRaw !== '') {
      const n = Number(bidRaw);
      if (Number.isFinite(n) && n >= 1) {
        patch.bookingId = Math.floor(n);
        this.returnToBookingId = patch.bookingId;
      }
    }
    if (amtRaw != null && amtRaw !== '') {
      const a = Number(amtRaw);
      if (Number.isFinite(a) && a >= 1) patch.amount = Math.floor(a);
    }
    if (Object.keys(patch).length) this.form.patchValue(patch);
  }

  submit(): void {
    if (this.form.invalid) return;
    this.loading = true;
    this.paymentService.create(this.form.value).subscribe({
      next: () => {
        this.snackBar.open('Payment recorded', 'Close', { duration: 2000 });
        if (this.returnToBookingId != null) {
          this.router.navigate(['/bookings', this.returnToBookingId]);
        } else {
          this.router.navigate(['/payments']);
        }
      },
      error: () => { this.loading = false; this.snackBar.open('Failed', 'Close', { duration: 3000 }); }
    });
  }
}
