import { Component, OnInit, ViewChild } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { MatPaginator } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog } from '@angular/material/dialog';
import { Router } from '@angular/router';
import { DatePipe, DecimalPipe } from '@angular/common';
import { VehicleService } from '../../../core/services/vehicle.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import {
  Vehicle, VehicleStatus, VehicleStatusLabels,
  FuelType, FuelTypeLabels
} from '../../../core/models/vehicle.model';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../shared/components/confirm-dialog/confirm-dialog.component';

type InsuranceFilter = 'ALL' | 'VALID' | 'EXPIRING' | 'EXPIRED';

interface VehicleFilters {
  search: string;
  status:    VehicleStatus | 'ALL';
  fuelType:  FuelType      | 'ALL';
  insurance: InsuranceFilter;
  seatsMin:  number | null;
  seatsMax:  number | null;
}

interface StatusChip {
  label: string;
  value: VehicleStatus | 'ALL';
  count: number;
}

const EXPIRING_WINDOW_DAYS = 30;
const DAY_MS = 86_400_000;

@Component({
  selector: 'app-vehicle-list',
  templateUrl: './vehicle-list.component.html',
  styleUrls: ['./vehicle-list.component.scss'],
  providers: [DatePipe, DecimalPipe]
})
export class VehicleListComponent implements OnInit {
  displayedColumns = [
    'name', 'registrationNumber', 'model', 'year',
    'seatingCapacity', 'fuelAverage', 'fuelType',
    'currentMileage', 'insuranceExpiryDate',
    'status', 'createdAt', 'actions'
  ];

  dataSource = new MatTableDataSource<Vehicle>();
  allVehicles: Vehicle[] = [];

  loading = true;
  error: string | null = null;
  totalCount = 0;

  readonly fuelTypes = [FuelType.Petrol, FuelType.Diesel, FuelType.CNG];
  readonly insuranceOptions: { value: InsuranceFilter; label: string }[] = [
    { value: 'ALL',      label: 'All' },
    { value: 'VALID',    label: 'Valid' },
    { value: 'EXPIRING', label: `Expiring (${EXPIRING_WINDOW_DAYS}d)` },
    { value: 'EXPIRED',  label: 'Expired' }
  ];

  filters: VehicleFilters = this.emptyFilters();

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  constructor(
    private vehicleService: VehicleService,
    private router: Router,
    private snackBar: MatSnackBar,
    private dialog: MatDialog,
    private exportService: ExportService,
    private datePipe: DatePipe,
    private decimalPipe: DecimalPipe
  ) {}

  ngOnInit(): void { this.load(); }

  // ---------- Data loading ---------------------------------------------------

  load(page = 1, pageSize = 500): void {
    this.loading = true;
    this.error = null;
    this.vehicleService.getAll(page, pageSize).subscribe({
      next: result => {
        this.allVehicles = result.items;
        this.totalCount = result.totalCount;
        this.applyFilters();
        this.loading = false;
      },
      error: () => { this.loading = false; this.error = 'Failed to load vehicles.'; }
    });
  }

  // ---------- Filter state ---------------------------------------------------

  applyFilters(): void {
    this.dataSource.data = this.allVehicles.filter(v => this.matches(v));
    setTimeout(() => {
      if (this.paginator) {
        this.dataSource.paginator = this.paginator;
      }
    });
  }

  setStatus(value: VehicleStatus | 'ALL'): void {
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
    if (f.search.trim())     n++;
    if (f.status   !== 'ALL') n++;
    if (f.fuelType !== 'ALL') n++;
    if (f.insurance !== 'ALL') n++;
    if (f.seatsMin != null)  n++;
    if (f.seatsMax != null)  n++;
    return n;
  }

  get statusChips(): StatusChip[] {
    const counts = (status: VehicleStatus) =>
      this.allVehicles.filter(v => v.status === status).length;

    return [
      { label: 'All',         value: 'ALL',                       count: this.allVehicles.length },
      { label: 'Available',   value: VehicleStatus.Available,     count: counts(VehicleStatus.Available) },
      { label: 'On Trip',     value: VehicleStatus.OnTrip,        count: counts(VehicleStatus.OnTrip) },
      { label: 'Maintenance', value: VehicleStatus.Maintenance,   count: counts(VehicleStatus.Maintenance) },
      { label: 'Retired',     value: VehicleStatus.Retired,       count: counts(VehicleStatus.Retired) }
    ];
  }

  // ---------- Row actions ----------------------------------------------------

  edit(id: number): void { this.router.navigate(['/vehicles', id, 'edit']); }

  delete(id: number): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: 'Delete Vehicle',
        message: 'Are you sure you want to delete this vehicle?',
        confirmText: 'Delete',
        confirmColor: 'warn'
      } as ConfirmDialogData
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) return;
      this.vehicleService.delete(id).subscribe({
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

  /**
   * Builds the export column set + metadata (title/subtitle/filename) from the
   * current filter state so exported files always reflect what the user sees.
   */
  private buildExport(): { columns: ExportColumn<Vehicle>[]; meta: { filename: string; title: string; subtitle: string; sheetName: string } } {
    const columns: ExportColumn<Vehicle>[] = [
      { header: 'Name',             accessor: v => v.name,                                     excelWidth: 22, pdfWeight: 2.2 },
      { header: 'Registration',     accessor: v => v.registrationNumber,                       excelWidth: 16, pdfWeight: 1.4 },
      { header: 'Model',            accessor: v => v.model ?? '',                              excelWidth: 18, pdfWeight: 1.4 },
      { header: 'Year',             accessor: v => v.year ?? '',              align: 'center', excelWidth: 8,  pdfWeight: 0.6 },
      { header: 'Seats',            accessor: v => v.seatingCapacity,         align: 'center', excelWidth: 8,  pdfWeight: 0.6 },
      { header: 'Fuel Avg (km/L)',  accessor: v => v.fuelAverage,             align: 'right',  excelWidth: 12, pdfWeight: 1 },
      { header: 'Fuel Type',        accessor: v => this.fuelTypeLabel(v.fuelType),             excelWidth: 12, pdfWeight: 1 },
      { header: 'Mileage (km)',     accessor: v => v.currentMileage,          align: 'right',  excelWidth: 14, pdfWeight: 1 },
      { header: 'Insurance Expiry', accessor: v => v.insuranceExpiryDate
                                                   ? this.datePipe.transform(v.insuranceExpiryDate, 'mediumDate') ?? ''
                                                   : '',                                       excelWidth: 16, pdfWeight: 1.3 },
      { header: 'Status',           accessor: v => this.statusLabel(v.status),                 excelWidth: 14, pdfWeight: 1 },
      { header: 'Created',          accessor: v => this.datePipe.transform(v.createdAt, 'mediumDate') ?? '',
                                                                                               excelWidth: 16, pdfWeight: 1.2 }
    ];

    const stamp = this.datePipe.transform(new Date(), 'yyyyMMdd-HHmm') ?? '';
    const scope = this.activeFilterCount > 0 ? 'filtered' : 'all';
    const filterSummary = this.describeActiveFilters();

    return {
      columns,
      meta: {
        filename: `vehicles-${scope}-${stamp}`,
        title: 'Vehicles',
        subtitle: [
          `${this.dataSource.data.length} of ${this.allVehicles.length} vehicle(s)`,
          filterSummary
        ].filter(Boolean).join(' · '),
        sheetName: 'Vehicles'
      }
    };
  }

  private describeActiveFilters(): string {
    const f = this.filters;
    const parts: string[] = [];

    if (f.status   !== 'ALL') parts.push(`Status: ${this.statusLabel(f.status as VehicleStatus)}`);
    if (f.fuelType !== 'ALL') parts.push(`Fuel: ${this.fuelTypeLabel(f.fuelType as FuelType)}`);
    if (f.insurance !== 'ALL') {
      const label = this.insuranceOptions.find(o => o.value === f.insurance)?.label ?? f.insurance;
      parts.push(`Insurance: ${label}`);
    }
    if (f.seatsMin != null || f.seatsMax != null) {
      parts.push(`Seats: ${f.seatsMin ?? '…'}–${f.seatsMax ?? '…'}`);
    }
    if (f.search.trim()) parts.push(`Search: "${f.search.trim()}"`);

    return parts.length ? `Filters — ${parts.join('; ')}` : '';
  }

  // ---------- Display helpers ------------------------------------------------

  statusLabel(status: VehicleStatus): string  { return VehicleStatusLabels[status] ?? 'Unknown'; }
  fuelTypeLabel(ft: FuelType):      string    { return FuelTypeLabels[ft]           ?? '—'; }

  statusChipClass(status: VehicleStatus): string {
    switch (status) {
      case VehicleStatus.Available:   return 'chip-success';
      case VehicleStatus.OnTrip:      return 'chip-info';
      case VehicleStatus.Maintenance: return 'chip-warn';
      case VehicleStatus.Retired:     return 'chip-muted';
      default:                        return '';
    }
  }

  isExpired(date: string | null | undefined): boolean {
    return !!date && new Date(date).getTime() < Date.now();
  }

  // ---------- Internals ------------------------------------------------------

  private matches(v: Vehicle): boolean {
    const f = this.filters;

    if (f.status   !== 'ALL' && v.status   !== f.status)   return false;
    if (f.fuelType !== 'ALL' && v.fuelType !== f.fuelType) return false;
    if (f.seatsMin != null && v.seatingCapacity < f.seatsMin) return false;
    if (f.seatsMax != null && v.seatingCapacity > f.seatsMax) return false;

    if (!this.matchesInsurance(v)) return false;

    const term = f.search.trim().toLowerCase();
    if (term) {
      const haystack = [
        v.name, v.registrationNumber, v.model, v.year,
        this.fuelTypeLabel(v.fuelType), this.statusLabel(v.status)
      ].filter(x => x != null).join(' ').toLowerCase();
      if (!haystack.includes(term)) return false;
    }
    return true;
  }

  private matchesInsurance(v: Vehicle): boolean {
    const choice = this.filters.insurance;
    if (choice === 'ALL') return true;

    const ts = v.insuranceExpiryDate ? new Date(v.insuranceExpiryDate).getTime() : null;
    if (ts == null) return false;

    const now = Date.now();
    const soon = now + EXPIRING_WINDOW_DAYS * DAY_MS;

    switch (choice) {
      case 'EXPIRED':  return ts < now;
      case 'EXPIRING': return ts >= now && ts < soon;
      case 'VALID':    return ts >= soon;
    }
  }

  private emptyFilters(): VehicleFilters {
    return {
      search:    '',
      status:    'ALL',
      fuelType:  'ALL',
      insurance: 'ALL',
      seatsMin:  null,
      seatsMax:  null
    };
  }
}
