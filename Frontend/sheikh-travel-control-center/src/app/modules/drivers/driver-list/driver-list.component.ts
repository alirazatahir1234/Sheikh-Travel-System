import { Component, OnInit, ViewChild } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { MatPaginator } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog } from '@angular/material/dialog';
import { Router } from '@angular/router';
import { DatePipe } from '@angular/common';
import { DriverService } from '../../../core/services/driver.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import {
  Driver,
  DriverStatus,
  DriverStatusLabels
} from '../../../core/models/driver.model';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../shared/components/confirm-dialog/confirm-dialog.component';

type LicenseFilter = 'ALL' | 'VALID' | 'EXPIRING' | 'EXPIRED';
type ActiveFilter  = 'ALL' | 'ACTIVE' | 'INACTIVE';

interface DriverFilters {
  search:   string;
  status:   DriverStatus | 'ALL';
  active:   ActiveFilter;
  license:  LicenseFilter;
}

interface StatusChip {
  label: string;
  value: DriverStatus | 'ALL';
  count: number;
}

const EXPIRING_WINDOW_DAYS = 30;
const DAY_MS = 86_400_000;

@Component({
  selector: 'app-driver-list',
  templateUrl: './driver-list.component.html',
  styleUrls: ['./driver-list.component.scss'],
  providers: [DatePipe]
})
export class DriverListComponent implements OnInit {
  displayedColumns = [
    'fullName', 'phone', 'licenseNumber', 'licenseExpiryDate',
    'cnic', 'status', 'isActive', 'createdAt', 'actions'
  ];

  dataSource = new MatTableDataSource<Driver>();
  allDrivers: Driver[] = [];

  loading = true;
  error: string | null = null;
  totalCount = 0;

  readonly activeOptions: { value: ActiveFilter; label: string }[] = [
    { value: 'ALL',      label: 'All' },
    { value: 'ACTIVE',   label: 'Active' },
    { value: 'INACTIVE', label: 'Inactive' }
  ];
  readonly licenseOptions: { value: LicenseFilter; label: string }[] = [
    { value: 'ALL',      label: 'All' },
    { value: 'VALID',    label: 'Valid' },
    { value: 'EXPIRING', label: `Expiring (${EXPIRING_WINDOW_DAYS}d)` },
    { value: 'EXPIRED',  label: 'Expired' }
  ];

  filters: DriverFilters = this.emptyFilters();

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  constructor(
    private driverService: DriverService,
    private router: Router,
    private snackBar: MatSnackBar,
    private dialog: MatDialog,
    private exportService: ExportService,
    private datePipe: DatePipe
  ) {}

  ngOnInit(): void { this.load(); }

  // ---------- Data loading ---------------------------------------------------

  load(page = 1, pageSize = 500): void {
    this.loading = true;
    this.error = null;
    this.driverService.getAll(page, pageSize).subscribe({
      next: result => {
        this.allDrivers = result.items;
        this.totalCount = result.totalCount;
        this.applyFilters();
        this.loading = false;
      },
      error: () => { this.loading = false; this.error = 'Failed to load drivers.'; }
    });
  }

  // ---------- Filter state ---------------------------------------------------

  applyFilters(): void {
    this.dataSource.data = this.allDrivers.filter(d => this.matches(d));
    setTimeout(() => {
      if (this.paginator) {
        this.dataSource.paginator = this.paginator;
      }
    });
  }

  setStatus(value: DriverStatus | 'ALL'): void {
    this.filters.status = value;
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
    if (f.status  !== 'ALL')  n++;
    if (f.active  !== 'ALL')  n++;
    if (f.license !== 'ALL')  n++;
    return n;
  }

  get statusChips(): StatusChip[] {
    const counts = (status: DriverStatus) =>
      this.allDrivers.filter(d => d.status === status).length;

    return [
      { label: 'All',       value: 'ALL',                     count: this.allDrivers.length },
      { label: 'Available', value: DriverStatus.Available,    count: counts(DriverStatus.Available) },
      { label: 'On Trip',   value: DriverStatus.OnTrip,       count: counts(DriverStatus.OnTrip) },
      { label: 'Off Duty',  value: DriverStatus.OffDuty,      count: counts(DriverStatus.OffDuty) },
      { label: 'Suspended', value: DriverStatus.Suspended,    count: counts(DriverStatus.Suspended) }
    ];
  }

  // ---------- Row actions ----------------------------------------------------

  edit(id: number): void { this.router.navigate(['/drivers', id, 'edit']); }

  delete(id: number): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: 'Delete Driver',
        message: 'Are you sure you want to delete this driver?',
        confirmText: 'Delete',
        confirmColor: 'warn'
      } as ConfirmDialogData
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) return;
      this.driverService.delete(id).subscribe({
        next: () => { this.snackBar.open('Deleted', 'Close', { duration: 2000 }); this.load(); },
        error: () => this.snackBar.open('Delete failed', 'Close', { duration: 3000 })
      });
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
    columns: ExportColumn<Driver>[];
    meta: { filename: string; title: string; subtitle: string; sheetName: string };
  } {
    const columns: ExportColumn<Driver>[] = [
      { header: 'Name',           accessor: d => d.fullName,                                               excelWidth: 22, pdfWeight: 2   },
      { header: 'Phone',          accessor: d => d.phone,                                                  excelWidth: 16, pdfWeight: 1.3 },
      { header: 'License #',      accessor: d => d.licenseNumber,                                         excelWidth: 16, pdfWeight: 1.3 },
      { header: 'License Expiry', accessor: d => d.licenseExpiryDate
                                                ? this.datePipe.transform(d.licenseExpiryDate, 'mediumDate') ?? ''
                                                : '',                                                     excelWidth: 16, pdfWeight: 1.3 },
      { header: 'CNIC',           accessor: d => d.cnic ?? '',                                            excelWidth: 18, pdfWeight: 1.3 },
      { header: 'Status',         accessor: d => this.statusLabel(d.status),                              excelWidth: 14, pdfWeight: 1   },
      { header: 'Active',         accessor: d => d.isActive ? 'Yes' : 'No',        align: 'center',       excelWidth: 10, pdfWeight: 0.6 },
      { header: 'Created',        accessor: d => this.datePipe.transform(d.createdAt, 'mediumDate') ?? '',
                                                                                                          excelWidth: 16, pdfWeight: 1.2 }
    ];

    const stamp = this.datePipe.transform(new Date(), 'yyyyMMdd-HHmm') ?? '';
    const scope = this.activeFilterCount > 0 ? 'filtered' : 'all';
    const filterSummary = this.describeActiveFilters();

    return {
      columns,
      meta: {
        filename: `drivers-${scope}-${stamp}`,
        title: 'Drivers',
        subtitle: [
          `${this.dataSource.data.length} of ${this.allDrivers.length} driver(s)`,
          filterSummary
        ].filter(Boolean).join(' · '),
        sheetName: 'Drivers'
      }
    };
  }

  private describeActiveFilters(): string {
    const f = this.filters;
    const parts: string[] = [];

    if (f.status  !== 'ALL') parts.push(`Status: ${this.statusLabel(f.status as DriverStatus)}`);
    if (f.active  !== 'ALL') {
      parts.push(`Active: ${this.activeOptions.find(o => o.value === f.active)?.label ?? f.active}`);
    }
    if (f.license !== 'ALL') {
      parts.push(`License: ${this.licenseOptions.find(o => o.value === f.license)?.label ?? f.license}`);
    }
    if (f.search.trim()) parts.push(`Search: "${f.search.trim()}"`);

    return parts.length ? `Filters — ${parts.join('; ')}` : '';
  }

  // ---------- Display helpers ------------------------------------------------

  statusLabel(status: DriverStatus): string { return DriverStatusLabels[status] ?? 'Unknown'; }

  statusChipClass(status: DriverStatus): string {
    switch (status) {
      case DriverStatus.Available: return 'chip-success';
      case DriverStatus.OnTrip:    return 'chip-info';
      case DriverStatus.OffDuty:   return 'chip-muted';
      case DriverStatus.Suspended: return 'chip-danger';
      default:                     return '';
    }
  }

  isLicenseExpiring(date: string | null | undefined): boolean {
    if (!date) return false;
    const diff = new Date(date).getTime() - Date.now();
    return diff > 0 && diff < EXPIRING_WINDOW_DAYS * DAY_MS;
  }

  isLicenseExpired(date: string | null | undefined): boolean {
    return !!date && new Date(date).getTime() < Date.now();
  }

  // ---------- Internals ------------------------------------------------------

  private matches(d: Driver): boolean {
    const f = this.filters;

    if (f.status !== 'ALL' && d.status !== f.status) return false;

    if (f.active === 'ACTIVE'   && !d.isActive) return false;
    if (f.active === 'INACTIVE' &&  d.isActive) return false;

    if (!this.matchesLicense(d)) return false;

    const term = f.search.trim().toLowerCase();
    if (term) {
      const haystack = [
        d.fullName, d.phone, d.licenseNumber, d.cnic ?? '', this.statusLabel(d.status)
      ].join(' ').toLowerCase();
      if (!haystack.includes(term)) return false;
    }
    return true;
  }

  private matchesLicense(d: Driver): boolean {
    const choice = this.filters.license;
    if (choice === 'ALL') return true;

    const ts = d.licenseExpiryDate ? new Date(d.licenseExpiryDate).getTime() : null;
    if (ts == null) return false;

    const now = Date.now();
    const soon = now + EXPIRING_WINDOW_DAYS * DAY_MS;

    switch (choice) {
      case 'EXPIRED':  return ts < now;
      case 'EXPIRING': return ts >= now && ts < soon;
      case 'VALID':    return ts >= soon;
    }
  }

  private emptyFilters(): DriverFilters {
    return {
      search:  '',
      status:  'ALL',
      active:  'ALL',
      license: 'ALL'
    };
  }
}
