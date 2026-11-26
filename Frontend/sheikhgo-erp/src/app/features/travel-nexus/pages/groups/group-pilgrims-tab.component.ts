import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { UiModalComponent, UiStatusBadgeComponent } from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  MEMBER_STATUS_VARIANT, TRAVEL_PERMISSIONS, TRAVEL_PRICE_CATEGORIES, TRAVEL_ROOM_TYPES, TravelCustomerListItem, TravelGroup,
  TravelGroupMember, TravelPriceCategory, TravelRoomType, TravelTraveler
} from '../../models/travel.models';
import { TravelCustomerPickerComponent } from '../../components/customer-picker.component';
import { TravelCustomerService } from '../../services/travel-customer.service';
import { TravelGroupService } from '../../services/travel-group.service';

interface Candidate { traveler: TravelTraveler; selected: boolean; roomPreference: TravelRoomType; priceCategory: TravelPriceCategory; }

/** Age-based default price category at the departure date: under 2 infant, under 12 child. */
export function defaultPriceCategory(dateOfBirth: string | null, departureDate: string): TravelPriceCategory {
  if (!dateOfBirth) return 'Adult';
  const dob = new Date(dateOfBirth.substring(0, 10));
  const dep = new Date(departureDate.substring(0, 10));
  let age = dep.getUTCFullYear() - dob.getUTCFullYear();
  if (dep.getUTCMonth() < dob.getUTCMonth() || (dep.getUTCMonth() === dob.getUTCMonth() && dep.getUTCDate() < dob.getUTCDate())) age--;
  return age < 2 ? 'Infant' : age < 12 ? 'Child' : 'Adult';
}

@Component({
  selector: 'app-group-pilgrims-tab',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, FormsModule, RouterLink, MatButtonModule, MatIconModule, UiModalComponent, UiStatusBadgeComponent,
    HasPermissionDirective, TravelCustomerPickerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-card">
      <div class="flex flex-wrap items-center justify-between gap-3 border-b border-fleet-border px-5 py-4">
        <div>
          <h3 class="tn-section-title">Pilgrims · {{ group().activeMembers }} of {{ group().capacity }}</h3>
          <p class="text-xs text-fleet-text-muted">Register travelers from a customer (the booking party). Confirm each pilgrim before visa processing.</p>
        </div>
        <div class="flex items-center gap-3">
          <label class="flex items-center gap-2 text-xs"><input type="checkbox" [ngModel]="showCancelled()" (ngModelChange)="showCancelled.set($event)" /> Show cancelled</label>
          @if (can('RegisterMembers')) {
            <button mat-flat-button color="primary" *appHasPermission="perms.groupManage" (click)="openRegister()"><mat-icon>person_add</mat-icon> Register pilgrims</button>
          }
        </div>
      </div>
      <div class="overflow-x-auto">
        <table class="w-full min-w-[980px] text-left text-sm">
          <thead class="bg-fleet-surface-alt/60">
            <tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
              <th class="px-5 py-3">Pilgrim</th><th class="px-5 py-3">Booking party</th><th class="px-5 py-3">Passport</th>
              <th class="px-5 py-3">Room / category</th><th class="px-5 py-3 text-right">Quoted</th><th class="px-5 py-3">Status</th>
              <th class="px-5 py-3">Visa</th><th class="px-5 py-3"></th>
            </tr>
          </thead>
          <tbody class="divide-y divide-fleet-border">
            @if (!visibleMembers().length) {
              <tr><td colspan="8" class="py-8 text-center text-sm text-fleet-text-muted">No pilgrims registered yet.</td></tr>
            }
            @for (m of visibleMembers(); track m.id) {
              <tr [class.opacity-60]="m.status === 'Cancelled'">
                <td class="px-5 py-3"><strong>{{ m.travelerName }}</strong>
                  <span class="block text-xs text-fleet-text-muted">{{ m.gender }}{{ m.dateOfBirth ? ' · ' + (m.dateOfBirth | date: 'mediumDate': 'UTC') : '' }}
                    @if (m.mahramTravelerId) { · mahram: {{ nameOf(m.mahramTravelerId) }} }</span></td>
                <td class="px-5 py-3"><a class="text-fleet-primary underline" [routerLink]="['/travel/customers', m.customerId]">{{ m.customerName }}</a></td>
                <td class="px-5 py-3 font-mono text-xs">{{ m.passportNumber || '—' }}
                  @if (m.passportExpiryDate) { <span class="block font-sans text-fleet-text-muted">exp. {{ m.passportExpiryDate | date: 'mediumDate': 'UTC' }}</span> }</td>
                <td class="px-5 py-3">{{ m.roomPreference }} · {{ m.priceCategory }}</td>
                <td class="px-5 py-3 text-right">{{ m.quotedPrice !== null && m.currencyCode ? (m.quotedPrice | currency: m.currencyCode : 'code' : '1.0-2') : '—' }}</td>
                <td class="px-5 py-3"><ui-status-badge [status]="m.status" [variant]="memberVariant[m.status]"></ui-status-badge>
                  @if (m.cancelReason) { <span class="block text-xs text-fleet-text-muted">{{ m.cancelReason }}</span> }</td>
                <td class="px-5 py-3">{{ m.visaDecision || '—' }}{{ m.visaNumber ? ' · ' + m.visaNumber : '' }}</td>
                <td class="px-5 py-3 whitespace-nowrap text-right">
                  @if (m.status !== 'Cancelled' && can('ManageMembers')) {
                    <ng-container *appHasPermission="perms.groupManage">
                      @if (m.status === 'Registered') { <button mat-stroked-button [disabled]="busy()" (click)="confirm(m)">Confirm</button> }
                      <button mat-icon-button aria-label="Edit pilgrim" (click)="openEdit(m)"><mat-icon>edit</mat-icon></button>
                      <button mat-icon-button aria-label="Cancel pilgrim" (click)="openCancel(m)"><mat-icon>person_remove</mat-icon></button>
                    </ng-container>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>

    <ui-modal [(open)]="registerOpen" title="Register pilgrims" size="lg">
      <div class="space-y-4">
        <div class="tn-field"><label for="reg-customer" class="tn-required">Booking party (customer)</label>
          <app-travel-customer-picker inputId="reg-customer" [(selected)]="customer" /></div>
        @if (customer()) {
          @if (candidates() === null) {
            <p class="text-sm text-fleet-text-muted">Loading travelers…</p>
          } @else if (!candidates()!.length) {
            <p class="text-sm">This customer has no travelers that are not already in the group.
              <a class="text-fleet-primary underline" [routerLink]="['/travel/customers', customer()!.customerId]">Add travelers</a> first.</p>
          } @else {
            <p class="text-xs text-fleet-text-muted">Places left: {{ placesLeft() }}. Quoted prices come from the package price table.</p>
            <ul class="divide-y divide-fleet-border">
              @for (c of candidates()!; track c.traveler.id) {
                <li class="flex flex-wrap items-center gap-3 py-2">
                  <input type="checkbox" [id]="'rc-' + c.traveler.id" [(ngModel)]="c.selected" (ngModelChange)="touch()" />
                  <label [for]="'rc-' + c.traveler.id" class="min-w-[200px] flex-1 text-sm"><strong>{{ c.traveler.fullName }}</strong>
                    <span class="block text-xs text-fleet-text-muted">{{ c.traveler.gender }}{{ c.traveler.dateOfBirth ? ' · ' + (c.traveler.dateOfBirth | date: 'mediumDate': 'UTC') : '' }}
                      {{ c.traveler.passportNumber ? '· ' + c.traveler.passportNumber : '· no passport on file' }}</span></label>
                  <select class="tn-input w-28" [attr.aria-label]="'Room for ' + c.traveler.fullName" [(ngModel)]="c.roomPreference">
                    @for (t of roomTypes; track t) { <option [value]="t">{{ t }}</option> }
                  </select>
                  <select class="tn-input w-28" [attr.aria-label]="'Category for ' + c.traveler.fullName" [(ngModel)]="c.priceCategory">
                    @for (p of categories; track p) { <option [value]="p">{{ p }}</option> }
                  </select>
                </li>
              }
            </ul>
          }
        }
        @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
      </div>
      <div modal-footer class="tn-actions">
        <button mat-stroked-button (click)="registerOpen.set(false)">Cancel</button>
        <button mat-flat-button color="primary" [disabled]="busy() || !selectedCount() || selectedCount() > placesLeft()" (click)="register()">
          Register {{ selectedCount() || '' }}</button>
      </div>
    </ui-modal>

    <ui-modal [(open)]="editOpen" [title]="'Edit ' + (editing()?.travelerName ?? '')" size="sm">
      <div class="space-y-4">
        <div class="tn-grid">
          <div class="tn-field"><label for="me-room">Room preference</label>
            <select id="me-room" class="tn-input" [(ngModel)]="editRoom">@for (t of roomTypes; track t) { <option [value]="t">{{ t }}</option> }</select></div>
          <div class="tn-field"><label for="me-cat">Price category</label>
            <select id="me-cat" class="tn-input" [(ngModel)]="editCategory">@for (p of categories; track p) { <option [value]="p">{{ p }}</option> }</select></div>
        </div>
        <div class="tn-field"><label for="me-price">Quoted price ({{ group().packageCurrencyCode }})</label>
          <input id="me-price" type="number" min="0" step="0.01" class="tn-input" [(ngModel)]="editPrice" />
          <span class="tn-hint">Leave blank to re-price from the package table.</span></div>
        @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
      </div>
      <div modal-footer class="tn-actions">
        <button mat-stroked-button (click)="editOpen.set(false)">Cancel</button>
        <button mat-flat-button color="primary" [disabled]="busy()" (click)="saveEdit()">Save</button>
      </div>
    </ui-modal>

    <ui-modal [(open)]="cancelOpen" [title]="'Cancel ' + (editing()?.travelerName ?? '')" size="sm">
      <div class="space-y-3">
        @if (group().visaCaseId) { <p class="text-sm">The pilgrim is also withdrawn from the group visa case.</p> }
        <div class="tn-field"><label for="mc-reason" class="tn-required">Reason</label>
          <textarea id="mc-reason" class="tn-input" rows="3" maxlength="500" [(ngModel)]="cancelReason"></textarea></div>
        @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
      </div>
      <div modal-footer class="tn-actions">
        <button mat-stroked-button (click)="cancelOpen.set(false)">Back</button>
        <button mat-flat-button color="warn" [disabled]="busy() || !cancelReason.trim()" (click)="cancel()">Cancel pilgrim</button>
      </div>
    </ui-modal>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class GroupPilgrimsTabComponent {
  private readonly api = inject(TravelGroupService);
  private readonly customers = inject(TravelCustomerService);
  private readonly toast = inject(UiToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly group = input.required<TravelGroup>();
  readonly changed = output<void>();

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly memberVariant = MEMBER_STATUS_VARIANT;
  protected readonly roomTypes = TRAVEL_ROOM_TYPES;
  protected readonly categories = TRAVEL_PRICE_CATEGORIES;
  protected readonly busy = signal(false);
  protected readonly dialogError = signal<string | null>(null);
  protected readonly showCancelled = signal(false);

  protected readonly visibleMembers = computed(() => this.group().members.filter(m => this.showCancelled() || m.status !== 'Cancelled'));
  protected readonly placesLeft = computed(() => Math.max(0, this.group().capacity - this.group().activeMembers));

  protected readonly registerOpen = signal(false);
  protected readonly customer = signal<TravelCustomerListItem | null>(null);
  protected readonly candidates = signal<Candidate[] | null>(null);
  private readonly tick = signal(0);
  protected readonly selectedCount = computed(() => { this.tick(); return (this.candidates() ?? []).filter(c => c.selected).length; });

  protected readonly editOpen = signal(false);
  protected readonly cancelOpen = signal(false);
  protected readonly editing = signal<TravelGroupMember | null>(null);
  protected editRoom: TravelRoomType = 'Quad';
  protected editCategory: TravelPriceCategory = 'Adult';
  protected editPrice: number | null = null;
  protected cancelReason = '';

  constructor() {
    effect(() => {
      const c = this.customer();
      this.candidates.set(null);
      if (!c) return;
      const g = this.group();
      const taken = new Set(g.members.filter(m => m.status !== 'Cancelled').map(m => m.travelerId));
      this.customers.getTravelers(c.customerId).pipe(catchError(() => of([] as TravelTraveler[])), takeUntilDestroyed(this.destroyRef))
        .subscribe(list => this.candidates.set(list.filter(t => !taken.has(t.id)).map(t => ({
          traveler: t, selected: false, roomPreference: 'Quad', priceCategory: defaultPriceCategory(t.dateOfBirth, g.departureDate)
        }))));
    });
  }

  protected can(a: string): boolean { return this.group().allowedActions.includes(a as never); }
  protected touch(): void { this.tick.update(n => n + 1); }
  protected nameOf(travelerId: number): string { return this.group().members.find(m => m.travelerId === travelerId)?.travelerName ?? 'not in group'; }

  protected openRegister(): void {
    this.customer.set(null);
    this.dialogError.set(null);
    this.registerOpen.set(true);
  }

  protected register(): void {
    const members = (this.candidates() ?? []).filter(c => c.selected)
      .map(c => ({ travelerId: c.traveler.id, roomPreference: c.roomPreference, priceCategory: c.priceCategory }));
    this.busy.set(true);
    this.dialogError.set(null);
    this.api.register(this.group().id, this.customer()!.customerId, members).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); this.registerOpen.set(false); this.toast.success(`${members.length} pilgrim(s) registered.`); this.changed.emit(); },
      error: err => { this.busy.set(false); this.dialogError.set(apiErrorMessage(err, 'Could not register the pilgrims.')); }
    });
  }

  protected confirm(m: TravelGroupMember): void {
    this.busy.set(true);
    this.api.confirmMember(this.group().id, m.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); this.toast.success(`${m.travelerName} confirmed.`); this.changed.emit(); },
      error: err => { this.busy.set(false); this.toast.error(apiErrorMessage(err, 'Could not confirm the pilgrim.')); }
    });
  }

  protected openEdit(m: TravelGroupMember): void {
    this.editing.set(m);
    this.editRoom = m.roomPreference;
    this.editCategory = m.priceCategory;
    this.editPrice = m.quotedPrice;
    this.dialogError.set(null);
    this.editOpen.set(true);
  }

  protected saveEdit(): void {
    const m = this.editing()!;
    const price = this.editPrice === null || (this.editPrice as unknown) === '' ? null : Number(this.editPrice);
    this.busy.set(true);
    this.api.updateMember(this.group().id, m.id, { roomPreference: this.editRoom, priceCategory: this.editCategory, quotedPrice: price })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => { this.busy.set(false); this.editOpen.set(false); this.toast.success('Pilgrim updated.'); this.changed.emit(); },
        error: err => { this.busy.set(false); this.dialogError.set(apiErrorMessage(err, 'Could not update the pilgrim.')); }
      });
  }

  protected openCancel(m: TravelGroupMember): void {
    this.editing.set(m);
    this.cancelReason = '';
    this.dialogError.set(null);
    this.cancelOpen.set(true);
  }

  protected cancel(): void {
    const m = this.editing()!;
    this.busy.set(true);
    this.api.cancelMember(this.group().id, m.id, this.cancelReason.trim()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: ({ message }) => {
        this.busy.set(false);
        this.cancelOpen.set(false);
        // The server says what happened in the same write (visa case withdrawal, a ticket still held).
        this.toast.success(message ?? `${m.travelerName} cancelled.`);
        this.changed.emit();
      },
      error: err => { this.busy.set(false); this.dialogError.set(apiErrorMessage(err, 'Could not cancel the pilgrim.')); }
    });
  }
}
