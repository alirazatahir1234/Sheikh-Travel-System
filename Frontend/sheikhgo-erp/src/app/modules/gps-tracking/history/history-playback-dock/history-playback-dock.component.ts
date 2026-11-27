import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  Output,
  ViewChild
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ActivityBlock,
  AxisTick,
  DockZoom,
  SPEED_CHART_HEIGHT,
  SPEED_CHART_WIDTH
} from '../../utils/history-playback.util';

/**
 * Playback dock under the History map: transport, clock, the speed chart and the activity bar
 * with the playhead. Presentational — the page owns time, selection and the timer.
 */
@Component({
  selector: 'app-history-playback-dock',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './history-playback-dock.component.html',
  styleUrls: ['./history-playback-dock.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HistoryPlaybackDockComponent {
  @ViewChild('track') track?: ElementRef<HTMLDivElement>;

  @Input() playing = false;
  /** False while there is no trip to play. */
  @Input() canPlay = false;
  @Input() hasStops = false;
  @Input() rate = 1;
  @Input() follow = true;
  @Input() zoom: DockZoom = 'day';
  @Input() weekAvailable = true;
  @Input() dateLabel = '';
  @Input() clockLabel = '—';
  @Input() speedKmh: number | null = null;
  @Input() blocks: ActivityBlock[] = [];
  /** Parts of the dock outside the loaded period. */
  @Input() outside: { leftPercent: number; widthPercent: number }[] = [];
  @Input() speedLine = '';
  @Input() speedArea = '';
  @Input() maxSpeedLabel = '';
  @Input() chartLabel = 'Speed over time';
  @Input() ticks: AxisTick[] = [];
  /** Null when the playhead is outside what the dock shows. */
  @Input() playheadPercent: number | null = null;
  @Input() selectedId: string | null = null;

  @Output() playToggle = new EventEmitter<void>();
  @Output() previousStop = new EventEmitter<void>();
  @Output() nextStop = new EventEmitter<void>();
  @Output() zoomChange = new EventEmitter<DockZoom>();
  @Output() rateCycle = new EventEmitter<void>();
  @Output() followToggle = new EventEmitter<void>();
  /** Position in the dock, 0–100. */
  @Output() seek = new EventEmitter<number>();
  @Output() blockSelect = new EventEmitter<string>();

  readonly viewBox = `0 0 ${SPEED_CHART_WIDTH} ${SPEED_CHART_HEIGHT}`;
  readonly gridPath = `M0 4H${SPEED_CHART_WIDTH}M0 54H${SPEED_CHART_WIDTH}`;
  readonly zooms: { id: DockZoom; label: string }[] = [
    { id: 'trip', label: 'Trip' },
    { id: 'day', label: 'Day' },
    { id: 'week', label: 'Week' }
  ];

  private dragging = false;
  private frame: number | null = null;
  private pending: number | null = null;

  get speedLabel(): string {
    return this.speedKmh == null ? '—' : `${Math.round(this.speedKmh)} km/h`;
  }

  get rateLabel(): string {
    return `${this.rate}×`;
  }

  trackBlock(_: number, block: ActivityBlock): string {
    return block.id;
  }

  setZoom(zoom: DockZoom): void {
    if (zoom === 'week' && !this.weekAvailable) return;
    if (zoom !== this.zoom) this.zoomChange.emit(zoom);
  }

  // ---- seeking: press or drag anywhere on the chart or the bar ----------------------------------

  onPointerDown(event: PointerEvent): void {
    if (event.button !== 0 && event.pointerType === 'mouse') return;
    this.dragging = true;
    this.track?.nativeElement.setPointerCapture?.(event.pointerId);
    this.queueSeek(event.clientX);
  }

  onPointerMove(event: PointerEvent): void {
    if (this.dragging) this.queueSeek(event.clientX);
  }

  onPointerUp(event: PointerEvent): void {
    if (!this.dragging) return;
    this.dragging = false;
    this.track?.nativeElement.releasePointerCapture?.(event.pointerId);
  }

  /** A block activated from the keyboard selects its segment; a pointer press has already seeked. */
  onBlockClick(block: ActivityBlock, event: MouseEvent): void {
    if (event.detail === 0) this.blockSelect.emit(block.id);
  }

  onHeadKey(event: KeyboardEvent): void {
    const at = this.playheadPercent ?? 0;
    const step = event.shiftKey ? 5 : 1;
    let next: number | null = null;
    switch (event.key) {
      case 'ArrowLeft':
      case 'ArrowDown':
        next = at - step;
        break;
      case 'ArrowRight':
      case 'ArrowUp':
        next = at + step;
        break;
      case 'Home':
        next = 0;
        break;
      case 'End':
        next = 100;
        break;
    }
    if (next == null) return;
    event.preventDefault();
    this.seek.emit(Math.min(100, Math.max(0, next)));
  }

  /** At most one seek per frame while dragging. */
  private queueSeek(clientX: number): void {
    const el = this.track?.nativeElement;
    if (!el) return;
    const rect = el.getBoundingClientRect();
    if (!(rect.width > 0)) return;
    this.pending = Math.min(100, Math.max(0, ((clientX - rect.left) / rect.width) * 100));
    if (this.frame != null) return;
    this.frame = requestAnimationFrame(() => {
      this.frame = null;
      if (this.pending != null) this.seek.emit(this.pending);
      this.pending = null;
    });
  }
}
