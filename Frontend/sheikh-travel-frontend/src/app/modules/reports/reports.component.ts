import { Component, OnInit, AfterViewInit, ElementRef, ViewChild } from '@angular/core';
import { Chart, registerables } from 'chart.js';
import { DashboardService } from '../../core/services/dashboard.service';

Chart.register(...registerables);

@Component({
  selector: 'app-reports',
  templateUrl: './reports.component.html',
  styleUrls: ['./reports.component.scss']
})
export class ReportsComponent implements OnInit, AfterViewInit {
  @ViewChild('revenueChart') revenueCanvasRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('statusChart') statusCanvasRef!: ElementRef<HTMLCanvasElement>;

  fromDate = new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().split('T')[0];
  toDate = new Date().toISOString().split('T')[0];
  loading = false;

  private revenueChart?: Chart;
  private statusChart?: Chart;

  constructor(private dashboardService: DashboardService) {}

  ngOnInit(): void {}

  ngAfterViewInit(): void {
    this.loadReports();
  }

  loadReports(): void {
    this.loading = true;
    this.dashboardService.getRevenueReport(this.fromDate, this.toDate).subscribe({
      next: data => {
        this.renderRevenueChart(data.map(d => d.period), data.map(d => d.totalRevenue));
        this.loading = false;
      },
      error: () => {
        // demo data
        this.renderRevenueChart(
          ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun'],
          [45000, 62000, 38000, 75000, 55000, 83000]
        );
        this.loading = false;
      }
    });

    this.dashboardService.getBookingStatusReport().subscribe({
      next: data => this.renderStatusChart(data.map(d => d.status), data.map(d => d.count)),
      error: () => this.renderStatusChart(
        ['Pending', 'Confirmed', 'InProgress', 'Completed', 'Cancelled'],
        [12, 25, 8, 95, 7]
      )
    });
  }

  private renderRevenueChart(labels: string[], data: number[]): void {
    if (this.revenueChart) this.revenueChart.destroy();
    this.revenueChart = new Chart(this.revenueCanvasRef.nativeElement, {
      type: 'bar',
      data: {
        labels,
        datasets: [{
          label: 'Revenue (PKR)',
          data,
          backgroundColor: 'rgba(26, 35, 126, 0.7)',
          borderColor: '#1a237e',
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
    if (this.statusChart) this.statusChart.destroy();
    this.statusChart = new Chart(this.statusCanvasRef.nativeElement, {
      type: 'doughnut',
      data: {
        labels,
        datasets: [{
          data,
          backgroundColor: ['#f57f17', '#1565c0', '#00695c', '#2e7d32', '#c62828'],
          borderWidth: 2
        }]
      },
      options: { responsive: true }
    });
  }
}
