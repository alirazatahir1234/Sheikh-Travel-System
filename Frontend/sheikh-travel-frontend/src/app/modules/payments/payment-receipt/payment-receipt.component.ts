import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { PaymentService } from '../../../core/services/payment.service';
import { PaymentDetail } from '../../../core/models/payment.model';

@Component({
  selector: 'app-payment-receipt',
  templateUrl: './payment-receipt.component.html',
  styleUrls: ['./payment-receipt.component.scss']
})
export class PaymentReceiptComponent implements OnInit {
  payment: PaymentDetail | null = null;
  loading = true;
  error: string | null = null;
  receiptDate = new Date();

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private paymentService: PaymentService
  ) {}

  ngOnInit(): void {
    const id = +this.route.snapshot.paramMap.get('id')!;
    this.paymentService.getById(id).subscribe({
      next: p => { this.payment = p; this.loading = false; },
      error: () => { this.loading = false; this.error = 'Failed to load receipt.'; }
    });
  }

  print(): void { window.print(); }

  back(): void { this.router.navigate(['/payments']); }
}
