import { Component, OnInit } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { Router } from '@angular/router';
import { PaymentService } from '../../../core/services/payment.service';
import { Payment } from '../../../core/models/payment.model';

@Component({
  selector: 'app-payment-list',
  templateUrl: './payment-list.component.html',
  styleUrls: ['./payment-list.component.scss']
})
export class PaymentListComponent implements OnInit {
  displayedColumns = ['bookingNumber', 'amount', 'paymentMethod', 'paymentStatus', 'paidAt'];
  dataSource = new MatTableDataSource<Payment>();
  loading = true;
  error: string | null = null;
  totalCount = 0;

  statusColors: Record<string, string> = {
    Paid: '#2e7d32', PartiallyPaid: '#f57f17', Pending: '#1565c0', Refunded: '#c62828'
  };

  constructor(private paymentService: PaymentService, private router: Router) {}

  ngOnInit(): void { this.load(); }

  load(page = 1, pageSize = 10): void {
    this.loading = true;
    this.error = null;
    this.paymentService.getAll(page, pageSize).subscribe({
      next: r => { this.dataSource.data = r.items; this.totalCount = r.totalCount; this.loading = false; },
      error: () => { this.loading = false; this.error = 'Failed to load payments.'; }
    });
  }

  getStatusColor(status: string): string {
    return this.statusColors[status] ?? '#666';
  }
}
