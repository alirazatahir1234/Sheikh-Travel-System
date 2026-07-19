import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { TripService } from '../../../core/services/trip.service';
import { TripCalendarItem, TripStatus } from '../../../core/models/trip.model';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

interface CalendarDay {
  date: Date;
  inMonth: boolean;
  key: string;
  items: TripCalendarItem[];
}

@Component({
  standalone: false,
  selector: 'app-trip-calendar',
  templateUrl: './trip-calendar.component.html',
  styleUrls: ['./trip-calendar.component.scss']
})
export class TripCalendarComponent implements OnInit {
  readonly weekdays = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
  cursor = this.startOfMonth(new Date());
  days: CalendarDay[] = [];
  items: TripCalendarItem[] = [];
  selectedKey: string | null = null;
  loading = true;

  constructor(
    private trips: TripService,
    private router: Router,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  get monthLabel(): string {
    return this.cursor.toLocaleDateString(undefined, { month: 'long', year: 'numeric' });
  }

  get selectedItems(): TripCalendarItem[] {
    if (!this.selectedKey) return [];
    return this.days.find(d => d.key === this.selectedKey)?.items ?? [];
  }

  get selectedDate(): Date | null {
    if (!this.selectedKey) return null;
    return this.days.find(d => d.key === this.selectedKey)?.date ?? null;
  }

  shiftMonth(delta: number): void {
    const next = new Date(this.cursor);
    next.setMonth(next.getMonth() + delta);
    this.cursor = this.startOfMonth(next);
    this.selectedKey = null;
    this.load();
  }

  selectDay(day: CalendarDay): void {
    this.selectedKey = day.key;
  }

  load(): void {
    this.loading = true;
    const from = this.toDateKey(this.cursor);
    const end = new Date(this.cursor.getFullYear(), this.cursor.getMonth() + 1, 0);
    const to = this.toDateKey(end);

    this.trips.getCalendar(from, to).subscribe({
      next: items => {
        this.items = items;
        this.days = this.buildMonth(this.cursor, items);
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load trip calendar.'));
      }
    });
  }

  openTrip(id: number): void {
    this.router.navigate(['/trips', id]);
  }

  statusClass(status: TripStatus): string {
    return `status-${status}`;
  }

  private buildMonth(month: Date, items: TripCalendarItem[]): CalendarDay[] {
    const year = month.getFullYear();
    const m = month.getMonth();
    const first = new Date(year, m, 1);
    const start = new Date(first);
    start.setDate(start.getDate() - start.getDay());

    const byDate = new Map<string, TripCalendarItem[]>();
    for (const item of items) {
      const key = (item.tripDate || '').slice(0, 10);
      if (!key) continue;
      const list = byDate.get(key) ?? [];
      list.push(item);
      byDate.set(key, list);
    }

    const days: CalendarDay[] = [];
    const cursor = new Date(start);
    for (let i = 0; i < 42; i++) {
      const key = this.toDateKey(cursor);
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

  private toDateKey(d: Date): string {
    const y = d.getFullYear();
    const m = `${d.getMonth() + 1}`.padStart(2, '0');
    const day = `${d.getDate()}`.padStart(2, '0');
    return `${y}-${m}-${day}`;
  }
}
