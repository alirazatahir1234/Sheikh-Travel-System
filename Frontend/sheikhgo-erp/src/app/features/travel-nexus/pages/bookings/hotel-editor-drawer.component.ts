import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, model, output, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { UiDrawerComponent } from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  MEAL_PLAN_LABEL, ROOM_CAPACITY, SaveTravelHotelBooking, TRAVEL_MEAL_PLANS, TRAVEL_ROOM_TYPES, TravelGroupRoom, TravelGroupStay, TravelHotel,
  TravelHotelBooking, TravelMealPlan, TravelRoomType, TravelSupplier
} from '../../models/travel.models';
import { toDateInput } from '../../models/travel-date.util';
import { TravelBookingService } from '../../services/travel-booking.service';
import { TravelSupplierFinanceService } from '../../services/travel-supplier-finance.service';
import { BookingCandidate } from './flight-editor-drawer.component';

/** Rooms to book for a group stay: the saved rooming, counted by room type. */
export function roomsFromRooming(stayId: number, rooms: TravelGroupRoom[]): { roomType: TravelRoomType; quantity: number }[] {
  const counts = new Map<TravelRoomType, number>();
  for (const r of rooms.filter(x => x.groupHotelId === stayId)) counts.set(r.roomType, (counts.get(r.roomType) ?? 0) + 1);
  return TRAVEL_ROOM_TYPES.filter(t => counts.has(t)).map(t => ({ roomType: t, quantity: counts.get(t)! }));
}

@Component({
  selector: 'app-hotel-editor-drawer',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatIconModule, UiDrawerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-drawer [(open)]="open" [title]="hotel() ? 'Edit hotel booking' : 'Add hotel booking'" width="640px">
      <div class="space-y-5">
        @if (groupStays().length) {
          <div class="tn-field"><label for="hb-stay">Group stay</label>
            <select id="hb-stay" class="tn-input" [ngModel]="groupHotelId" (ngModelChange)="pickStay($event)">
              <option [ngValue]="null">Not linked</option>
              @for (s of groupStays(); track s.id) { <option [ngValue]="s.id">{{ s.city }} · {{ s.hotelName }} ({{ s.checkIn.substring(0, 10) }} → {{ s.checkOut.substring(0, 10) }})</option> }
            </select>
            <span class="tn-hint">Linking fills hotel, dates, rooms (from the saved rooming) and guests; the confirmation number is copied to the stay.</span></div>
        }
        <div class="tn-grid">
          <div class="tn-field"><label for="hb-hotel" class="tn-required">Hotel</label>
            <select id="hb-hotel" class="tn-input" [(ngModel)]="hotelId">
              <option [ngValue]="null">Choose…</option>
              @for (h of hotels(); track h.id) { <option [ngValue]="h.id">{{ h.city }} · {{ h.name }}</option> }
            </select></div>
          <div class="tn-field"><label for="hb-sup">Booked through</label>
            <select id="hb-sup" class="tn-input" [(ngModel)]="supplierId">
              <option [ngValue]="null">—</option>
              @for (s of hotelSuppliers(); track s.id) { <option [ngValue]="s.id">{{ s.name }}</option> }
            </select></div>
          <div class="tn-field"><label for="hb-in" class="tn-required">Check-in</label><input id="hb-in" type="date" class="tn-input" [(ngModel)]="checkIn" /></div>
          <div class="tn-field"><label for="hb-out" class="tn-required">Check-out</label><input id="hb-out" type="date" class="tn-input" [(ngModel)]="checkOut" /></div>
          <div class="tn-field"><label for="hb-meal">Meal plan</label>
            <select id="hb-meal" class="tn-input" [(ngModel)]="mealPlan">@for (m of mealPlans; track m) { <option [value]="m">{{ mealLabel[m] }}</option> }</select></div>
          <div class="tn-field"><label for="hb-conf">Confirmation no.</label><input id="hb-conf" class="tn-input" maxlength="50" [(ngModel)]="confirmationNumber" /></div>
        </div>

        <section class="space-y-2">
          <div class="flex items-center justify-between"><h4 class="tn-section-title">Rooms</h4>
            <button mat-stroked-button type="button" [disabled]="rooms().length >= roomTypes.length" (click)="addRoom()"><mat-icon>add</mat-icon> Room type</button></div>
          @for (r of rooms(); track $index; let i = $index) {
            <div class="flex items-end gap-3">
              <div class="tn-field w-40"><label [for]="'hr-t-' + i">Type</label>
                <select [id]="'hr-t-' + i" class="tn-input" [(ngModel)]="r.roomType">@for (t of roomTypes; track t) { <option [value]="t">{{ t }} ({{ capacity[t] }})</option> }</select></div>
              <div class="tn-field w-28"><label [for]="'hr-q-' + i">Rooms</label><input [id]="'hr-q-' + i" type="number" min="1" class="tn-input" [(ngModel)]="r.quantity" /></div>
              <button mat-icon-button type="button" aria-label="Remove room type" (click)="removeRoom(i)"><mat-icon>delete_outline</mat-icon></button>
            </div>
          }
          <p class="text-xs text-fleet-text-muted">Beds: {{ beds() }} · guests selected: {{ selectedGuests() }}</p>
        </section>

        <section class="space-y-2">
          <div class="flex items-center justify-between"><h4 class="tn-section-title">Guests</h4>
            @if (candidates().length > 1) { <button mat-button type="button" (click)="selectAll()"><mat-icon>done_all</mat-icon> Select all</button> }</div>
          <ul class="max-h-64 divide-y divide-fleet-border overflow-y-auto">
            @for (g of guests(); track g.candidate.travelerId) {
              <li class="flex items-center gap-3 py-1.5">
                <input type="checkbox" [id]="'hg-' + g.candidate.travelerId" [(ngModel)]="g.selected" (ngModelChange)="touch()" />
                <label [for]="'hg-' + g.candidate.travelerId" class="text-sm">{{ g.candidate.name }}</label>
              </li>
            }
          </ul>
        </section>

        <div class="tn-grid">
          <div class="tn-field"><label for="hb-sell">Sell price</label><input id="hb-sell" type="number" min="0" step="0.01" class="tn-input" [(ngModel)]="sellAmount" /></div>
          <div class="tn-field"><label for="hb-cost">Supplier cost</label><input id="hb-cost" type="number" min="0" step="0.01" class="tn-input" [(ngModel)]="costAmount" /></div>
          <div class="tn-field"><label for="hb-cur">Currency</label><input id="hb-cur" class="tn-input uppercase" maxlength="3" [(ngModel)]="currencyCode" /></div>
        </div>
        @if (supplierId && hotelId && checkIn && checkOut) {
          <div class="flex flex-wrap items-center gap-2 text-xs">
            <button mat-stroked-button type="button" [disabled]="quoting()" (click)="useRateCard()"><mat-icon>sell</mat-icon> {{ quoting() ? 'Quoting…' : 'Use rate card' }}</button>
            @if (quoteNote()) { <span [class.text-amber-700]="quoteWarn()">{{ quoteNote() }}</span> }
          </div>
        }
        <div class="tn-field"><label for="hb-req">Special requests</label><textarea id="hb-req" class="tn-input" rows="2" maxlength="1000" [(ngModel)]="specialRequests"></textarea></div>
        @if (error()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div> }
        <div class="tn-actions">
          <button mat-stroked-button type="button" (click)="open.set(false)">Cancel</button>
          <button mat-flat-button color="primary" type="button" [disabled]="busy() || !hotelId || !checkIn || !checkOut || !rooms().length" (click)="save()">
            {{ busy() ? 'Saving…' : 'Save hotel booking' }}</button>
        </div>
      </div>
    </ui-drawer>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class HotelEditorDrawerComponent {
  private readonly api = inject(TravelBookingService);
  private readonly toast = inject(UiToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly open = model(false);
  readonly bookingId = input.required<number>();
  readonly hotel = input<TravelHotelBooking | null>(null);
  readonly candidates = input<BookingCandidate[]>([]);
  readonly suppliers = input<TravelSupplier[]>([]);
  readonly hotels = input<TravelHotel[]>([]);
  readonly groupStays = input<TravelGroupStay[]>([]);
  readonly groupRooms = input<TravelGroupRoom[]>([]);
  readonly defaultCurrency = input<string | null>(null);
  readonly saved = output<void>();

  protected readonly mealPlans = TRAVEL_MEAL_PLANS;
  protected readonly mealLabel = MEAL_PLAN_LABEL;
  protected readonly roomTypes = TRAVEL_ROOM_TYPES;
  protected readonly capacity = ROOM_CAPACITY;
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly rooms = signal<{ roomType: TravelRoomType; quantity: number }[]>([]);
  protected readonly guests = signal<{ candidate: BookingCandidate; selected: boolean }[]>([]);
  private readonly tick = signal(0);
  protected readonly selectedGuests = computed(() => { this.tick(); return this.guests().filter(g => g.selected).length; });
  protected readonly beds = computed(() => { this.tick(); return this.rooms().reduce((n, r) => n + ROOM_CAPACITY[r.roomType] * Number(r.quantity || 0), 0); });
  protected readonly quoting = signal(false);
  protected readonly quoteNote = signal<string | null>(null);
  protected readonly quoteWarn = signal(false);
  private readonly rateCards = inject(TravelSupplierFinanceService);
  protected readonly hotelSuppliers = computed(() => this.suppliers().filter(s => s.isActive && s.supplierType !== 'Airline'));

  protected groupHotelId: number | null = null;
  protected hotelId: number | null = null;
  protected supplierId: number | null = null;
  protected checkIn = '';
  protected checkOut = '';
  protected mealPlan: TravelMealPlan = 'Breakfast';
  protected confirmationNumber = '';
  protected sellAmount: number | null = null;
  protected costAmount: number | null = null;
  protected currencyCode = '';
  protected specialRequests = '';

  constructor() {
    effect(() => {
      if (!this.open()) return;
      const h = this.hotel();
      const candidates = this.candidates();
      untracked(() => this.reset(h, candidates));
    });
  }

  private reset(h: TravelHotelBooking | null, candidates: BookingCandidate[]): void {
    this.error.set(null);
    this.quoteNote.set(null);
    this.groupHotelId = h?.groupHotelId ?? null;
    this.hotelId = h?.hotelId ?? null;
    this.supplierId = h?.supplierId ?? null;
    this.checkIn = toDateInput(h?.checkIn);
    this.checkOut = toDateInput(h?.checkOut);
    this.mealPlan = h?.mealPlan ?? 'Breakfast';
    this.confirmationNumber = h?.confirmationNumber ?? '';
    this.sellAmount = h?.sellAmount ?? null;
    this.costAmount = h?.costAmount ?? null;
    this.currencyCode = h?.currencyCode ?? this.defaultCurrency() ?? '';
    this.specialRequests = h?.specialRequests ?? '';
    this.rooms.set(h ? h.rooms.map(r => ({ roomType: r.roomType, quantity: r.quantity })) : [{ roomType: 'Double', quantity: 1 }]);
    const chosen = new Set((h?.guests ?? []).map(g => g.travelerId));
    this.guests.set(candidates.map(c => ({ candidate: c, selected: chosen.has(c.travelerId) })));
    this.touch();
  }

  protected touch(): void { this.tick.update(n => n + 1); }
  protected addRoom(): void {
    const used = new Set(this.rooms().map(r => r.roomType));
    const next = TRAVEL_ROOM_TYPES.find(t => !used.has(t));
    if (next) this.rooms.update(r => [...r, { roomType: next, quantity: 1 }]);
    this.touch();
  }
  /** Fills the supplier cost from the supplier's rate cards; nights without a card are reported, never guessed. */
  protected useRateCard(): void {
    const rooms = this.rooms().filter(r => Number(r.quantity) > 0).map(r => ({ roomType: r.roomType, quantity: Number(r.quantity) }));
    this.quoting.set(true);
    this.quoteNote.set(null);
    this.rateCards.quote({ supplierId: this.supplierId!, hotelId: this.hotelId!, checkIn: this.checkIn, checkOut: this.checkOut, rooms })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: q => {
          this.quoting.set(false);
          if (q.mixedCurrencies) { this.quoteWarn.set(true); this.quoteNote.set('The rate cards for this stay are in different currencies — enter the cost by hand.'); return; }
          if (!q.currencyCode) { this.quoteWarn.set(true); this.quoteNote.set('No rate card covers this stay.'); return; }
          this.costAmount = q.total;
          this.currencyCode = q.currencyCode;
          this.quoteWarn.set(!q.complete);
          this.quoteNote.set(q.complete ? `${q.nights} night(s) from the rate card.`
            : `${q.missing.length} room-night(s) have no rate (from ${q.missing[0].night.substring(0, 10)}) and are not included.`);
          this.touch();
        },
        error: err => { this.quoting.set(false); this.quoteWarn.set(true); this.quoteNote.set(apiErrorMessage(err, 'Could not quote from the rate card.')); }
      });
  }

  protected removeRoom(i: number): void { this.rooms.update(r => r.filter((_, j) => j !== i)); this.touch(); }
  protected selectAll(): void { this.guests.update(g => g.map(x => ({ ...x, selected: true }))); this.touch(); }

  protected pickStay(stayId: number | null): void {
    this.groupHotelId = stayId;
    const stay = this.groupStays().find(s => s.id === stayId);
    if (!stay) return;
    this.hotelId = stay.hotelId;
    this.checkIn = toDateInput(stay.checkIn);
    this.checkOut = toDateInput(stay.checkOut);
    if (stay.confirmationNumber && !this.confirmationNumber) this.confirmationNumber = stay.confirmationNumber;
    const rooms = roomsFromRooming(stay.id, this.groupRooms());
    if (rooms.length) this.rooms.set(rooms);
    this.selectAll();
  }

  protected save(): void {
    const rooms = this.rooms().map(r => ({ roomType: r.roomType, quantity: Number(r.quantity) }));
    if (new Set(rooms.map(r => r.roomType)).size !== rooms.length) { this.error.set('List each room type once.'); return; }
    if (rooms.some(r => !(r.quantity >= 1))) { this.error.set('Each room type needs at least one room.'); return; }
    const num = (n: number | null) => (n === null || (n as unknown) === '' ? null : Number(n));
    const dto: SaveTravelHotelBooking = {
      hotelId: this.hotelId!, supplierId: this.supplierId, groupHotelId: this.groupHotelId, checkIn: this.checkIn, checkOut: this.checkOut,
      mealPlan: this.mealPlan, confirmationNumber: this.confirmationNumber.trim() || null, specialRequests: this.specialRequests.trim() || null,
      sellAmount: num(this.sellAmount), costAmount: num(this.costAmount), currencyCode: this.currencyCode.trim().toUpperCase() || null,
      rooms, guestTravelerIds: this.guests().filter(g => g.selected).map(g => g.candidate.travelerId)
    };
    this.busy.set(true);
    this.error.set(null);
    this.api.saveHotel(this.bookingId(), this.hotel()?.id ?? null, dto).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); this.open.set(false); this.toast.success('Hotel booking saved.'); this.saved.emit(); },
      error: err => { this.busy.set(false); this.error.set(apiErrorMessage(err, 'Could not save the hotel booking.')); }
    });
  }
}
