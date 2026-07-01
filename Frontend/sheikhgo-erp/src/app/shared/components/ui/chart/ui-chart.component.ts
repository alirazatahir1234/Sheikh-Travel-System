import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  effect,
  ElementRef,
  input,
  OnDestroy,
  viewChild
} from '@angular/core';
import {
  Chart,
  ChartConfiguration,
  ChartData,
  ChartOptions,
  ChartType,
  registerables
} from 'chart.js';

Chart.register(...registerables);

export type UiChartType = Extract<ChartType, 'line' | 'bar' | 'doughnut'>;

/** Chart options accepted by ui-chart (avoids doughnut/line/bar generic conflicts in templates). */
export type UiChartOptions = ChartOptions<UiChartType>;

@Component({
  selector: 'ui-chart',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="relative w-full" [style.height]="height()">
      <canvas #canvas></canvas>
    </div>
  `,
  styles: [`:host { display: block; }`]
})
export class UiChartComponent implements AfterViewInit, OnDestroy {
  readonly type = input<UiChartType>('line');
  readonly data = input<ChartData>({ labels: [], datasets: [] });
  readonly options = input<UiChartOptions>();
  readonly height = input('280px');

  private readonly canvasRef = viewChild<ElementRef<HTMLCanvasElement>>('canvas');
  private chart?: Chart;
  private viewReady = false;

  constructor() {
    effect(() => {
      // Track reactive inputs so the chart re-renders on change.
      this.type();
      this.data();
      this.options();
      if (this.viewReady) {
        this.render();
      }
    });
  }

  ngAfterViewInit(): void {
    this.viewReady = true;
    this.render();
  }

  private render(): void {
    const canvas = this.canvasRef()?.nativeElement;
    if (!canvas) {
      return;
    }
    this.chart?.destroy();
    const config = {
      type: this.type(),
      data: this.data(),
      options: this.options() ?? this.defaultOptions()
    } as ChartConfiguration<UiChartType>;
    this.chart = new Chart(canvas, config);
  }

  private defaultOptions(): UiChartOptions {
    return {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { display: false } },
      scales: {
        x: { grid: { display: false } },
        y: { beginAtZero: true, grid: { color: 'rgba(15, 23, 42, 0.06)' } }
      }
    };
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
  }
}
