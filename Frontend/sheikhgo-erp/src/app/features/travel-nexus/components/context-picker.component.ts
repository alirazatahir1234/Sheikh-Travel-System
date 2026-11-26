import { ChangeDetectionStrategy, Component, DestroyRef, inject, input, model, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Observable, Subject, catchError, debounceTime, map, of, switchMap } from 'rxjs';
import { CONTEXT_TYPE_LABEL, TRAVEL_CONTEXT_TYPES, TravelContextType } from '../models/travel.models';
import { TravelBookingService } from '../services/travel-booking.service';
import { TravelGroupService } from '../services/travel-group.service';
import { TravelVisaService } from '../services/travel-visa.service';

export interface TravelContextRef { type: TravelContextType; id: number; label: string; }

/** Picks a booking file, visa case or Umrah group by searching its number / name (8 results at a time). */
@Component({
  selector: 'app-travel-context-picker',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (selected(); as s) {
      <div class="flex items-center gap-2 text-sm">
        <span class="tn-chip active">{{ typeLabel[s.type] }} · {{ s.label }}</span>
        <button mat-icon-button type="button" [attr.aria-label]="'Clear ' + s.label" (click)="selected.set(null)"><mat-icon>close</mat-icon></button>
      </div>
    } @else {
      <div class="flex flex-wrap items-center gap-2">
        <select class="tn-input w-36" [attr.aria-label]="label() + ' type'" [ngModel]="type()" (ngModelChange)="type.set($event); search$.next(term)">
          @for (t of types; track t) { <option [value]="t">{{ typeLabel[t] }}</option> }
        </select>
        <input type="search" class="tn-input w-56" [attr.aria-label]="label()" placeholder="Number or name…" [(ngModel)]="term" (ngModelChange)="search$.next($event)" />
      </div>
      @if (results().length) {
        <ul class="mt-1 max-h-48 overflow-y-auto rounded border border-fleet-border bg-white text-sm" role="listbox">
          @for (r of results(); track r.id) {
            <li><button type="button" class="w-full px-3 py-1.5 text-left hover:bg-fleet-surface-alt" (click)="pick(r)">{{ r.label }}</button></li>
          }
        </ul>
      }
    }
  `
})
export class TravelContextPickerComponent {
  private readonly bookings = inject(TravelBookingService);
  private readonly visas = inject(TravelVisaService);
  private readonly groups = inject(TravelGroupService);
  private readonly destroyRef = inject(DestroyRef);

  readonly selected = model<TravelContextRef | null>(null);
  readonly label = input('Booking, visa case or group');
  protected readonly types = TRAVEL_CONTEXT_TYPES;
  protected readonly typeLabel = CONTEXT_TYPE_LABEL;
  protected readonly type = signal<TravelContextType>('Booking');
  protected readonly results = signal<TravelContextRef[]>([]);
  protected readonly search$ = new Subject<string>();
  protected term = '';

  constructor() {
    this.search$.pipe(debounceTime(300), switchMap(term => this.find(this.type(), (term ?? '').trim())), takeUntilDestroyed(this.destroyRef))
      .subscribe(r => this.results.set(r));
  }

  private find(type: TravelContextType, search: string): Observable<TravelContextRef[]> {
    if (search.length < 2) return of([]);
    const page = { page: 1, pageSize: 8, search };
    const found$: Observable<TravelContextRef[]> = type === 'Booking'
      ? this.bookings.getBookings(page).pipe(map(r => r.items.map(b => ({ type, id: b.id, label: `${b.bookingNumber} · ${b.title}` }))))
      : type === 'VisaCase'
        ? this.visas.getCases(page).pipe(map(r => r.items.map(v => ({ type, id: v.id, label: `${v.caseNumber} · ${v.customerName}` }))))
        : this.groups.getGroups(page).pipe(map(r => r.items.map(g => ({ type, id: g.id, label: `${g.groupNumber} · ${g.name}` }))));
    return found$.pipe(catchError(() => of([])));
  }

  protected pick(r: TravelContextRef): void {
    this.selected.set(r);
    this.results.set([]);
    this.term = '';
  }
}
