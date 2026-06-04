import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { PortalSeatLayoutDto } from '../../core/models/portal.models';

@Component({
  selector: 'app-seat-map',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="rounded-2xl border border-slate-200 bg-slate-50 p-4">
      <p class="mb-3 text-center text-xs font-semibold uppercase text-slate-500">Driver</p>
      <div class="mx-auto grid max-w-xs gap-2" [style.gridTemplateColumns]="'repeat(' + cols() + ', 1fr)'">
        @for (seat of seats(); track seat.seatLabel) {
          <button
            type="button"
            class="rounded-lg border px-2 py-3 text-sm font-semibold transition"
            [class.border-slate-300]="!seat.isBooked && !isSelected(seat.seatLabel)"
            [class.bg-white]="!seat.isBooked && !isSelected(seat.seatLabel)"
            [class.border-primary-500]="isSelected(seat.seatLabel)"
            [class.bg-primary-100]="isSelected(seat.seatLabel)"
            [class.border-slate-200]="seat.isBooked"
            [class.bg-slate-200]="seat.isBooked"
            [class.text-slate-400]="seat.isBooked"
            [disabled]="seat.isBooked"
            (click)="toggle(seat.seatLabel)"
          >
            {{ seat.seatLabel }}
          </button>
        }
      </div>
      <p class="mt-3 text-center text-xs text-slate-500">Tap an available seat</p>
    </div>
  `
})
export class SeatMapComponent {
  readonly seats = input.required<PortalSeatLayoutDto[]>();
  readonly selected = input<string[]>([]);
  readonly selectedChange = output<string[]>();

  cols(): number {
    const s = this.seats();
    if (!s.length) return 2;
    return Math.max(...s.map((x) => x.colIndex)) + 1;
  }

  isSelected(label: string): boolean {
    return this.selected().includes(label);
  }

  toggle(label: string): void {
    const cur = this.selected();
    const next = cur.includes(label) ? cur.filter((l) => l !== label) : [...cur, label];
    this.selectedChange.emit(next);
  }
}
