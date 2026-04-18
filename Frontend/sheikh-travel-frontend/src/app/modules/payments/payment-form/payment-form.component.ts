import { Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PaymentService } from '../../../core/services/payment.service';
import { PaymentMethod } from '../../../core/models/payment.model';

@Component({
  selector: 'app-payment-form',
  templateUrl: './payment-form.component.html',
  styleUrls: ['./payment-form.component.scss']
})
export class PaymentFormComponent {
  form: FormGroup;
  loading = false;
  methods: PaymentMethod[] = ['Cash', 'Card', 'BankTransfer', 'Online'];

  constructor(
    private fb: FormBuilder,
    private paymentService: PaymentService,
    private router: Router,
    private snackBar: MatSnackBar
  ) {
    this.form = this.fb.group({
      bookingId: [null, [Validators.required, Validators.min(1)]],
      amount: [null, [Validators.required, Validators.min(1)]],
      paymentMethod: ['Cash', Validators.required],
      referenceNumber: ['']
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    this.loading = true;
    this.paymentService.create(this.form.value).subscribe({
      next: () => {
        this.snackBar.open('Payment recorded', 'Close', { duration: 2000 });
        this.router.navigate(['/payments']);
      },
      error: () => { this.loading = false; this.snackBar.open('Failed', 'Close', { duration: 3000 }); }
    });
  }
}
