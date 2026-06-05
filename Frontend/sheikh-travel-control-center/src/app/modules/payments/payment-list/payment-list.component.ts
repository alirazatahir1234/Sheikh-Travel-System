import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { MatTableDataSource } from '@angular/material/table';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { PaymentService } from '../../../core/services/payment.service';
import { Payment, PaymentFilter, PaymentStatus } from '../../../core/models/payment.model';

@Component({
  selector: 'app-payment-list',
  templateUrl: './payment-list.component.html',
  styleUrls: ['./payment-list.component.scss']
})
export class PaymentListComponent implements OnInit {
  displayedColumns = ['bookingId', 'amount', 'paymentMethod', 'status', 'paymentDate', 'actions'];
  dataSource = new MatTableDataSource<Payment>();
  loading = true;
  error: string | null = null;
  totalCount = 0;
  currentPage = 1;
  currentPageSize = 10;

  filterForm: FormGroup;
  statusOptions: { value: string; label: string }[] = [
    { value: '',             label: 'All Statuses'   },
    { value: 'Pending',      label: 'Pending'         },
    { value: 'PartiallyPaid',label: 'Partially Paid'  },
    { value: 'Paid',         label: 'Paid'            },
    { value: 'Refunded',     label: 'Refunded'        }
  ];

  statusColors: Record<string, string> = {
    Paid: '#2e7d32', PartiallyPaid: '#f57f17', Pending: '#1565c0', Refunded: '#c62828'
  };

  constructor(
    private fb: FormBuilder,
    private paymentService: PaymentService,
    private router: Router,
    private snackBar: MatSnackBar
  ) {
    this.filterForm = this.fb.group({
      bookingId: [null],
      status:    [''],
      dateFrom:  [null],
      dateTo:    [null]
    });
  }

  ngOnInit(): void { this.load(); }

  private buildFilter(): PaymentFilter {
    const v = this.filterForm.value;
    return {
      bookingId: v.bookingId || null,
      status:    v.status    || null,
      dateFrom:  v.dateFrom  ? new Date(v.dateFrom).toISOString().split('T')[0]  : null,
      dateTo:    v.dateTo    ? new Date(v.dateTo).toISOString().split('T')[0]    : null
    };
  }

  load(page = 1, pageSize = this.currentPageSize): void {
    this.currentPage = page;
    this.currentPageSize = pageSize;
    this.loading = true;
    this.error = null;
    this.paymentService.getAll(page, pageSize, this.buildFilter()).subscribe({
      next: r => { this.dataSource.data = r.items; this.totalCount = r.totalCount; this.loading = false; },
      error: () => { this.loading = false; this.error = 'Failed to load payments.'; }
    });
  }

  applyFilter(): void { this.load(1, this.currentPageSize); }

  clearFilter(): void {
    this.filterForm.reset({ bookingId: null, status: '', dateFrom: null, dateTo: null });
    this.load(1, this.currentPageSize);
  }

  viewReceipt(id: number): void { this.router.navigate(['/payments', id, 'receipt']); }

  refund(payment: Payment): void {
    if (!confirm(`Refund payment of PKR ${payment.amount.toLocaleString()} for Booking #${payment.bookingId}?`)) return;
    this.paymentService.updateStatus(payment.id, { status: 'Refunded' }).subscribe({
      next: () => {
        this.snackBar.open('Payment marked as refunded.', 'Close', { duration: 3000 });
        this.load(this.currentPage, this.currentPageSize);
      },
      error: () => this.snackBar.open('Failed to refund payment.', 'Close', { duration: 3000 })
    });
  }

  exportCsv(): void {
    this.paymentService.exportCsv(this.buildFilter()).subscribe({
      next: r => {
        const headers = ['ID', 'Booking ID', 'Amount', 'Method', 'Status', 'Date', 'Reference', 'Notes'];
        const rows = r.items.map(p => [
          p.id, p.bookingId, p.amount, p.paymentMethod, p.status,
          new Date(p.paymentDate).toLocaleDateString(),
          p.transactionReference ?? '', p.notes ?? ''
        ]);
        const csv = [headers, ...rows].map(r => r.join(',')).join('\n');
        const blob = new Blob([csv], { type: 'text/csv' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = `payments-${new Date().toISOString().slice(0,10)}.csv`;
        a.click(); URL.revokeObjectURL(url);
      },
      error: () => this.snackBar.open('Export failed.', 'Close', { duration: 3000 })
    });
  }

  getStatusColor(status: string): string {
    return this.statusColors[status] ?? '#666';
  }

  canRefund(payment: Payment): boolean {
    return payment.status === 'Paid' || payment.status === 'PartiallyPaid';
  }
}

