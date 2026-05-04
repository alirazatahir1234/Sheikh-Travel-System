import { Component, OnInit, ViewChild } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { MatPaginator } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { DatePipe } from '@angular/common';
import { SelectionModel } from '@angular/cdk/collections';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

import { CustomerService } from '../../../core/services/customer.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { Customer } from '../../../core/models/customer.model';

type ActiveFilter = 'ALL' | 'ACTIVE' | 'INACTIVE';
type RecencyFilter = 'ALL' | 'NEW' | 'RETURNING';

/** A customer is considered "new" when they were created within this many days. */
const NEW_CUSTOMER_DAYS = 30;
const DAY_MS = 86_400_000;

interface CustomerFilters {
  search: string;
  active: ActiveFilter;
  recency: RecencyFilter;
}

interface QuickChip {
  label: string;
  value: RecencyFilter;
  count: number;
}

@Component({
  selector: 'app-customer-list',
  templateUrl: './customer-list.component.html',
  styleUrls: ['./customer-list.component.scss'],
  providers: [DatePipe]
})
export class CustomerListComponent implements OnInit {
  displayedColumns = [
    'select', 'fullName', 'phone', 'email', 'cnic',
    'address', 'isActive', 'createdAt', 'actions'
  ];

  dataSource = new MatTableDataSource<Customer>();
  allCustomers: Customer[] = [];

  loading = true;
  deleting = false;
  error: string | null = null;
  totalCount = 0;

  readonly selection = new SelectionModel<Customer>(true, []);

  readonly activeOptions: { value: ActiveFilter; label: string }[] = [
    { value: 'ALL',      label: 'All' },
    { value: 'ACTIVE',   label: 'Active' },
    { value: 'INACTIVE', label: 'Inactive' }
  ];

  filters: CustomerFilters = this.emptyFilters();

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  constructor(
    private customerService: CustomerService,
    private router: Router,
    private snackBar: MatSnackBar,
    private exportService: ExportService,
    private datePipe: DatePipe
  ) {}

  ngOnInit(): void { this.load(); }

  // ---------- Data loading ---------------------------------------------------

  load(page = 1, pageSize = 500): void {
    this.loading = true;
    this.error = null;
    this.selection.clear();
    this.customerService.getAll(page, pageSize).subscribe({
      next: result => {
        this.allCustomers = result.items;
        this.totalCount = result.totalCount;
        this.applyFilters();
        this.loading = false;
      },
      error: () => { this.loading = false; this.error = 'Failed to load customers.'; }
    });
  }

  // ---------- Filter state ---------------------------------------------------

  applyFilters(): void {
    this.dataSource.data = this.allCustomers.filter(c => this.matches(c));
    this.selection.clear();
    setTimeout(() => {
      if (this.paginator) {
        this.dataSource.paginator = this.paginator;
      }
    });
  }

  setRecency(value: RecencyFilter): void {
    this.filters.recency = value;
    this.applyFilters();
  }

  resetFilters(): void {
    this.filters = this.emptyFilters();
    this.applyFilters();
  }

  get activeFilterCount(): number {
    const f = this.filters;
    let n = 0;
    if (f.search.trim())      n++;
    if (f.active  !== 'ALL')  n++;
    if (f.recency !== 'ALL')  n++;
    return n;
  }

  get recencyChips(): QuickChip[] {
    const countNew        = this.allCustomers.filter(c => this.isNew(c)).length;
    const countReturning  = this.allCustomers.filter(c => !this.isNew(c)).length;

    return [
      { label: 'All customers',                     value: 'ALL',       count: this.allCustomers.length },
      { label: `New (${NEW_CUSTOMER_DAYS}d)`,       value: 'NEW',       count: countNew },
      { label: 'Returning',                         value: 'RETURNING', count: countReturning }
    ];
  }

  // ---------- Selection ------------------------------------------------------

  isAllSelected(): boolean {
    return this.dataSource.data.length > 0 &&
      this.dataSource.data.every(c => this.selection.isSelected(c));
  }

  masterToggle(): void {
    if (this.isAllSelected()) {
      this.selection.clear();
    } else {
      this.selection.select(...this.dataSource.data);
    }
  }

  // ---------- Row / bulk actions --------------------------------------------

  edit(id: number): void { this.router.navigate(['/customers', id, 'edit']); }

  delete(id: number): void {
    if (!confirm('Delete this customer?')) return;
    this.customerService.delete(id).subscribe({
      next: () => { this.snackBar.open('Customer deleted', 'Close', { duration: 2000 }); this.load(); },
      error: () => this.snackBar.open('Delete failed', 'Close', { duration: 3000 })
    });
  }

  bulkDelete(): void {
    const selected = this.selection.selected;
    if (selected.length === 0) return;
    if (!confirm(`Delete ${selected.length} selected customer${selected.length > 1 ? 's' : ''}? This cannot be undone.`)) return;

    this.deleting = true;
    const calls = selected.map(c =>
      this.customerService.delete(c.id).pipe(
        map(() => ({ ok: true as const })),
        catchError(() => of({ ok: false as const }))
      )
    );

    forkJoin(calls).subscribe(results => {
      this.deleting = false;
      const ok = results.filter(x => x.ok).length;
      const failed = results.length - ok;
      const msg = failed === 0
        ? `Deleted ${ok} customer${ok > 1 ? 's' : ''}.`
        : `Deleted ${ok}, failed ${failed}.`;
      this.snackBar.open(msg, 'Close', { duration: 3500 });
      this.load();
    });
  }

  // ---------- Export ---------------------------------------------------------

  exportExcel(): void {
    const { columns, meta } = this.buildExport();
    this.exportService.exportExcel(this.dataSource.data, columns, meta);
  }

  exportPdf(): void {
    const { columns, meta } = this.buildExport();
    this.exportService.exportPdf(this.dataSource.data, columns, meta);
  }

  private buildExport(): {
    columns: ExportColumn<Customer>[];
    meta: { filename: string; title: string; subtitle: string; sheetName: string };
  } {
    const columns: ExportColumn<Customer>[] = [
      { header: 'Name',    accessor: c => c.fullName,                                      excelWidth: 24, pdfWeight: 2.2 },
      { header: 'Phone',   accessor: c => c.phone,                                         excelWidth: 16, pdfWeight: 1.3 },
      { header: 'Email',   accessor: c => c.email ?? '',                                   excelWidth: 22, pdfWeight: 1.8 },
      { header: 'CNIC',    accessor: c => c.cnic ?? '',                                    excelWidth: 18, pdfWeight: 1.3 },
      { header: 'Address', accessor: c => c.address ?? '',                                 excelWidth: 28, pdfWeight: 2.2 },
      { header: 'Active',  accessor: c => c.isActive ? 'Yes' : 'No', align: 'center',      excelWidth: 10, pdfWeight: 0.6 },
      { header: 'Created', accessor: c => this.datePipe.transform(c.createdAt, 'mediumDate') ?? '',
                                                                                            excelWidth: 16, pdfWeight: 1.1 }
    ];

    const stamp = this.datePipe.transform(new Date(), 'yyyyMMdd-HHmm') ?? '';
    const scope = this.activeFilterCount > 0 ? 'filtered' : 'all';
    const filterSummary = this.describeActiveFilters();

    return {
      columns,
      meta: {
        filename: `customers-${scope}-${stamp}`,
        title: 'Customers',
        subtitle: [
          `${this.dataSource.data.length} of ${this.allCustomers.length} customer(s)`,
          filterSummary
        ].filter(Boolean).join(' · '),
        sheetName: 'Customers'
      }
    };
  }

  private describeActiveFilters(): string {
    const f = this.filters;
    const parts: string[] = [];

    if (f.active  !== 'ALL') parts.push(`Active: ${this.activeOptions.find(o => o.value === f.active)?.label}`);
    if (f.recency !== 'ALL') parts.push(`Segment: ${this.recencyChips.find(o => o.value === f.recency)?.label}`);
    if (f.search.trim())     parts.push(`Search: "${f.search.trim()}"`);

    return parts.length ? `Filters — ${parts.join('; ')}` : '';
  }

  // ---------- Display helpers ------------------------------------------------

  isNew(c: Customer): boolean {
    if (!c.createdAt) return false;
    const ts = new Date(c.createdAt).getTime();
    return Number.isFinite(ts) && (Date.now() - ts) <= NEW_CUSTOMER_DAYS * DAY_MS;
  }

  // ---------- Internals ------------------------------------------------------

  private matches(c: Customer): boolean {
    const f = this.filters;

    if (f.active === 'ACTIVE'   && !c.isActive) return false;
    if (f.active === 'INACTIVE' &&  c.isActive) return false;

    if (f.recency === 'NEW'       && !this.isNew(c)) return false;
    if (f.recency === 'RETURNING' &&  this.isNew(c)) return false;

    const term = f.search.trim().toLowerCase();
    if (term) {
      const haystack = [
        c.fullName, c.phone,
        c.email ?? '', c.cnic ?? '', c.address ?? ''
      ].join(' ').toLowerCase();
      if (!haystack.includes(term)) return false;
    }
    return true;
  }

  private emptyFilters(): CustomerFilters {
    return { search: '', active: 'ALL', recency: 'ALL' };
  }
}
