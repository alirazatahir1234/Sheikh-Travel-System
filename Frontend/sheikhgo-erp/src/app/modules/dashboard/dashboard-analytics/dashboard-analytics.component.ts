import {
  AfterViewInit,
  Component,
  ElementRef,
  Input,
  OnChanges,
  OnDestroy,
  SimpleChanges,
  ViewChild
} from '@angular/core';
import { Chart, registerables } from 'chart.js';
import { SharedModule } from '../../../shared/shared.module';

Chart.register(...registerables);

@Component({
  selector: 'app-dashboard-analytics',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './dashboard-analytics.component.html',
  styleUrls: ['./dashboard-analytics.component.scss']
})
export class DashboardAnalyticsComponent implements AfterViewInit, OnChanges, OnDestroy {
  @ViewChild('revenueCanvas') revenueCanvasRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('bookingsCanvas') bookingsCanvasRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('fleetCanvas') fleetCanvasRef!: ElementRef<HTMLCanvasElement>;

  @Input() revenueLabels: string[] = [];
  @Input() revenueValues: number[] = [];
  @Input() bookingLabels: string[] = [];
  @Input() bookingValues: number[] = [];
  @Input() fleetLabels: string[] = ['In use', 'Available', 'Pending trips'];
  @Input() fleetValues: number[] = [0, 0, 0];
  @Input() revenueTotal = 0;
  @Input() loading = false;

  readonly skeletonBars = [0, 1, 2, 3, 4, 5, 6];
  readonly skeletonLegend = [0, 1, 2, 3];

  private revenueChart?: Chart;
  private bookingsChart?: Chart;
  private fleetChart?: Chart;
  private viewReady = false;
  private resizeObserver?: ResizeObserver;

  constructor(private host: ElementRef<HTMLElement>) {}

  ngAfterViewInit(): void {
    this.viewReady = true;
    this.renderCharts();
    this.resizeObserver = new ResizeObserver(() => this.resizeCharts());
    this.resizeObserver.observe(this.host.nativeElement);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (
      this.viewReady &&
      (changes['revenueLabels'] ||
        changes['bookingLabels'] ||
        changes['fleetValues'] ||
        changes['loading'])
    ) {
      this.renderCharts();
    }
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    this.revenueChart?.destroy();
    this.bookingsChart?.destroy();
    this.fleetChart?.destroy();
  }

  private resizeCharts(): void {
    if (this.loading) return;
    this.revenueChart?.resize();
    this.bookingsChart?.resize();
    this.fleetChart?.resize();
  }

  private renderCharts(): void {
    if (this.loading) return;
    requestAnimationFrame(() => {
      this.renderRevenue();
      this.renderBookings();
      this.renderFleet();
      this.resizeCharts();
    });
  }

  private primaryColor(): string {
    return getComputedStyle(document.documentElement).getPropertyValue('--stb-primary').trim() || '#0F766E';
  }

  private gradientFill(ctx: CanvasRenderingContext2D, height: number): CanvasGradient {
    const primary = this.primaryColor();
    const g = ctx.createLinearGradient(0, 0, 0, height);
    g.addColorStop(0, `${primary}55`);
    g.addColorStop(1, `${primary}08`);
    return g;
  }

  private renderRevenue(): void {
    const el = this.revenueCanvasRef?.nativeElement;
    if (!el) return;
    this.revenueChart?.destroy();
    const primary = this.primaryColor();
    const labels = this.revenueLabels.length ? this.revenueLabels : ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
    let values = this.revenueValues.length ? [...this.revenueValues] : [];
    if (values.every(v => v === 0) && this.revenueTotal > 0) {
      const slice = Math.round(this.revenueTotal / 7);
      values = labels.map((_, i) => Math.round(slice * (0.7 + (i % 3) * 0.15)));
    }
    if (!values.length) values = labels.map(() => 0);

    this.revenueChart = new Chart(el, {
      type: 'line',
      data: {
        labels,
        datasets: [{
          label: 'Revenue (PKR)',
          data: values,
          borderColor: primary,
          backgroundColor: (context) => {
            const chart = context.chart;
            const { ctx, chartArea } = chart;
            if (!chartArea) return `${primary}22`;
            return this.gradientFill(ctx, chartArea.bottom);
          },
          fill: true,
          tension: 0.4,
          pointRadius: 4,
          pointHoverRadius: 6,
          pointBackgroundColor: '#fff',
          pointBorderColor: primary,
          pointBorderWidth: 2,
          borderWidth: 2.5
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: { duration: 800, easing: 'easeOutQuad' },
        interaction: { intersect: false, mode: 'index' },
        plugins: {
          legend: { display: false },
          tooltip: {
            backgroundColor: '#0F172A',
            titleFont: { size: 12, weight: 'bold' },
            bodyFont: { size: 12 },
            padding: 12,
            callbacks: {
              label: (ctx) => ` PKR ${Number(ctx.parsed.y).toLocaleString()}`
            }
          }
        },
        scales: {
          x: { grid: { display: false }, ticks: { font: { size: 11 }, color: '#64748B' } },
          y: {
            beginAtZero: true,
            grid: { color: 'rgba(148, 163, 184, 0.25)' },
            ticks: {
              font: { size: 11 },
              color: '#64748B',
              callback: (v) => (Number(v) >= 1000 ? `${Number(v) / 1000}k` : String(v))
            }
          }
        }
      }
    });
  }

  private renderBookings(): void {
    const el = this.bookingsCanvasRef?.nativeElement;
    if (!el) return;
    this.bookingsChart?.destroy();
    const labels = this.bookingLabels.length ? this.bookingLabels : ['Pending', 'Active', 'Completed', 'Cancelled'];
    const values = this.bookingValues.length ? this.bookingValues : [1, 1, 1, 0];
    const statusColors: Record<string, string> = {
      Pending: '#F59E0B',
      Active: '#3B82F6',
      Completed: '#22C55E',
      Cancelled: '#EF4444'
    };
    const colors = labels.map(l => statusColors[l] ?? '#94A3B8');

    this.bookingsChart = new Chart(el, {
      type: 'doughnut',
      data: {
        labels,
        datasets: [{
          data: values,
          backgroundColor: colors,
          borderWidth: 2,
          borderColor: '#fff',
          hoverOffset: 6
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        cutout: '62%',
        animation: { animateRotate: true, animateScale: true, duration: 700 },
        plugins: {
          legend: { position: 'bottom', labels: { boxWidth: 12, padding: 14, font: { size: 11, weight: 600 } } },
          tooltip: {
            backgroundColor: '#0F172A',
            padding: 10
          }
        }
      }
    });
  }

  private renderFleet(): void {
    const el = this.fleetCanvasRef?.nativeElement;
    if (!el) return;
    this.fleetChart?.destroy();
    const primary = this.primaryColor();
    const info = getComputedStyle(document.documentElement).getPropertyValue('--stb-info').trim() || '#3B82F6';
    const warn = getComputedStyle(document.documentElement).getPropertyValue('--stb-warning').trim() || '#F59E0B';

    this.fleetChart = new Chart(el, {
      type: 'bar',
      data: {
        labels: this.fleetLabels,
        datasets: [{
          label: 'Count',
          data: this.fleetValues,
          backgroundColor: [primary, `${info}CC`, `${warn}CC`],
          borderRadius: 8,
          borderSkipped: false
        }]
      },
      options: {
        indexAxis: 'y',
        responsive: true,
        maintainAspectRatio: false,
        plugins: { legend: { display: false } },
        scales: {
          x: { beginAtZero: true, grid: { color: 'rgba(148,163,184,0.15)' } },
          y: { grid: { display: false } }
        }
      }
    });
  }
}
