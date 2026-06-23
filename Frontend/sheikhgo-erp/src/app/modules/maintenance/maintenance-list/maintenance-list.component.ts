import { Component, OnInit, ViewChild } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { MatPaginator } from '@angular/material/paginator';
import { MatDialog } from '@angular/material/dialog';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { Router } from '@angular/router';
import { DatePipe, DecimalPipe } from '@angular/common';
import { forkJoin } from 'rxjs';
import { MaintenanceService } from '../../../core/services/maintenance.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { exportDocumentTitle } from '../../../core/constants/app-brand';
import {
  Maintenance,
  MaintenanceStatus,
  MaintenanceStatusLabels
} from '../../../core/models/maintenance.model';
import { Vehicle } from '../../../core/models/vehicle.model';
import { StatTile } from '../../../shared/ui/ui.types';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../shared/components/confirm-dialog/confirm-dialog.component';

export type DisplayMaintStatus = 'scheduled' | 'in_progress' | 'completed' | 'overdue';

interface TimelineItem {
  id: number;
  title: string;
  subtitle: string;
  date: string;
  type: DisplayMaintStatus;
  icon: string;
}

interface VehicleHealthCard {
  vehicleId: number;
  name: string;
  registration: string;
  score: number;
  nextService?: string;
  alert?: string;
}

interface MaintAlert {
  icon: string;
  message: string;
  severity: 'ok' | 'warn' | 'danger';
}

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
  selectedRow: Maintenance | null = null;

  searchTerm = '';
  statusFilter: MaintenanceStatus | 'ALL' = 'ALL';
  overdueFilter: 'ALL' | 'OVERDUE' | 'UPCOMING' = 'ALL';

  readonly statuses = [
    MaintenanceStatus.Scheduled,
    MaintenanceStatus.InProgress,
    MaintenanceStatus.Completed
  ];

  readonly statusChips: { id: MaintenanceStatus | 'ALL'; label: string }[] = [
    { id: 'ALL', label: 'All' },
    { id: MaintenanceStatus.Scheduled, label: 'Scheduled' },
    { id: MaintenanceStatus.InProgress, label: 'In Progress' },
    { id: MaintenanceStatus.Completed, label: 'Completed' }
  ];

  readonly quickActions = [
    { label: 'Oil change', icon: 'oil_barrel', template: 'oil' },
    { label: 'Tire rotation', icon: 'tire_repair', template: 'tires' },
    { label: 'Brake inspection', icon: 'car_crash', template: 'brakes' },
    { label: 'Battery check', icon: 'battery_charging_full', template: 'battery' }
  ];

  stats: StatTile[] = [];
  timeline: TimelineItem[] = [];
  healthCards: VehicleHealthCard[] = [];
  alerts: MaintAlert[] = [];

  totalCost = 0;
  scheduledCount = 0;
  inProgressCount = 0;
  completedCount = 0;
  overdueCount = 0;
  upcomingCount = 0;
  dueTodayCount = 0;

  costChartLabels: string[] = [];
  costChartValues: number[] = [];
  costChartTotal = 0;
  statusChartLabels: string[] = [];
  statusChartValues: number[] = [];
  vehicleChartLabels: string[] = [];
  vehicleChartValues: number[] = [];
  completionRate = 0;

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  constructor(
    private maintenanceService: MaintenanceService,
    private vehicleService: VehicleService,
    private exportService: ExportService,
    private router: Router,
    private toast: UiToastService,
    private dialog: MatDialog,
    private datePipe: DatePipe,
    private decimalPipe: DecimalPipe
  ) {}

  ngOnInit(): void {
    this.initStatsPlaceholder();
    this.load();
  }

  get fleetHealthy(): boolean {
    return this.overdueCount === 0 && this.dueTodayCount <= 1;
  }

  get hasFilteredResults(): boolean {
    return this.dataSource.data.length > 0;
  }

  get showHealthyEmpty(): boolean {
    return !this.loading && this.allRecords.length > 0 && !this.hasFilteredResults && this.overdueFilter === 'ALL' && !this.searchTerm && this.statusFilter === 'ALL';
  }

  get showFleetHealthyEmpty(): boolean {
    return !this.loading && this.allRecords.length === 0;
  }

  private initStatsPlaceholder(): void {
    this.stats = [
      { key: 'scheduled', label: 'Scheduled', value: '—', icon: 'schedule', color: 'blue' },
      { key: 'progress', label: 'In progress', value: '—', icon: 'build', color: 'amber' },
      { key: 'overdue', label: 'Overdue', value: '—', icon: 'warning', color: 'rose', variant: 'danger' },
      { key: 'cost', label: 'Total cost', value: '—', icon: 'payments', color: 'teal', prefix: 'PKR ' }
    ];
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
        this.calculateSummary(this.allRecords);
        this.buildAnalytics();
        this.buildTimeline();
        this.buildHealthCards();
        this.buildAlerts();
        this.applyFilters();
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.toast.error('Failed to load maintenance records.');
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
        filtered = filtered.filter(r => this.isOverdue(r));
      } else if (this.overdueFilter === 'UPCOMING') {
        filtered = filtered.filter(r => this.isUpcoming(r));
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

  private calculateSummary(list: Maintenance[]): void {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const tomorrow = new Date(today);
    tomorrow.setDate(tomorrow.getDate() + 1);
    const in7Days = new Date(today);
    in7Days.setDate(in7Days.getDate() + 7);

    this.totalCost = list.reduce((sum, r) => sum + r.cost, 0);
    this.scheduledCount = list.filter(r => r.status === MaintenanceStatus.Scheduled).length;
    this.inProgressCount = list.filter(r => r.status === MaintenanceStatus.InProgress).length;
    this.completedCount = list.filter(r => r.status === MaintenanceStatus.Completed).length;
    this.overdueCount = list.filter(r => this.isOverdue(r)).length;
    this.upcomingCount = list.filter(r => this.isUpcoming(r)).length;
    this.dueTodayCount = list.filter(r => {
      if (!r.nextDueDate) return false;
      const d = new Date(r.nextDueDate);
      d.setHours(0, 0, 0, 0);
      return d.getTime() === today.getTime() && r.status !== MaintenanceStatus.Completed;
    }).length;

    const scheduledWeek = this.countRecent(7, MaintenanceStatus.Scheduled);
    const scheduledPrev = this.countRecent(14, MaintenanceStatus.Scheduled) - scheduledWeek;

    this.stats = [
      {
        key: 'scheduled',
        label: 'Scheduled',
        value: this.scheduledCount,
        icon: 'schedule',
        color: 'blue',
        trend: scheduledWeek > scheduledPrev ? `+${scheduledWeek - scheduledPrev}` : undefined,
        trendUp: scheduledWeek >= scheduledPrev,
        trendDetail: 'this week',
        sparkline: this.sparkFromStatus(MaintenanceStatus.Scheduled)
      },
      {
        key: 'progress',
        label: 'In progress',
        value: this.inProgressCount,
        icon: 'build',
        color: 'amber',
        variant: this.inProgressCount > 0 ? 'warning' : 'default',
        trendDetail: 'active work orders',
        sparkline: this.sparkFromStatus(MaintenanceStatus.InProgress)
      },
      {
        key: 'overdue',
        label: 'Overdue',
        value: this.overdueCount,
        icon: 'warning',
        color: 'rose',
        variant: this.overdueCount > 0 ? 'danger' : 'success',
        trendDetail: this.overdueCount ? 'needs attention' : 'on track',
        sparkline: [20, 40, this.overdueCount * 15, 30, 25, this.overdueCount * 20, 10]
      },
      {
        key: 'cost',
        label: 'Total cost',
        value: this.formatNumber(this.totalCost),
        icon: 'payments',
        color: 'teal',
        prefix: 'PKR ',
        trendDetail: 'all records',
        sparkline: this.costChartValues.length ? this.costChartValues : [10, 20, 15, 30, 25, 40, 35]
      }
    ];
  }

  private countRecent(days: number, status: MaintenanceStatus): number {
    const cutoff = new Date();
    cutoff.setDate(cutoff.getDate() - days);
    return this.allRecords.filter(
      r => r.status === status && new Date(r.maintenanceDate) >= cutoff
    ).length;
  }

  private sparkFromStatus(status: MaintenanceStatus): number[] {
    const months: number[] = [];
    for (let i = 5; i >= 0; i--) {
      const d = new Date();
      d.setMonth(d.getMonth() - i);
      const m = d.getMonth();
      const y = d.getFullYear();
      months.push(
        this.allRecords.filter(r => {
          const dt = new Date(r.maintenanceDate);
          return r.status === status && dt.getMonth() === m && dt.getFullYear() === y;
        }).length * 18 + 8
      );
    }
    return months;
  }

  private buildAnalytics(): void {
    const monthMap = new Map<string, number>();
    for (let i = 5; i >= 0; i--) {
      const d = new Date();
      d.setDate(1);
      d.setMonth(d.getMonth() - i);
      const key = d.toLocaleString('en', { month: 'short' });
      monthMap.set(key, 0);
    }
    this.allRecords.forEach(r => {
      const d = new Date(r.maintenanceDate);
      const key = d.toLocaleString('en', { month: 'short' });
      if (monthMap.has(key)) monthMap.set(key, (monthMap.get(key) ?? 0) + r.cost);
    });
    this.costChartLabels = [...monthMap.keys()];
    this.costChartValues = [...monthMap.values()];
    this.costChartTotal = this.costChartValues.reduce((a, b) => a + b, 0);

    this.statusChartLabels = ['Scheduled', 'In progress', 'Completed', 'Overdue'];
    this.statusChartValues = [
      this.scheduledCount,
      this.inProgressCount,
      this.completedCount,
      this.overdueCount
    ];
    const total = this.allRecords.length || 1;
    this.completionRate = Math.round((this.completedCount / total) * 100);

    const byVehicle = new Map<string, number>();
    this.allRecords.forEach(r => {
      const name = r.vehicleName ?? 'Unknown';
      byVehicle.set(name, (byVehicle.get(name) ?? 0) + 1);
    });
    const sorted = [...byVehicle.entries()].sort((a, b) => b[1] - a[1]).slice(0, 5);
    this.vehicleChartLabels = sorted.map(([n]) => (n.length > 12 ? n.slice(0, 12) + '…' : n));
    this.vehicleChartValues = sorted.map(([, c]) => c);
  }

  private buildTimeline(): void {
    const sorted = [...this.allRecords].sort(
      (a, b) => new Date(b.maintenanceDate).getTime() - new Date(a.maintenanceDate).getTime()
    );
    this.timeline = sorted.slice(0, 8).map(r => {
      const display = this.displayStatus(r);
      return {
        id: r.id,
        title: r.description,
        subtitle: `${r.vehicleName} · ${this.statusLabel(r.status)}`,
        date: r.maintenanceDate,
        type: display,
        icon: this.statusIcon(display)
      };
    });
  }

  private buildHealthCards(): void {
    const cards: VehicleHealthCard[] = [];
    for (const v of this.vehicles.slice(0, 12)) {
      const recs = this.allRecords.filter(r => r.vehicleId === v.id);
      const overdue = recs.filter(r => this.isOverdue(r)).length;
      const upcoming = recs.filter(r => this.isUpcoming(r)).length;
      let score = 96 - overdue * 22 - upcoming * 6;
      if (recs.some(r => r.status === MaintenanceStatus.InProgress)) score -= 4;
      score = Math.max(42, Math.min(100, score));

      const nextRec = recs
        .filter(r => r.nextDueDate && r.status !== MaintenanceStatus.Completed)
        .sort((a, b) => new Date(a.nextDueDate!).getTime() - new Date(b.nextDueDate!).getTime())[0];

      let alert: string | undefined;
      if (overdue) alert = 'Service overdue — schedule immediately';
      else if (upcoming) alert = `Due soon (${this.datePipe.transform(nextRec?.nextDueDate, 'mediumDate')})`;

      cards.push({
        vehicleId: v.id,
        name: v.name,
        registration: v.registrationNumber,
        score,
        nextService: nextRec?.nextDueDate
          ? (this.datePipe.transform(nextRec.nextDueDate, 'mediumDate') ?? undefined)
          : undefined,
        alert
      });
    }
    this.healthCards = cards
      .sort((a, b) => a.score - b.score)
      .slice(0, 6);
  }

  private buildAlerts(): void {
    this.alerts = [];
    if (this.dueTodayCount > 0) {
      this.alerts.push({
        icon: 'event',
        message: `${this.dueTodayCount} service${this.dueTodayCount > 1 ? 's' : ''} due today`,
        severity: 'warn'
      });
    }
    if (this.overdueCount > 0) {
      this.alerts.push({
        icon: 'warning',
        message: `${this.overdueCount} overdue maintenance record${this.overdueCount > 1 ? 's' : ''}`,
        severity: 'danger'
      });
    }
    if (this.fleetHealthy && this.allRecords.length > 0) {
      this.alerts.push({
        icon: 'verified',
        message: 'Fleet maintenance healthy',
        severity: 'ok'
      });
    }
  }

  displayStatus(r: Maintenance): DisplayMaintStatus {
    if (this.isOverdue(r)) return 'overdue';
    if (r.status === MaintenanceStatus.InProgress) return 'in_progress';
    if (r.status === MaintenanceStatus.Completed) return 'completed';
    return 'scheduled';
  }

  statusIcon(type: DisplayMaintStatus): string {
    const icons: Record<DisplayMaintStatus, string> = {
      scheduled: 'schedule',
      in_progress: 'handyman',
      completed: 'check_circle',
      overdue: 'error'
    };
    return icons[type];
  }

  openRowMenu(row: Maintenance): void {
    this.selectedRow = row;
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

  onStatClick(tile: StatTile): void {
    if (tile.key === 'scheduled') this.onStatusChange(MaintenanceStatus.Scheduled);
    else if (tile.key === 'progress') this.onStatusChange(MaintenanceStatus.InProgress);
    else if (tile.key === 'overdue') this.onOverdueFilterChange('OVERDUE');
    else this.onOverdueFilterChange('ALL');
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.statusFilter = 'ALL';
    this.overdueFilter = 'ALL';
    this.applyFilters();
  }

  scheduleQuick(template: string): void {
    this.router.navigate(['/maintenance/new'], { queryParams: { template } });
  }

  viewVehicle(vehicleId: number): void {
    this.router.navigate(['/vehicles', vehicleId]);
  }

  isOverdue(r: Maintenance): boolean {
    if (!r.nextDueDate || r.status === MaintenanceStatus.Completed) return false;
    const due = new Date(r.nextDueDate);
    due.setHours(0, 0, 0, 0);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return due < today;
  }

  isUpcoming(r: Maintenance): boolean {
    if (!r.nextDueDate || r.status === MaintenanceStatus.Completed) return false;
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const in7Days = new Date(today);
    in7Days.setDate(in7Days.getDate() + 7);
    const dueDate = new Date(r.nextDueDate);
    dueDate.setHours(0, 0, 0, 0);
    return dueDate >= today && dueDate <= in7Days;
  }

  statusLabel(s: MaintenanceStatus): string {
    return MaintenanceStatusLabels[s] ?? 'Unknown';
  }

  displayStatusLabel(r: Maintenance): string {
    const d = this.displayStatus(r);
    const labels: Record<DisplayMaintStatus, string> = {
      scheduled: 'Scheduled',
      in_progress: 'In Progress',
      completed: 'Completed',
      overdue: 'Overdue'
    };
    return labels[d];
  }

  formatNumber(n: number, digits = '1.0-0'): string {
    return this.decimalPipe.transform(n, digits) ?? '0';
  }

  updateStatus(record: Maintenance, newStatus: MaintenanceStatus): void {
    this.maintenanceService.updateStatus({ id: record.id, status: newStatus }).subscribe({
      next: () => {
        record.status = newStatus;
        this.toast.success('Status updated.');
        this.buildAnalytics();
        this.buildTimeline();
        this.buildHealthCards();
        this.buildAlerts();
        this.applyFilters();
      },
      error: () => this.toast.error('Failed to update status.')
    });
  }

  editRecord(record: Maintenance): void {
    this.router.navigate(['/maintenance', record.id, 'edit']);
  }

  completeRecord(record: Maintenance): void {
    this.updateStatus(record, MaintenanceStatus.Completed);
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
          this.toast.success('Maintenance record deleted.');
          this.allRecords = this.allRecords.filter(r => r.id !== record.id);
          this.buildAnalytics();
          this.buildTimeline();
          this.buildHealthCards();
          this.buildAlerts();
          this.applyFilters();
        },
        error: () => this.toast.error('Failed to delete record.')
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
      title: exportDocumentTitle('Maintenance Records'),
      subtitle: `Total Cost: PKR ${this.formatNumber(this.totalCost)}`
    });
  }

  private getColumns(): ExportColumn<Maintenance>[] {
    return [
      { header: 'Date', accessor: (m: Maintenance) => this.datePipe.transform(m.maintenanceDate, 'mediumDate') ?? '', excelWidth: 14, pdfWeight: 1.2 },
      { header: 'Vehicle', accessor: (m: Maintenance) => `${m.vehicleName} (${m.vehicleRegistration})`, excelWidth: 26, pdfWeight: 2 },
      { header: 'Description', accessor: (m: Maintenance) => m.description, excelWidth: 30, pdfWeight: 2.2 },
      { header: 'Status', accessor: (m: Maintenance) => this.displayStatusLabel(m), excelWidth: 14, pdfWeight: 1 },
      { header: 'Cost (PKR)', accessor: (m: Maintenance) => m.cost, align: 'right', excelWidth: 12, pdfWeight: 1 },
      { header: 'Provider', accessor: (m: Maintenance) => m.serviceProvider ?? '—', excelWidth: 18, pdfWeight: 1.4 },
      { header: 'Next Due', accessor: (m: Maintenance) => this.datePipe.transform(m.nextDueDate, 'mediumDate') ?? '—', excelWidth: 14, pdfWeight: 1.2 }
    ];
  }
}
