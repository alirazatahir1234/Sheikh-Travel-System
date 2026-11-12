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
import type {
  ChartConfiguration,
  ChartData,
  ChartOptions,
  ChartType
} from 'chart.js';

export type UiChartType = Extract<ChartType, 'line' | 'bar' | 'doughnut' | 'pie'>;

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
  private chart?: { destroy: () => void };
  private viewReady = false;
  private chartJsPromise: Promise<typeof import('chart.js')> | null = null;

  constructor() {
    effect(() => {
      // Track reactive inputs so the chart re-renders on change.
      this.type();
      this.data();
      this.options();
      if (this.viewReady) {
        void this.render();
      }
    });
  }

  ngAfterViewInit(): void {
    this.viewReady = true;
    void this.render();
  }

  private loadChartJs(): Promise<typeof import('chart.js')> {
    if (!this.chartJsPromise) {
      this.chartJsPromise = import('chart.js').then(mod => {
        mod.Chart.register(...mod.registerables);
        return mod;
      });
    }
    return this.chartJsPromise;
  }

  private async render(): Promise<void> {
    const canvas = this.canvasRef()?.nativeElement;
    if (!canvas) {
      return;
    }
    const { Chart } = await this.loadChartJs();
    this.chart?.destroy();
    this.chart = new Chart(canvas, {
      type: this.type(),
      data: this.data(),
      options: this.options() ?? this.defaultOptions()
    } as ChartConfiguration);
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
