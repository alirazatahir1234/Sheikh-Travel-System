import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatMenuModule } from '@angular/material/menu';
import {
  HIDE_STOP_OPTIONS,
  hideStopsLabel,
  TimelineBriefRow,
  TimelineDayRow,
  TimelineEmptyRow,
  TimelineFilter,
  TimelineNoteRow,
  TimelineRow,
  TimelineSegmentRow
} from '../../utils/history-timeline.util';

export type TimelinePanelState = 'ready' | 'loading' | 'idle' | 'noData' | 'unavailable';

/**
 * Left panel of the History workspace: the period by day, and under an open day its trips, stops
 * and parking. Presentational — rows are built by the page from the API's timeline.
 */
@Component({
  selector: 'app-history-timeline-panel',
  standalone: true,
  imports: [CommonModule, MatMenuModule],
  templateUrl: './history-timeline-panel.component.html',
  styleUrls: ['./history-timeline-panel.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HistoryTimelinePanelComponent implements OnChanges {
  @Input() rows: TimelineRow[] = [];
  @Input() filter: TimelineFilter = 'all';
  @Input() hideStopsUnderMinutes = 5;
  @Input() selectedId: string | null = null;
  @Input() state: TimelinePanelState = 'idle';
  /** Shown instead of the list when there is nothing to list. */
  @Input() message = '';

  @Output() filterChange = new EventEmitter<TimelineFilter>();
  @Output() hideStopsChange = new EventEmitter<number>();
  @Output() dayToggle = new EventEmitter<string>();
  @Output() segmentSelect = new EventEmitter<string>();
  @Output() briefSelect = new EventEmitter<{ tripId: string; atMs: number }>();
  /** "Show" on the hidden-stops note. */
  @Output() showHidden = new EventEmitter<void>();

  readonly filters: { id: TimelineFilter; label: string }[] = [
    { id: 'all', label: 'All' },
    { id: 'trips', label: 'Trips' },
    { id: 'stops', label: 'Stops' }
  ];
  readonly hideOptions = HIDE_STOP_OPTIONS;

  constructor(private readonly host: ElementRef<HTMLElement>) {}

  get hideLabel(): string {
    return hideStopsLabel(this.hideStopsUnderMinutes);
  }

  ngOnChanges(changes: SimpleChanges): void {
    // A selection made on the map or the dock brings its row into view.
    if (changes['selectedId'] && this.selectedId) {
      const id = this.selectedId;
      setTimeout(() => this.revealRow(id));
    }
  }

  /** Scrolls the list only — never the page — so the selected row is fully visible. */
  private revealRow(id: string): void {
    const host = this.host.nativeElement;
    const list = host.querySelector<HTMLElement>('.tl-list');
    const row = host.querySelector<HTMLElement>(`[data-segment="${CSS.escape(id)}"]`);
    if (!list || !row) return;
    const listBox = list.getBoundingClientRect();
    const rowBox = row.getBoundingClientRect();
    const margin = 8;
    if (rowBox.top < listBox.top + margin) {
      list.scrollTop -= listBox.top + margin - rowBox.top;
    } else if (rowBox.bottom > listBox.bottom - margin) {
      list.scrollTop += rowBox.bottom - (listBox.bottom - margin);
    }
  }

  trackRow(_: number, row: TimelineRow): string {
    return row.key;
  }

  // The template cannot narrow the row union by its `type`, so each kind is read through a cast.
  day(row: TimelineRow): TimelineDayRow {
    return row as TimelineDayRow;
  }

  segment(row: TimelineRow): TimelineSegmentRow {
    return row as TimelineSegmentRow;
  }

  brief(row: TimelineRow): TimelineBriefRow {
    return row as TimelineBriefRow;
  }

  note(row: TimelineRow): TimelineNoteRow | TimelineEmptyRow {
    return row as TimelineNoteRow | TimelineEmptyRow;
  }
}
