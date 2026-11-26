import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, model, output, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { UiDrawerComponent } from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  CABIN_LABEL, DIRECTION_LABEL, SaveTravelFlight, SaveTravelFlightSegment, TRAVEL_CABIN_CLASSES, TRAVEL_FLIGHT_DIRECTIONS, TravelAirport,
  TravelFlightBooking, TravelFlightDirection, TravelPassengerType, TravelSupplier
} from '../../models/travel.models';
import { TravelBookingService } from '../../services/travel-booking.service';

/** A traveler who may fly on the file: the customer's travelers, or the group's pilgrims. */
export interface BookingCandidate { travelerId: number; name: string; groupMemberId: number | null; passengerType: TravelPassengerType; }

interface PassengerRow { candidate: BookingCandidate; selected: boolean; passengerType: TravelPassengerType; locked: boolean; }

const blankSegment = (): SaveTravelFlightSegment => ({
  airlineCode: '', flightNumber: '', fromAirport: '', toAirport: '', departLocal: '', arriveLocal: '', cabinClass: 'Economy', bookingClass: null, baggage: null
});

@Component({
  selector: 'app-flight-editor-drawer',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatIconModule, UiDrawerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-drawer [(open)]="open" [title]="flight() ? 'Edit flight' : 'Add flight'" width="760px">
      <div class="space-y-5">
        <div class="tn-grid">
          <div class="tn-field"><label for="fl-dir">Direction</label>
            <select id="fl-dir" class="tn-input" [(ngModel)]="direction">
              @for (d of directions; track d) { <option [value]="d">{{ directionLabel[d] }}</option> }
            </select></div>
          <div class="tn-field"><label for="fl-sup">Airline / consolidator</label>
            <select id="fl-sup" class="tn-input" [(ngModel)]="supplierId">
              <option [ngValue]="null">—</option>
              @for (s of flightSuppliers(); track s.id) { <option [ngValue]="s.id">{{ s.name }}</option> }
            </select></div>
          <div class="tn-field"><label for="fl-pnr">PNR</label><input id="fl-pnr" class="tn-input uppercase" maxlength="8" [(ngModel)]="pnr" /></div>
          <div class="tn-field"><label for="fl-apnr">Airline PNR</label><input id="fl-apnr" class="tn-input uppercase" maxlength="8" [(ngModel)]="airlinePnr" /></div>
        </div>

        <section class="space-y-2">
          <div class="flex items-center justify-between">
            <div><h4 class="tn-section-title">Segments</h4>
              <p class="text-xs text-fleet-text-muted">Times as printed on the ticket — local at each airport.</p></div>
            <button mat-stroked-button type="button" (click)="addSegment()"><mat-icon>add</mat-icon> Segment</button>
          </div>
          <datalist id="airport-codes">
            @for (a of airports(); track a.iataCode) { <option [value]="a.iataCode">{{ a.city }} — {{ a.name }}</option> }
          </datalist>
          @for (s of segments(); track $index; let i = $index) {
            <div class="rounded-lg border border-fleet-border p-3">
              <div class="grid grid-cols-2 gap-2 md:grid-cols-6">
                <div class="tn-field"><label [for]="'sg-al-' + i">Airline</label><input [id]="'sg-al-' + i" class="tn-input uppercase" maxlength="3" [(ngModel)]="s.airlineCode" placeholder="PK" /></div>
                <div class="tn-field"><label [for]="'sg-no-' + i">Flight no.</label><input [id]="'sg-no-' + i" class="tn-input uppercase" maxlength="5" [(ngModel)]="s.flightNumber" placeholder="759" /></div>
                <div class="tn-field"><label [for]="'sg-fr-' + i">From</label><input [id]="'sg-fr-' + i" class="tn-input uppercase" maxlength="3" list="airport-codes" [(ngModel)]="s.fromAirport" /></div>
                <div class="tn-field"><label [for]="'sg-to-' + i">To</label><input [id]="'sg-to-' + i" class="tn-input uppercase" maxlength="3" list="airport-codes" [(ngModel)]="s.toAirport" /></div>
                <div class="tn-field"><label [for]="'sg-cb-' + i">Cabin</label>
                  <select [id]="'sg-cb-' + i" class="tn-input" [(ngModel)]="s.cabinClass">@for (c of cabins; track c) { <option [value]="c">{{ cabinLabel[c] }}</option> }</select></div>
                <div class="tn-field"><label [for]="'sg-bg-' + i">Baggage</label><input [id]="'sg-bg-' + i" class="tn-input" maxlength="30" [(ngModel)]="s.baggage" placeholder="2PC 23kg" /></div>
                <div class="tn-field col-span-2 md:col-span-3"><label [for]="'sg-dp-' + i">Departs (local {{ s.fromAirport || '—' }})</label>
                  <input [id]="'sg-dp-' + i" type="datetime-local" class="tn-input" [(ngModel)]="s.departLocal" /></div>
                <div class="tn-field col-span-2 md:col-span-3"><label [for]="'sg-ar-' + i">Arrives (local {{ s.toAirport || '—' }})</label>
                  <input [id]="'sg-ar-' + i" type="datetime-local" class="tn-input" [(ngModel)]="s.arriveLocal" /></div>
              </div>
              <div class="mt-2 flex justify-end"><button mat-button type="button" (click)="removeSegment(i)"><mat-icon>delete_outline</mat-icon> Remove</button></div>
            </div>
          }
          @if (!segments().length) { <p class="text-sm text-fleet-text-muted">No segments yet — they are needed before ticketing.</p> }
        </section>

        <section class="space-y-2">
          <h4 class="tn-section-title">Passengers</h4>
          @if (passengersLocked()) { <p class="text-xs text-fleet-text-muted">Passengers are fixed once ticketed; cancel a ticket from the flight card.</p> }
          @if (!passengerRows().length) { <p class="text-sm text-fleet-text-muted">No travelers available for this booking.</p> }
          <ul class="divide-y divide-fleet-border">
            @for (p of passengerRows(); track p.candidate.travelerId) {
              <li class="flex items-center gap-3 py-2">
                <input type="checkbox" [id]="'px-' + p.candidate.travelerId" [(ngModel)]="p.selected" [disabled]="p.locked || passengersLocked()" />
                <label [for]="'px-' + p.candidate.travelerId" class="flex-1 text-sm">{{ p.candidate.name }}</label>
                <select class="tn-input w-28" [attr.aria-label]="'Type for ' + p.candidate.name" [(ngModel)]="p.passengerType" [disabled]="passengersLocked()">
                  <option value="Adult">Adult</option><option value="Child">Child</option><option value="Infant">Infant</option>
                </select>
              </li>
            }
          </ul>
          @if (passengerRows().length > 1 && !passengersLocked()) {
            <button mat-button type="button" (click)="selectAll()"><mat-icon>done_all</mat-icon> Select all</button>
          }
        </section>

        <div class="tn-grid">
          <div class="tn-field"><label for="fl-sell">Sell price</label><input id="fl-sell" type="number" min="0" step="0.01" class="tn-input" [(ngModel)]="sellAmount" /></div>
          <div class="tn-field"><label for="fl-cost">Supplier cost</label><input id="fl-cost" type="number" min="0" step="0.01" class="tn-input" [(ngModel)]="costAmount" /></div>
          <div class="tn-field"><label for="fl-cur">Currency</label><input id="fl-cur" class="tn-input uppercase" maxlength="3" [(ngModel)]="currencyCode" /></div>
        </div>
        <div class="tn-field"><label for="fl-notes">Notes</label><textarea id="fl-notes" class="tn-input" rows="2" maxlength="1000" [(ngModel)]="notes"></textarea></div>
        @if (error()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div> }
        <div class="tn-actions">
          <button mat-stroked-button type="button" (click)="open.set(false)">Cancel</button>
          <button mat-flat-button color="primary" type="button" [disabled]="busy()" (click)="save()">{{ busy() ? 'Saving…' : 'Save flight' }}</button>
        </div>
      </div>
    </ui-drawer>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class FlightEditorDrawerComponent {
  private readonly api = inject(TravelBookingService);
  private readonly toast = inject(UiToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly open = model(false);
  readonly bookingId = input.required<number>();
  readonly flight = input<TravelFlightBooking | null>(null);
  readonly candidates = input<BookingCandidate[]>([]);
  readonly suppliers = input<TravelSupplier[]>([]);
  readonly airports = input<TravelAirport[]>([]);
  readonly defaultCurrency = input<string | null>(null);
  readonly saved = output<void>();

  protected readonly directions = TRAVEL_FLIGHT_DIRECTIONS;
  protected readonly directionLabel = DIRECTION_LABEL;
  protected readonly cabins = TRAVEL_CABIN_CLASSES;
  protected readonly cabinLabel = CABIN_LABEL;
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly segments = signal<SaveTravelFlightSegment[]>([]);
  protected readonly passengerRows = signal<PassengerRow[]>([]);
  protected readonly flightSuppliers = computed(() => this.suppliers().filter(s => s.isActive && (s.supplierType === 'Airline' || s.supplierType === 'Consolidator' || s.supplierType === 'Other')));
  protected readonly passengersLocked = computed(() => !!this.flight() && !this.flight()!.allowedActions.includes('EditPassengers'));

  protected direction: TravelFlightDirection = 'Outbound';
  protected supplierId: number | null = null;
  protected pnr = '';
  protected airlinePnr = '';
  protected sellAmount: number | null = null;
  protected costAmount: number | null = null;
  protected currencyCode = '';
  protected notes = '';

  constructor() {
    effect(() => {
      if (!this.open()) return;
      const f = this.flight();
      const candidates = this.candidates();
      untracked(() => this.reset(f, candidates));
    });
  }

  private reset(f: TravelFlightBooking | null, candidates: BookingCandidate[]): void {
    this.error.set(null);
    this.direction = f?.direction ?? 'Outbound';
    this.supplierId = f?.supplierId ?? null;
    this.pnr = f?.pnr ?? '';
    this.airlinePnr = f?.airlinePnr ?? '';
    this.sellAmount = f?.sellAmount ?? null;
    this.costAmount = f?.costAmount ?? null;
    this.currencyCode = f?.currencyCode ?? this.defaultCurrency() ?? '';
    this.notes = f?.notes ?? '';
    this.segments.set(f ? f.segments.map(s => ({
      airlineCode: s.airlineCode, flightNumber: s.flightNumber, fromAirport: s.fromAirport, toAirport: s.toAirport,
      departLocal: s.departLocal.substring(0, 16), arriveLocal: s.arriveLocal.substring(0, 16), cabinClass: s.cabinClass,
      bookingClass: s.bookingClass, baggage: s.baggage
    })) : [blankSegment()]);
    const onFlight = new Map((f?.passengers ?? []).map(p => [p.travelerId, p]));
    this.passengerRows.set(candidates.map(c => {
      const existing = onFlight.get(c.travelerId);
      return { candidate: c, selected: !!existing, passengerType: existing?.passengerType ?? c.passengerType, locked: !!existing && existing.ticketStatus !== 'NotIssued' };
    }));
  }

  protected addSegment(): void { this.segments.update(s => [...s, blankSegment()]); }
  protected removeSegment(i: number): void { this.segments.update(s => s.filter((_, j) => j !== i)); }
  protected selectAll(): void { this.passengerRows.update(rows => rows.map(r => ({ ...r, selected: true }))); }

  protected save(): void {
    const segments = this.segments().filter(s => s.airlineCode || s.flightNumber || s.fromAirport || s.toAirport);
    if (segments.some(s => !s.airlineCode || !s.flightNumber || !s.fromAirport || !s.toAirport || !s.departLocal || !s.arriveLocal)) {
      this.error.set('Complete every segment (airline, flight number, airports, departure and arrival), or remove it.');
      return;
    }
    const num = (n: number | null) => (n === null || (n as unknown) === '' ? null : Number(n));
    const dto: SaveTravelFlight = {
      supplierId: this.supplierId, direction: this.direction, pnr: this.pnr.trim() || null, airlinePnr: this.airlinePnr.trim() || null,
      sellAmount: num(this.sellAmount), costAmount: num(this.costAmount), currencyCode: this.currencyCode.trim().toUpperCase() || null,
      notes: this.notes.trim() || null,
      segments: segments.map(s => ({
        ...s, airlineCode: s.airlineCode.trim().toUpperCase(), flightNumber: s.flightNumber.trim().toUpperCase(),
        fromAirport: s.fromAirport.trim().toUpperCase(), toAirport: s.toAirport.trim().toUpperCase(),
        bookingClass: s.bookingClass?.trim().toUpperCase() || null, baggage: s.baggage?.trim() || null
      })),
      passengers: this.passengerRows().filter(p => p.selected)
        .map(p => ({ travelerId: p.candidate.travelerId, groupMemberId: p.candidate.groupMemberId, passengerType: p.passengerType }))
    };
    this.busy.set(true);
    this.error.set(null);
    this.api.saveFlight(this.bookingId(), this.flight()?.id ?? null, dto).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); this.open.set(false); this.toast.success('Flight saved.'); this.saved.emit(); },
      error: err => { this.busy.set(false); this.error.set(apiErrorMessage(err, 'Could not save the flight.')); }
    });
  }
}
