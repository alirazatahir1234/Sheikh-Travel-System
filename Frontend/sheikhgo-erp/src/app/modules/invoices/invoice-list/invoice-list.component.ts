import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { MatTableDataSource } from '@angular/material/table';
import { Router } from '@angular/router';
import { InvoiceService } from '../../../core/services/invoice.service';
import { Invoice, InvoiceBillingSummary, InvoiceFilter } from '../../../core/models/invoice.model';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  standalone: false,
  selector: 'app-invoice-list',
  templateUrl: './invoice-list.component.html',
  styleUrls: ['./invoice-list.component.scss']
})
export class InvoiceListComponent implements OnInit {
  displayedColumns = ['invoiceNumber', 'customerName', 'tripNumber', 'totalAmount', 'balance', 'paymentStatus', 'dueDate', 'actions'];
  dataSource = new MatTableDataSource<Invoice>();
  loading = true;
  error: string | null = null;
  totalCount = 0;
  currentPage = 1;
  currentPageSize = 10;
  filterForm: FormGroup;
  billing: InvoiceBillingSummary | null = null;

  paymentStatusOptions = [
    { value: '', label: 'All payment statuses' },
    { value: 'Pending', label: 'Pending' },
    { value: 'Paid', label: 'Paid' },
    { value: 'Overdue', label: 'Overdue' },
  ];

  paymentStatusColors: Record<string, string> = {
    Pending: '#1565c0',
    Paid: '#2e7d32',
    Overdue: '#c62828'
  };

  constructor(
    private fb: FormBuilder,
    private invoiceService: InvoiceService,
    private router: Router,
    public auth: AuthService
  ) {
    this.filterForm = this.fb.group({
      tripId: [null],
      paymentStatus: [''],
    });
  }

  ngOnInit(): void {
    this.load();
    if (this.auth.hasPermission('Invoice.View')) {
      this.invoiceService.getBillingSummary().subscribe({
        next: s => { this.billing = s; },
        error: () => { this.billing = null; }
      });
    }
  }

  private buildFilter(): InvoiceFilter {
    const v = this.filterForm.value;
    return {
      tripId: v.tripId || null,
      paymentStatus: v.paymentStatus || null
    };
  }

  load(page = 1, pageSize = this.currentPageSize): void {
    this.currentPage = page;
    this.currentPageSize = pageSize;
    this.loading = true;
    this.error = null;
    this.invoiceService.getAll(page, pageSize, this.buildFilter()).subscribe({
      next: r => {
        this.dataSource.data = r.items;
        this.totalCount = r.totalCount;
        this.loading = false;
      },
      error: () => { this.loading = false; this.error = 'Failed to load invoices.'; }
    });
  }

  applyFilter(): void { this.load(1); }
  clearFilter(): void {
    this.filterForm.reset({ tripId: null, paymentStatus: '' });
    this.load(1);
  }

  open(id: number): void { void this.router.navigate(['/invoices', id]); }
}
