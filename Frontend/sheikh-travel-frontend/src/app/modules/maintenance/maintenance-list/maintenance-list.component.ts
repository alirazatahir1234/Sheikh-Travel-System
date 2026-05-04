import { Component, OnInit, ViewChild } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { MatPaginator } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog } from '@angular/material/dialog';
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
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../shared/components/confirm-dialog/confirm-dialog.component';

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
  overdueFilter: 'ALL' | 'OVERDUE' | 'UPCOMING' = 'ALL';

  readonly statuses = [
    MaintenanceStatus.Scheduled,
    MaintenanceStatus.InProgress,
    MaintenanceStatus.Completed
  ];

  totalCost = 0;
  scheduledCount = 0;
  overdueCount = 0;
  upcomingCount = 0;

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  constructor(
    private maintenanceService: MaintenanceService,
    private vehicleService: VehicleService,
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

    if (this.overdueFilter !== 'ALL') {
      const today = new Date();
      today.setHours(0, 0, 0, 0);
      const in7Days = new Date(today);
      in7Days.setDate(in7Days.getDate() + 7);
      
      if (this.overdueFilter === 'OVERDUE') {
        filtered = filtered.filter(r => r.nextDueDate && new Date(r.nextDueDate) < today);
      } else if (this.overdueFilter === 'UPCOMING') {
        filtered = filtered.filter(r => 
          r.nextDueDate && 
          new Date(r.nextDueDate) >= today && 
          new Date(r.nextDueDate) <= in7Days
        );
      }
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
    this.calculateSummary(this.allRecords);
    setTimeout(() => (this.dataSource.paginator = this.paginator));
  }

  calculateSummary(list: Maintenance[]): void {
    this.totalCost = list.reduce((sum, r) => sum + r.cost, 0);
    this.scheduledCount = list.filter(r => r.status === MaintenanceStatus.Scheduled).length;
    
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const in7Days = new Date(today);
    in7Days.setDate(in7Days.getDate() + 7);
    
    this.overdueCount = list.filter(r => r.nextDueDate && new Date(r.nextDueDate) < today).length;
    this.upcomingCount = list.filter(r => 
      r.nextDueDate && 
      new Date(r.nextDueDate) >= today && 
      new Date(r.nextDueDate) <= in7Days
    ).length;
  }

  onSearch(term: string): void {
    this.searchTerm = term;
    this.applyFilters();
  }

  onStatusChange(s: MaintenanceStatus | 'ALL'): void {
    this.statusFilter = s;
    this.applyFilters();
  }

  onOverdueFilterChange(filter: 'ALL' | 'OVERDUE' | 'UPCOMING'): void {
    this.overdueFilter = filter;
    this.applyFilters();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.statusFilter = 'ALL';
    this.overdueFilter = 'ALL';
    this.applyFilters();
  }

  isOverdue(r: Maintenance): boolean {
    if (!r.nextDueDate) return false;
    return new Date(r.nextDueDate) < new Date();
  }

  isUpcoming(r: Maintenance): boolean {
    if (!r.nextDueDate) return false;
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const in7Days = new Date(today);
    in7Days.setDate(in7Days.getDate() + 7);
    const dueDate = new Date(r.nextDueDate);
    return dueDate >= today && dueDate <= in7Days;
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

  editRecord(record: Maintenance): void {
    this.router.navigate(['/maintenance', record.id, 'edit']);
  }

  deleteRecord(record: Maintenance): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: 'Delete Maintenance Record',
        message: `Delete maintenance record for ${record.vehicleName}?`,
        confirmText: 'Delete',
        confirmColor: 'warn'
      } as ConfirmDialogData
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) return;
      this.maintenanceService.delete(record.id).subscribe({
        next: () => {
          this.snackBar.open('Maintenance record deleted.', 'Close', { duration: 2000 });
          this.allRecords = this.allRecords.filter(r => r.id !== record.id);
          this.applyFilters();
        },
        error: () => this.snackBar.open('Failed to delete record.', 'Close', { duration: 3000 })
      });
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
