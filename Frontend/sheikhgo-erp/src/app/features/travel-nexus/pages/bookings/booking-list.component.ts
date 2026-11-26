import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap, tap } from 'rxjs';
import { UiEmptyStateComponent, UiModalComponent, UiPageHeaderComponent, UiStatusBadgeComponent } from '../../../../shared/components/ui';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { statusFromQuery } from '../../models/travel-date.util';
import {
  BOOKING_STATUS_VARIANT, TRAVEL_BOOKING_STATUSES, TRAVEL_PERMISSIONS, TravelBookingListItem, TravelBookingStatus, TravelBookingSummary,
  TravelCustomerListItem
} from '../../models/travel.models';
import { asUtc, isDue } from '../../models/travel-date.util';
import { TravelCustomerPickerComponent } from '../../components/customer-picker.component';
import { TravelBookingService } from '../../services/travel-booking.service';

@Component({
  selector: 'app-travel-booking-list',
  standalone: true,
  imports: [DatePipe, FormsModule, MatButtonModule, MatIconModule, UiPageHeaderComponent, UiEmptyStateComponent, UiStatusBadgeComponent,
    UiModalComponent, HasPermissionDirective, TravelCustomerPickerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      <ui-page-header title="Bookings" eyebrow="TravelNexus" icon="airplane_ticket"
        subtitle="Booking files: flights with PNRs and tickets, hotel bookings, vouchers, itineraries and refunds."
        [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Bookings' }]">
        <button mat-flat-button color="primary" *appHasPermission="perms.bookingManage" (click)="openCreate()"><mat-icon>add</mat-icon> New booking</button>
      </ui-page-header>

      <section aria-label="Booking files by status (all time)">
        <div class="mb-2 flex items-center justify-between">
          <h3 class="tn-section-title">Booking files</h3>
          <span class="text-xs text-fleet-text-soft">All files, all time · total {{ summary()?.total ?? '–' }}</span>
        </div>
        <div class="tn-pipeline">
          @for (s of statuses; track s) {
            <button type="button" [class.active]="status() === s" (click)="setStatus(status() === s ? '' : s)">
              <small>{{ s }}</small><strong>{{ summary()?.counts?.[s] ?? '–' }}</strong>
            </button>
          }
        </div>
      </section>

      <div class="tn-card">
        <div class="flex flex-col gap-3 border-b border-fleet-border px-5 py-4 lg:flex-row lg:items-end">
          <div class="relative lg:w-72">
            <mat-icon class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 !text-[20px] text-fleet-text-muted">search</mat-icon>
            <input type="search" class="tn-input pl-10" placeholder="Booking no., title, customer, PNR…" aria-label="Search bookings"
              [ngModel]="search()" (ngModelChange)="searchInput$.next($event)" />
          </div>
          <select class="tn-input lg:w-44" aria-label="Owner" [ngModel]="ownerType()" (ngModelChange)="ownerType.set($event); reset()">
            <option value="">Customers and groups</option><option value="Customer">Customer bookings</option><option value="Group">Umrah group bookings</option>
          </select>
          <label class="tn-field lg:w-40"><span class="text-xs">Travel from</span>
            <input type="date" class="tn-input" [ngModel]="from()" (ngModelChange)="from.set($event); reset()" /></label>
          <label class="tn-field lg:w-40"><span class="text-xs">Travel to</span>
            <input type="date" class="tn-input" [ngModel]="to()" (ngModelChange)="to.set($event); reset()" /></label>
          <button type="button" class="tn-chip warn" [class.active]="due()" (click)="due.set(!due()); reset()"
            title="Flights on hold whose ticketing deadline is within the next 72 hours">
            <mat-icon class="!text-[16px]">schedule</mat-icon> Ticketing due · next 72 h <span>{{ summary()?.ticketingDueNext72Hours ?? '–' }}</span>
          </button>
        </div>
        @if (error()) {
          <div class="tn-card-body"><div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}
            <button mat-stroked-button class="ml-auto" (click)="reload()">Retry</button></div></div>
        } @else {
          <div class="overflow-x-auto">
            <table class="w-full min-w-[920px] text-left text-sm">
              <thead class="bg-fleet-surface-alt/60">
                <tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                  <th class="px-5 py-3">Booking</th><th class="px-5 py-3">Title</th><th class="px-5 py-3">For</th><th class="px-5 py-3">Travel</th>
                  <th class="px-5 py-3">Services</th><th class="px-5 py-3">PNR</th><th class="px-5 py-3">Status</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-fleet-border">
                @if (loading()) {
                  @for (s of [1,2,3]; track s) { <tr>@for (c of [1,2,3,4,5,6,7]; track c) { <td class="px-5 py-4"><div class="h-3.5 w-3/4 animate-pulse rounded bg-fleet-surface-muted"></div></td> }</tr> }
                } @else if (!rows().length) {
                  <tr><td colspan="7"><ui-empty-state icon="airplane_ticket" title="No bookings"
                    [description]="hasFilters() ? 'No booking files match these filters.' : 'Open a booking file for a customer, or from an Umrah group.'"></ui-empty-state></td></tr>
                } @else {
                  @for (r of rows(); track r.id) {
                    <tr class="cursor-pointer hover:bg-fleet-surface-alt/40" (click)="open(r.id)">
                      <td class="px-5 py-3 font-mono text-xs">{{ r.bookingNumber }}</td>
                      <td class="px-5 py-3 font-semibold">{{ r.title }}</td>
                      <td class="px-5 py-3">@if (r.groupId) { <mat-icon class="!text-[16px] align-middle text-fleet-text-muted" title="Umrah group">groups</mat-icon> } {{ r.ownerName }}</td>
                      <td class="px-5 py-3 whitespace-nowrap">{{ r.firstTravelDate ? (r.firstTravelDate | date: 'mediumDate': 'UTC') : '—' }}
                        @if (r.lastTravelDate && r.lastTravelDate !== r.firstTravelDate) { → {{ r.lastTravelDate | date: 'mediumDate': 'UTC' }} }</td>
                      <td class="px-5 py-3">{{ r.flightCount }} flight(s) · {{ r.hotelCount }} hotel(s)</td>
                      <td class="px-5 py-3 font-mono text-xs">{{ r.pnrs || '—' }}
                        @if (r.nextTicketingLimitUtc) {
                          <span class="block font-sans" [class.text-red-600]="due72(r.nextTicketingLimitUtc)">ticket by {{ utc(r.nextTicketingLimitUtc) | date: 'd MMM, HH:mm' }}</span>
                        }</td>
                      <td class="px-5 py-3"><ui-status-badge [status]="r.status" [variant]="statusVariant[r.status]"></ui-status-badge></td>
                    </tr>
                  }
                }
              </tbody>
            </table>
          </div>
          @if (!loading() && totalPages() > 1) {
            <div class="flex items-center justify-between border-t border-fleet-border px-5 py-3 text-sm">
              <span class="text-fleet-text-muted">Page {{ page() }} of {{ totalPages() }}</span>
              <div class="flex gap-2">
                <button mat-stroked-button [disabled]="page() <= 1" (click)="goTo(page() - 1)">Previous</button>
                <button mat-stroked-button [disabled]="page() >= totalPages()" (click)="goTo(page() + 1)">Next</button>
              </div>
            </div>
          }
        }
      </div>
    </div>

    <ui-modal [(open)]="createOpen" title="New booking file" size="sm">
      <div class="space-y-4">
        <div class="tn-field"><label for="bk-customer" class="tn-required">Customer</label>
          <app-travel-customer-picker inputId="bk-customer" [(selected)]="customer" /></div>
        <div class="tn-field"><label for="bk-title" class="tn-required">Title</label>
          <input id="bk-title" class="tn-input" maxlength="150" [(ngModel)]="title" placeholder="e.g. Dubai family holiday, March" /></div>
        <p class="text-xs text-fleet-text-muted">Group bookings are opened from the Umrah group's Bookings tab.</p>
        @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
      </div>
      <div modal-footer class="tn-actions">
        <button mat-stroked-button (click)="createOpen.set(false)">Cancel</button>
        <button mat-flat-button color="primary" [disabled]="busy() || !customer() || !title.trim()" (click)="create()">Open booking</button>
      </div>
    </ui-modal>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelBookingListComponent {
  private readonly api = inject(TravelBookingService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly statuses = TRAVEL_BOOKING_STATUSES;
  protected readonly statusVariant = BOOKING_STATUS_VARIANT;
  protected readonly utc = asUtc;

  protected readonly rows = signal<TravelBookingListItem[]>([]);
  protected readonly summary = signal<TravelBookingSummary | null>(null);
  protected readonly total = signal(0);
  protected readonly totalPages = signal(1);
  protected readonly page = signal(1);
  protected readonly search = signal('');
  /** `?status=` opens the list already filtered (dashboard pipeline tiles). */
  protected readonly status = signal<TravelBookingStatus | ''>(statusFromQuery(inject(ActivatedRoute).snapshot.queryParamMap.get('status'), TRAVEL_BOOKING_STATUSES));
  protected readonly ownerType = signal<'Customer' | 'Group' | ''>('');
  protected readonly from = signal('');
  protected readonly to = signal('');
  protected readonly due = signal(inject(ActivatedRoute).snapshot.queryParamMap.get('ticketingDue') === 'true');
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly searchInput$ = new Subject<string>();
  private readonly load$ = new Subject<void>();

  protected readonly createOpen = signal(false);
  protected readonly busy = signal(false);
  protected readonly dialogError = signal<string | null>(null);
  protected readonly customer = signal<TravelCustomerListItem | null>(null);
  protected title = '';

  constructor() {
    this.load$.pipe(
      tap(() => { this.loading.set(true); this.error.set(null); }),
      switchMap(() => this.api.getBookings({
        page: this.page(), pageSize: 20, search: this.search(), status: this.status(), ownerType: this.ownerType(),
        travelFrom: this.from() || undefined, travelTo: this.to() || undefined, ticketingDue: this.due()
      }).pipe(catchError(err => { this.error.set(apiErrorMessage(err, 'Could not load bookings.')); return of(null); }))),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(r => {
      if (r) { this.rows.set(r.items); this.total.set(r.totalCount); this.totalPages.set(r.totalPages); }
      this.loading.set(false);
    });
    this.searchInput$.pipe(debounceTime(350), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(term => { this.search.set(term ?? ''); this.reset(); });
    this.api.getSummary().pipe(catchError(() => of(null)), takeUntilDestroyed(this.destroyRef)).subscribe(s => this.summary.set(s));
    this.reload();
  }

  protected due72(iso: string): boolean { return isDue(iso, new Date(Date.now() + 72 * 3600_000)); }
  protected setStatus(s: TravelBookingStatus | ''): void { this.status.set(s); this.reset(); }
  protected reset(): void { this.page.set(1); this.reload(); }
  protected goTo(p: number): void { this.page.set(p); this.reload(); }
  protected reload(): void { this.load$.next(); }
  protected open(id: number): void { this.router.navigate(['/travel/bookings', id]); }
  protected hasFilters(): boolean { return !!(this.search() || this.status() || this.ownerType() || this.from() || this.to() || this.due()); }

  protected openCreate(): void {
    this.customer.set(null);
    this.title = '';
    this.dialogError.set(null);
    this.createOpen.set(true);
  }

  protected create(): void {
    this.busy.set(true);
    this.dialogError.set(null);
    this.api.create({ customerId: this.customer()!.customerId, groupId: null, title: this.title.trim(), enquiryId: null, visaCaseId: null,
      assignedAgentUserId: null, branchId: null, notes: null }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: id => { this.busy.set(false); this.createOpen.set(false); this.open(id); },
      error: err => { this.busy.set(false); this.dialogError.set(apiErrorMessage(err, 'Could not open the booking.')); }
    });
  }
}
