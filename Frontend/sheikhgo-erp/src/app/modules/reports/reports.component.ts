import { Component, OnInit, OnDestroy, AfterViewInit, ElementRef, ViewChild, NgZone, inject } from '@angular/core';
import { MatTabChangeEvent } from '@angular/material/tabs';
import { Chart, registerables } from 'chart.js';
import { AppChartInstance, createAppChart } from '../../core/utils/chart.util';
import { DashboardService, RevenueReportDto } from '../../core/services/dashboard.service';
import { ReportService } from '../../core/services/report.service';
import { ExportService, ExportColumn } from '../../core/services/export.service';
import { UiToastService } from '../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../core/utils/api-error.util';
import {
  VehicleProfitDto,
  DriverPerformanceDto,
  PaymentReportDto,
  PaymentReportItemDto
} from '../../core/models/common.model';

Chart.register(...registerables);

type ReportDatePreset = 'thisMonth' | 'lastMonth' | 'last7Days' | 'last30Days' | 'thisYear' | 'custom';

interface DatePresetOption {
  id: ReportDatePreset;
  label: string;
}

@Component({
  standalone: false,
  selector: 'app-reports',
  templateUrl: './reports.component.html',
  styleUrls: ['./reports.component.scss']
})
export class ReportsComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('revenueChart') revenueCanvasRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('statusChart') statusCanvasRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('vehicleProfitChart') vehicleProfitCanvasRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('driverChart') driverCanvasRef!: ElementRef<HTMLCanvasElement>;

  private readonly dashboardService = inject(DashboardService);
  private readonly reportService = inject(ReportService);
  private readonly exportService = inject(ExportService);
  private readonly toast = inject(UiToastService);
  private readonly ngZone = inject(NgZone);

  readonly datePresets: DatePresetOption[] = [
    { id: 'thisMonth', label: 'This Month' },
    { id: 'lastMonth', label: 'Last Month' },
    { id: 'last7Days', label: 'Last 7 Days' },
    { id: 'last30Days', label: 'Last 30 Days' },
    { id: 'thisYear', label: 'This Year' },
    { id: 'custom', label: 'Custom' }
  ];

  datePreset: ReportDatePreset = 'thisMonth';
  fromDate = '';
  toDate = '';
  dateError = '';

  selectedTabIndex = 0;
  private tabDataLoaded = new Set<number>();

  revenueReport: RevenueReportDto | null = null;
  bookingStatusCounts: { status: string; count: number }[] = [];
  loadingRevenue = false;
  revenueError = '';

  vehicleProfitRows: VehicleProfitDto[] = [];
  vehicleProfitCols = ['vehicleName', 'revenue', 'fuelCost', 'maintenanceCost', 'profit'];
  loadingVehicleProfit = false;
  vehicleProfitError = '';

  driverRows: DriverPerformanceDto[] = [];
  driverCols = ['driverName', 'totalTrips', 'completedTrips', 'totalRevenue'];
  loadingDrivers = false;
  driverError = '';

  paymentReport: PaymentReportDto | null = null;
  paymentCols = ['paymentDate', 'bookingId', 'amount', 'paymentMethod', 'status'];
  loadingPayments = false;
  paymentError = '';

  private revenueChart?: AppChartInstance;
  private statusChart?: AppChartInstance;
  private vehicleProfitChart?: AppChartInstance;
  private driverChart?: AppChartInstance;

  ngOnInit(): void {
    this.applyPreset('thisMonth');
  }

  ngAfterViewInit(): void {
    this.loadTabData(0);
  }

  onTabChange(event: MatTabChangeEvent): void {
    this.selectedTabIndex = event.index;
    this.loadTabData(event.index);
  }

  applyPreset(preset: ReportDatePreset): void {
    this.datePreset = preset;
    if (preset === 'custom') return;

    const now = new Date();
    const today = this.startOfDay(now);
    let from: Date;
    let to: Date = this.endOfDay(now);

    switch (preset) {
      case 'thisMonth':
        from = new Date(now.getFullYear(), now.getMonth(), 1);
        break;
      case 'lastMonth': {
        from = new Date(now.getFullYear(), now.getMonth() - 1, 1);
        to = this.endOfDay(new Date(now.getFullYear(), now.getMonth(), 0));
        break;
      }
      case 'last7Days':
        from = new Date(now.getTime() - 6 * 24 * 60 * 60 * 1000);
        from = this.startOfDay(from);
        break;
      case 'last30Days':
        from = new Date(now.getTime() - 29 * 24 * 60 * 60 * 1000);
        from = this.startOfDay(from);
        break;
      case 'thisYear':
        from = new Date(now.getFullYear(), 0, 1);
        break;
      default:
        from = new Date(now.getFullYear(), now.getMonth(), 1);
    }

    this.fromDate = this.toDateInput(from);
    this.toDate = this.toDateInput(to);
    this.dateError = '';
  }

  onCustomDateChange(): void {
    this.datePreset = 'custom';
    this.dateError = this.validateDates();
  }

  get periodLabel(): string {
    if (!this.fromDate || !this.toDate) return '';
    return `${this.formatDisplayDate(this.fromDate)} – ${this.formatDisplayDate(this.toDate)}`;
  }

  get vehicleTotals(): { revenue: number; fuel: number; maintenance: number; profit: number } {
    return this.vehicleProfitRows.reduce(
      (acc, r) => ({
        revenue: acc.revenue + (r.revenue || 0),
        fuel: acc.fuel + (r.fuelCost || 0),
        maintenance: acc.maintenance + (r.maintenanceCost || 0),
        profit: acc.profit + (r.profit || 0)
      }),
      { revenue: 0, fuel: 0, maintenance: 0, profit: 0 }
    );
  }

  get driverTotals(): { trips: number; completed: number; revenue: number } {
    return this.driverRows.reduce(
      (acc, r) => ({
        trips: acc.trips + (r.totalTrips || 0),
        completed: acc.completed + (r.completedTrips || 0),
        revenue: acc.revenue + (r.totalRevenue || 0)
      }),
      { trips: 0, completed: 0, revenue: 0 }
    );
  }

  private loadTabData(tabIndex: number): void {
    if (this.tabDataLoaded.has(tabIndex)) {
      this.deferChartRender(tabIndex);
      return;
    }

    this.tabDataLoaded.add(tabIndex);

    switch (tabIndex) {
      case 0:
        this.loadRevenue();
        break;
      case 1:
        this.loadVehicleProfit();
        break;
      case 2:
        this.loadDriverPerformance();
        break;
      case 3:
        this.loadPaymentReport();
        break;
    }
  }

  private deferChartRender(tabIndex: number): void {
    requestAnimationFrame(() => {
      this.ngZone.run(() => {
        switch (tabIndex) {
          case 0:
            if (this.revenueReport) {
              this.renderRevenueChartFromReport(this.revenueReport);
            }
            if (this.bookingStatusCounts.length > 0) {
              this.renderStatusChart(
                this.bookingStatusCounts.map(d => d.status),
                this.bookingStatusCounts.map(d => d.count)
              );
            }
            break;
          case 1:
            if (this.vehicleProfitRows.length > 0) {
              this.renderVehicleProfitChart(this.vehicleProfitRows);
            }
            break;
          case 2:
            if (this.driverRows.length > 0) {
              this.renderDriverChart(this.driverRows);
            }
            break;
        }
      });
    });
  }

  ngOnDestroy(): void {
    [this.revenueChart, this.statusChart, this.vehicleProfitChart, this.driverChart]
      .forEach(c => c?.destroy());
  }

  loadReports(): void {
    this.dateError = this.validateDates();
    if (this.dateError) {
      this.toast.warning(this.dateError);
      return;
    }
    this.tabDataLoaded.clear();
    this.loadTabData(this.selectedTabIndex);
  }

  loadRevenue(): void {
    this.loadingRevenue = true;
    this.revenueError = '';
    this.revenueReport = null;
    this.bookingStatusCounts = [];

    this.dashboardService.getRevenueReport(this.fromDate, this.toDate).subscribe({
      next: (data: RevenueReportDto) => {
        this.revenueReport = data;
        this.renderRevenueChartFromReport(data);
        this.loadingRevenue = false;
      },
      error: err => {
        this.loadingRevenue = false;
        this.revenueError = apiErrorMessage(err, 'Could not load revenue report');
        this.toast.error(this.revenueError);
        this.destroyChart(this.revenueChart);
        this.revenueChart = undefined;
      }
    });

    this.dashboardService.getBookingStatusReport(this.fromDate, this.toDate).subscribe({
      next: data => {
        this.bookingStatusCounts = data.map(d => ({ status: d.status, count: d.count }));
        this.renderStatusChart(
          this.bookingStatusCounts.map(d => d.status),
          this.bookingStatusCounts.map(d => d.count)
        );
      },
      error: err => {
        this.bookingStatusCounts = [];
        this.destroyChart(this.statusChart);
        this.statusChart = undefined;
        this.toast.error(apiErrorMessage(err, 'Could not load booking status'));
      }
    });
  }

  loadVehicleProfit(): void {
    this.loadingVehicleProfit = true;
    this.vehicleProfitError = '';
    this.reportService.getVehicleProfit(this.fromDate, this.toDate).subscribe({
      next: rows => {
        this.vehicleProfitRows = rows;
        this.renderVehicleProfitChart(rows);
        this.loadingVehicleProfit = false;
      },
      error: err => {
        this.vehicleProfitRows = [];
        this.loadingVehicleProfit = false;
        this.vehicleProfitError = apiErrorMessage(err, 'Could not load vehicle profit');
        this.toast.error(this.vehicleProfitError);
      }
    });
  }

  loadDriverPerformance(): void {
    this.loadingDrivers = true;
    this.driverError = '';
    this.reportService.getDriverPerformance(this.fromDate, this.toDate).subscribe({
      next: rows => {
        this.driverRows = rows;
        this.renderDriverChart(rows);
        this.loadingDrivers = false;
      },
      error: err => {
        this.driverRows = [];
        this.loadingDrivers = false;
        this.driverError = apiErrorMessage(err, 'Could not load driver performance');
        this.toast.error(this.driverError);
      }
    });
  }

  loadPaymentReport(): void {
    this.loadingPayments = true;
    this.paymentError = '';
    this.reportService.getPaymentReport(this.fromDate, this.toDate).subscribe({
      next: report => {
        this.paymentReport = report;
        this.loadingPayments = false;
      },
      error: err => {
        this.paymentReport = null;
        this.loadingPayments = false;
        this.paymentError = apiErrorMessage(err, 'Could not load payment report');
        this.toast.error(this.paymentError);
      }
    });
  }

  paymentStatusLabel(status: number): string {
    const map: Record<number, string> = { 1: 'Pending', 2: 'Partially Paid', 3: 'Paid', 4: 'Refunded' };
    return map[status] ?? 'Unknown';
  }

  paymentStatusColor(status: number): string {
    const map: Record<number, string> = { 1: '#f57f17', 2: '#1565c0', 3: '#2e7d32', 4: '#6a1b9a' };
    return map[status] ?? '#666';
  }

  private renderRevenueChartFromReport(data: RevenueReportDto): void {
    this.renderRevenueChart(
      ['Total Revenue', 'Fuel Expense', 'Maintenance', 'Net Profit'],
      [data.totalRevenue, data.fuelExpense, data.maintenanceCost, data.netProfit]
    );
  }

  private renderRevenueChart(labels: string[], data: number[]): void {
    requestAnimationFrame(() => this.doRenderRevenueChart(labels, data));
  }

  private doRenderRevenueChart(labels: string[], data: number[]): void {
    this.destroyChart(this.revenueChart);
    if (!this.revenueCanvasRef?.nativeElement) return;
    this.revenueChart = createAppChart(this.revenueCanvasRef.nativeElement, {
      type: 'bar',
      data: {
        labels,
        datasets: [{
          label: 'Amount (PKR)',
          data,
          backgroundColor: [
            'rgba(11, 107, 80, 0.75)', 'rgba(198, 40, 40, 0.7)',
            'rgba(245, 127, 23, 0.7)', 'rgba(46, 125, 50, 0.7)'
          ],
          borderColor: ['#0B6B50', '#c62828', '#f57f17', '#2e7d32'],
          borderWidth: 1
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: { legend: { display: false } },
        scales: { y: { beginAtZero: true } }
      }
    });
  }

  private renderStatusChart(labels: string[], data: number[]): void {
    requestAnimationFrame(() => this.doRenderStatusChart(labels, data));
  }

  private doRenderStatusChart(labels: string[], data: number[]): void {
    this.destroyChart(this.statusChart);
    if (!this.statusCanvasRef?.nativeElement) return;
    const hasData = data.some(n => n > 0);
    this.statusChart = createAppChart(this.statusCanvasRef.nativeElement, {
      type: 'doughnut',
      data: {
        labels,
        datasets: [{
          data: hasData ? data : [1],
          backgroundColor: hasData
            ? ['#2e7d32', '#f57f17', '#1565c0', '#c62828']
            : ['#e2e8f0'],
          borderWidth: 2
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { position: 'bottom' },
          tooltip: { enabled: hasData }
        }
      }
    });
  }

  private renderVehicleProfitChart(rows: VehicleProfitDto[]): void {
    requestAnimationFrame(() => this.doRenderVehicleProfitChart(rows));
  }

  private doRenderVehicleProfitChart(rows: VehicleProfitDto[]): void {
    this.destroyChart(this.vehicleProfitChart);
    if (!this.vehicleProfitCanvasRef?.nativeElement || rows.length === 0) return;
    const top = rows.slice(0, 12);
    this.vehicleProfitChart = createAppChart(this.vehicleProfitCanvasRef.nativeElement, {
      type: 'bar',
      data: {
        labels: top.map(r => r.vehicleName),
        datasets: [
          { label: 'Revenue', data: top.map(r => r.revenue), backgroundColor: 'rgba(11, 107, 80, 0.75)', borderColor: '#0B6B50', borderWidth: 1 },
          { label: 'Fuel Cost', data: top.map(r => r.fuelCost), backgroundColor: 'rgba(198, 40, 40, 0.7)', borderColor: '#c62828', borderWidth: 1 },
          { label: 'Maintenance', data: top.map(r => r.maintenanceCost), backgroundColor: 'rgba(245, 127, 23, 0.7)', borderColor: '#f57f17', borderWidth: 1 },
          { label: 'Profit', data: top.map(r => r.profit), backgroundColor: 'rgba(46, 125, 50, 0.7)', borderColor: '#2e7d32', borderWidth: 1 }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        scales: { y: { beginAtZero: true } }
      }
    });
  }

  private renderDriverChart(rows: DriverPerformanceDto[]): void {
    requestAnimationFrame(() => this.doRenderDriverChart(rows));
  }

  private doRenderDriverChart(rows: DriverPerformanceDto[]): void {
    this.destroyChart(this.driverChart);
    if (!this.driverCanvasRef?.nativeElement || rows.length === 0) return;
    const top = rows.slice(0, 12);
    this.driverChart = createAppChart(this.driverCanvasRef.nativeElement, {
      type: 'bar',
      data: {
        labels: top.map(r => r.driverName),
        datasets: [
          { label: 'Total Trips', data: top.map(r => r.totalTrips), backgroundColor: 'rgba(11, 107, 80, 0.75)', borderColor: '#0B6B50', borderWidth: 1 },
          { label: 'Completed', data: top.map(r => r.completedTrips), backgroundColor: 'rgba(46, 125, 50, 0.7)', borderColor: '#2e7d32', borderWidth: 1 }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        scales: { y: { beginAtZero: true } }
      }
    });
  }

  private destroyChart(chart?: AppChartInstance): void {
    chart?.destroy();
  }

  private validateDates(): string {
    if (!this.fromDate || !this.toDate) return 'Select both From and To dates.';
    if (this.fromDate > this.toDate) return 'From date cannot be after To date.';
    return '';
  }

  private startOfDay(d: Date): Date {
    return new Date(d.getFullYear(), d.getMonth(), d.getDate(), 0, 0, 0, 0);
  }

  private endOfDay(d: Date): Date {
    return new Date(d.getFullYear(), d.getMonth(), d.getDate(), 23, 59, 59, 999);
  }

  private toDateInput(d: Date): string {
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  }

  private formatDisplayDate(isoDate: string): string {
    const d = new Date(isoDate + 'T00:00:00');
    if (Number.isNaN(d.getTime())) return isoDate;
    return d.toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' });
  }

  exportRevenueExcel(): void {
    if (!this.revenueReport) return;
    const rows = [
      { metric: 'Total Revenue', amount: this.revenueReport.totalRevenue },
      { metric: 'Fuel Expense', amount: this.revenueReport.fuelExpense },
      { metric: 'Maintenance Cost', amount: this.revenueReport.maintenanceCost },
      { metric: 'Net Profit', amount: this.revenueReport.netProfit }
    ];
    const columns: ExportColumn<(typeof rows)[number]>[] = [
      { header: 'Metric', accessor: r => r.metric },
      { header: 'Amount (PKR)', accessor: r => r.amount }
    ];
    this.exportService.exportExcel(rows, columns, {
      filename: `revenue-report-${this.fromDate}-to-${this.toDate}`,
      sheetName: 'Revenue',
      title: `Revenue Report (${this.periodLabel})`
    });
  }

  exportRevenuePdf(): void {
    if (!this.revenueReport) return;
    const rows = [
      { metric: 'Total Revenue', amount: this.revenueReport.totalRevenue },
      { metric: 'Fuel Expense', amount: this.revenueReport.fuelExpense },
      { metric: 'Maintenance Cost', amount: this.revenueReport.maintenanceCost },
      { metric: 'Net Profit', amount: this.revenueReport.netProfit }
    ];
    const columns: ExportColumn<(typeof rows)[number]>[] = [
      { header: 'Metric', accessor: r => r.metric },
      { header: 'Amount (PKR)', accessor: r => r.amount }
    ];
    void this.exportService.exportPdf(rows, columns, {
      filename: `revenue-report-${this.fromDate}-to-${this.toDate}`,
      title: `Revenue Report (${this.periodLabel})`
    });
  }

  exportVehicleProfitExcel(): void {
    const columns: ExportColumn<VehicleProfitDto>[] = [
      { header: 'Vehicle', accessor: r => r.vehicleName },
      { header: 'Revenue (PKR)', accessor: r => r.revenue },
      { header: 'Fuel Cost (PKR)', accessor: r => r.fuelCost },
      { header: 'Maintenance Cost (PKR)', accessor: r => r.maintenanceCost },
      { header: 'Net Profit (PKR)', accessor: r => r.profit }
    ];
    this.exportService.exportExcel(this.vehicleProfitRows, columns, {
      filename: `vehicle-profit-${this.fromDate}-to-${this.toDate}`,
      sheetName: 'Vehicle Profit',
      title: `Vehicle Profit (${this.periodLabel})`
    });
  }

  exportVehicleProfitPdf(): void {
    const columns: ExportColumn<VehicleProfitDto>[] = [
      { header: 'Vehicle', accessor: r => r.vehicleName },
      { header: 'Revenue (PKR)', accessor: r => r.revenue },
      { header: 'Fuel Cost (PKR)', accessor: r => r.fuelCost },
      { header: 'Maintenance (PKR)', accessor: r => r.maintenanceCost },
      { header: 'Net Profit (PKR)', accessor: r => r.profit }
    ];
    void this.exportService.exportPdf(this.vehicleProfitRows, columns, {
      filename: `vehicle-profit-${this.fromDate}-to-${this.toDate}`,
      title: `Vehicle Profit Report (${this.periodLabel})`
    });
  }

  exportDriverPerformanceExcel(): void {
    const columns: ExportColumn<DriverPerformanceDto>[] = [
      { header: 'Driver', accessor: r => r.driverName },
      { header: 'Total Trips', accessor: r => r.totalTrips },
      { header: 'Completed Trips', accessor: r => r.completedTrips },
      { header: 'Revenue (PKR)', accessor: r => r.totalRevenue }
    ];
    this.exportService.exportExcel(this.driverRows, columns, {
      filename: `driver-performance-${this.fromDate}-to-${this.toDate}`,
      sheetName: 'Driver Performance',
      title: `Driver Performance (${this.periodLabel})`
    });
  }

  exportDriverPerformancePdf(): void {
    const columns: ExportColumn<DriverPerformanceDto>[] = [
      { header: 'Driver', accessor: r => r.driverName },
      { header: 'Total Trips', accessor: r => r.totalTrips },
      { header: 'Completed', accessor: r => r.completedTrips },
      { header: 'Revenue (PKR)', accessor: r => r.totalRevenue }
    ];
    void this.exportService.exportPdf(this.driverRows, columns, {
      filename: `driver-performance-${this.fromDate}-to-${this.toDate}`,
      title: `Driver Performance Report (${this.periodLabel})`
    });
  }

  exportPaymentReportExcel(): void {
    if (!this.paymentReport) return;
    const columns: ExportColumn<PaymentReportItemDto>[] = [
      { header: 'Date', accessor: r => new Date(r.paymentDate).toLocaleDateString() },
      { header: 'Booking ID', accessor: r => r.bookingId },
      { header: 'Amount (PKR)', accessor: r => r.amount },
      { header: 'Method', accessor: r => r.paymentMethod },
      { header: 'Status', accessor: r => this.paymentStatusLabel(r.status) }
    ];
    this.exportService.exportExcel(this.paymentReport.recentPayments, columns, {
      filename: `payment-report-${this.fromDate}-to-${this.toDate}`,
      sheetName: 'Payments',
      title: `Payment Report (${this.periodLabel})`
    });
  }

  exportPaymentReportPdf(): void {
    if (!this.paymentReport) return;
    const columns: ExportColumn<PaymentReportItemDto>[] = [
      { header: 'Date', accessor: r => new Date(r.paymentDate).toLocaleDateString() },
      { header: 'Booking ID', accessor: r => r.bookingId },
      { header: 'Amount (PKR)', accessor: r => r.amount },
      { header: 'Method', accessor: r => r.paymentMethod },
      { header: 'Status', accessor: r => this.paymentStatusLabel(r.status) }
    ];
    void this.exportService.exportPdf(this.paymentReport.recentPayments, columns, {
      filename: `payment-report-${this.fromDate}-to-${this.toDate}`,
      title: `Payment Report (${this.periodLabel})`
    });
  }
}
