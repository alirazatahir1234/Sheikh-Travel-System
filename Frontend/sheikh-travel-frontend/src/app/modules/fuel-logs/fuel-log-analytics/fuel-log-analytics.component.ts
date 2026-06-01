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
  selector: 'app-fuel-log-analytics',
  templateUrl: './fuel-log-analytics.component.html',
  styleUrls: ['./fuel-log-analytics.component.scss']
})
export class FuelLogAnalyticsComponent implements AfterViewInit, OnChanges, OnDestroy {
  @ViewChild('expenseCanvas') expenseCanvasRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('typeCanvas') typeCanvasRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('vehicleCanvas') vehicleCanvasRef!: ElementRef<HTMLCanvasElement>;

  @Input() expenseLabels: string[] = [];
  @Input() expenseValues: number[] = [];
  @Input() typeLabels: string[] = [];
  @Input() typeValues: number[] = [];
  @Input() vehicleLabels: string[] = [];
  @Input() vehicleLiters: number[] = [];
  @Input() expenseTotal = 0;
  @Input() fleetEfficiency = 0;
  @Input() loading = false;

  private expenseChart?: Chart;
  private typeChart?: Chart;
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
    this.expenseChart?.destroy();
    this.typeChart?.destroy();
    this.vehicleChart?.destroy();
  }

  private renderCharts(): void {
    if (this.loading) return;
    requestAnimationFrame(() => {
      this.renderExpense();
      this.renderTypes();
      this.renderVehicles();
    });
  }

  private primary(): string {
    return getComputedStyle(document.documentElement).getPropertyValue('--stb-primary').trim() || '#0F766E';
  }

  private renderExpense(): void {
    const el = this.expenseCanvasRef?.nativeElement;
    if (!el) return;
    this.expenseChart?.destroy();
    const ctx = el.getContext('2d');
    if (!ctx) return;
    const h = el.height || 160;
    const g = ctx.createLinearGradient(0, 0, 0, h);
    const p = this.primary();
    g.addColorStop(0, `${p}50`);
    g.addColorStop(1, `${p}06`);
    this.expenseChart = new Chart(ctx, {
      type: 'line',
      data: {
        labels: this.expenseLabels,
        datasets: [{
          label: 'PKR',
          data: this.expenseValues,
          borderColor: '#F59E0B',
          backgroundColor: g,
          fill: true,
          tension: 0.35,
          pointRadius: 4
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: { duration: 700, easing: 'easeOutQuad' },
        plugins: { legend: { display: false } },
        scales: {
          x: { grid: { display: false }, ticks: { color: '#94a3b8', font: { size: 10 } } },
          y: { grid: { color: 'rgba(148,163,184,0.12)' }, ticks: { color: '#94a3b8', font: { size: 10 } } }
        }
      }
    });
  }

  private renderTypes(): void {
    const el = this.typeCanvasRef?.nativeElement;
    if (!el) return;
    this.typeChart?.destroy();
    const ctx = el.getContext('2d');
    if (!ctx) return;
    const colors = ['#3B82F6', '#F97316', '#10B981', '#8B5CF6', '#14B8A6'];
    this.typeChart = new Chart(ctx, {
      type: 'doughnut',
      data: {
        labels: this.typeLabels,
        datasets: [{
          data: this.typeValues,
          backgroundColor: colors.slice(0, this.typeValues.length),
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
          label: 'Liters',
          data: this.vehicleLiters,
          backgroundColor: 'rgba(15, 118, 110, 0.8)',
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
          y: { grid: { color: 'rgba(148,163,184,0.1)' }, ticks: { color: '#94a3b8' } }
        }
      }
    });
  }
}
