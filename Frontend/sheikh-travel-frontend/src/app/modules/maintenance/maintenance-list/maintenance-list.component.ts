import { Component, OnInit, ViewChild } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { MatPaginator } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { DatePipe, DecimalPipe } from '@angular/common';
import { forkJoin } from 'rxjs';
import { MaintenanceService } from '../../../core/services/maintenance.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import {
  Maintenance,
  MaintenanceStatus,
  MaintenanceStatusLabels
} from '../../../core/models/maintenance.model';
import { Vehicle } from '../../../core/models/vehicle.model';

@Component({
  selector: 'app-maintenance-list',
  templateUrl: './maintenance-list.component.html',
  styleUrls: ['./maintenance-list.component.scss'],
  providers: [DatePipe, DecimalPipe]
})
export class MaintenanceListComponent implements OnInit {
  displayedColumns = ['maintenanceDate', 'vehicle', 'description', 'status', 'cost', 'serviceProvider', 'nextDueDate', 'actions'];

  dataSource = new MatTableDataSource<Maintenance>();
  loading = true;
  allRecords: Maintenance[] = [];
  vehicles: Vehicle[] = [];

  searchTerm = '';
  statusFilter: MaintenanceStatus | 'ALL' = 'ALL';

  readonly statuses = [
    MaintenanceStatus.Scheduled,
    MaintenanceStatus.InProgress,
    MaintenanceStatus.Completed
  ];

  totalCost = 0;
  scheduledCount = 0;

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  constructor(
    private maintenanceService: MaintenanceService,
    private vehicleService: VehicleService,
    private exportService: ExportService,
    private router: Router,
    private snackBar: MatSnackBar,
    private datePipe: DatePipe,
    private decimalPipe: DecimalPipe
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    forkJoin({
      records: this.maintenanceService.getAll(1, 500),
      vehicles: this.vehicleService.getAll(1, 500)
    }).subscribe({
      next: ({ records, vehicles }) => {
        this.vehicles = vehicles.items;
        this.allRecords = records.items.map(r => ({
          ...r,
          vehicleName: this.vehicles.find(v => v.id === r.vehicleId)?.name ?? '—',
          vehicleRegistration: this.vehicles.find(v => v.id === r.vehicleId)?.registrationNumber ?? ''
        }));
        this.applyFilters();
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.snackBar.open('Failed to load maintenance records.', 'Close', { duration: 3000 });
      }
    });
  }

  applyFilters(): void {
    let filtered = [...this.allRecords];

    if (this.statusFilter !== 'ALL') {
      filtered = filtered.filter(r => r.status === this.statusFilter);
    }

    if (this.searchTerm.trim()) {
      const q = this.searchTerm.trim().toLowerCase();
      filtered = filtered.filter(r =>
        (r.vehicleName ?? '').toLowerCase().includes(q) ||
        (r.vehicleRegistration ?? '').toLowerCase().includes(q) ||
        (r.description ?? '').toLowerCase().includes(q) ||
        (r.serviceProvider ?? '').toLowerCase().includes(q)
      );
    }

    this.dataSource.data = filtered;
    this.calculateSummary(filtered);
    setTimeout(() => (this.dataSource.paginator = this.paginator));
  }

  calculateSummary(list: Maintenance[]): void {
    this.totalCost = list.reduce((sum, r) => sum + r.cost, 0);
    this.scheduledCount = list.filter(r => r.status === MaintenanceStatus.Scheduled).length;
  }

  onSearch(term: string): void {
    this.searchTerm = term;
    this.applyFilters();
  }

  onStatusChange(s: MaintenanceStatus | 'ALL'): void {
    this.statusFilter = s;
    this.applyFilters();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.statusFilter = 'ALL';
    this.applyFilters();
  }

  statusLabel(s: MaintenanceStatus): string {
    return MaintenanceStatusLabels[s] ?? 'Unknown';
  }

  formatNumber(n: number, digits = '1.0-0'): string {
    return this.decimalPipe.transform(n, digits) ?? '0';
  }

  updateStatus(record: Maintenance, newStatus: MaintenanceStatus): void {
    this.maintenanceService.updateStatus({ id: record.id, status: newStatus }).subscribe({
      next: () => {
        record.status = newStatus;
        this.snackBar.open('Status updated.', 'Close', { duration: 2000 });
        this.applyFilters();
      },
      error: () => this.snackBar.open('Failed to update status.', 'Close', { duration: 3000 })
    });
  }

  exportExcel(): void {
    this.exportService.exportExcel(this.dataSource.filteredData, this.getColumns(), {
      filename: 'maintenance-records',
      sheetName: 'Maintenance'
    });
  }

  exportPdf(): void {
    this.exportService.exportPdf(this.dataSource.filteredData, this.getColumns(), {
      filename: 'maintenance-records',
      title: 'Sheikh Travel – Maintenance Records',
      subtitle: `Total Cost: PKR ${this.formatNumber(this.totalCost)}`
    });
  }

  private getColumns(): ExportColumn<Maintenance>[] {
    return [
      { header: 'Date',        accessor: (m: Maintenance) => this.datePipe.transform(m.maintenanceDate, 'mediumDate') ?? '', excelWidth: 14, pdfWeight: 1.2 },
      { header: 'Vehicle',     accessor: (m: Maintenance) => `${m.vehicleName} (${m.vehicleRegistration})`,                  excelWidth: 26, pdfWeight: 2   },
      { header: 'Description', accessor: (m: Maintenance) => m.description,                                                  excelWidth: 30, pdfWeight: 2.2 },
      { header: 'Status',      accessor: (m: Maintenance) => this.statusLabel(m.status),                                     excelWidth: 14, pdfWeight: 1   },
      { header: 'Cost (PKR)',  accessor: (m: Maintenance) => m.cost, align: 'right',                                         excelWidth: 12, pdfWeight: 1   },
      { header: 'Provider',    accessor: (m: Maintenance) => m.serviceProvider ?? '—',                                       excelWidth: 18, pdfWeight: 1.4 },
      { header: 'Next Due',    accessor: (m: Maintenance) => this.datePipe.transform(m.nextDueDate, 'mediumDate') ?? '—',    excelWidth: 14, pdfWeight: 1.2 }
    ];
  }
}
