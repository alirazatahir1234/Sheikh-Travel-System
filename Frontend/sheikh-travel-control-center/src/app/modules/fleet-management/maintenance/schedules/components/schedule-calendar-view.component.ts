import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { MaintenanceScheduleCalendarItem, ScheduleStatus } from '../../../../../core/models/maintenance.model';

interface CalendarDay {
  date: Date;
  inMonth: boolean;
  key: string;
  items: MaintenanceScheduleCalendarItem[];
}

@Component({
  selector: 'schedule-calendar-view',
  standalone: true,
  imports: [DatePipe, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cal">
      <header class="cal__head">
        <button type="button" class="cal__nav" (click)="shiftMonth(-1)">‹</button>
        <h3>{{ monthLabel() }}</h3>
        <button type="button" class="cal__nav" (click)="shiftMonth(1)">›</button>
      </header>

      <div class="cal__weekdays">
        @for (d of weekdays; track d) { <span>{{ d }}</span> }
      </div>

      <div class="cal__grid">
        @for (day of days(); track day.key) {
          <button
            type="button"
            class="cal__cell"
            [class.cal__cell--muted]="!day.inMonth"
            [class.cal__cell--selected]="selectedDay() === day.key"
            (click)="selectDay(day)">
            <span class="cal__num">{{ day.date.getDate() }}</span>
            <div class="cal__dots">
              @for (item of day.items.slice(0, 3); track item.scheduleId) {
                <span class="cal__dot cal__dot--{{ item.status }}"></span>
              }
            </div>
          </button>
        }
      </div>

      @if (selectedItems().length) {
        <aside class="cal__panel">
          <h4>{{ selectedDate() | date:'fullDate' }}</h4>
          <ul>
            @for (item of selectedItems(); track item.scheduleId) {
              <li>
                <span class="cal__dot cal__dot--{{ item.status }}"></span>
                <div>
                  <strong>{{ item.vehicleName }}</strong> — {{ item.serviceTypeName }}
                  @if (item.nextServiceMileage != null) {
                    <span class="muted"> · {{ item.nextServiceMileage | number:'1.0-0' }} km</span>
                  }
                </div>
              </li>
            }
          </ul>
        </aside>
      }
    </div>
  `,
  styles: [`
    .cal { background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 1rem; }
    .cal__head { display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.75rem; }
    .cal__head h3 { margin: 0; font-size: 1rem; font-weight: 700; }
    .cal__nav { border: none; background: #f1f5f9; width: 2rem; height: 2rem; border-radius: 8px; cursor: pointer; font-size: 1.25rem; }
    .cal__weekdays { display: grid; grid-template-columns: repeat(7, 1fr); gap: 0.25rem; margin-bottom: 0.25rem; }
    .cal__weekdays span { text-align: center; font-size: 0.6875rem; font-weight: 700; color: #94a3b8; text-transform: uppercase; }
    .cal__grid { display: grid; grid-template-columns: repeat(7, 1fr); gap: 0.25rem; }
    .cal__cell {
      min-height: 4.5rem;
      border: 1px solid #f1f5f9;
      border-radius: 8px;
      background: #fff;
      padding: 0.35rem;
      text-align: left;
      cursor: pointer;
    }
    .cal__cell--muted { opacity: 0.45; }
    .cal__cell--selected { border-color: #0B6B50; box-shadow: inset 0 0 0 1px #0B6B50; }
    .cal__num { font-size: 0.8125rem; font-weight: 700; }
    .cal__dots { display: flex; gap: 0.2rem; margin-top: 0.35rem; flex-wrap: wrap; }
    .cal__dot { width: 0.5rem; height: 0.5rem; border-radius: 50%; display: inline-block; }
    .cal__dot--Upcoming { background: #0B6B50; }
    .cal__dot--DueSoon { background: #F59E0B; }
    .cal__dot--Overdue { background: #DC2626; }
    .cal__panel { margin-top: 1rem; padding-top: 1rem; border-top: 1px solid #e2e8f0; }
    .cal__panel h4 { margin: 0 0 0.5rem; font-size: 0.875rem; }
    .cal__panel ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.5rem; }
    .cal__panel li { display: flex; gap: 0.5rem; align-items: flex-start; font-size: 0.8125rem; }
    .muted { color: #94a3b8; }
  `]
})
export class ScheduleCalendarViewComponent {
  readonly items = input.required<MaintenanceScheduleCalendarItem[]>();
  readonly monthChange = output<{ from: string; to: string }>();

  readonly weekdays = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
  readonly cursor = signal(this.startOfMonth(new Date()));
  readonly selectedDay = signal<string | null>(null);

  readonly monthLabel = computed(() =>
    this.cursor().toLocaleDateString(undefined, { month: 'long', year: 'numeric' }));

  readonly days = computed(() => this.buildMonth(this.cursor(), this.items()));
  readonly selectedItems = computed(() => {
    const key = this.selectedDay();
    if (!key) return [];
    return this.days().find(d => d.key === key)?.items ?? [];
  });
  readonly selectedDate = computed(() => {
    const key = this.selectedDay();
    const day = this.days().find(d => d.key === key);
    return day?.date ?? new Date();
  });

  shiftMonth(delta: number): void {
    const next = new Date(this.cursor());
    next.setMonth(next.getMonth() + delta);
    this.cursor.set(this.startOfMonth(next));
    this.selectedDay.set(null);
    this.emitRange();
  }

  selectDay(day: CalendarDay): void {
    this.selectedDay.set(day.key);
  }

  emitRange(): void {
    const start = this.startOfMonth(this.cursor());
    const end = new Date(start.getFullYear(), start.getMonth() + 1, 0);
    this.monthChange.emit({ from: start.toISOString(), to: end.toISOString() });
  }

  private buildMonth(month: Date, items: MaintenanceScheduleCalendarItem[]): CalendarDay[] {
    const year = month.getFullYear();
    const m = month.getMonth();
    const first = new Date(year, m, 1);
    const start = new Date(first);
    start.setDate(start.getDate() - start.getDay());

    const byDate = new Map<string, MaintenanceScheduleCalendarItem[]>();
    for (const item of items) {
      if (!item.dueDate) continue;
      const key = item.dueDate.slice(0, 10);
      const list = byDate.get(key) ?? [];
      list.push(item);
      byDate.set(key, list);
    }

    const days: CalendarDay[] = [];
    const cursor = new Date(start);
    for (let i = 0; i < 42; i++) {
      const key = cursor.toISOString().slice(0, 10);
      days.push({
        date: new Date(cursor),
        inMonth: cursor.getMonth() === m,
        key,
        items: byDate.get(key) ?? []
      });
      cursor.setDate(cursor.getDate() + 1);
    }
    return days;
  }

  private startOfMonth(d: Date): Date {
    return new Date(d.getFullYear(), d.getMonth(), 1);
  }
}
