import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { UiModalComponent, UiPageHeaderComponent, UiStatusBadgeComponent, UiTab, UiTabsComponent } from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { AuthService } from '../../../../core/services/auth.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  GROUP_STATUS_LABEL, GROUP_STATUS_VARIANT, GROUP_TRANSITION_LABEL, TRAVEL_PERMISSIONS, TravelGroup, TravelGroupAction,
  TravelGroupTransitionAction, VISA_STATUS_LABEL
} from '../../models/travel.models';
import { asUtc, toDateInput } from '../../models/travel-date.util';
import { TravelGroupService } from '../../services/travel-group.service';
import { GroupBookingsTabComponent } from './group-bookings-tab.component';
import { GroupFinanceTabComponent } from './group-finance-tab.component';
import { GroupManifestTabComponent } from './group-manifest-tab.component';
import { GroupPilgrimsTabComponent } from './group-pilgrims-tab.component';
import { GroupRoomsTabComponent } from './group-rooms-tab.component';
import { GroupTransportTabComponent } from './group-transport-tab.component';

const TRANSITIONS: TravelGroupTransitionAction[] =
  ['Open', 'CloseRegistration', 'Reopen', 'StartVisaProcessing', 'MarkReady', 'Unready', 'Depart', 'Return', 'Close'];

@Component({
  selector: 'app-travel-group-detail',
  standalone: true,
  imports: [DatePipe, FormsModule, RouterLink, MatButtonModule, MatIconModule, UiPageHeaderComponent, UiStatusBadgeComponent, UiTabsComponent,
    UiModalComponent, HasPermissionDirective, GroupPilgrimsTabComponent, GroupRoomsTabComponent, GroupTransportTabComponent, GroupManifestTabComponent, GroupBookingsTabComponent, GroupFinanceTabComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      @if (error()) {
        <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div>
      } @else if (group(); as g) {
        <ui-page-header [title]="g.name" eyebrow="TravelNexus · Umrah groups" icon="groups" [subtitle]="g.groupNumber + ' · ' + g.packageName"
          [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Umrah groups', route: '/travel/groups' }, { label: g.groupNumber }]">
          <ui-status-badge [status]="g.status" [label]="statusLabel[g.status]" [variant]="statusVariant[g.status]"></ui-status-badge>
        </ui-page-header>

        <ui-tabs [tabs]="tabs()" [(activeId)]="active"></ui-tabs>

        @switch (active()) {
          @case ('overview') {
            <div class="grid gap-5 lg:grid-cols-3">
              <div class="tn-card tn-card-body space-y-4">
                <div class="flex items-center justify-between">
                  <h3 class="tn-section-title">Group</h3>
                  @if (can('Edit')) { <button mat-stroked-button *appHasPermission="perms.groupManage" (click)="openEdit()"><mat-icon>edit</mat-icon> Edit</button> }
                </div>
                <dl class="tn-kv">
                  <div><dt>Package</dt><dd><a class="text-fleet-primary underline" [routerLink]="['/travel/packages', g.packageId]">{{ g.packageName }}</a></dd></div>
                  <div><dt>Dates</dt><dd>{{ g.departureDate | date: 'mediumDate': 'UTC' }} → {{ g.returnDate | date: 'mediumDate': 'UTC' }}</dd></div>
                  <div><dt>Pilgrims</dt><dd>{{ g.activeMembers }} / {{ g.capacity }} ({{ g.confirmedMembers }} confirmed)</dd></div>
                  <div><dt>Leader</dt><dd>{{ g.leaderDisplayName || '—' }}{{ g.leaderPhone ? ' · ' + g.leaderPhone : '' }}</dd></div>
                  <div><dt>Visa case</dt><dd>
                    @if (g.visaCaseId) {
                      <a class="text-fleet-primary underline" [routerLink]="['/travel/visa-cases', g.visaCaseId]">{{ g.visaCaseNumber }}</a>
                      @if (g.visaCaseStatus) { <span class="text-xs text-fleet-text-muted"> · {{ visaStatusLabel[g.visaCaseStatus] }}</span> }
                    } @else { Opens at "Start visa processing" }
                  </dd></div>
                  <div><dt>Branch</dt><dd>{{ g.branchName || '—' }}</dd></div>
                  @if (g.cancelReason) { <div><dt>Cancel reason</dt><dd>{{ g.cancelReason }}</dd></div> }
                  @if (g.closedAt) { <div><dt>Closed</dt><dd>{{ utc(g.closedAt) | date: 'medium' }}</dd></div> }
                </dl>
                @if (g.notes) { <p class="whitespace-pre-line text-sm text-fleet-text-muted">{{ g.notes }}</p> }
              </div>

              <div class="space-y-5 lg:col-span-2">
                <div class="tn-card tn-card-body space-y-3">
                  <h3 class="tn-section-title">Next steps</h3>
                  @if (transitions().length) {
                    <div class="flex flex-wrap gap-2" *appHasPermission="perms.groupManage">
                      @for (a of transitions(); track a) {
                        <button mat-flat-button [color]="a === 'Unready' || a === 'Reopen' ? undefined : 'primary'" [disabled]="busy()" (click)="ask(a)">{{ transitionLabel[a] }}</button>
                      }
                      @if (can('Cancel')) { <button mat-stroked-button color="warn" [disabled]="busy()" (click)="ask('Cancel')">{{ transitionLabel.Cancel }}</button> }
                    </div>
                  } @else {
                    <p class="text-sm text-fleet-text-muted">No further status changes for this group.</p>
                  }
                </div>

                <div class="tn-card tn-card-body space-y-2">
                  <h3 class="tn-section-title">Readiness for departure</h3>
                  <ul class="space-y-1 text-sm">
                    @for (c of checklist(); track c.label) {
                      <li class="flex items-center gap-2">
                        <mat-icon class="!text-[18px]" [class.text-emerald-600]="c.ok" [class.text-fleet-text-muted]="!c.ok">{{ c.ok ? 'check_circle' : 'radio_button_unchecked' }}</mat-icon>
                        {{ c.label }}
                      </li>
                    }
                  </ul>
                  @if (g.readiness.membersWithoutTickets > 0) {
                    <button mat-button class="!px-0" (click)="active.set('bookings')"><mat-icon>airplane_ticket</mat-icon> Ticket pilgrims in the group's bookings</button>
                  }
                </div>
              </div>
            </div>
          }
          @case ('pilgrims') { <app-group-pilgrims-tab [group]="g" (changed)="load()" /> }
          @case ('rooms') { <app-group-rooms-tab [group]="g" (changed)="load()" /> }
          @case ('transport') { <app-group-transport-tab [group]="g" (changed)="load()" /> }
          @case ('bookings') { <app-group-bookings-tab [group]="g" /> }
          @case ('finance') { <app-group-finance-tab [group]="g" /> }
          @case ('manifest') { <app-group-manifest-tab [group]="g" (generated)="load()" /> }
          @case ('history') {
            <div class="tn-card tn-card-body">
              @if (!g.history.length) { <p class="text-sm text-fleet-text-muted">No history yet.</p> }
              <ol class="tn-timeline">
                @for (h of g.history; track h.id) {
                  <li>
                    <span class="tn-timeline-icon"><mat-icon>{{ historyIcon(h.eventType) }}</mat-icon></span>
                    <div class="flex-1 text-sm">
                      <p><strong>{{ historyLabel(h.eventType) }}</strong>
                        @if (h.memberName) { · {{ h.memberName }} }
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

        <ui-modal [(open)]="confirmOpen" [title]="pending() ? transitionLabel[pending()!] : ''" size="sm">
          <div class="space-y-3">
            @if (pending() === 'StartVisaProcessing') {
              <p class="text-sm">Opens one visa case for the group with all {{ g.confirmedMembers }} confirmed pilgrims as applicants.</p>
            }
            @if (pending() === 'Cancel') { <p class="text-sm">Cancelling cannot be undone.</p> }
            <div class="tn-field"><label for="tr-note" [class.tn-required]="pending() === 'Cancel'">{{ pending() === 'Cancel' ? 'Reason' : 'Note' }}</label>
              <textarea id="tr-note" class="tn-input" rows="3" maxlength="1000" [(ngModel)]="note"></textarea></div>
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          </div>
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="confirmOpen.set(false)">Back</button>
            <button mat-flat-button [color]="pending() === 'Cancel' ? 'warn' : 'primary'" [disabled]="busy() || (pending() === 'Cancel' && !note.trim())" (click)="transition()">Confirm</button>
          </div>
        </ui-modal>

        <ui-modal [(open)]="editOpen" title="Edit group" size="md">
          <div class="space-y-4">
            <div class="tn-field"><label for="ge-name" class="tn-required">Name</label><input id="ge-name" class="tn-input" maxlength="150" [(ngModel)]="edit.name" /></div>
            <div class="tn-grid">
              <div class="tn-field"><label for="ge-dep" class="tn-required">Departure</label><input id="ge-dep" type="date" class="tn-input" [(ngModel)]="edit.departureDate" /></div>
              <div class="tn-field"><label for="ge-ret" class="tn-required">Return</label><input id="ge-ret" type="date" class="tn-input" [(ngModel)]="edit.returnDate" /></div>
              <div class="tn-field"><label for="ge-cap" class="tn-required">Capacity</label><input id="ge-cap" type="number" min="1" class="tn-input" [(ngModel)]="edit.capacity" /></div>
            </div>
            <div class="tn-field"><label for="ge-lead">Group leader (pilgrim)</label>
              <select id="ge-lead" class="tn-input" [(ngModel)]="edit.groupLeaderTravelerId">
                <option [ngValue]="null">None — staff leader below</option>
                @for (m of activeMembers(); track m.id) { <option [ngValue]="m.travelerId">{{ m.travelerName }}</option> }
              </select></div>
            @if (edit.groupLeaderTravelerId === null) {
              <div class="tn-grid">
                <div class="tn-field"><label for="ge-ln">Staff leader name</label><input id="ge-ln" class="tn-input" maxlength="150" [(ngModel)]="edit.leaderName" /></div>
                <div class="tn-field"><label for="ge-lp">Leader phone</label><input id="ge-lp" class="tn-input" maxlength="30" [(ngModel)]="edit.leaderPhone" /></div>
              </div>
            }
            <div class="tn-field"><label for="ge-notes">Notes</label><textarea id="ge-notes" class="tn-input" rows="3" maxlength="2000" [(ngModel)]="edit.notes"></textarea></div>
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          </div>
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="editOpen.set(false)">Cancel</button>
            <button mat-flat-button color="primary" [disabled]="busy() || !edit.name.trim() || !edit.departureDate || !edit.returnDate" (click)="saveEdit()">Save</button>
          </div>
        </ui-modal>
      } @else {
        <p class="text-sm text-fleet-text-muted">Loading group…</p>
      }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelGroupDetailComponent {
  private readonly api = inject(TravelGroupService);
  private readonly toast = inject(UiToastService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly id = Number(inject(ActivatedRoute).snapshot.paramMap.get('id'));

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly statusLabel = GROUP_STATUS_LABEL;
  protected readonly statusVariant = GROUP_STATUS_VARIANT;
  protected readonly transitionLabel = GROUP_TRANSITION_LABEL;
  protected readonly visaStatusLabel = VISA_STATUS_LABEL;
  protected readonly utc = asUtc;

  protected readonly group = signal<TravelGroup | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly dialogError = signal<string | null>(null);
  protected readonly active = signal('overview');
  protected readonly confirmOpen = signal(false);
  protected readonly editOpen = signal(false);
  protected readonly pending = signal<TravelGroupTransitionAction | null>(null);
  protected note = '';
  protected edit = { name: '', departureDate: '', returnDate: '', capacity: 1, groupLeaderTravelerId: null as number | null, leaderName: '', leaderPhone: '', notes: '' };

  protected readonly tabs = computed<UiTab[]>(() => {
    const g = this.group();
    const t: UiTab[] = [
      { id: 'overview', label: 'Overview', icon: 'dashboard' },
      { id: 'pilgrims', label: `Pilgrims (${g?.activeMembers ?? 0})`, icon: 'people' },
      { id: 'rooms', label: 'Hotels & rooms', icon: 'hotel' },
      { id: 'transport', label: 'Transport', icon: 'directions_bus' }
    ];
    if (this.auth.hasPermission(TRAVEL_PERMISSIONS.bookingView)) t.push({ id: 'bookings', label: 'Bookings', icon: 'airplane_ticket' });
    if (this.auth.hasPermission(TRAVEL_PERMISSIONS.financeView)) t.push({ id: 'finance', label: 'Finance', icon: 'payments' });
    if (this.auth.hasPermission(TRAVEL_PERMISSIONS.groupManifest)) t.push({ id: 'manifest', label: 'Manifest', icon: 'list_alt' });
    t.push({ id: 'history', label: 'History', icon: 'history' });
    return t;
  });
  protected readonly transitions = computed(() => TRANSITIONS.filter(a => this.can(a)));
  protected readonly activeMembers = computed(() => (this.group()?.members ?? []).filter(m => m.status !== 'Cancelled'));
  protected readonly checklist = computed(() => {
    const r = this.group()?.readiness;
    if (!r) return [];
    return [
      { ok: r.confirmedMembers > 0 && r.unconfirmedMembers === 0, label: `All pilgrims confirmed (${r.confirmedMembers} confirmed, ${r.unconfirmedMembers} pending)` },
      { ok: r.stays > 0, label: `Hotel stays planned (${r.stays})` },
      { ok: r.visaCaseDecided, label: 'Group visa case decided' },
      { ok: r.confirmedMembers > 0 && r.membersWithApprovedVisa === r.confirmedMembers, label: `Approved visas (${r.membersWithApprovedVisa} of ${r.confirmedMembers})` },
      { ok: r.confirmedMembers > 0 && r.membersWithoutRoom === 0, label: `Every pilgrim has a room in every stay (${r.membersWithoutRoom} missing)` },
      { ok: r.confirmedMembers > 0 && r.membersWithoutTickets === 0, label: `Flights confirmed: every pilgrim ticketed out and back (${r.membersWithoutTickets} missing)` }
    ];
  });

  constructor() { this.load(); }

  load(): void {
    this.api.getGroup(this.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: g => { this.group.set(g); this.error.set(null); },
      error: err => this.error.set(apiErrorMessage(err, 'Could not load the group.'))
    });
  }

  protected can(a: TravelGroupAction): boolean { return this.group()?.allowedActions.includes(a) ?? false; }

  protected ask(a: TravelGroupTransitionAction): void {
    this.pending.set(a);
    this.note = '';
    this.dialogError.set(null);
    this.confirmOpen.set(true);
  }

  protected transition(): void {
    const a = this.pending();
    if (!a) return;
    this.busy.set(true);
    this.api.transition(this.id, a, this.note.trim() || null).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); this.confirmOpen.set(false); this.toast.success(`${this.transitionLabel[a]}: done.`); this.load(); },
      error: err => { this.busy.set(false); this.dialogError.set(apiErrorMessage(err, 'The status change was refused.')); }
    });
  }

  protected openEdit(): void {
    const g = this.group()!;
    this.edit = {
      name: g.name, departureDate: toDateInput(g.departureDate), returnDate: toDateInput(g.returnDate), capacity: g.capacity,
      groupLeaderTravelerId: g.groupLeaderTravelerId, leaderName: g.leaderName ?? '', leaderPhone: g.leaderPhone ?? '', notes: g.notes ?? ''
    };
    this.dialogError.set(null);
    this.editOpen.set(true);
  }

  protected saveEdit(): void {
    const e = this.edit;
    this.busy.set(true);
    this.api.update(this.id, {
      name: e.name.trim(), departureDate: e.departureDate, returnDate: e.returnDate, capacity: Number(e.capacity),
      groupLeaderTravelerId: e.groupLeaderTravelerId, leaderName: e.groupLeaderTravelerId ? null : e.leaderName.trim() || null,
      leaderPhone: e.groupLeaderTravelerId ? null : e.leaderPhone.trim() || null, notes: e.notes.trim() || null
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); this.editOpen.set(false); this.toast.success('Group saved.'); this.load(); },
      error: err => { this.busy.set(false); this.dialogError.set(apiErrorMessage(err, 'Could not save the group.')); }
    });
  }

  protected historyIcon(t: string): string {
    return ({ GroupStatus: 'flag', MemberRegistered: 'person_add', MemberConfirmed: 'how_to_reg', MemberCancelled: 'person_remove',
      RoomAssigned: 'hotel', RoomUnassigned: 'hotel', LeaderChanged: 'star', ManifestGenerated: 'list_alt' } as Record<string, string>)[t] ?? 'info';
  }
  protected historyLabel(t: string): string {
    return ({ GroupStatus: 'Status', MemberRegistered: 'Pilgrim registered', MemberConfirmed: 'Pilgrim confirmed', MemberCancelled: 'Pilgrim cancelled',
      RoomAssigned: 'Rooming saved', RoomUnassigned: 'Room unassigned', LeaderChanged: 'Leader changed', ManifestGenerated: 'Manifest generated' } as Record<string, string>)[t] ?? t;
  }
}
