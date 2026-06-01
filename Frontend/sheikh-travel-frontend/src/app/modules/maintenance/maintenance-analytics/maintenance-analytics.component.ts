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

Chart.register(...registerables);

@Component({
  selector: 'app-maintenance-analytics',
  templateUrl: './maintenance-analytics.component.html',
  styleUrls: ['./maintenance-analytics.component.scss']
})
export class MaintenanceAnalyticsComponent implements AfterViewInit, OnChanges, OnDestroy {
  @ViewChild('costCanvas') costCanvasRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('statusCanvas') statusCanvasRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('vehicleCanvas') vehicleCanvasRef!: ElementRef<HTMLCanvasElement>;

  @Input() costLabels: string[] = [];
  @Input() costValues: number[] = [];
  @Input() statusLabels: string[] = [];
  @Input() statusValues: number[] = [];
  @Input() vehicleLabels: string[] = [];
  @Input() vehicleValues: number[] = [];
  @Input() costTotal = 0;
  @Input() completionRate = 0;
  @Input() loading = false;

  private costChart?: Chart;
  private statusChart?: Chart;
  private vehicleChart?: Chart;
  private viewReady = false;

  ngAfterViewInit(): void {
    this.viewReady = true;
    this.renderCharts();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (this.viewReady && !changes['loading']?.firstChange) {
      this.renderCharts();
    }
  }

  ngOnDestroy(): void {
    this.costChart?.destroy();
    this.statusChart?.destroy();
    this.vehicleChart?.destroy();
  }

  private renderCharts(): void {
    if (this.loading) return;
    requestAnimationFrame(() => {
      this.renderCost();
      this.renderStatus();
      this.renderVehicles();
    });
  }

  private primary(): string {
    return getComputedStyle(document.documentElement).getPropertyValue('--stb-primary').trim() || '#0F766E';
  }

  private renderCost(): void {
    const el = this.costCanvasRef?.nativeElement;
    if (!el) return;
    this.costChart?.destroy();
    const ctx = el.getContext('2d');
    if (!ctx) return;
    const h = el.height || 160;
    const g = ctx.createLinearGradient(0, 0, 0, h);
    const p = this.primary();
    g.addColorStop(0, `${p}55`);
    g.addColorStop(1, `${p}06`);
    this.costChart = new Chart(ctx, {
      type: 'line',
      data: {
        labels: this.costLabels,
        datasets: [{
          label: 'Cost (PKR)',
          data: this.costValues,
          borderColor: p,
          backgroundColor: g,
          fill: true,
          tension: 0.35,
          pointRadius: 4,
          pointHoverRadius: 6
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: { duration: 700, easing: 'easeOutQuad' },
        plugins: { legend: { display: false } },
        scales: {
          x: { grid: { display: false }, ticks: { color: '#94a3b8', font: { size: 10 } } },
          y: { grid: { color: 'rgba(148,163,184,0.15)' }, ticks: { color: '#94a3b8', font: { size: 10 } } }
        }
      }
    });
  }

  private renderStatus(): void {
    const el = this.statusCanvasRef?.nativeElement;
    if (!el) return;
    this.statusChart?.destroy();
    const ctx = el.getContext('2d');
    if (!ctx) return;
    const colors = ['#3B82F6', '#F59E0B', '#10B981', '#EF4444'];
    this.statusChart = new Chart(ctx, {
      type: 'doughnut',
      data: {
        labels: this.statusLabels,
        datasets: [{
          data: this.statusValues,
          backgroundColor: colors.slice(0, this.statusValues.length),
          borderWidth: 0,
          hoverOffset: 6
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: { duration: 600 },
        cutout: '68%',
        plugins: { legend: { position: 'bottom', labels: { boxWidth: 10, font: { size: 10 } } } }
      }
    });
  }

  private renderVehicles(): void {
    const el = this.vehicleCanvasRef?.nativeElement;
    if (!el) return;
    this.vehicleChart?.destroy();
    const ctx = el.getContext('2d');
    if (!ctx) return;
    this.vehicleChart = new Chart(ctx, {
      type: 'bar',
      data: {
        labels: this.vehicleLabels,
        datasets: [{
          label: 'Services',
          data: this.vehicleValues,
          backgroundColor: 'rgba(15, 118, 110, 0.75)',
          borderRadius: 6,
          maxBarThickness: 36
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: { duration: 600 },
        plugins: { legend: { display: false } },
        scales: {
          x: { grid: { display: false }, ticks: { color: '#94a3b8', font: { size: 9 }, maxRotation: 45 } },
          y: { grid: { color: 'rgba(148,163,184,0.12)' }, ticks: { stepSize: 1, color: '#94a3b8' } }
        }
      }
    });
  }
}
