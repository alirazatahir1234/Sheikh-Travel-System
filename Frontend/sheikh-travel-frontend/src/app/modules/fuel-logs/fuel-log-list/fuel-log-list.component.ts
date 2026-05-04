import { Component, OnInit, ViewChild } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { MatPaginator } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog } from '@angular/material/dialog';
import { Router } from '@angular/router';
import { DatePipe, DecimalPipe } from '@angular/common';
import { forkJoin } from 'rxjs';
import { FuelLogService } from '../../../core/services/fuel-log.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { DriverService } from '../../../core/services/driver.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { FuelLog, FuelType, FuelTypeLabels } from '../../../core/models/fuel-log.model';
import { Vehicle } from '../../../core/models/vehicle.model';
import { Driver } from '../../../core/models/driver.model';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../shared/components/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-fuel-log-list',
  templateUrl: './fuel-log-list.component.html',
  styleUrls: ['./fuel-log-list.component.scss'],
  providers: [DatePipe, DecimalPipe]
})
export class FuelLogListComponent implements OnInit {
  displayedColumns = ['fuelDate', 'vehicle', 'driver', 'fuelType', 'liters', 'pricePerLiter', 'totalCost', 'odometer', 'station', 'actions'];

  dataSource = new MatTableDataSource<FuelLog>();
  loading = true;
  allLogs: FuelLog[] = [];
  vehicles: Vehicle[] = [];
  drivers: Driver[] = [];

  searchTerm = '';
  fuelTypeFilter: FuelType | 'ALL' = 'ALL';

  readonly fuelTypes = [FuelType.Petrol, FuelType.Diesel, FuelType.CNG];

  // Summary stats
  totalLiters = 0;
  totalCost = 0;

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  constructor(
    private fuelLogService: FuelLogService,
    private vehicleService: VehicleService,
    private driverService: DriverService,
    private exportService: ExportService,
    private router: Router,
    private snackBar: MatSnackBar,
    private dialog: MatDialog,
    private datePipe: DatePipe,
    private decimalPipe: DecimalPipe
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    forkJoin({
      logs: this.fuelLogService.getAll(1, 500),
      vehicles: this.vehicleService.getAll(1, 500),
      drivers: this.driverService.getAll(1, 500)
    }).subscribe({
      next: ({ logs, vehicles, drivers }) => {
        this.vehicles = vehicles.items;
        this.drivers = drivers.items;

        // Enrich logs with vehicle/driver names
        this.allLogs = logs.items.map(log => ({
          ...log,
          vehicleName: this.vehicles.find(v => v.id === log.vehicleId)?.name ?? '—',
          vehicleRegistration: this.vehicles.find(v => v.id === log.vehicleId)?.registrationNumber ?? '',
          driverName: log.driverId ? (this.drivers.find(d => d.id === log.driverId)?.fullName ?? '—') : '—'
        }));

        this.applyFilters();
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.snackBar.open('Failed to load fuel logs.', 'Close', { duration: 3000 });
      }
    });
  }

  applyFilters(): void {
    let filtered = [...this.allLogs];

    if (this.fuelTypeFilter !== 'ALL') {
      filtered = filtered.filter(l => l.fuelType === this.fuelTypeFilter);
    }

    if (this.searchTerm.trim()) {
      const q = this.searchTerm.trim().toLowerCase();
      filtered = filtered.filter(l =>
        (l.vehicleName ?? '').toLowerCase().includes(q) ||
        (l.vehicleRegistration ?? '').toLowerCase().includes(q) ||
        (l.driverName ?? '').toLowerCase().includes(q) ||
        (l.station ?? '').toLowerCase().includes(q)
      );
    }

    this.dataSource.data = filtered;
    this.calculateSummary(filtered);
    setTimeout(() => (this.dataSource.paginator = this.paginator));
  }

  calculateSummary(logs: FuelLog[]): void {
    this.totalLiters = logs.reduce((sum, l) => sum + l.liters, 0);
    this.totalCost = logs.reduce((sum, l) => sum + l.totalCost, 0);
  }

  onSearch(term: string): void {
    this.searchTerm = term;
    this.applyFilters();
  }

  onFuelTypeChange(type: FuelType | 'ALL'): void {
    this.fuelTypeFilter = type;
    this.applyFilters();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.fuelTypeFilter = 'ALL';
    this.applyFilters();
  }

  fuelTypeLabel(f: FuelType): string {
    return FuelTypeLabels[f] ?? 'Unknown';
  }

  formatNumber(n: number, digits = '1.0-0'): string {
    return this.decimalPipe.transform(n, digits) ?? '0';
  }

  editLog(log: FuelLog): void {
    this.router.navigate(['/fuel-logs', log.id, 'edit']);
  }

  deleteLog(log: FuelLog): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: 'Delete Fuel Log',
        message: `Delete fuel log for ${log.vehicleName} on ${this.datePipe.transform(log.fuelDate, 'mediumDate')}?`,
        confirmText: 'Delete',
        confirmColor: 'warn'
      } as ConfirmDialogData
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) return;
      this.fuelLogService.delete(log.id).subscribe({
        next: () => {
          this.snackBar.open('Fuel log deleted.', 'Close', { duration: 2000 });
          this.allLogs = this.allLogs.filter(l => l.id !== log.id);
          this.applyFilters();
        },
        error: () => this.snackBar.open('Failed to delete fuel log.', 'Close', { duration: 3000 })
      });
    });
  }

  exportExcel(): void {
    this.exportService.exportExcel(this.dataSource.filteredData, this.getColumns(), { filename: 'fuel-logs', sheetName: 'Fuel Logs' });
  }

  exportPdf(): void {
    this.exportService.exportPdf(this.dataSource.filteredData, this.getColumns(), {
      filename: 'fuel-logs',
      title: 'Sheikh Travel – Fuel Logs',
      subtitle: `Total: ${this.formatNumber(this.totalLiters, '1.1-1')} L | PKR ${this.formatNumber(this.totalCost)}`
    });
  }

  private getColumns(): ExportColumn<FuelLog>[] {
    return [
      { header: 'Date',       accessor: (l: FuelLog) => this.datePipe.transform(l.fuelDate, 'mediumDate') ?? '', excelWidth: 14, pdfWeight: 1.2 },
      { header: 'Vehicle',    accessor: (l: FuelLog) => `${l.vehicleName} (${l.vehicleRegistration})`,           excelWidth: 28, pdfWeight: 2   },
      { header: 'Driver',     accessor: (l: FuelLog) => l.driverName ?? '—',                                     excelWidth: 20, pdfWeight: 1.5 },
      { header: 'Fuel Type',  accessor: (l: FuelLog) => this.fuelTypeLabel(l.fuelType),                          excelWidth: 12, pdfWeight: 0.8 },
      { header: 'Liters',     accessor: (l: FuelLog) => l.liters,                       align: 'right',          excelWidth: 10, pdfWeight: 0.8 },
      { header: 'Price/L',    accessor: (l: FuelLog) => l.pricePerLiter,                align: 'right',          excelWidth: 10, pdfWeight: 0.8 },
      { header: 'Total (PKR)',accessor: (l: FuelLog) => l.totalCost,                    align: 'right',          excelWidth: 12, pdfWeight: 1   },
      { header: 'Odometer',   accessor: (l: FuelLog) => l.odometerReading,              align: 'right',          excelWidth: 12, pdfWeight: 0.9 },
      { header: 'Station',    accessor: (l: FuelLog) => l.station ?? '—',                                        excelWidth: 18, pdfWeight: 1.2 }
    ];
  }
}
