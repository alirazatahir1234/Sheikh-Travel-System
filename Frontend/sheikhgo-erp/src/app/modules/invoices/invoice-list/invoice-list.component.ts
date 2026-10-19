import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { MatTableDataSource } from '@angular/material/table';
import { Router } from '@angular/router';
import { InvoiceService } from '../../../core/services/invoice.service';
import { Invoice, InvoiceFilter } from '../../../core/models/invoice.model';

@Component({
  standalone: false,
  selector: 'app-invoice-list',
  templateUrl: './invoice-list.component.html',
  styleUrls: ['./invoice-list.component.scss']
})
export class InvoiceListComponent implements OnInit {
  displayedColumns = ['invoiceNumber', 'customerName', 'bookingNumber', 'totalAmount', 'balance', 'status', 'issuedAt', 'actions'];
  dataSource = new MatTableDataSource<Invoice>();
  loading = true;
  error: string | null = null;
  totalCount = 0;
  currentPage = 1;
  currentPageSize = 10;
  filterForm: FormGroup;

  statusOptions = [
    { value: '', label: 'All Statuses' },
    { value: 'Draft', label: 'Draft' },
    { value: 'Issued', label: 'Issued' },
    { value: 'Paid', label: 'Paid' },
    { value: 'Void', label: 'Void' }
  ];

  statusColors: Record<string, string> = {
    Draft: '#757575',
    Issued: '#1565c0',
    Paid: '#2e7d32',
    Void: '#c62828'
  };

  constructor(
    private fb: FormBuilder,
    private invoiceService: InvoiceService,
    private router: Router
  ) {
    this.filterForm = this.fb.group({
      bookingId: [null],
      status: [''],
    });
  }

  ngOnInit(): void { this.load(); }

  private buildFilter(): InvoiceFilter {
    const v = this.filterForm.value;
    return {
      bookingId: v.bookingId || null,
      status: v.status || null
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
    this.filterForm.reset({ bookingId: null, status: '' });
    this.load(1);
  }

  open(id: number): void { void this.router.navigate(['/invoices', id]); }
}
