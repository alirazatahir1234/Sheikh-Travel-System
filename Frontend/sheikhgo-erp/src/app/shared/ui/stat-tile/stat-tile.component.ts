import {
  AfterViewInit,
  Component,
  ElementRef,
  Input,
  OnChanges,
  QueryList,
  SimpleChanges,
  ViewChildren
} from '@angular/core';
import { AccentColor, StatVariant } from '../ui.types';

@Component({
  selector: 'stb-stat-tile',
  templateUrl: './stat-tile.component.html',
  styleUrls: ['./stat-tile.component.scss']
})
export class StatTileComponent implements AfterViewInit, OnChanges {
  @Input() label = '';
  @Input() value: string | number = 0;
  @Input() hint?: string;
  @Input() icon = 'insights';
  @Input() color: AccentColor = 'teal';
  @Input() prefix = '';
  @Input() suffix = '';
  @Input() loading = false;
  @Input() trend?: string;
  @Input() trendUp?: boolean;
  @Input() trendDetail?: string;
  @Input() sparkline?: number[];
  @Input() variant: StatVariant = 'default';

  @ViewChildren('spark') sparkCanvases!: QueryList<ElementRef<HTMLCanvasElement>>;

  ngAfterViewInit(): void {
    this.drawSparklines();
  }

  ngOnChanges(_changes: SimpleChanges): void {
    queueMicrotask(() => this.drawSparklines());
  }

  private drawSparklines(): void {
    const canvas = this.sparkCanvases?.first?.nativeElement;
    const data = this.sparkline;
    if (!canvas || !data?.length) return;

    const dpr = window.devicePixelRatio || 1;
    const w = 72;
    const h = 28;
    canvas.width = w * dpr;
    canvas.height = h * dpr;
    canvas.style.width = `${w}px`;
    canvas.style.height = `${h}px`;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    ctx.scale(dpr, dpr);

    const max = Math.max(...data, 1);
    const min = Math.min(...data, 0);
    const range = max - min || 1;
    const step = w / Math.max(data.length - 1, 1);
    const primary = getComputedStyle(document.documentElement).getPropertyValue('--stb-primary').trim() || '#0F766E';

    ctx.beginPath();
    data.forEach((v, i) => {
      const x = i * step;
      const y = h - ((v - min) / range) * (h - 4) - 2;
      if (i === 0) ctx.moveTo(x, y);
      else ctx.lineTo(x, y);
    });
    ctx.strokeStyle = primary;
    ctx.lineWidth = 2;
    ctx.lineJoin = 'round';
    ctx.stroke();

    ctx.lineTo(w, h);
    ctx.lineTo(0, h);
    ctx.closePath();
    const grad = ctx.createLinearGradient(0, 0, 0, h);
    grad.addColorStop(0, `${primary}40`);
    grad.addColorStop(1, `${primary}05`);
    ctx.fillStyle = grad;
    ctx.fill();
  }
}
