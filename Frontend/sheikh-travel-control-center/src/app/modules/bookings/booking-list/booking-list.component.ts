import { Component, OnInit } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog } from '@angular/material/dialog';
import { MatCheckboxChange } from '@angular/material/checkbox';
import { Router } from '@angular/router';
import { PageEvent } from '@angular/material/paginator';
import { BookingService } from '../../../core/services/booking.service';
import { Booking, BookingStatus } from '../../../core/models/booking.model';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../shared/components/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-booking-list',
  templateUrl: './booking-list.component.html',
  styleUrls: ['./booking-list.component.scss']
})
export class BookingListComponent implements OnInit {
  displayedColumns = ['select', 'bookingNumber', 'customerName', 'routeName', 'pickupTime', 'passengerCount', 'totalAmount', 'status', 'actions'];
  dataSource = new MatTableDataSource<Booking>();
  private allRows: Booking[] = [];
  private filteredRows: Booking[] = [];
  /** Selected booking ids (can span multiple pages of the filtered list). */
  readonly selectedIds = new Set<number>();
  bulkDeleting = false;
  loading = true;
  error: string | null = null;
  totalCount = 0;
  pageIndex = 0;
  pageSize = 10;
  selectedStatus = '';
  searchTerm = '';
  dateFrom: Date | null = null;
  dateTo: Date | null = null;
  amountMin: number | null = null;
  amountMax: number | null = null;

  statusOptions: Array<{ value: string; label: string }> = [
    { value: '', label: 'All' },
    { value: 'Pending', label: 'Pending' },
    { value: 'Confirmed', label: 'Confirmed' },
    { value: 'Started', label: 'Started' },
    { value: 'Completed', label: 'Completed' },
    { value: 'Cancelled', label: 'Cancelled' }
  ];

  statusColors: Record<string, string> = {
    Pending: '#f57f17', Confirmed: '#1565c0', Started: '#00695c',
    Completed: '#2e7d32', Cancelled: '#c62828'
  };

  constructor(
    private bookingService: BookingService,
    private router: Router,
    private snackBar: MatSnackBar,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading = true;
    this.error = null;
    this.bookingService.getAll(1, 1000, this.selectedStatus || undefined).subscribe({
      next: r => {
        this.allRows = r.items;
        this.applyFilters(true);
        this.loading = false;
      },
      error: () => { this.loading = false; this.error = 'Failed to load bookings.'; }
    });
  }

  applyFilters(resetPage = false): void {
    const q = this.searchTerm.trim().toLowerCase();
    const fromMs = this.startOfDayMs(this.dateFrom);
    const toMs = this.endOfDayMs(this.dateTo);
    const min = this.amountMin ?? null;
    const max = this.amountMax ?? null;

    this.filteredRows = this.allRows.filter(b => {
      if (q) {
        const haystack = `${b.bookingNumber} ${b.customerName} ${b.routeName}`.toLowerCase();
        if (!haystack.includes(q)) return false;
      }
      const pickup = new Date(b.pickupTime).getTime();
      if (fromMs !== null && pickup < fromMs) return false;
      if (toMs !== null && pickup > toMs) return false;
      if (min !== null && b.totalAmount < min) return false;
      if (max !== null && b.totalAmount > max) return false;
      return true;
    });

    this.pruneSelectionToIds(new Set(this.filteredRows.map(b => b.id)));

    this.totalCount = this.filteredRows.length;
    if (resetPage) this.pageIndex = 0;
    this.applyPaging();
  }

  applyPaging(): void {
    const start = this.pageIndex * this.pageSize;
    this.dataSource.data = this.filteredRows.slice(start, start + this.pageSize);
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.applyPaging();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.dateFrom = null;
    this.dateTo = null;
    this.amountMin = null;
    this.amountMax = null;
    this.selectedStatus = '';
    this.load();
  }

  applySmartFilter(key: 'today' | 'pending' | 'highValue'): void {
    const now = new Date();
    if (key === 'today') {
      this.dateFrom = new Date(now.getFullYear(), now.getMonth(), now.getDate());
      this.dateTo = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    } else if (key === 'pending') {
      this.selectedStatus = 'Pending';
      this.load();
      return;
    } else if (key === 'highValue') {
      this.amountMin = 10000;
    }
    this.applyFilters(true);
  }

  exportCsv(): void {
    const rows = this.filteredRows.length ? this.filteredRows : this.allRows;
    const csvRows = [
      ['BookingNumber', 'Customer', 'Route', 'PickupTime', 'Passengers', 'Amount', 'Status'],
      ...rows.map(b => [
        b.bookingNumber,
        b.customerName,
        b.routeName,
        new Date(b.pickupTime).toISOString(),
        String(b.passengerCount),
        String(b.totalAmount),
        b.status
      ])
    ];
    const csv = csvRows.map(r => r.map(v => `"${String(v).replace(/"/g, '""')}"`).join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `bookings-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
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
          this.snackBar.open('Booking deleted', 'Close', { duration: 2000 });
          this.load();
        },
        error: () => {
          this.snackBar.open('Failed to delete booking', 'Close', { duration: 2000 });
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
            this.snackBar.open(
              count > 0 ? `${count} booking(s) deleted.` : 'No bookings were deleted (they may have already been removed).',
              'Close',
              { duration: 3500 }
            );
            this.load();
          },
          error: () => {
            this.bulkDeleting = false;
            this.snackBar.open('Bulk delete failed.', 'Close', { duration: 3000 });
          }
        });
      });
  }

  private pruneSelectionToIds(validIds: Set<number>): void {
    for (const id of [...this.selectedIds]) {
      if (!validIds.has(id)) this.selectedIds.delete(id);
    }
  }

  getStatusColor(status: BookingStatus): string {
    return this.statusColors[status] ?? '#666';
  }

  trackByValue(_index: number, item: { value: string }): string {
    return item.value;
  }

  private startOfDayMs(value: Date | null): number | null {
    if (!value) return null;
    const d = new Date(value);
    d.setHours(0, 0, 0, 0);
    return d.getTime();
  }

  private endOfDayMs(value: Date | null): number | null {
    if (!value) return null;
    const d = new Date(value);
    d.setHours(23, 59, 59, 999);
    return d.getTime();
  }
}
