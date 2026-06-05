import { Component, OnInit, OnDestroy, AfterViewInit, ElementRef, ViewChild, NgZone } from '@angular/core';
import { MatTabChangeEvent } from '@angular/material/tabs';
import { Chart, registerables } from 'chart.js';
import { DashboardService, RevenueReportDto } from '../../core/services/dashboard.service';
import { ReportService } from '../../core/services/report.service';
import { ExportService, ExportColumn } from '../../core/services/export.service';
import {
  VehicleProfitDto,
  DriverPerformanceDto,
  PaymentReportDto,
  PaymentReportItemDto
} from '../../core/models/common.model';

Chart.register(...registerables);

@Component({
  selector: 'app-reports',
  templateUrl: './reports.component.html',
  styleUrls: ['./reports.component.scss']
})
export class ReportsComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('revenueChart') revenueCanvasRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('statusChart') statusCanvasRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('vehicleProfitChart') vehicleProfitCanvasRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('driverChart') driverCanvasRef!: ElementRef<HTMLCanvasElement>;

  fromDate = new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().split('T')[0];
  toDate = new Date().toISOString().split('T')[0];
  
  selectedTabIndex = 0;
  private tabDataLoaded = new Set<number>();

  // Revenue & Bookings
  loadingRevenue = false;

  // Vehicle Profit
  vehicleProfitRows: VehicleProfitDto[] = [];
  vehicleProfitCols = ['vehicleName', 'revenue', 'fuelCost', 'maintenanceCost', 'profit'];
  loadingVehicleProfit = false;

  // Driver Performance
  driverRows: DriverPerformanceDto[] = [];
  driverCols = ['driverName', 'totalTrips', 'completedTrips', 'totalRevenue'];
  loadingDrivers = false;

  // Payment Report
  paymentReport: PaymentReportDto | null = null;
  paymentCols = ['paymentDate', 'bookingId', 'amount', 'paymentMethod', 'status'];
  loadingPayments = false;

  private revenueChart?: Chart;
  private statusChart?: Chart;
  private vehicleProfitChart?: Chart;
  private driverChart?: Chart;

  constructor(
    private dashboardService: DashboardService,
    private reportService: ReportService,
    private exportService: ExportService,
    private ngZone: NgZone
  ) {}

  ngOnInit(): void {}

  ngAfterViewInit(): void {
    this.loadTabData(0);
  }
  
  onTabChange(event: MatTabChangeEvent): void {
    this.selectedTabIndex = event.index;
    this.loadTabData(event.index);
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

  // ── Reload all on date change ──────────────────────────────────────────────
  loadReports(): void {
    this.tabDataLoaded.clear();
    this.loadTabData(this.selectedTabIndex);
  }

  // ── Tab 1: Revenue & Bookings ──────────────────────────────────────────────
  loadRevenue(): void {
    this.loadingRevenue = true;
    this.dashboardService.getRevenueReport(this.fromDate, this.toDate).subscribe({
      next: (data: RevenueReportDto) => {
        this.renderRevenueChart(
          ['Total Revenue', 'Fuel Expense', 'Maintenance', 'Net Profit'],
          [data.totalRevenue, data.fuelExpense, data.maintenanceCost, data.netProfit]
        );
        this.loadingRevenue = false;
      },
      error: () => {
        this.renderRevenueChart(
          ['Total Revenue', 'Fuel Expense', 'Maintenance', 'Net Profit'],
          [250000, 45000, 15000, 190000]
        );
        this.loadingRevenue = false;
      }
    });

    this.dashboardService.getBookingStatusReport(this.fromDate, this.toDate).subscribe({
      next: data => this.renderStatusChart(data.map(d => d.status), data.map(d => d.count)),
      error: () => this.renderStatusChart(
        ['Pending', 'Active', 'Completed', 'Cancelled'],
        [12, 8, 95, 7]
      )
    });
  }

  // ── Tab 2: Vehicle Profit ──────────────────────────────────────────────────
  loadVehicleProfit(): void {
    this.loadingVehicleProfit = true;
    this.reportService.getVehicleProfit(this.fromDate, this.toDate).subscribe({
      next: rows => {
        this.vehicleProfitRows = rows;
        this.renderVehicleProfitChart(rows);
        this.loadingVehicleProfit = false;
      },
      error: () => { this.vehicleProfitRows = []; this.loadingVehicleProfit = false; }
    });
  }

  // ── Tab 3: Driver Performance ──────────────────────────────────────────────
  loadDriverPerformance(): void {
    this.loadingDrivers = true;
    this.reportService.getDriverPerformance(this.fromDate, this.toDate).subscribe({
      next: rows => {
        this.driverRows = rows;
        this.renderDriverChart(rows);
        this.loadingDrivers = false;
      },
      error: () => { this.driverRows = []; this.loadingDrivers = false; }
    });
  }

  // ── Tab 4: Payment Report ──────────────────────────────────────────────────
  loadPaymentReport(): void {
    this.loadingPayments = true;
    this.reportService.getPaymentReport(this.fromDate, this.toDate).subscribe({
      next: report => { this.paymentReport = report; this.loadingPayments = false; },
      error: () => { this.paymentReport = null; this.loadingPayments = false; }
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

  // ── Private chart renderers ────────────────────────────────────────────────
  private renderRevenueChart(labels: string[], data: number[]): void {
    requestAnimationFrame(() => {
      this.doRenderRevenueChart(labels, data);
    });
  }
  
  private doRenderRevenueChart(labels: string[], data: number[]): void {
    if (this.revenueChart) this.revenueChart.destroy();
    if (!this.revenueCanvasRef?.nativeElement) return;
    this.revenueChart = new Chart(this.revenueCanvasRef.nativeElement, {
      type: 'bar',
      data: {
        labels,
        datasets: [{
          label: 'Amount (PKR)',
          data,
          backgroundColor: [
            'rgba(26, 35, 126, 0.7)', 'rgba(198, 40, 40, 0.7)',
            'rgba(245, 127, 23, 0.7)', 'rgba(46, 125, 50, 0.7)'
          ],
          borderColor: ['#1a237e', '#c62828', '#f57f17', '#2e7d32'],
          borderWidth: 1
        }]
      },
      options: {
        responsive: true,
        plugins: { legend: { display: false } },
        scales: { y: { beginAtZero: true } }
      }
    });
  }

  private renderStatusChart(labels: string[], data: number[]): void {
    requestAnimationFrame(() => {
      this.doRenderStatusChart(labels, data);
    });
  }
  
  private doRenderStatusChart(labels: string[], data: number[]): void {
    if (this.statusChart) this.statusChart.destroy();
    if (!this.statusCanvasRef?.nativeElement) return;
    this.statusChart = new Chart(this.statusCanvasRef.nativeElement, {
      type: 'doughnut',
      data: {
        labels,
        datasets: [{ data, backgroundColor: ['#f57f17', '#1565c0', '#2e7d32', '#c62828'], borderWidth: 2 }]
      },
      options: { responsive: true }
    });
  }

  private renderVehicleProfitChart(rows: VehicleProfitDto[]): void {
    requestAnimationFrame(() => {
      this.doRenderVehicleProfitChart(rows);
    });
  }
  
  private doRenderVehicleProfitChart(rows: VehicleProfitDto[]): void {
    if (this.vehicleProfitChart) this.vehicleProfitChart.destroy();
    if (!this.vehicleProfitCanvasRef?.nativeElement || rows.length === 0) return;
    this.vehicleProfitChart = new Chart(this.vehicleProfitCanvasRef.nativeElement, {
      type: 'bar',
      data: {
        labels: rows.map(r => r.vehicleName),
        datasets: [
          { label: 'Revenue', data: rows.map(r => r.revenue), backgroundColor: 'rgba(26, 35, 126, 0.7)', borderColor: '#1a237e', borderWidth: 1 },
          { label: 'Fuel Cost', data: rows.map(r => r.fuelCost), backgroundColor: 'rgba(198, 40, 40, 0.7)', borderColor: '#c62828', borderWidth: 1 },
          { label: 'Maintenance', data: rows.map(r => r.maintenanceCost), backgroundColor: 'rgba(245, 127, 23, 0.7)', borderColor: '#f57f17', borderWidth: 1 },
          { label: 'Profit', data: rows.map(r => r.profit), backgroundColor: 'rgba(46, 125, 50, 0.7)', borderColor: '#2e7d32', borderWidth: 1 }
        ]
      },
      options: {
        responsive: true,
        scales: { y: { beginAtZero: true } }
      }
    });
  }

  private renderDriverChart(rows: DriverPerformanceDto[]): void {
    requestAnimationFrame(() => {
      this.doRenderDriverChart(rows);
    });
  }
  
  private doRenderDriverChart(rows: DriverPerformanceDto[]): void {
    if (this.driverChart) this.driverChart.destroy();
    if (!this.driverCanvasRef?.nativeElement || rows.length === 0) return;
    this.driverChart = new Chart(this.driverCanvasRef.nativeElement, {
      type: 'bar',
      data: {
        labels: rows.map(r => r.driverName),
        datasets: [
          { label: 'Total Trips', data: rows.map(r => r.totalTrips), backgroundColor: 'rgba(26, 35, 126, 0.7)', borderColor: '#1a237e', borderWidth: 1 },
          { label: 'Completed', data: rows.map(r => r.completedTrips), backgroundColor: 'rgba(46, 125, 50, 0.7)', borderColor: '#2e7d32', borderWidth: 1 }
        ]
      },
      options: {
        responsive: true,
        scales: { y: { beginAtZero: true } }
      }
    });
  }

  // ── Export methods ───────────────────────────────────────────────────────────
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
      sheetName: 'Vehicle Profit'
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
    this.exportService.exportPdf(this.vehicleProfitRows, columns, {
      filename: `vehicle-profit-${this.fromDate}-to-${this.toDate}`,
      title: `Vehicle Profit Report (${this.fromDate} to ${this.toDate})`
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
      sheetName: 'Driver Performance'
    });
  }

  exportDriverPerformancePdf(): void {
    const columns: ExportColumn<DriverPerformanceDto>[] = [
      { header: 'Driver', accessor: r => r.driverName },
      { header: 'Total Trips', accessor: r => r.totalTrips },
      { header: 'Completed', accessor: r => r.completedTrips },
      { header: 'Revenue (PKR)', accessor: r => r.totalRevenue }
    ];
    this.exportService.exportPdf(this.driverRows, columns, {
      filename: `driver-performance-${this.fromDate}-to-${this.toDate}`,
      title: `Driver Performance Report (${this.fromDate} to ${this.toDate})`
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
      sheetName: 'Payments'
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
    this.exportService.exportPdf(this.paymentReport.recentPayments, columns, {
      filename: `payment-report-${this.fromDate}-to-${this.toDate}`,
      title: `Payment Report (${this.fromDate} to ${this.toDate})`
    });
  }
}

