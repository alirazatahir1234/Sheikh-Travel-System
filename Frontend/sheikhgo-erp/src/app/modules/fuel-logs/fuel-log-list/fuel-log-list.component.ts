import { Component, OnInit, ViewChild } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { MatPaginator } from '@angular/material/paginator';
import { MatDialog } from '@angular/material/dialog';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
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
import { StatTile } from '../../../shared/ui/ui.types';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../shared/components/confirm-dialog/confirm-dialog.component';

type EfficiencyLevel = 'efficient' | 'average' | 'high' | 'unknown';

interface FuelAlert {
  icon: string;
  message: string;
  severity: 'ok' | 'warn' | 'danger';
}

interface VehicleEfficiencyCard {
  vehicleId: number;
  name: string;
  registration: string;
  kmPerLiter: number | null;
  baseline: number;
  level: EfficiencyLevel;
  totalLiters: number;
}

interface TimelineItem {
  id: number;
  title: string;
  subtitle: string;
  date: string;
  icon: string;
}

interface MonthlyHighlight {
  key: string;
  label: string;
  value: string;
  detail: string;
  icon: string;
}

@Component({
  selector: 'app-fuel-log-list',
  templateUrl: './fuel-log-list.component.html',
  styleUrls: ['./fuel-log-list.component.scss'],
  providers: [DatePipe, DecimalPipe]
})
export class FuelLogListComponent implements OnInit {
  displayedColumns = ['fuelDate', 'vehicle', 'driver', 'fuelType', 'liters', 'pricePerLiter', 'totalCost', 'odometer', 'efficiency', 'station', 'actions'];

  dataSource = new MatTableDataSource<FuelLog>();
  loading = true;
  allLogs: FuelLog[] = [];
  vehicles: Vehicle[] = [];
  drivers: Driver[] = [];
  selectedRow: FuelLog | null = null;

  searchTerm = '';
  fuelTypeFilter: FuelType | 'ALL' = 'ALL';

  readonly fuelTypes = [FuelType.Petrol, FuelType.Diesel, FuelType.CNG];
  readonly fuelTypeChips: { id: FuelType | 'ALL'; label: string }[] = [
    { id: 'ALL', label: 'All' },
    { id: FuelType.Petrol, label: 'Petrol' },
    { id: FuelType.Diesel, label: 'Diesel' },
    { id: FuelType.CNG, label: 'CNG' }
  ];

  stats: StatTile[] = [];
  alerts: FuelAlert[] = [];
  efficiencyCards: VehicleEfficiencyCard[] = [];
  timeline: TimelineItem[] = [];
  highlights: MonthlyHighlight[] = [];

  expenseChartLabels: string[] = [];
  expenseChartValues: number[] = [];
  expenseChartTotal = 0;
  typeChartLabels: string[] = [];
  typeChartValues: number[] = [];
  vehicleChartLabels: string[] = [];
  vehicleChartLiters: number[] = [];
  fleetEfficiencyAvg = 0;

  totalLiters = 0;
  totalCost = 0;
  avgLitersPerDay = 0;
  avgPricePerLiter = 0;

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  constructor(
    private fuelLogService: FuelLogService,
    private vehicleService: VehicleService,
    private driverService: DriverService,
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

  get hasNoLogs(): boolean {
    return !this.loading && this.allLogs.length === 0;
  }

  get hasNoFilterMatch(): boolean {
    return !this.loading && this.allLogs.length > 0 && this.dataSource.data.length === 0;
  }

  private initStatsPlaceholder(): void {
    this.stats = [
      { key: 'liters', label: 'Total liters', value: '—', icon: 'opacity', color: 'teal' },
      { key: 'expense', label: 'Total expense', value: '—', icon: 'payments', color: 'amber', prefix: 'PKR ' },
      { key: 'avgday', label: 'Avg / day', value: '—', icon: 'speed', color: 'blue', suffix: ' L' },
      { key: 'price', label: 'Avg price/L', value: '—', icon: 'local_gas_station', color: 'green', prefix: 'PKR ' }
    ];
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
        this.allLogs = logs.items.map(log => this.enrichLog(log));
        this.calculateSummary(this.allLogs);
        this.buildAnalytics();
        this.buildEfficiencyCards();
        this.buildTimeline();
        this.buildHighlights();
        this.buildAlerts();
        this.applyFilters();
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.toast.error('Failed to load fuel logs.');
      }
    });
  }

  private enrichLog(log: FuelLog): FuelLog {
    const vehicle = this.vehicles.find(v => v.id === log.vehicleId);
    const resolvedType = this.resolveFuelType(log.fuelType, vehicle);
    return {
      ...log,
      fuelType: resolvedType,
      vehicleName: vehicle?.name ?? '—',
      vehicleRegistration: vehicle?.registrationNumber ?? '',
      driverName: log.driverId ? (this.drivers.find(d => d.id === log.driverId)?.fullName ?? '—') : '—'
    };
  }

  private resolveFuelType(type: FuelType | number | undefined, vehicle?: Vehicle): FuelType {
    if (type && FuelTypeLabels[type as FuelType]) return type as FuelType;
    if (vehicle?.fuelType && FuelTypeLabels[vehicle.fuelType]) return vehicle.fuelType;
    return FuelType.Petrol;
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
    setTimeout(() => (this.dataSource.paginator = this.paginator));
  }

  private calculateSummary(logs: FuelLog[]): void {
    this.totalLiters = logs.reduce((s, l) => s + l.liters, 0);
    this.totalCost = logs.reduce((s, l) => s + l.totalCost, 0);
    this.avgPricePerLiter = logs.length
      ? logs.reduce((s, l) => s + l.pricePerLiter, 0) / logs.length
      : 0;

    const dates = logs.map(l => new Date(l.fuelDate).getTime());
    const daySpan =
      dates.length > 1
        ? Math.max(1, (Math.max(...dates) - Math.min(...dates)) / (24 * 60 * 60 * 1000))
        : 1;
    this.avgLitersPerDay = this.totalLiters / daySpan;

    const weekLiters = this.sumLitersSince(7);
    const prevWeekLiters = this.sumLitersSince(14) - weekLiters;
    const weekCost = this.sumCostSince(7);
    const prevWeekCost = this.sumCostSince(14) - weekCost;

    this.stats = [
      {
        key: 'liters',
        label: 'Total liters',
        value: this.formatNumber(this.totalLiters, '1.1-1'),
        suffix: ' L',
        icon: 'opacity',
        color: 'teal',
        trend: weekLiters > prevWeekLiters ? `+${this.formatNumber(weekLiters - prevWeekLiters, '1.0-0')} L` : undefined,
        trendUp: weekLiters >= prevWeekLiters,
        trendDetail: 'this week',
        sparkline: this.sparkMonthlyLiters()
      },
      {
        key: 'expense',
        label: 'Total expense',
        value: this.formatNumber(this.totalCost),
        prefix: 'PKR ',
        icon: 'payments',
        color: 'amber',
        trend:
          prevWeekCost > 0
            ? `${Math.round(((weekCost - prevWeekCost) / prevWeekCost) * 100)}%`
            : undefined,
        trendUp: weekCost <= prevWeekCost,
        trendDetail: 'vs prior week',
        sparkline: this.sparkMonthlyCost()
      },
      {
        key: 'avgday',
        label: 'Avg / day',
        value: this.formatNumber(this.avgLitersPerDay, '1.1-1'),
        suffix: ' L',
        icon: 'speed',
        color: 'blue',
        trendDetail: 'fleet consumption',
        sparkline: this.sparkMonthlyLiters()
      },
      {
        key: 'price',
        label: 'Avg price/L',
        value: this.formatNumber(this.avgPricePerLiter, '1.2-2'),
        prefix: 'PKR ',
        icon: 'local_gas_station',
        color: 'green',
        trendDetail: 'blended rate',
        sparkline: [40, 42, 41, 43, this.avgPricePerLiter, 42, 44]
      }
    ];
  }

  private sumLitersSince(days: number): number {
    const cutoff = Date.now() - days * 24 * 60 * 60 * 1000;
    return this.allLogs.filter(l => new Date(l.fuelDate).getTime() >= cutoff).reduce((s, l) => s + l.liters, 0);
  }

  private sumCostSince(days: number): number {
    const cutoff = Date.now() - days * 24 * 60 * 60 * 1000;
    return this.allLogs.filter(l => new Date(l.fuelDate).getTime() >= cutoff).reduce((s, l) => s + l.totalCost, 0);
  }

  private sparkMonthlyLiters(): number[] {
    return this.monthlyBuckets().map(([, liters]) => Math.max(8, liters * 2));
  }

  private sparkMonthlyCost(): number[] {
    return this.monthlyBuckets().map(([, , cost]) => Math.max(8, cost / 500));
  }

  private monthlyBuckets(): [string, number, number][] {
    const map = new Map<string, { liters: number; cost: number }>();
    for (let i = 5; i >= 0; i--) {
      const d = new Date();
      d.setDate(1);
      d.setMonth(d.getMonth() - i);
      map.set(d.toLocaleString('en', { month: 'short' }), { liters: 0, cost: 0 });
    }
    this.allLogs.forEach(l => {
      const key = new Date(l.fuelDate).toLocaleString('en', { month: 'short' });
      if (map.has(key)) {
        const b = map.get(key)!;
        b.liters += l.liters;
        b.cost += l.totalCost;
      }
    });
    return [...map.entries()].map(([k, v]) => [k, v.liters, v.cost]);
  }

  private buildAnalytics(): void {
    const buckets = this.monthlyBuckets();
    this.expenseChartLabels = buckets.map(([k]) => k);
    this.expenseChartValues = buckets.map(([, , c]) => c);
    this.expenseChartTotal = this.expenseChartValues.reduce((a, b) => a + b, 0);

    const byType = new Map<string, number>();
    this.allLogs.forEach(l => {
      const label = this.fuelTypeLabel(l.fuelType);
      byType.set(label, (byType.get(label) ?? 0) + l.liters);
    });
    this.typeChartLabels = [...byType.keys()];
    this.typeChartValues = [...byType.values()];

    const byVehicle = new Map<string, number>();
    this.allLogs.forEach(l => {
      const n = l.vehicleName ?? 'Unknown';
      byVehicle.set(n, (byVehicle.get(n) ?? 0) + l.liters);
    });
    const sorted = [...byVehicle.entries()].sort((a, b) => b[1] - a[1]).slice(0, 5);
    this.vehicleChartLabels = sorted.map(([n]) => (n.length > 12 ? n.slice(0, 12) + '…' : n));
    this.vehicleChartLiters = sorted.map(([, v]) => v);

    const effs = this.vehicles
      .map(v => this.vehicleKmPerLiter(v.id))
      .filter((e): e is number => e != null && e > 0);
    this.fleetEfficiencyAvg = effs.length ? effs.reduce((a, b) => a + b, 0) / effs.length : 0;
  }

  private buildEfficiencyCards(): void {
    this.efficiencyCards = this.vehicles
      .map(v => {
        const kmPerLiter = this.vehicleKmPerLiter(v.id);
        const baseline = v.fuelAverage > 0 ? v.fuelAverage : 12;
        const totalLiters = this.allLogs.filter(l => l.vehicleId === v.id).reduce((s, l) => s + l.liters, 0);
        return {
          vehicleId: v.id,
          name: v.name,
          registration: v.registrationNumber,
          kmPerLiter,
          baseline,
          level: this.efficiencyLevel(kmPerLiter, baseline),
          totalLiters
        };
      })
      .filter(c => c.totalLiters > 0)
      .sort((a, b) => (b.kmPerLiter ?? 0) - (a.kmPerLiter ?? 0))
      .slice(0, 6);
  }

  private buildTimeline(): void {
    this.timeline = [...this.allLogs]
      .sort((a, b) => new Date(b.fuelDate).getTime() - new Date(a.fuelDate).getTime())
      .slice(0, 8)
      .map(l => ({
        id: l.id,
        title: `${this.formatNumber(l.liters, '1.1-1')} L · ${this.fuelTypeLabel(l.fuelType)}`,
        subtitle: `${l.vehicleName} · PKR ${this.formatNumber(l.totalCost)}`,
        date: l.fuelDate,
        icon: 'local_gas_station'
      }));
  }

  private buildHighlights(): void {
    if (!this.allLogs.length) {
      this.highlights = [];
      return;
    }
    const best = [...this.efficiencyCards].sort((a, b) => (b.kmPerLiter ?? 0) - (a.kmPerLiter ?? 0))[0];
    const byLiters = new Map<number, number>();
    const byCount = new Map<number, number>();
    this.allLogs.forEach(l => {
      byLiters.set(l.vehicleId, (byLiters.get(l.vehicleId) ?? 0) + l.liters);
      byCount.set(l.vehicleId, (byCount.get(l.vehicleId) ?? 0) + 1);
    });
    const topUsage = [...byLiters.entries()].sort((a, b) => b[1] - a[1])[0];
    const topActive = [...byCount.entries()].sort((a, b) => b[1] - a[1])[0];
    const usageV = this.vehicles.find(v => v.id === topUsage?.[0]);
    const activeV = this.vehicles.find(v => v.id === topActive?.[0]);

    this.highlights = [
      {
        key: 'best',
        label: 'Best efficiency',
        value: best?.name ?? '—',
        detail: best?.kmPerLiter ? `${this.formatNumber(best.kmPerLiter, '1.1-1')} km/L` : '—',
        icon: 'eco'
      },
      {
        key: 'usage',
        label: 'Highest usage',
        value: usageV?.name ?? '—',
        detail: topUsage ? `${this.formatNumber(topUsage[1], '1.1-1')} L total` : '—',
        icon: 'trending_up'
      },
      {
        key: 'active',
        label: 'Most active',
        value: activeV?.name ?? '—',
        detail: topActive ? `${topActive[1]} fill-ups` : '—',
        icon: 'directions_bus'
      }
    ];
  }

  private buildAlerts(): void {
    this.alerts = [];
    const weekCost = this.sumCostSince(7);
    const prevWeekCost = this.sumCostSince(14) - weekCost;
    if (prevWeekCost > 0 && weekCost > prevWeekCost * 1.08) {
      const pct = Math.round(((weekCost - prevWeekCost) / prevWeekCost) * 100);
      this.alerts.push({
        icon: 'trending_up',
        message: `Fuel costs up ${pct}% this week`,
        severity: 'warn'
      });
    } else if (this.allLogs.length > 0) {
      this.alerts.push({
        icon: 'check_circle',
        message: 'Fleet fuel spend stable',
        severity: 'ok'
      });
    }

    const highUse = this.efficiencyCards.filter(c => c.level === 'high');
    if (highUse.length) {
      this.alerts.push({
        icon: 'warning',
        message: `${highUse.length} vehicle${highUse.length > 1 ? 's' : ''} above average consumption`,
        severity: 'danger'
      });
    }

    const missingOdo = this.allLogs.filter(l => !l.odometerReading || l.odometerReading <= 0).length;
    if (missingOdo) {
      this.alerts.push({
        icon: 'speed',
        message: `${missingOdo} log${missingOdo > 1 ? 's' : ''} missing odometer reading`,
        severity: 'warn'
      });
    }
  }

  vehicleKmPerLiter(vehicleId: number): number | null {
    const logs = this.allLogs
      .filter(l => l.vehicleId === vehicleId)
      .sort((a, b) => new Date(a.fuelDate).getTime() - new Date(b.fuelDate).getTime());
    if (logs.length < 2) {
      const v = this.vehicles.find(x => x.id === vehicleId);
      return v && v.fuelAverage > 0 ? v.fuelAverage : null;
    }
    let totalKm = 0;
    let totalL = 0;
    for (let i = 1; i < logs.length; i++) {
      const km = logs[i].odometerReading - logs[i - 1].odometerReading;
      if (km > 10 && km < 8000) {
        totalKm += km;
        totalL += logs[i].liters;
      }
    }
    if (totalL <= 0) return null;
    return totalKm / totalL;
  }

  efficiencyLevel(kmPerLiter: number | null, baseline: number): EfficiencyLevel {
    if (kmPerLiter == null) return 'unknown';
    if (kmPerLiter >= baseline * 0.95) return 'efficient';
    if (kmPerLiter >= baseline * 0.75) return 'average';
    return 'high';
  }

  logEfficiency(log: FuelLog): EfficiencyLevel {
    const v = this.vehicles.find(x => x.id === log.vehicleId);
    const baseline = v && v.fuelAverage > 0 ? v.fuelAverage : 12;
    const km = this.vehicleKmPerLiter(log.vehicleId);
    return this.efficiencyLevel(km, baseline);
  }

  efficiencyLabel(level: EfficiencyLevel): string {
    const labels: Record<EfficiencyLevel, string> = {
      efficient: 'Efficient',
      average: 'Average',
      high: 'High usage',
      unknown: '—'
    };
    return labels[level];
  }

  onSearch(term: string): void {
    this.searchTerm = term;
    this.applyFilters();
  }

  onFuelTypeChange(type: FuelType | 'ALL'): void {
    this.fuelTypeFilter = type;
    this.applyFilters();
  }

  onStatClick(tile: StatTile): void {
    if (tile.key === 'expense') this.onFuelTypeChange('ALL');
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.fuelTypeFilter = 'ALL';
    this.applyFilters();
  }

  fuelTypeLabel(f: FuelType | number): string {
    return FuelTypeLabels[f as FuelType] ?? 'Petrol';
  }

  fuelTypeCss(f: FuelType): string {
    const map: Record<FuelType, string> = {
      [FuelType.Petrol]: 'petrol',
      [FuelType.Diesel]: 'diesel',
      [FuelType.CNG]: 'cng'
    };
    return map[f] ?? 'petrol';
  }

  formatNumber(n: number, digits = '1.0-0'): string {
    return this.decimalPipe.transform(n, digits) ?? '0';
  }

  openRowMenu(row: FuelLog): void {
    this.selectedRow = row;
  }

  viewVehicle(vehicleId: number): void {
    this.router.navigate(['/vehicles', vehicleId]);
  }

  editLog(log: FuelLog): void {
    this.router.navigate(['/fuel-logs', log.id, 'edit']);
  }

  flagAnomaly(log: FuelLog): void {
    this.toast.success(`Flagged fuel log #${log.id} for review.`);
  }

  exportRowInvoice(log: FuelLog): void {
    this.exportService.exportPdf([log], this.getColumns(), {
      filename: `fuel-invoice-${log.id}`,
      title: 'Fuel purchase receipt',
      subtitle: `${log.vehicleName} · ${this.datePipe.transform(log.fuelDate, 'mediumDate')}`
    });
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
          this.toast.success('Fuel log deleted.');
          this.allLogs = this.allLogs.filter(l => l.id !== log.id);
          this.calculateSummary(this.allLogs);
          this.buildAnalytics();
          this.buildEfficiencyCards();
          this.buildTimeline();
          this.buildHighlights();
          this.buildAlerts();
          this.applyFilters();
        },
        error: () => this.toast.error('Failed to delete fuel log.')
      });
    });
  }

  exportExcel(): void {
    this.exportService.exportExcel(this.dataSource.filteredData, this.getColumns(), {
      filename: 'fuel-logs',
      sheetName: 'Fuel Logs'
    });
  }

  exportPdf(): void {
    this.exportService.exportPdf(this.dataSource.filteredData, this.getColumns(), {
      filename: 'fuel-logs',
      title: 'Sheikh Travel Control Center – Fuel Analytics',
      subtitle: `Total: ${this.formatNumber(this.totalLiters, '1.1-1')} L | PKR ${this.formatNumber(this.totalCost)}`
    });
  }

  private getColumns(): ExportColumn<FuelLog>[] {
    return [
      { header: 'Date', accessor: (l: FuelLog) => this.datePipe.transform(l.fuelDate, 'mediumDate') ?? '', excelWidth: 14, pdfWeight: 1.2 },
      { header: 'Vehicle', accessor: (l: FuelLog) => `${l.vehicleName} (${l.vehicleRegistration})`, excelWidth: 28, pdfWeight: 2 },
      { header: 'Driver', accessor: (l: FuelLog) => l.driverName ?? '—', excelWidth: 20, pdfWeight: 1.5 },
      { header: 'Fuel Type', accessor: (l: FuelLog) => this.fuelTypeLabel(l.fuelType), excelWidth: 12, pdfWeight: 0.8 },
      { header: 'Liters', accessor: (l: FuelLog) => l.liters, align: 'right', excelWidth: 10, pdfWeight: 0.8 },
      { header: 'Price/L', accessor: (l: FuelLog) => l.pricePerLiter, align: 'right', excelWidth: 10, pdfWeight: 0.8 },
      { header: 'Total (PKR)', accessor: (l: FuelLog) => l.totalCost, align: 'right', excelWidth: 12, pdfWeight: 1 },
      { header: 'Odometer', accessor: (l: FuelLog) => l.odometerReading, align: 'right', excelWidth: 12, pdfWeight: 0.9 },
      { header: 'Station', accessor: (l: FuelLog) => l.station ?? '—', excelWidth: 18, pdfWeight: 1.2 }
    ];
  }
}
