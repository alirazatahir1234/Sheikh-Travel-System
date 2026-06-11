import { Component, OnDestroy, OnInit } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PageEvent } from '@angular/material/paginator';
import { Router } from '@angular/router';
import { DatePipe } from '@angular/common';
import { SelectionModel } from '@angular/cdk/collections';
import { Subject, Subscription, forkJoin, of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, map } from 'rxjs/operators';

import { CustomerService } from '../../../core/services/customer.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { Customer, CustomerFilter, CustomerListStats } from '../../../core/models/customer.model';

type ActiveFilter = 'ALL' | 'ACTIVE' | 'INACTIVE';
type RecencyFilter = 'ALL' | 'NEW' | 'RETURNING';

const NEW_CUSTOMER_DAYS = 30;

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
export class CustomerListComponent implements OnInit, OnDestroy {
  displayedColumns = [
    'select', 'fullName', 'phone', 'email', 'cnic',
    'address', 'isActive', 'createdAt', 'actions'
  ];

  dataSource = new MatTableDataSource<Customer>();
  recencyStats: CustomerListStats = { total: 0, newCount: 0, returning: 0 };

  loading = true;
  deleting = false;
  error: string | null = null;
  totalCount = 0;
  pageIndex = 0;
  pageSize = 25;

  readonly selection = new SelectionModel<Customer>(true, []);

  readonly activeOptions: { value: ActiveFilter; label: string }[] = [
    { value: 'ALL',      label: 'All' },
    { value: 'ACTIVE',   label: 'Active' },
    { value: 'INACTIVE', label: 'Inactive' }
  ];

  filters: CustomerFilters = this.emptyFilters();

  private readonly searchSubject = new Subject<string>();
  private searchSub?: Subscription;

  constructor(
    private customerService: CustomerService,
    private router: Router,
    private snackBar: MatSnackBar,
    private exportService: ExportService,
    private datePipe: DatePipe
  ) {}

  ngOnInit(): void {
    this.searchSub = this.searchSubject.pipe(
      debounceTime(350),
      distinctUntilChanged()
    ).subscribe(term => {
      this.filters.search = term;
      this.load(true);
    });
    this.load();
  }

  ngOnDestroy(): void {
    this.searchSub?.unsubscribe();
  }

  load(resetPage = false): void {
    if (resetPage) {
      this.pageIndex = 0;
      this.selection.clear();
    }

    this.loading = true;
    this.error = null;

    forkJoin({
      page: this.customerService.getAll(this.pageIndex + 1, this.pageSize, this.buildFilter()),
      stats: this.customerService.getStats(this.buildStatsFilter()).pipe(
        catchError(() => of({ total: 0, newCount: 0, returning: 0 }))
      )
    }).subscribe({
      next: ({ page, stats }) => {
        this.dataSource.data = page.items;
        this.totalCount = page.totalCount;
        this.recencyStats = stats;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.error = 'Failed to load customers.';
      }
    });
  }

  onSearchChange(term: string): void {
    this.searchSubject.next(term);
  }

  applyFilters(): void {
    this.load(true);
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.selection.clear();
    this.load();
  }

  setRecency(value: RecencyFilter): void {
    this.filters.recency = value;
    this.load(true);
  }

  resetFilters(): void {
    this.filters = this.emptyFilters();
    this.load(true);
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
    const s = this.recencyStats;
    return [
      { label: 'All customers',               value: 'ALL',       count: s.total },
      { label: `New (${NEW_CUSTOMER_DAYS}d)`, value: 'NEW',       count: s.newCount },
      { label: 'Returning',                   value: 'RETURNING', count: s.returning }
    ];
  }

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

  exportExcel(): void {
    this.fetchForExport(rows => {
      const { columns, meta } = this.buildExport(rows);
      this.exportService.exportExcel(rows, columns, meta);
    });
  }

  exportPdf(): void {
    this.fetchForExport(rows => {
      const { columns, meta } = this.buildExport(rows);
      this.exportService.exportPdf(rows, columns, meta);
    });
  }

  isNew(c: Customer): boolean {
    if (!c.createdAt) return false;
    const ts = new Date(c.createdAt).getTime();
    return Number.isFinite(ts) && (Date.now() - ts) <= NEW_CUSTOMER_DAYS * 86_400_000;
  }

  trackById(_index: number, item: Customer): number {
    return item.id;
  }

  private fetchForExport(onReady: (rows: Customer[]) => void): void {
    this.customerService.getAll(1, 10000, this.buildFilter()).subscribe({
      next: r => onReady(r.items),
      error: () => this.snackBar.open('Failed to load customers for export.', 'Close', { duration: 3000 })
    });
  }

  private buildExport(rows: Customer[]): {
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
          `${rows.length} of ${this.totalCount} customer(s)`,
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

  private buildFilter(): CustomerFilter {
    const f = this.filters;
    return {
      search: f.search.trim() || undefined,
      isActive: f.active === 'ALL' ? undefined : f.active === 'ACTIVE',
      recency: f.recency === 'ALL' ? undefined : f.recency
    };
  }

  private buildStatsFilter(): Omit<CustomerFilter, 'recency'> {
    const { recency: _recency, ...statsFilter } = this.buildFilter();
    return statsFilter;
  }

  private emptyFilters(): CustomerFilters {
    return { search: '', active: 'ALL', recency: 'ALL' };
  }
}
