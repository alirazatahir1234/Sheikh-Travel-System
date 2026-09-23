import { Component, OnDestroy, OnInit } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { MatDialog } from '@angular/material/dialog';
import { MatCheckboxChange } from '@angular/material/checkbox';
import { Router } from '@angular/router';
import { PageEvent } from '@angular/material/paginator';
import { Subject, Subscription, debounceTime, distinctUntilChanged, forkJoin } from 'rxjs';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { BookingService } from '../../../core/services/booking.service';
import { Booking, BookingFilter, BookingStatus } from '../../../core/models/booking.model';
import { DEFAULT_CURRENCY } from '../../../core/models/platform.model';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../shared/components/confirm-dialog/confirm-dialog.component';

@Component({
  standalone: false,
  selector: 'app-booking-list',
  templateUrl: './booking-list.component.html',
  styleUrls: ['./booking-list.component.scss']
})
export class BookingListComponent implements OnInit, OnDestroy {
  displayedColumns = [
    'select', 'index', 'bookingNumber', 'customerName', 'routeName',
    'pickupTime', 'passengerCount', 'totalAmount', 'status', 'createdAt', 'actions'
  ];
  dataSource = new MatTableDataSource<Booking>();
  activeBooking: Booking | null = null;
  readonly selectedIds = new Set<number>();
  bulkDeleting = false;
  loading = true;
  error: string | null = null;
  totalCount = 0;
  pageIndex = 0;
  pageSize = 25;
  selectedStatus = '';
  selectedRoute = '';
  searchTerm = '';
  dateFrom: Date | null = null;
  dateTo: Date | null = null;
  amountMin: number | null = null;
  amountMax: number | null = null;
  routeOptions: Array<{ value: string; label: string }> = [];
  summary = { total: 0, pending: 0, confirmed: 0, completed: 0, cancelled: 0 };

  private readonly searchSubject = new Subject<string>();
  private searchSub?: Subscription;

  statusOptions: Array<{ value: string; label: string }> = [
    { value: '', label: 'All Status' },
    { value: 'Pending', label: 'Pending' },
    { value: 'Confirmed', label: 'Confirmed' },
    { value: 'Started', label: 'Started' },
    { value: 'Completed', label: 'Completed' },
    { value: 'Cancelled', label: 'Cancelled' }
  ];

  constructor(
    private bookingService: BookingService,
    private router: Router,
    private toast: UiToastService,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    this.searchSub = this.searchSubject.pipe(
      debounceTime(350),
      distinctUntilChanged()
    ).subscribe(term => {
      this.searchTerm = term;
      this.load(true);
    });
    this.initDefaultDateRange();
    this.loadSummary();
    this.load();
  }

  ngOnDestroy(): void {
    this.searchSub?.unsubscribe();
  }

  load(resetPage = false): void {
    if (resetPage) this.pageIndex = 0;
    this.loading = true;
    this.error = null;
    this.bookingService.getAll(this.pageIndex + 1, this.pageSize, this.buildFilter()).subscribe({
      next: r => {
        this.dataSource.data = r.items;
        this.totalCount = r.totalCount;
        this.mergeRouteOptions(r.items);
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.error = 'Failed to load bookings.';
      }
    });
  }

  loadSummary(): void {
    forkJoin({
      total: this.bookingService.getAll(1, 1, {}),
      pending: this.bookingService.getAll(1, 1, { status: 'Pending' }),
      confirmed: this.bookingService.getAll(1, 1, { status: 'Confirmed' }),
      completed: this.bookingService.getAll(1, 1, { status: 'Completed' }),
      cancelled: this.bookingService.getAll(1, 1, { status: 'Cancelled' })
    }).subscribe({
      next: r => {
        this.summary = {
          total: r.total.totalCount,
          pending: r.pending.totalCount,
          confirmed: r.confirmed.totalCount,
          completed: r.completed.totalCount,
          cancelled: r.cancelled.totalCount
        };
      }
    });
  }

  filterByStat(status: string): void {
    this.selectedStatus = status;
    this.load(true);
  }

  onSearchChange(term: string): void {
    this.searchSubject.next(term);
  }

  applyFilters(resetPage = false): void {
    this.load(resetPage);
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.load();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.amountMin = null;
    this.amountMax = null;
    this.selectedStatus = '';
    this.selectedRoute = '';
    this.initDefaultDateRange();
    this.load(true);
  }

  exportCsv(): void {
    this.bookingService.getAll(1, 10000, this.buildFilter()).subscribe({
      next: r => {
        const rows = r.items;
        const csvRows = [
          ['BookingNumber', 'Customer', 'Route', 'PickupTime', 'Passengers', 'Amount', 'Status', 'CreatedAt'],
          ...rows.map(b => [
            b.bookingNumber,
            b.customerName,
            b.routeName,
            new Date(b.pickupTime).toISOString(),
            String(b.passengerCount),
            String(b.totalAmount),
            b.status,
            b.createdAt ? new Date(b.createdAt).toISOString() : ''
          ])
        ];
        const csv = csvRows.map(row => row.map(v => `"${String(v).replace(/"/g, '""')}"`).join(',')).join('\n');
        const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `bookings-${new Date().toISOString().slice(0, 10)}.csv`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.toast.error('Failed to export bookings.')
    });
  }

  view(id: number): void { this.router.navigate(['/bookings', id]); }

  edit(id: number): void {
    this.router.navigate(['/bookings', id, 'edit']);
  }

  delete(id: number): void {
    if (window.confirm('Are you sure you want to delete this booking?')) {
      this.bookingService.delete(id).subscribe({
        next: () => {
          this.selectedIds.delete(id);
          this.toast.success('Booking deleted');
          this.loadSummary();
          this.load();
        },
        error: () => {
          this.toast.error('Failed to delete booking');
        }
      });
    }
  }

  get selectionCount(): number {
    return this.selectedIds.size;
  }

  isSelected(id: number): boolean {
    return this.selectedIds.has(id);
  }

  allPageSelected(): boolean {
    const rows = this.dataSource.data;
    return rows.length > 0 && rows.every(b => this.selectedIds.has(b.id));
  }

  somePageSelected(): boolean {
    const rows = this.dataSource.data;
    return rows.some(b => this.selectedIds.has(b.id)) && !this.allPageSelected();
  }

  toggleSelectAllOnPage(ev: MatCheckboxChange): void {
    const checked = ev.checked;
    for (const b of this.dataSource.data) {
      if (checked) this.selectedIds.add(b.id);
      else this.selectedIds.delete(b.id);
    }
  }

  toggleRowSelection(id: number, checked: boolean): void {
    if (checked) this.selectedIds.add(id);
    else this.selectedIds.delete(id);
  }

  bulkDeleteSelected(): void {
    const ids = [...this.selectedIds];
    if (!ids.length) return;

    this.dialog
      .open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
        width: '440px',
        data: {
          title: 'Delete selected bookings',
          message: `You are about to remove ${ids.length} booking(s) from the list (soft delete). This cannot be undone from the app.`,
          confirmText: 'Delete all',
          confirmColor: 'warn'
        }
      })
      .afterClosed()
      .subscribe(confirmed => {
        if (!confirmed) return;
        this.bulkDeleting = true;
        this.bookingService.bulkDelete(ids).subscribe({
          next: count => {
            this.bulkDeleting = false;
            this.selectedIds.clear();
            this.toast.warning(
              count > 0 ? `${count} booking(s) deleted.` : 'No bookings were deleted (they may have already been removed).');
            this.loadSummary();
            this.load();
          },
          error: () => {
            this.bulkDeleting = false;
            this.toast.error('Bulk delete failed.');
          }
        });
      });
  }

  statusClass(status: BookingStatus): string {
    const key = String(status || '').toLowerCase().replace(/\s+/g, '');
    return `ops-status--${key}`;
  }

  formatRoute(routeName: string | null | undefined): string {
    if (!routeName) return '—';
    if (routeName.includes('→') || routeName.includes('->')) {
      return routeName.replace(/->/g, '→');
    }
    if (routeName.includes(' - ')) {
      return routeName.replace(' - ', ' → ');
    }
    return routeName;
  }

  formatAmount(amount: number): string {
    const value = Number(amount) || 0;
    return `${DEFAULT_CURRENCY} ${value.toLocaleString('en-PK')}`;
  }

  trackByValue(_index: number, item: { value: string }): string {
    return item.value;
  }

  trackById(_index: number, item: Booking): number {
    return item.id;
  }

  toDateInput(value: Date | null): string {
    if (!value) return '';
    const y = value.getFullYear();
    const m = String(value.getMonth() + 1).padStart(2, '0');
    const d = String(value.getDate()).padStart(2, '0');
    return `${y}-${m}-${d}`;
  }

  onDateFromChange(value: string): void {
    this.dateFrom = value ? new Date(`${value}T00:00:00`) : null;
    this.load(true);
  }

  onDateToChange(value: string): void {
    this.dateTo = value ? new Date(`${value}T00:00:00`) : null;
    this.load(true);
  }

  private initDefaultDateRange(): void {
    this.dateFrom = null;
    this.dateTo = null;
  }

  private mergeRouteOptions(items: Booking[]): void {
    const map = new Map(this.routeOptions.map(r => [r.value, r.label]));
    for (const b of items) {
      const name = (b.routeName || '').trim();
      if (name && !map.has(name)) map.set(name, name);
    }
    this.routeOptions = [...map.entries()]
      .map(([value, label]) => ({ value, label }))
      .sort((a, b) => a.label.localeCompare(b.label));
  }

  private buildFilter(): BookingFilter {
    const searchParts = [
      this.searchTerm.trim(),
      this.selectedRoute.trim()
    ].filter(Boolean);

    return {
      status: this.selectedStatus || undefined,
      search: searchParts.length ? searchParts.join(' ') : undefined,
      dateFrom: this.dateFrom ? this.toDateInput(this.dateFrom) : undefined,
      dateTo: this.dateTo ? this.toDateInput(this.dateTo) : undefined,
      amountMin: this.amountMin ?? undefined,
      amountMax: this.amountMax ?? undefined
    };
  }
}
