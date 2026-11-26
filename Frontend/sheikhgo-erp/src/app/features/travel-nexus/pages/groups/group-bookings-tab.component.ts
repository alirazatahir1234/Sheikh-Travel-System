import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterLink } from '@angular/router';
import { UiModalComponent, UiStatusBadgeComponent } from '../../../../shared/components/ui';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { BOOKING_STATUS_VARIANT, TRAVEL_PERMISSIONS, TravelBookingListItem, TravelGroup } from '../../models/travel.models';
import { TravelBookingService } from '../../services/travel-booking.service';

/** Group detail → Bookings: the group's booking files (flights for every pilgrim, hotel bookings for its stays). */
@Component({
  selector: 'app-group-bookings-tab',
  standalone: true,
  imports: [DatePipe, FormsModule, RouterLink, MatButtonModule, MatIconModule, UiModalComponent, UiStatusBadgeComponent, HasPermissionDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-card">
      <div class="flex flex-wrap items-center justify-between gap-3 border-b border-fleet-border px-5 py-4">
        <div><h3 class="tn-section-title">Bookings</h3>
          <p class="text-xs text-fleet-text-muted">Mark ready needs every confirmed pilgrim ticketed on an outbound and a return flight (or a round trip) in these files.
            {{ group().readiness.membersWithoutTickets }} pilgrim(s) still need tickets.</p></div>
        @if (canCreate()) {
          <button mat-flat-button color="primary" *appHasPermission="perms.bookingManage" (click)="openCreate()"><mat-icon>add</mat-icon> Create group booking</button>
        }
      </div>
      @if (error()) { <div class="tn-card-body"><div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div></div> }
      @if (!rows().length && !error()) { <p class="px-5 py-8 text-center text-sm text-fleet-text-muted">No booking files for this group yet.</p> }
      <ul class="divide-y divide-fleet-border">
        @for (r of rows(); track r.id) {
          <li><a class="flex flex-wrap items-center justify-between gap-2 px-5 py-3 text-sm hover:bg-fleet-surface-alt/40" [routerLink]="['/travel/bookings', r.id]">
            <span><span class="font-mono text-xs">{{ r.bookingNumber }}</span> · <strong>{{ r.title }}</strong> · {{ r.flightCount }} flight(s), {{ r.hotelCount }} hotel(s)
              @if (r.pnrs) { <span class="font-mono text-xs"> · {{ r.pnrs }}</span> }
              @if (r.firstTravelDate) { · {{ r.firstTravelDate | date: 'mediumDate': 'UTC' }} }</span>
            <ui-status-badge [status]="r.status" [variant]="statusVariant[r.status]"></ui-status-badge></a></li>
        }
      </ul>
    </div>

    <ui-modal [(open)]="createOpen" title="Create group booking" size="sm">
      <div class="space-y-3">
        <div class="tn-field"><label for="gb-title" class="tn-required">Title</label>
          <input id="gb-title" class="tn-input" maxlength="150" [(ngModel)]="title" /></div>
        <p class="text-xs text-fleet-text-muted">Then add the flights (select all pilgrims) and a hotel booking per stay.</p>
        @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
      </div>
      <div modal-footer class="tn-actions">
        <button mat-stroked-button (click)="createOpen.set(false)">Cancel</button>
        <button mat-flat-button color="primary" [disabled]="busy() || !title.trim()" (click)="create()">Create and open</button>
      </div>
    </ui-modal>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class GroupBookingsTabComponent {
  private readonly api = inject(TravelBookingService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly group = input.required<TravelGroup>();

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly statusVariant = BOOKING_STATUS_VARIANT;
  protected readonly rows = signal<TravelBookingListItem[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly createOpen = signal(false);
  protected readonly busy = signal(false);
  protected readonly dialogError = signal<string | null>(null);
  protected title = '';

  constructor() {
    effect(() => {
      const id = this.group().id;
      untracked(() => this.api.getByGroup(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: r => { this.rows.set(r); this.error.set(null); },
        error: err => this.error.set(apiErrorMessage(err, 'Could not load the group bookings.'))
      }));
    });
  }

  protected canCreate(): boolean { return !['Closed', 'Cancelled'].includes(this.group().status); }

  protected openCreate(): void {
    this.title = `${this.group().name} — flights and hotels`;
    this.dialogError.set(null);
    this.createOpen.set(true);
  }

  protected create(): void {
    this.busy.set(true);
    this.api.create({ customerId: null, groupId: this.group().id, title: this.title.trim(), enquiryId: null, visaCaseId: this.group().visaCaseId,
      assignedAgentUserId: null, branchId: this.group().branchId, notes: null }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: id => { this.busy.set(false); this.createOpen.set(false); this.router.navigate(['/travel/bookings', id]); },
      error: err => { this.busy.set(false); this.dialogError.set(apiErrorMessage(err, 'Could not create the booking.')); }
    });
  }
}
