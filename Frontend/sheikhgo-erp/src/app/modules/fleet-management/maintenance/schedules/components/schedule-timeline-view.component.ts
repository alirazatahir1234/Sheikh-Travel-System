import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { MaintenanceScheduleCalendarItem } from '../../../../../core/models/maintenance.model';

interface TimelineRow {
  vehicleId: number;
  vehicleName: string;
  items: MaintenanceScheduleCalendarItem[];
}

@Component({
  selector: 'schedule-timeline-view',
  standalone: true,
  imports: [DatePipe, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tl">
      <header class="tl__head">
        <button type="button" (click)="shiftWeeks(-1)">‹</button>
        <span>{{ rangeLabel() }}</span>
        <button type="button" (click)="shiftWeeks(1)">›</button>
      </header>

      <div class="tl__axis">
        @for (d of axisDays(); track d.key) {
          <span class="tl__axis-day">{{ d.date | date:'d MMM' }}</span>
        }
      </div>

      @if (!rows().length) {
        <p class="tl__empty">No scheduled services in this range.</p>
      } @else {
        @for (row of rows(); track row.vehicleId) {
          <div class="tl__row">
            <div class="tl__label">{{ row.vehicleName }}</div>
            <div class="tl__track">
              @for (item of row.items; track item.scheduleId) {
                <div
                  class="tl__bar tl__bar--{{ item.status }}"
                  [style.left.%]="barLeft(item)"
                  [title]="item.serviceTypeName">
                  {{ item.serviceTypeName }}
                </div>
              }
            </div>
          </div>
        }
      }
    </div>
  `,
  styles: [`
    .tl { background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 1rem; overflow-x: auto; }
    .tl__head { display: flex; align-items: center; justify-content: center; gap: 1rem; margin-bottom: 1rem; font-weight: 700; }
    .tl__head button { border: none; background: #f1f5f9; width: 2rem; height: 2rem; border-radius: 8px; cursor: pointer; }
    .tl__axis { display: grid; grid-template-columns: 140px repeat(7, 1fr); gap: 0.25rem; margin-bottom: 0.5rem; }
    .tl__axis-day { font-size: 0.6875rem; color: #94a3b8; text-align: center; font-weight: 700; }
    .tl__row { display: grid; grid-template-columns: 140px 1fr; gap: 0.5rem; align-items: center; margin-bottom: 0.5rem; }
    .tl__label { font-size: 0.8125rem; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .tl__track { position: relative; height: 2rem; background: #f8fafc; border-radius: 6px; min-width: 560px; }
    .tl__bar {
      position: absolute;
      top: 0.25rem;
      height: 1.5rem;
      padding: 0 0.5rem;
      border-radius: 4px;
      font-size: 0.6875rem;
      font-weight: 700;
      color: #fff;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      line-height: 1.5rem;
      min-width: 4rem;
    }
    .tl__bar--Upcoming { background: #0B6B50; }
    .tl__bar--DueSoon { background: #F59E0B; }
    .tl__bar--Overdue { background: #DC2626; }
    .tl__empty { color: #94a3b8; text-align: center; padding: 2rem; }
  `]
})
export class ScheduleTimelineViewComponent {
  readonly items = input.required<MaintenanceScheduleCalendarItem[]>();
  readonly rangeChange = output<{ from: string; to: string }>();

  readonly weekStart = signal(this.startOfWeek(new Date()));

  readonly axisDays = computed(() => {
    const start = this.weekStart();
    return Array.from({ length: 7 }, (_, i) => {
      const date = new Date(start);
      date.setDate(date.getDate() + i);
      return { date, key: date.toISOString().slice(0, 10) };
    });
  });

  readonly rangeEnd = computed(() => {
    const end = new Date(this.weekStart());
    end.setDate(end.getDate() + 6);
    return end;
  });

  readonly rangeLabel = computed(() => {
    const start = this.weekStart();
    const end = this.rangeEnd();
    return `${start.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })} – ${end.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })}`;
  });

  readonly rows = computed((): TimelineRow[] => {
    const map = new Map<number, TimelineRow>();
    for (const item of this.items()) {
      const existing = map.get(item.vehicleId);
      if (existing) existing.items.push(item);
      else map.set(item.vehicleId, { vehicleId: item.vehicleId, vehicleName: item.vehicleName, items: [item] });
    }
    return [...map.values()].sort((a, b) => a.vehicleName.localeCompare(b.vehicleName));
  });

  shiftWeeks(delta: number): void {
    const next = new Date(this.weekStart());
    next.setDate(next.getDate() + delta * 7);
    this.weekStart.set(this.startOfWeek(next));
    this.emitRange();
  }

  barLeft(item: MaintenanceScheduleCalendarItem): number {
    if (!item.dueDate) return 0;
    const due = new Date(item.dueDate);
    const start = this.weekStart();
    const diff = Math.round((due.getTime() - start.getTime()) / 86_400_000);
    const clamped = Math.max(0, Math.min(6, diff));
    return (clamped / 7) * 100;
  }

  emitRange(): void {
    const from = this.weekStart();
    const to = this.rangeEnd();
    this.rangeChange.emit({ from: from.toISOString(), to: to.toISOString() });
  }

  private startOfWeek(d: Date): Date {
    const date = new Date(d);
    date.setDate(date.getDate() - date.getDay());
    date.setHours(0, 0, 0, 0);
    return date;
  }
}
