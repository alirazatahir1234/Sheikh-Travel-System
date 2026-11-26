import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, catchError, map, of } from 'rxjs';
import { UiConfirmService, UiModalComponent, UiPageHeaderComponent, UiStatusBadgeComponent, UiTab, UiTabsComponent } from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { AuthService } from '../../../../core/services/auth.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  BOOKING_STATUS_VARIANT, CABIN_LABEL, CancelTravelService, DIRECTION_LABEL, FLIGHT_STATUS_LABEL, FLIGHT_STATUS_VARIANT, HOTEL_BOOKING_STATUS_LABEL,
  HOTEL_BOOKING_STATUS_VARIANT, HOTEL_TRANSITION_LABEL, MEAL_PLAN_LABEL, REFUND_STATUS_LABEL, REFUND_STATUS_VARIANT, TICKET_STATUS_VARIANT,
  TRAVEL_PERMISSIONS, TravelBooking, TravelBookingCancellation, TravelBookingTransitionAction, TravelFlightAction, TravelFlightBooking,
  TravelFlightPassenger, TravelGroup, TravelHotel, TravelHotelAction, TravelHotelBooking, TravelHotelTransitionAction, TravelRefundStatus,
  TravelSupplier, TravelTraveler, TravelInvoiceListItem
} from '../../models/travel.models';
import { asUtc, fromDateTimeLocal, isDue, toDateTimeLocal } from '../../models/travel-date.util';
import { TravelBookingService } from '../../services/travel-booking.service';
import { TravelFinanceService, newRequestKey } from '../../services/travel-finance.service';
import { TravelConfigService } from '../../services/travel-config.service';
import { TravelCustomerService } from '../../services/travel-customer.service';
import { TravelGroupService } from '../../services/travel-group.service';
import { TravelPackageService } from '../../services/travel-package.service';
import { TravelSupplierService } from '../../services/travel-supplier.service';
import { defaultPriceCategory } from '../groups/group-pilgrims-tab.component';
import { buildItineraryPdf, buildVoucherPdf, itineraryFileName, itineraryRows, roomsLabel, voucherFileName } from './booking-documents';
import { BookingCandidate, FlightEditorDrawerComponent } from './flight-editor-drawer.component';
import { HotelEditorDrawerComponent } from './hotel-editor-drawer.component';

type CancelTarget =
  | { kind: 'flight'; flight: TravelFlightBooking }
  | { kind: 'passenger'; flight: TravelFlightBooking; passenger: TravelFlightPassenger }
  | { kind: 'hotel'; hotel: TravelHotelBooking };

@Component({
  selector: 'app-travel-booking-detail',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, FormsModule, RouterLink, MatButtonModule, MatIconModule, UiPageHeaderComponent, UiStatusBadgeComponent,
    UiTabsComponent, UiModalComponent, HasPermissionDirective, FlightEditorDrawerComponent, HotelEditorDrawerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      @if (error()) {
        <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div>
      } @else if (booking(); as b) {
        <ui-page-header [title]="b.title" eyebrow="TravelNexus · Bookings" icon="airplane_ticket" [subtitle]="b.bookingNumber + ' · ' + b.ownerName"
          [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Bookings', route: '/travel/bookings' }, { label: b.bookingNumber }]">
          <ui-status-badge [status]="b.status" [variant]="statusVariant[b.status]"></ui-status-badge>
          @if (b.customerId) {
            <a mat-stroked-button *appHasPermission="perms.invoiceManage" [routerLink]="['/travel/finance/invoices/new']" [queryParams]="{ customerId: b.customerId }">
              <mat-icon>receipt_long</mat-icon> Invoice</a>
          }
          <ng-container *appHasPermission="perms.bookingManage">
            @if (can('Edit')) { <button mat-stroked-button (click)="openEdit()"><mat-icon>edit</mat-icon> Edit</button> }
            @if (can('Complete')) { <button mat-stroked-button [disabled]="busy()" (click)="askFile('Complete')"><mat-icon>task_alt</mat-icon> Complete</button> }
            @if (can('Reopen')) { <button mat-stroked-button [disabled]="busy()" (click)="askFile('Reopen')"><mat-icon>lock_open</mat-icon> Reopen</button> }
            @if (can('Cancel')) { <button mat-stroked-button color="warn" [disabled]="busy()" (click)="askFile('Cancel')"><mat-icon>block</mat-icon> Cancel file</button> }
          </ng-container>
        </ui-page-header>

        <div class="grid gap-5 lg:grid-cols-3">
          <div class="tn-card tn-card-body">
            <dl class="tn-kv">
              @if (b.groupId) {
                <div><dt>Umrah group</dt><dd><a class="text-fleet-primary underline" [routerLink]="['/travel/groups', b.groupId]">{{ b.groupNumber }} · {{ b.groupName }}</a></dd></div>
              } @else {
                <div><dt>Customer</dt><dd><a class="text-fleet-primary underline" [routerLink]="['/travel/customers', b.customerId]">{{ b.customerName }}</a></dd></div>
              }
              @if (b.enquiryId) { <div><dt>Enquiry</dt><dd><a class="text-fleet-primary underline" [routerLink]="['/travel/enquiries', b.enquiryId]">{{ b.enquiryNumber }}</a></dd></div> }
              @if (b.visaCaseId) { <div><dt>Visa case</dt><dd><a class="text-fleet-primary underline" [routerLink]="['/travel/visa-cases', b.visaCaseId]">{{ b.visaCaseNumber }}</a></dd></div> }
              <div><dt>Travel</dt><dd>{{ b.firstTravelDate ? (b.firstTravelDate | date: 'mediumDate': 'UTC') : '—' }}{{ b.lastTravelDate ? ' → ' + (b.lastTravelDate | date: 'mediumDate': 'UTC') : '' }}</dd></div>
              <div><dt>Agent</dt><dd>{{ b.assignedAgentName || 'Unassigned' }}</dd></div>
              <div><dt>Branch</dt><dd>{{ b.branchName || '—' }}</dd></div>
              @if (b.cancelReason) { <div><dt>Cancel reason</dt><dd>{{ b.cancelReason }}</dd></div> }
            </dl>
            @if (b.notes) { <p class="mt-3 whitespace-pre-line text-sm text-fleet-text-muted">{{ b.notes }}</p> }
          </div>
          <div class="tn-card tn-card-body lg:col-span-2">
            <h3 class="tn-section-title">Totals <span class="text-xs font-normal text-fleet-text-muted">· non-cancelled services, per currency (no conversion)</span></h3>
            @if (!b.totals.length) { <p class="mt-2 text-sm text-fleet-text-muted">No prices recorded yet.</p> }
            <table class="mt-2 w-full text-sm">
              @if (b.totals.length) {
                <thead><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                  <th class="py-1 text-left">Currency</th><th class="py-1 text-right">Sell</th><th class="py-1 text-right">Cost</th><th class="py-1 text-right">Margin</th></tr></thead>
              }
              <tbody>
                @for (t of b.totals; track t.currencyCode) {
                  <tr><td class="py-1 font-mono">{{ t.currencyCode }}</td>
                    <td class="py-1 text-right">{{ t.sell | currency: t.currencyCode : 'code' : '1.0-2' }}</td>
                    <td class="py-1 text-right">{{ t.cost | currency: t.currencyCode : 'code' : '1.0-2' }}</td>
                    <td class="py-1 text-right font-semibold" [class.text-red-600]="t.margin < 0">{{ t.margin | currency: t.currencyCode : 'code' : '1.0-2' }}</td></tr>
                }
              </tbody>
            </table>
          </div>
        </div>

        <ui-tabs [tabs]="tabs()" [(activeId)]="active"></ui-tabs>

        @switch (active()) {
          @case ('services') {
            @if (can('ManageServices')) {
              <div class="flex gap-2" *appHasPermission="perms.bookingManage">
                <button mat-flat-button color="primary" (click)="openFlight(null)"><mat-icon>flight</mat-icon> Add flight</button>
                <button mat-stroked-button (click)="openHotel(null)"><mat-icon>hotel</mat-icon> Add hotel</button>
              </div>
            }
            @if (!b.flights.length && !b.hotels.length) {
              <div class="tn-card tn-card-body"><p class="text-sm text-fleet-text-muted">No services yet. Add the flights and hotels for this trip.</p></div>
            }

            @for (f of b.flights; track f.id) {
              <div class="tn-card" [class.opacity-70]="f.status === 'Cancelled'">
                <div class="flex flex-wrap items-center justify-between gap-3 border-b border-fleet-border px-5 py-3">
                  <div class="flex flex-wrap items-center gap-2">
                    <mat-icon class="text-fleet-text-muted">flight</mat-icon>
                    <strong>{{ directionLabel[f.direction] }}</strong>
                    <span class="font-mono text-xs">PNR {{ f.pnr || '—' }}{{ f.airlinePnr ? ' / ' + f.airlinePnr : '' }}</span>
                    <ui-status-badge [status]="f.status" [label]="flightLabel[f.status]" [variant]="flightVariant[f.status]"></ui-status-badge>
                    @if (f.status === 'OnHold' && f.ticketingTimeLimitUtc) {
                      <span class="text-xs" [class.text-red-600]="limitSoon(f.ticketingTimeLimitUtc)" [class.font-semibold]="limitSoon(f.ticketingTimeLimitUtc)">
                        ticket by {{ utc(f.ticketingTimeLimitUtc) | date: 'd MMM y, HH:mm' }} ({{ hoursLeft(f.ticketingTimeLimitUtc) }})</span>
                    }
                    @if (f.supplierName) { <span class="text-xs text-fleet-text-muted">· {{ f.supplierName }}</span> }
                  </div>
                  <div class="flex flex-wrap gap-1" *appHasPermission="perms.bookingManage">
                    @if (has(f, 'Edit')) { <button mat-icon-button aria-label="Edit flight" (click)="openFlight(f)"><mat-icon>edit</mat-icon></button> }
                    @if (has(f, 'Hold')) { <button mat-stroked-button (click)="openFlightStep(f, 'Hold')">{{ f.status === 'OnHold' ? 'Extend hold' : 'Hold' }}</button> }
                    @if (has(f, 'Confirm')) { <button mat-stroked-button (click)="openFlightStep(f, 'Confirm')">Confirm</button> }
                    @if (has(f, 'IssueTickets')) { <button mat-stroked-button (click)="openTickets(f)"><mat-icon>confirmation_number</mat-icon> Tickets</button> }
                    @if (has(f, 'Ticket')) { <button mat-flat-button color="primary" [disabled]="busy()" (click)="ticket(f)">Mark ticketed</button> }
                    @if (has(f, 'Delete')) { <button mat-icon-button aria-label="Remove flight" (click)="deleteFlight(f)"><mat-icon>delete_outline</mat-icon></button> }
                    @if (has(f, 'Cancel') && canCancel()) { <button mat-stroked-button color="warn" (click)="openCancel({ kind: 'flight', flight: f })">Cancel flight</button> }
                  </div>
                </div>
                <div class="overflow-x-auto px-5 py-3">
                  @if (!f.segments.length) { <p class="text-sm text-fleet-text-muted">No segments yet.</p> }
                  @else {
                    <table class="w-full min-w-[640px] text-sm">
                      <thead><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                        <th class="py-1 text-left">Flight</th><th class="py-1 text-left">From</th><th class="py-1 text-left">Departs (local)</th>
                        <th class="py-1 text-left">To</th><th class="py-1 text-left">Arrives (local)</th><th class="py-1 text-left">Cabin</th></tr></thead>
                      <tbody>
                        @for (s of f.segments; track s.id) {
                          <tr><td class="py-1 font-mono">{{ s.airlineCode }} {{ s.flightNumber }}</td>
                            <td class="py-1">{{ s.fromCity }} <span class="font-mono text-xs">{{ s.fromAirport }}</span></td>
                            <td class="py-1">{{ local(s.departLocal) }}</td>
                            <td class="py-1">{{ s.toCity }} <span class="font-mono text-xs">{{ s.toAirport }}</span></td>
                            <td class="py-1">{{ local(s.arriveLocal) }}</td>
                            <td class="py-1">{{ cabinLabel[s.cabinClass] }}{{ s.baggage ? ' · ' + s.baggage : '' }}</td></tr>
                        }
                      </tbody>
                    </table>
                  }
                </div>
                <div class="border-t border-fleet-border px-5 py-3">
                  <p class="mb-1 text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">Passengers ({{ f.passengers.length }})</p>
                  <ul class="divide-y divide-fleet-border text-sm">
                    @for (p of f.passengers; track p.id) {
                      <li class="flex flex-wrap items-center gap-3 py-1.5">
                        <span class="min-w-[180px] flex-1">{{ p.travelerName }} <span class="text-xs text-fleet-text-muted">· {{ p.passengerType }}{{ p.passportNumber ? ' · ' + p.passportNumber : '' }}</span></span>
                        <span class="font-mono text-xs">{{ p.ticketNumber || '—' }}</span>
                        <ui-status-badge [status]="p.ticketStatus" [variant]="ticketVariant[p.ticketStatus]"></ui-status-badge>
                        @if (has(f, 'CancelPassenger') && p.ticketStatus === 'Issued' && canCancel()) {
                          <button mat-button color="warn" (click)="openCancel({ kind: 'passenger', flight: f, passenger: p })">Void / refund</button>
                        }
                      </li>
                    }
                  </ul>
                  @if (f.sellAmount !== null || f.costAmount !== null) {
                    <p class="mt-2 text-xs text-fleet-text-muted">Sell {{ f.sellAmount !== null ? (f.sellAmount | currency: f.currencyCode! : 'code' : '1.0-2') : '—' }}
                      · cost {{ f.costAmount !== null ? (f.costAmount | currency: f.currencyCode! : 'code' : '1.0-2') : '—' }}</p>
                  }
                  @if (f.notes) { <p class="mt-1 text-xs text-fleet-text-muted">{{ f.notes }}</p> }
                </div>
              </div>
            }

            @for (h of b.hotels; track h.id) {
              <div class="tn-card" [class.opacity-70]="h.status === 'Cancelled'">
                <div class="flex flex-wrap items-center justify-between gap-3 border-b border-fleet-border px-5 py-3">
                  <div class="flex flex-wrap items-center gap-2">
                    <mat-icon class="text-fleet-text-muted">hotel</mat-icon>
                    <strong>{{ h.hotelName }}</strong><span class="text-xs text-fleet-text-muted">{{ h.city }}</span>
                    <ui-status-badge [status]="h.status" [label]="hotelLabel[h.status]" [variant]="hotelVariant[h.status]"></ui-status-badge>
                    @if (h.groupHotelId) { <span class="tn-chip">group stay</span> }
                  </div>
                  <div class="flex flex-wrap gap-1">
                    @if (h.status !== 'Cancelled') { <button mat-stroked-button (click)="voucher(h)"><mat-icon>picture_as_pdf</mat-icon> Voucher</button> }
                    <ng-container *appHasPermission="perms.bookingManage">
                      @if (hasHotel(h, 'Edit')) { <button mat-icon-button aria-label="Edit hotel booking" (click)="openHotel(h)"><mat-icon>edit</mat-icon></button> }
                      @for (a of hotelSteps; track a) {
                        @if (hasHotel(h, a)) { <button mat-stroked-button [disabled]="busy()" (click)="hotelStep(h, a)">{{ hotelStepLabel[a] }}</button> }
                      }
                      @if (hasHotel(h, 'Delete')) { <button mat-icon-button aria-label="Remove hotel booking" (click)="deleteHotel(h)"><mat-icon>delete_outline</mat-icon></button> }
                      @if (hasHotel(h, 'Cancel') && canCancel()) { <button mat-stroked-button color="warn" (click)="openCancel({ kind: 'hotel', hotel: h })">Cancel</button> }
                    </ng-container>
                  </div>
                </div>
                <div class="grid gap-3 px-5 py-3 text-sm md:grid-cols-3">
                  <div><span class="text-xs text-fleet-text-muted">Stay</span><p>{{ h.checkIn | date: 'mediumDate': 'UTC' }} → {{ h.checkOut | date: 'mediumDate': 'UTC' }} · {{ h.nights }} night(s)</p></div>
                  <div><span class="text-xs text-fleet-text-muted">Rooms</span><p>{{ rooms(h) }} · {{ mealLabel[h.mealPlan] }}</p></div>
                  <div><span class="text-xs text-fleet-text-muted">Confirmation</span><p class="font-mono">{{ h.confirmationNumber || '—' }}</p></div>
                  <div class="md:col-span-3"><span class="text-xs text-fleet-text-muted">Guests ({{ h.guests.length }})</span>
                    <p>{{ guestNames(h) || '—' }}</p></div>
                  @if (h.sellAmount !== null || h.costAmount !== null) {
                    <p class="text-xs text-fleet-text-muted md:col-span-3">Sell {{ h.sellAmount !== null ? (h.sellAmount | currency: h.currencyCode! : 'code' : '1.0-2') : '—' }}
                      · cost {{ h.costAmount !== null ? (h.costAmount | currency: h.currencyCode! : 'code' : '1.0-2') : '—' }}{{ h.supplierName ? ' · via ' + h.supplierName : '' }}</p>
                  }
                  @if (h.specialRequests) { <p class="text-xs text-fleet-text-muted md:col-span-3">{{ h.specialRequests }}</p> }
                </div>
              </div>
            }
          }

          @case ('itinerary') {
            <div class="tn-card">
              <div class="flex items-center justify-between border-b border-fleet-border px-5 py-4">
                <div><h3 class="tn-section-title">Itinerary</h3>
                  <p class="text-xs text-fleet-text-muted">Flight times are local to each airport{{ b.groupId ? '; group transfers are shown in UTC' : '' }}.</p></div>
                <button mat-flat-button color="primary" [disabled]="!itinerary().length" (click)="itineraryPdf()"><mat-icon>picture_as_pdf</mat-icon> PDF</button>
              </div>
              @if (!itinerary().length) { <p class="px-5 py-8 text-center text-sm text-fleet-text-muted">Nothing booked yet.</p> }
              <ol class="tn-timeline px-5">
                @for (r of itinerary(); track $index) {
                  <li>
                    <span class="tn-timeline-icon"><mat-icon>{{ r.kind === 'Flight' ? 'flight' : r.kind === 'Hotel' ? 'hotel' : 'directions_bus' }}</mat-icon></span>
                    <div class="flex-1 text-sm"><p><strong>{{ r.date }}</strong> · {{ r.time }} — {{ r.title }}</p><p class="text-fleet-text-muted">{{ r.detail }}</p></div>
                  </li>
                }
              </ol>
            </div>
          }

          @case ('cancellations') {
            <div class="tn-card overflow-x-auto">
              @if (!b.cancellations.length) { <p class="px-5 py-8 text-center text-sm text-fleet-text-muted">No cancellations.</p> }
              @else {
                <table class="w-full min-w-[900px] text-left text-sm">
                  <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                    <th class="px-4 py-3">Service</th><th class="px-4 py-3">Reason</th><th class="px-4 py-3 text-right">Penalty (supplier / customer)</th>
                    <th class="px-4 py-3 text-right">Refund (from supplier / to customer)</th><th class="px-4 py-3">Refund status</th><th class="px-4 py-3"></th></tr></thead>
                  <tbody class="divide-y divide-fleet-border">
                    @for (c of b.cancellations; track c.id) {
                      <tr>
                        <td class="px-4 py-3">{{ c.serviceLabel }}<span class="block text-xs text-fleet-text-muted">{{ utc(c.cancelledAt) | date: 'medium' }} · {{ c.cancelledByName || '—' }}</span></td>
                        <td class="px-4 py-3">{{ c.reason }}</td>
                        <td class="px-4 py-3 text-right">{{ money(c.supplierPenalty, c) }} / {{ money(c.customerPenalty, c) }}</td>
                        <td class="px-4 py-3 text-right">{{ money(c.refundFromSupplier, c) }} / {{ money(c.refundToCustomer, c) }}</td>
                        <td class="px-4 py-3"><ui-status-badge [status]="c.refundStatus" [label]="refundLabel[c.refundStatus]" [variant]="refundVariant[c.refundStatus]"></ui-status-badge></td>
                        <td class="px-4 py-3 whitespace-nowrap text-right">
                          @if (b.customerId && c.refundToCustomer > 0 && c.refundStatus !== 'Closed' && c.refundStatus !== 'PaidToCustomer') {
                            <button mat-button *appHasPermission="perms.financeAdjust" (click)="openCredit(c)">Credit note</button>
                          }
                          <ng-container *appHasPermission="perms.bookingCancel">
                            @for (n of c.nextRefundStatuses; track n) { <button mat-button [disabled]="busy()" (click)="refund(c, n)">{{ refundLabel[n] }}</button> }
                          </ng-container>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              }
            </div>
          }

          @case ('history') {
            <div class="tn-card tn-card-body">
              <ol class="tn-timeline">
                @for (h of b.history; track h.id) {
                  <li>
                    <span class="tn-timeline-icon"><mat-icon>{{ h.serviceType === 'Flight' ? 'flight' : h.serviceType === 'Hotel' ? 'hotel' : 'folder' }}</mat-icon></span>
                    <div class="flex-1 text-sm">
                      <p><strong>{{ historyLabel(h.eventType) }}</strong>
                        @if (h.fromValue || h.toValue) { <span class="text-fleet-text-muted"> {{ h.fromValue || '—' }} → {{ h.toValue || '—' }}</span> }</p>
                      @if (h.note) { <p class="text-fleet-text-muted">{{ h.note }}</p> }
                      <p class="text-xs text-fleet-text-soft">{{ utc(h.changedAt) | date: 'medium' }} · {{ h.changedByName || 'System' }}</p>
                    </div>
                  </li>
                }
              </ol>
            </div>
          }
        }

        <app-flight-editor-drawer [(open)]="flightOpen" [bookingId]="b.id" [flight]="editingFlight()" [candidates]="candidates()"
          [suppliers]="suppliers()" [airports]="airports()" [defaultCurrency]="defaultCurrency()" (saved)="load()" />
        <app-hotel-editor-drawer [(open)]="hotelOpen" [bookingId]="b.id" [hotel]="editingHotel()" [candidates]="candidates()"
          [suppliers]="suppliers()" [hotels]="hotelCatalog()" [groupStays]="group()?.stays ?? []" [groupRooms]="group()?.rooms ?? []"
          [defaultCurrency]="defaultCurrency()" (saved)="load()" />

        <!-- Credit note from a cancellation -->
        <ui-modal [(open)]="creditOpen" title="Credit note for the cancellation" size="sm">
          <div class="space-y-3">
            <p class="text-xs text-fleet-text-muted">Reduces the customer's invoice by the refund agreed in the cancellation. Refund the money from the invoice afterwards.</p>
            <div class="tn-field"><label for="bc-inv" class="tn-required">Invoice</label>
              <select id="bc-inv" class="tn-input" [(ngModel)]="creditInvoiceId">
                <option [ngValue]="null">{{ creditInvoices().length ? 'Choose…' : 'No issued invoice in this currency' }}</option>
                @for (i of creditInvoices(); track i.id) { <option [ngValue]="i.id">{{ i.invoiceNumber }} · {{ i.total | currency: i.currencyCode : 'code' : '1.2-2' }}</option> }
              </select></div>
            <div class="tn-field"><label for="bc-amount" class="tn-required">Amount</label><input id="bc-amount" type="number" min="0" step="0.01" class="tn-input" [(ngModel)]="creditAmount" /></div>
            <div class="tn-field"><label for="bc-reason" class="tn-required">Reason</label><input id="bc-reason" class="tn-input" maxlength="500" [(ngModel)]="creditReason" /></div>
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          </div>
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="creditOpen.set(false)">Cancel</button>
            <button mat-flat-button color="primary" [disabled]="busy() || !creditInvoiceId || !(creditAmount > 0) || !creditReason.trim()" (click)="issueCredit()">Issue credit note</button>
          </div>
        </ui-modal>

        <!-- File transition -->
        <ui-modal [(open)]="fileOpen" [title]="fileAction() === 'Cancel' ? 'Cancel booking file' : fileAction() === 'Complete' ? 'Complete booking file' : 'Reopen booking file'" size="sm">
          <div class="space-y-3">
            <div class="tn-field"><label for="bf-note" [class.tn-required]="fileAction() === 'Cancel'">{{ fileAction() === 'Cancel' ? 'Reason' : 'Note' }}</label>
              <textarea id="bf-note" class="tn-input" rows="3" maxlength="500" [(ngModel)]="note"></textarea></div>
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          </div>
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="fileOpen.set(false)">Back</button>
            <button mat-flat-button [color]="fileAction() === 'Cancel' ? 'warn' : 'primary'" [disabled]="busy() || (fileAction() === 'Cancel' && !note.trim())" (click)="fileTransition()">Confirm</button>
          </div>
        </ui-modal>

        <!-- Edit file -->
        <ui-modal [(open)]="editOpen" title="Edit booking file" size="sm">
          <div class="space-y-3">
            <div class="tn-field"><label for="be-title" class="tn-required">Title</label><input id="be-title" class="tn-input" maxlength="150" [(ngModel)]="editTitle" /></div>
            <div class="tn-field"><label for="be-notes">Notes</label><textarea id="be-notes" class="tn-input" rows="4" maxlength="2000" [(ngModel)]="editNotes"></textarea></div>
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          </div>
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="editOpen.set(false)">Cancel</button>
            <button mat-flat-button color="primary" [disabled]="busy() || !editTitle.trim()" (click)="saveEdit()">Save</button>
          </div>
        </ui-modal>

        <!-- Flight hold / confirm -->
        <ui-modal [(open)]="stepOpen" [title]="stepAction() === 'Hold' ? 'Hold flight' : 'Confirm flight'" size="sm">
          <div class="space-y-3">
            <div class="tn-field"><label for="fs-pnr" class="tn-required">PNR</label><input id="fs-pnr" class="tn-input uppercase" maxlength="8" [(ngModel)]="stepPnr" /></div>
            @if (stepAction() === 'Hold') {
              <div class="tn-field"><label for="fs-limit" class="tn-required">Ticketing time limit (your local time)</label>
                <input id="fs-limit" type="datetime-local" class="tn-input" [(ngModel)]="stepLimit" /></div>
            }
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          </div>
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="stepOpen.set(false)">Back</button>
            <button mat-flat-button color="primary" [disabled]="busy() || !stepPnr.trim() || (stepAction() === 'Hold' && !stepLimit)" (click)="flightStep()">Save</button>
          </div>
        </ui-modal>

        <!-- Tickets -->
        <ui-modal [(open)]="ticketsOpen" title="Ticket numbers" size="md">
          <div class="space-y-3">
            <p class="text-xs text-fleet-text-muted">13 digits, e.g. 214-2345678901. Changing an issued number records a re-issue.</p>
            @for (t of ticketRows(); track t.passengerId) {
              <div class="flex items-center gap-3">
                <label [for]="'tk-' + t.passengerId" class="flex-1 text-sm">{{ t.name }}</label>
                <input [id]="'tk-' + t.passengerId" class="tn-input w-48 font-mono" maxlength="16" [(ngModel)]="t.ticketNumber" />
              </div>
            }
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          </div>
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="ticketsOpen.set(false)">Cancel</button>
            <button mat-flat-button color="primary" [disabled]="busy()" (click)="saveTickets()">Save tickets</button>
          </div>
        </ui-modal>

        <!-- Hotel confirmation -->
        <ui-modal [(open)]="confirmHotelOpen" title="Confirm hotel booking" size="sm">
          <div class="space-y-3">
            <div class="tn-field"><label for="hc-no" class="tn-required">Hotel confirmation number</label>
              <input id="hc-no" class="tn-input" maxlength="50" [(ngModel)]="confirmationNo" /></div>
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          </div>
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="confirmHotelOpen.set(false)">Back</button>
            <button mat-flat-button color="primary" [disabled]="busy() || !confirmationNo.trim()" (click)="confirmHotel()">Confirm</button>
          </div>
        </ui-modal>

        <!-- Cancel service -->
        <ui-modal [(open)]="cancelOpen" [title]="cancelTitle()" size="md">
          <div class="space-y-4">
            @if (cancelCurrency(); as cur) {
              <p class="text-xs text-fleet-text-muted">Amounts in {{ cur }}. Refund plus penalty cannot exceed what was sold (customer side) or bought (supplier side).</p>
            } @else {
              <p class="text-xs text-fleet-text-muted">This service has no price recorded, so no penalty or refund can be entered.</p>
            }
            <div class="tn-field"><label for="cx-reason" class="tn-required">Reason</label><textarea id="cx-reason" class="tn-input" rows="2" maxlength="500" [(ngModel)]="cx.reason"></textarea></div>
            @if (needsTicketOutcome()) {
              <div class="tn-field"><label for="cx-outcome" class="tn-required">Issued tickets become</label>
                <select id="cx-outcome" class="tn-input" [(ngModel)]="cx.ticketOutcome">
                  <option [ngValue]="null">Choose…</option><option value="Voided">Voided</option><option value="Refunded">Refunded</option>
                </select></div>
            }
            @if (cancelCurrency()) {
              <div class="tn-grid">
                <div class="tn-field"><label for="cx-sp">Supplier penalty</label><input id="cx-sp" type="number" min="0" step="0.01" class="tn-input" [(ngModel)]="cx.supplierPenalty" /></div>
                <div class="tn-field"><label for="cx-cp">Customer penalty</label><input id="cx-cp" type="number" min="0" step="0.01" class="tn-input" [(ngModel)]="cx.customerPenalty" /></div>
                <div class="tn-field"><label for="cx-rs">Refund from supplier</label><input id="cx-rs" type="number" min="0" step="0.01" class="tn-input" [(ngModel)]="cx.refundFromSupplier" /></div>
                <div class="tn-field"><label for="cx-rc">Refund to customer</label><input id="cx-rc" type="number" min="0" step="0.01" class="tn-input" [(ngModel)]="cx.refundToCustomer" /></div>
              </div>
            }
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          </div>
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="cancelOpen.set(false)">Back</button>
            <button mat-flat-button color="warn" [disabled]="busy() || !cx.reason.trim() || (needsTicketOutcome() && !cx.ticketOutcome)" (click)="submitCancel()">Record cancellation</button>
          </div>
        </ui-modal>
      } @else {
        <p class="text-sm text-fleet-text-muted">Loading booking…</p>
      }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelBookingDetailComponent {
  private readonly api = inject(TravelBookingService);
  private readonly customersApi = inject(TravelCustomerService);
  private readonly groupsApi = inject(TravelGroupService);
  private readonly toast = inject(UiToastService);
  private readonly confirmDialog = inject(UiConfirmService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly id = Number(inject(ActivatedRoute).snapshot.paramMap.get('id'));

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly statusVariant = BOOKING_STATUS_VARIANT;
  protected readonly flightLabel = FLIGHT_STATUS_LABEL;
  protected readonly flightVariant = FLIGHT_STATUS_VARIANT;
  protected readonly directionLabel = DIRECTION_LABEL;
  protected readonly cabinLabel = CABIN_LABEL;
  protected readonly ticketVariant = TICKET_STATUS_VARIANT;
  protected readonly hotelLabel = HOTEL_BOOKING_STATUS_LABEL;
  protected readonly hotelVariant = HOTEL_BOOKING_STATUS_VARIANT;
  protected readonly hotelStepLabel = HOTEL_TRANSITION_LABEL;
  protected readonly hotelSteps: TravelHotelTransitionAction[] = ['Confirm', 'CheckIn', 'CheckOut', 'NoShow'];
  protected readonly mealLabel = MEAL_PLAN_LABEL;
  protected readonly refundLabel = REFUND_STATUS_LABEL;
  protected readonly refundVariant = REFUND_STATUS_VARIANT;
  protected readonly utc = asUtc;

  protected readonly booking = signal<TravelBooking | null>(null);
  protected readonly group = signal<TravelGroup | null>(null);
  protected readonly candidates = signal<BookingCandidate[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly dialogError = signal<string | null>(null);
  protected readonly active = signal('services');

  protected readonly suppliers = toSignal(inject(TravelSupplierService).getSuppliers().pipe(catchError(() => of([] as TravelSupplier[]))), { initialValue: [] as TravelSupplier[] });
  protected readonly airports = toSignal(inject(TravelSupplierService).airports$.pipe(catchError(() => of([]))), { initialValue: [] });
  protected readonly hotelCatalog = toSignal(inject(TravelPackageService).getHotels(undefined, true).pipe(catchError(() => of([] as TravelHotel[]))), { initialValue: [] as TravelHotel[] });
  protected readonly defaultCurrency = toSignal(inject(TravelConfigService).getSettings().pipe(map(s => s.defaultCurrencyCode), catchError(() => of(null))),
    { initialValue: null as string | null });

  protected readonly tabs = computed<UiTab[]>(() => [
    { id: 'services', label: 'Services', icon: 'luggage' },
    { id: 'itinerary', label: 'Itinerary', icon: 'route' },
    { id: 'cancellations', label: `Cancellations (${this.booking()?.cancellations.length ?? 0})`, icon: 'assignment_return' },
    { id: 'history', label: 'History', icon: 'history' }
  ]);
  protected readonly itinerary = computed(() => {
    const b = this.booking();
    return b ? itineraryRows(b, this.group()?.transport ?? []) : [];
  });
  protected readonly canCancel = computed(() => this.auth.hasPermission(TRAVEL_PERMISSIONS.bookingCancel));

  // Drawers and dialogs
  protected readonly flightOpen = signal(false);
  protected readonly hotelOpen = signal(false);
  protected readonly editingFlight = signal<TravelFlightBooking | null>(null);
  protected readonly editingHotel = signal<TravelHotelBooking | null>(null);
  protected readonly fileOpen = signal(false);
  protected readonly fileAction = signal<TravelBookingTransitionAction>('Complete');
  protected readonly editOpen = signal(false);
  protected readonly stepOpen = signal(false);
  protected readonly stepAction = signal<'Hold' | 'Confirm'>('Hold');
  protected readonly stepFlight = signal<TravelFlightBooking | null>(null);
  protected readonly ticketsOpen = signal(false);
  protected readonly ticketFlight = signal<TravelFlightBooking | null>(null);
  protected readonly ticketRows = signal<{ passengerId: number; name: string; ticketNumber: string }[]>([]);
  protected readonly confirmHotelOpen = signal(false);
  protected readonly confirmTarget = signal<TravelHotelBooking | null>(null);
  protected readonly cancelOpen = signal(false);
  protected readonly cancelTarget = signal<CancelTarget | null>(null);
  protected note = '';
  protected editTitle = '';
  protected editNotes = '';
  protected stepPnr = '';
  protected stepLimit = '';
  protected confirmationNo = '';
  protected cx: CancelTravelService = this.blankCancel();
  private readonly finance = inject(TravelFinanceService);
  protected readonly creditOpen = signal(false);
  protected readonly creditInvoices = signal<TravelInvoiceListItem[]>([]);
  private creditCancellation: TravelBookingCancellation | null = null;
  private creditKey = newRequestKey();
  protected creditInvoiceId: number | null = null;
  protected creditAmount = 0;
  protected creditReason = '';

  protected readonly cancelTitle = computed(() => {
    const t = this.cancelTarget();
    if (!t) return '';
    return t.kind === 'hotel' ? `Cancel ${t.hotel.hotelName}` : t.kind === 'flight' ? `Cancel flight ${t.flight.pnr ?? ''}` : `Void / refund ticket · ${t.passenger.travelerName}`;
  });
  protected readonly cancelCurrency = computed(() => {
    const t = this.cancelTarget();
    return t ? (t.kind === 'hotel' ? t.hotel.currencyCode : t.flight.currencyCode) : null;
  });
  protected readonly needsTicketOutcome = computed(() => {
    const t = this.cancelTarget();
    return t?.kind === 'passenger' || (t?.kind === 'flight' && t.flight.passengers.some(p => p.ticketStatus === 'Issued'));
  });

  constructor() { this.load(); }

  load(): void {
    this.api.getBooking(this.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: b => {
        const first = !this.booking();
        this.booking.set(b);
        this.error.set(null);
        if (first) this.loadCandidates(b);
        else if (b.groupId) this.loadGroup(b.groupId);
      },
      error: err => this.error.set(apiErrorMessage(err, 'Could not load the booking.'))
    });
  }

  private loadCandidates(b: TravelBooking): void {
    if (b.groupId) { this.loadGroup(b.groupId); return; }
    if (b.customerId === null) return;
    const departure = b.firstTravelDate ?? new Date().toISOString();
    this.customersApi.getTravelers(b.customerId).pipe(catchError(() => of([] as TravelTraveler[])), takeUntilDestroyed(this.destroyRef))
      .subscribe(list => this.candidates.set(list.map(t => ({
        travelerId: t.id, name: t.fullName, groupMemberId: null, passengerType: defaultPriceCategory(t.dateOfBirth, departure)
      }))));
  }

  private loadGroup(groupId: number): void {
    this.groupsApi.getGroup(groupId).pipe(catchError(() => of(null)), takeUntilDestroyed(this.destroyRef)).subscribe(g => {
      this.group.set(g);
      this.candidates.set((g?.members ?? []).filter(m => m.status !== 'Cancelled').map(m => ({
        travelerId: m.travelerId, name: m.travelerName, groupMemberId: m.id, passengerType: m.priceCategory
      })));
    });
  }

  protected can(a: string): boolean { return this.booking()?.allowedActions.includes(a as never) ?? false; }
  protected has(f: TravelFlightBooking, a: TravelFlightAction): boolean { return f.allowedActions.includes(a); }
  protected hasHotel(h: TravelHotelBooking, a: TravelHotelAction): boolean { return h.allowedActions.includes(a); }
  protected local(iso: string): string { return iso.substring(0, 16).replace('T', ' '); }
  protected rooms(h: TravelHotelBooking): string { return roomsLabel(h) || '—'; }
  protected guestNames(h: TravelHotelBooking): string { return h.guests.map(g => g.travelerName).join(', '); }
  protected limitSoon(iso: string): boolean { return isDue(iso, new Date(Date.now() + 24 * 3600_000)); }
  protected hoursLeft(iso: string): string {
    const ms = new Date(asUtc(iso)!).getTime() - Date.now();
    if (ms <= 0) return 'expired';
    const h = Math.floor(ms / 3600_000);
    return h >= 48 ? `${Math.floor(h / 24)} days left` : `${h} h left`;
  }
  protected money(n: number, c: TravelBookingCancellation): string {
    return c.currencyCode ? `${n.toLocaleString(undefined, { maximumFractionDigits: 2 })} ${c.currencyCode}` : '—';
  }
  protected historyLabel(t: string): string {
    return ({ FileStatus: 'File status', FlightStatus: 'Flight status', HotelStatus: 'Hotel status', TicketIssued: 'Ticket issued',
      TicketVoided: 'Ticket voided / refunded', Cancelled: 'Service cancelled', RefundStatus: 'Refund status', ServiceAdded: 'Service added',
      ServiceUpdated: 'Service updated' } as Record<string, string>)[t] ?? t;
  }

  private run(obs: Observable<unknown>, ok: string, onDone?: () => void, inDialog = true): void {
    this.busy.set(true);
    this.dialogError.set(null);
    obs.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); onDone?.(); this.toast.success(ok); this.load(); },
      error: err => {
        this.busy.set(false);
        const msg = apiErrorMessage(err, 'The change was refused.');
        if (inDialog) this.dialogError.set(msg); else this.toast.error(msg);
      }
    });
  }

  // ── File ──
  protected askFile(a: TravelBookingTransitionAction): void { this.fileAction.set(a); this.note = ''; this.dialogError.set(null); this.fileOpen.set(true); }
  protected fileTransition(): void {
    this.run(this.api.transition(this.id, this.fileAction(), this.note.trim() || null), 'Booking file updated.', () => this.fileOpen.set(false));
  }
  protected openEdit(): void {
    const b = this.booking()!;
    this.editTitle = b.title;
    this.editNotes = b.notes ?? '';
    this.dialogError.set(null);
    this.editOpen.set(true);
  }
  protected saveEdit(): void {
    const b = this.booking()!;
    this.run(this.api.update(this.id, { title: this.editTitle.trim(), enquiryId: b.enquiryId, visaCaseId: b.visaCaseId,
      assignedAgentUserId: b.assignedAgentUserId, notes: this.editNotes.trim() || null }), 'Booking saved.', () => this.editOpen.set(false));
  }

  // ── Flights ──
  protected openFlight(f: TravelFlightBooking | null): void { this.editingFlight.set(f); this.flightOpen.set(true); }
  protected openFlightStep(f: TravelFlightBooking, a: 'Hold' | 'Confirm'): void {
    this.stepFlight.set(f);
    this.stepAction.set(a);
    this.stepPnr = f.pnr ?? '';
    this.stepLimit = toDateTimeLocal(f.ticketingTimeLimitUtc);
    this.dialogError.set(null);
    this.stepOpen.set(true);
  }
  protected flightStep(): void {
    const f = this.stepFlight()!;
    const limit = this.stepAction() === 'Hold' ? fromDateTimeLocal(this.stepLimit) : null;
    this.run(this.api.transitionFlight(this.id, f.id, this.stepAction(), this.stepPnr.trim(), limit, null),
      this.stepAction() === 'Hold' ? 'Flight on hold.' : 'Flight confirmed.', () => this.stepOpen.set(false));
  }
  protected ticket(f: TravelFlightBooking): void {
    this.run(this.api.transitionFlight(this.id, f.id, 'Ticket', null, null, null), 'Flight ticketed.', undefined, false);
  }
  protected openTickets(f: TravelFlightBooking): void {
    this.ticketFlight.set(f);
    this.ticketRows.set(f.passengers.filter(p => p.ticketStatus === 'NotIssued' || p.ticketStatus === 'Issued')
      .map(p => ({ passengerId: p.id, name: p.travelerName, ticketNumber: p.ticketNumber ?? '' })));
    this.dialogError.set(null);
    this.ticketsOpen.set(true);
  }
  protected saveTickets(): void {
    const tickets = this.ticketRows().filter(t => t.ticketNumber.trim()).map(t => ({ passengerId: t.passengerId, ticketNumber: t.ticketNumber.trim() }));
    if (!tickets.length) { this.dialogError.set('Enter at least one ticket number.'); return; }
    this.run(this.api.setTickets(this.id, this.ticketFlight()!.id, tickets), 'Ticket numbers saved.', () => this.ticketsOpen.set(false));
  }
  protected async deleteFlight(f: TravelFlightBooking): Promise<void> {
    if (!(await this.confirmDialog.confirmDelete('Remove this requested flight? Nothing has been booked with the airline yet.'))) return;
    this.run(this.api.deleteFlight(this.id, f.id), 'Flight removed.', undefined, false);
  }

  // ── Hotels ──
  protected openHotel(h: TravelHotelBooking | null): void { this.editingHotel.set(h); this.hotelOpen.set(true); }
  protected hotelStep(h: TravelHotelBooking, a: TravelHotelTransitionAction): void {
    if (a === 'Confirm') {
      this.confirmTarget.set(h);
      this.confirmationNo = h.confirmationNumber ?? '';
      this.dialogError.set(null);
      this.confirmHotelOpen.set(true);
      return;
    }
    this.run(this.api.transitionHotel(this.id, h.id, a, null, null), `${HOTEL_TRANSITION_LABEL[a]}: done.`, undefined, false);
  }
  protected confirmHotel(): void {
    this.run(this.api.transitionHotel(this.id, this.confirmTarget()!.id, 'Confirm', this.confirmationNo.trim(), null), 'Hotel booking confirmed.',
      () => this.confirmHotelOpen.set(false));
  }
  protected async deleteHotel(h: TravelHotelBooking): Promise<void> {
    if (!(await this.confirmDialog.confirmDelete(`Remove the requested booking at ${h.hotelName}?`))) return;
    this.run(this.api.deleteHotel(this.id, h.id), 'Hotel booking removed.', undefined, false);
  }
  protected voucher(h: TravelHotelBooking): void {
    const b = this.booking()!;
    try { buildVoucherPdf(b, h).save(voucherFileName(b, h)); } catch { this.toast.error('Could not build the voucher.'); }
  }
  protected itineraryPdf(): void {
    const b = this.booking()!;
    try { buildItineraryPdf(b, this.group()?.transport ?? []).save(itineraryFileName(b)); } catch { this.toast.error('Could not build the itinerary.'); }
  }

  // ── Cancellations ──
  private blankCancel(): CancelTravelService {
    return { reason: '', supplierPenalty: 0, customerPenalty: 0, refundFromSupplier: 0, refundToCustomer: 0, ticketOutcome: null };
  }
  protected openCancel(t: CancelTarget): void {
    this.cancelTarget.set(t);
    this.cx = this.blankCancel();
    this.dialogError.set(null);
    this.cancelOpen.set(true);
  }
  protected submitCancel(): void {
    const t = this.cancelTarget()!;
    const n = (v: number) => Number(v) || 0;
    const dto: CancelTravelService = {
      reason: this.cx.reason.trim(), supplierPenalty: n(this.cx.supplierPenalty), customerPenalty: n(this.cx.customerPenalty),
      refundFromSupplier: n(this.cx.refundFromSupplier), refundToCustomer: n(this.cx.refundToCustomer),
      ticketOutcome: this.needsTicketOutcome() ? this.cx.ticketOutcome : null
    };
    const obs = t.kind === 'hotel' ? this.api.cancelHotel(this.id, t.hotel.id, dto)
      : t.kind === 'flight' ? this.api.cancelFlight(this.id, t.flight.id, dto)
      : this.api.cancelPassenger(this.id, t.flight.id, t.passenger.id, dto);
    this.run(obs, 'Cancellation recorded.', () => this.cancelOpen.set(false));
  }
  protected openCredit(c: TravelBookingCancellation): void {
    const b = this.booking()!;
    this.creditCancellation = c;
    this.creditKey = newRequestKey();
    this.creditAmount = c.refundToCustomer;
    this.creditReason = `Cancellation: ${c.serviceLabel} — ${c.reason}`.substring(0, 500);
    this.creditInvoiceId = null;
    this.creditInvoices.set([]);
    this.dialogError.set(null);
    this.creditOpen.set(true);
    this.finance.getInvoices({ page: 1, pageSize: 50, customerId: b.customerId, status: 'Issued', currencyCode: c.currencyCode ?? undefined })
      .pipe(catchError(() => of(null)), takeUntilDestroyed(this.destroyRef)).subscribe(r => this.creditInvoices.set(r?.items ?? []));
  }
  protected issueCredit(): void {
    this.run(this.finance.issueCreditNote({ idempotencyKey: this.creditKey, invoiceId: this.creditInvoiceId!, amount: Number(this.creditAmount),
      reason: this.creditReason.trim(), cancellationId: this.creditCancellation!.id, issuedOn: null }), 'Credit note issued.', () => this.creditOpen.set(false));
  }

  protected refund(c: TravelBookingCancellation, next: TravelRefundStatus): void {
    this.run(this.api.updateRefundStatus(this.id, c.id, next, null), `Refund: ${REFUND_STATUS_LABEL[next]}.`, undefined, false);
  }
}
