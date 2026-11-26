import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { catchError, of } from 'rxjs';
import { UiConfirmService, UiModalComponent } from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  ROOM_CAPACITY, TRAVEL_GENDER_POLICIES, TRAVEL_PERMISSIONS, TRAVEL_ROOM_TYPES, TravelGenderPolicy, TravelGroup, TravelGroupMember,
  TravelGroupStay, TravelHotel, TravelRoomType, TravelRoomingRoom
} from '../../models/travel.models';
import { toDateInput } from '../../models/travel-date.util';
import { TravelGroupService } from '../../services/travel-group.service';
import { TravelPackageService } from '../../services/travel-package.service';

/** Client-side hints for a draft room; the server re-validates everything on save. */
export function roomIssues(room: TravelRoomingRoom, members: TravelGroupMember[]): string[] {
  const issues: string[] = [];
  const inRoom = members.filter(m => room.memberIds.includes(m.id));
  if (room.memberIds.length > ROOM_CAPACITY[room.roomType]) issues.push(`over capacity (${room.memberIds.length}/${ROOM_CAPACITY[room.roomType]})`);
  if (room.genderPolicy !== 'Family' && inRoom.some(m => m.gender !== room.genderPolicy)) issues.push(`${room.genderPolicy.toLowerCase()} room has other genders`);
  if (room.genderPolicy === 'Family' && new Set(inRoom.map(m => m.customerId)).size > 1
      && !inRoom.every(m => inRoom.some(o => o.id !== m.id && (o.travelerId === m.mahramTravelerId || o.mahramTravelerId === m.travelerId)))) {
    issues.push('family room mixes booking parties without a mahram link');
  }
  return issues;
}

@Component({
  selector: 'app-group-rooms-tab',
  standalone: true,
  imports: [DatePipe, FormsModule, MatButtonModule, MatIconModule, UiModalComponent, HasPermissionDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="grid gap-5 lg:grid-cols-3">
      <div class="tn-card">
        <div class="flex items-center justify-between border-b border-fleet-border px-5 py-4">
          <h3 class="tn-section-title">Hotel stays</h3>
          @if (can('ManageStays')) { <button mat-stroked-button *appHasPermission="perms.groupManage" (click)="openStay(null)"><mat-icon>add</mat-icon> Add</button> }
        </div>
        @if (!group().stays.length) { <p class="px-5 py-6 text-sm text-fleet-text-muted">No stays yet.</p> }
        <ul class="divide-y divide-fleet-border">
          @for (s of group().stays; track s.id) {
            <li class="flex cursor-pointer items-start gap-2 px-5 py-3 hover:bg-fleet-surface-alt/40" [class.bg-fleet-surface-alt]="s.id === stayId()" (click)="selectStay(s.id)">
              <div class="flex-1 text-sm">
                <p class="font-semibold">{{ s.city }} · {{ s.hotelName }}</p>
                <p class="text-xs text-fleet-text-muted">{{ s.checkIn | date: 'mediumDate': 'UTC' }} → {{ s.checkOut | date: 'mediumDate': 'UTC' }}
                  {{ s.confirmationNumber ? '· conf. ' + s.confirmationNumber : '' }}</p>
                <p class="text-xs">{{ s.roomCount }} room(s) · {{ s.assignedCount }} of {{ group().confirmedMembers }} placed</p>
              </div>
              @if (can('ManageStays')) {
                <ng-container *appHasPermission="perms.groupManage">
                  <button mat-icon-button aria-label="Edit stay" (click)="$event.stopPropagation(); openStay(s)"><mat-icon>edit</mat-icon></button>
                  <button mat-icon-button aria-label="Delete stay" (click)="$event.stopPropagation(); deleteStay(s)"><mat-icon>delete_outline</mat-icon></button>
                </ng-container>
              }
            </li>
          }
        </ul>
      </div>

      <div class="tn-card lg:col-span-2">
        @if (stay(); as s) {
          <div class="flex flex-wrap items-center justify-between gap-3 border-b border-fleet-border px-5 py-4">
            <div><h3 class="tn-section-title">Rooming · {{ s.hotelName }}</h3>
              <p class="text-xs text-fleet-text-muted">Confirmed pilgrims only. Families share Family rooms; others are split into Male and Female rooms.</p></div>
            @if (editable()) {
              <div class="flex gap-2" *appHasPermission="perms.groupManage">
                <button mat-stroked-button [disabled]="busy()" (click)="suggest()"><mat-icon>auto_fix_high</mat-icon> Suggest allocation</button>
                <button mat-stroked-button (click)="addRoom()"><mat-icon>add</mat-icon> Room</button>
                <button mat-flat-button color="primary" [disabled]="busy() || !dirty()" (click)="saveRooming()">Save rooming</button>
              </div>
            }
          </div>
          <div class="space-y-4 p-5">
            @if (dirty()) { <div class="tn-banner"><mat-icon>edit_note</mat-icon>Unsaved changes — nothing is stored until you save.</div> }
            @if (saveError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ saveError() }}</div> }
            @if (unplaced().length) {
              <div><p class="mb-1 text-xs font-bold uppercase tracking-wider text-fleet-text-muted">Unplaced ({{ unplaced().length }})</p>
                <div class="flex flex-wrap gap-1">@for (m of unplaced(); track m.id) { <span class="tn-chip">{{ m.travelerName }} <span>{{ m.gender[0] }} · {{ m.roomPreference }}</span></span> }</div></div>
            } @else if (confirmed().length) {
              <p class="text-sm text-emerald-700">Every confirmed pilgrim has a room in this stay.</p>
            } @else {
              <p class="text-sm text-fleet-text-muted">Confirm pilgrims before allocating rooms.</p>
            }
            <div class="grid gap-3 md:grid-cols-2">
              @for (r of draft(); track $index; let i = $index) {
                <div class="rounded-lg border border-fleet-border p-3" [class.border-red-400]="issues(r).length">
                  <div class="flex flex-wrap items-center gap-2">
                    <input class="tn-input w-24" placeholder="Room no." [attr.aria-label]="'Room number ' + (i + 1)" [disabled]="!editable()"
                      [ngModel]="r.roomNumber" (ngModelChange)="patch(i, { roomNumber: $event || null })" />
                    <select class="tn-input w-28" [attr.aria-label]="'Room type ' + (i + 1)" [disabled]="!editable()" [ngModel]="r.roomType" (ngModelChange)="patch(i, { roomType: $event })">
                      @for (t of roomTypes; track t) { <option [value]="t">{{ t }} ({{ capacity[t] }})</option> }
                    </select>
                    <select class="tn-input w-28" [attr.aria-label]="'Gender policy ' + (i + 1)" [disabled]="!editable()" [ngModel]="r.genderPolicy" (ngModelChange)="patch(i, { genderPolicy: $event })">
                      @for (g of policies; track g) { <option [value]="g">{{ g }}</option> }
                    </select>
                    @if (editable()) { <button mat-icon-button class="ml-auto" aria-label="Remove room" (click)="removeRoom(i)"><mat-icon>delete_outline</mat-icon></button> }
                  </div>
                  <ul class="mt-2 space-y-1 text-sm">
                    @for (id of r.memberIds; track id) {
                      <li class="flex items-center gap-2">{{ memberName(id) }}
                        @if (editable()) { <button mat-icon-button class="ml-auto !h-7 !w-7" [attr.aria-label]="'Unassign ' + memberName(id)" (click)="unassign(i, id)"><mat-icon class="!text-[18px]">close</mat-icon></button> }</li>
                    }
                  </ul>
                  @if (editable() && r.memberIds.length < capacity[r.roomType] && unplaced().length) {
                    <select class="tn-input mt-2" [attr.aria-label]="'Add pilgrim to room ' + (i + 1)" [ngModel]="null" (ngModelChange)="assign(i, $event)">
                      <option [ngValue]="null">+ Add pilgrim…</option>
                      @for (m of unplaced(); track m.id) { <option [ngValue]="m.id">{{ m.travelerName }} ({{ m.gender }}, {{ m.customerName }})</option> }
                    </select>
                  }
                  @for (x of issues(r); track x) { <p class="mt-1 text-xs text-red-600">{{ x }}</p> }
                </div>
              }
            </div>
            @if (!draft().length) { <p class="text-sm text-fleet-text-muted">No rooms yet. Use "Suggest allocation" or add rooms.</p> }
          </div>
        } @else {
          <p class="px-5 py-10 text-center text-sm text-fleet-text-muted">Choose a stay to manage its rooms.</p>
        }
      </div>
    </div>

    <ui-modal [(open)]="stayOpen" [title]="editingStay() ? 'Edit stay' : 'Add stay'" size="sm">
      <div class="space-y-4">
        <div class="tn-field"><label for="gs-hotel" class="tn-required">Hotel</label>
          <select id="gs-hotel" class="tn-input" [(ngModel)]="stayForm.hotelId">
            <option [ngValue]="null">Choose…</option>
            @for (h of hotels(); track h.id) { <option [ngValue]="h.id">{{ h.city }} · {{ h.name }}</option> }
          </select></div>
        <div class="tn-grid">
          <div class="tn-field"><label for="gs-in" class="tn-required">Check-in</label><input id="gs-in" type="date" class="tn-input" [(ngModel)]="stayForm.checkIn" /></div>
          <div class="tn-field"><label for="gs-out" class="tn-required">Check-out</label><input id="gs-out" type="date" class="tn-input" [(ngModel)]="stayForm.checkOut" /></div>
        </div>
        <div class="tn-field"><label for="gs-conf">Hotel confirmation no.</label><input id="gs-conf" class="tn-input" maxlength="50" [(ngModel)]="stayForm.confirmationNumber" /></div>
        @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
      </div>
      <div modal-footer class="tn-actions">
        <button mat-stroked-button (click)="stayOpen.set(false)">Cancel</button>
        <button mat-flat-button color="primary" [disabled]="busy() || !stayForm.hotelId || !stayForm.checkIn || !stayForm.checkOut" (click)="saveStay()">Save</button>
      </div>
    </ui-modal>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class GroupRoomsTabComponent {
  private readonly api = inject(TravelGroupService);
  private readonly toast = inject(UiToastService);
  private readonly confirmDialog = inject(UiConfirmService);
  private readonly destroyRef = inject(DestroyRef);

  readonly group = input.required<TravelGroup>();
  readonly changed = output<void>();

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly roomTypes = TRAVEL_ROOM_TYPES;
  protected readonly policies = TRAVEL_GENDER_POLICIES;
  protected readonly capacity = ROOM_CAPACITY;
  protected readonly hotels = toSignal(inject(TravelPackageService).getHotels(undefined, true).pipe(catchError(() => of([] as TravelHotel[]))),
    { initialValue: [] as TravelHotel[] });

  protected readonly busy = signal(false);
  protected readonly saveError = signal<string | null>(null);
  protected readonly dialogError = signal<string | null>(null);
  protected readonly stayId = signal<number | null>(null);
  protected readonly draft = signal<TravelRoomingRoom[]>([]);
  protected readonly dirty = signal(false);

  protected readonly stay = computed(() => this.group().stays.find(s => s.id === this.stayId()) ?? null);
  protected readonly editable = computed(() => this.group().allowedActions.includes('ManageRooms'));
  protected readonly confirmed = computed(() => this.group().members.filter(m => m.status === 'Confirmed'));
  protected readonly unplaced = computed(() => {
    const placed = new Set(this.draft().flatMap(r => r.memberIds));
    return this.confirmed().filter(m => !placed.has(m.id));
  });

  protected readonly stayOpen = signal(false);
  protected readonly editingStay = signal<TravelGroupStay | null>(null);
  protected stayForm = { hotelId: null as number | null, checkIn: '', checkOut: '', confirmationNumber: '' };

  constructor() {
    // Rebuild the draft from saved rooms whenever the group reloads or another stay is chosen.
    effect(() => {
      const g = this.group();
      const id = this.stayId() ?? g.stays[0]?.id ?? null;
      untracked(() => {
        if (this.stayId() !== id) this.stayId.set(id);
        this.draft.set(g.rooms.filter(r => r.groupHotelId === id)
          .map(r => ({ roomNumber: r.roomNumber, roomType: r.roomType, genderPolicy: r.genderPolicy, memberIds: [...r.memberIds] })));
        this.dirty.set(false);
        this.saveError.set(null);
      });
    });
  }

  protected can(a: string): boolean { return this.group().allowedActions.includes(a as never); }
  protected issues(r: TravelRoomingRoom): string[] { return roomIssues(r, this.group().members); }
  protected memberName(id: number): string { return this.group().members.find(m => m.id === id)?.travelerName ?? `#${id}`; }

  protected async selectStay(id: number): Promise<void> {
    if (id === this.stayId()) return;
    if (this.dirty() && !(await this.confirmDialog.open({ variant: 'warning', title: 'Discard changes?', message: 'Unsaved rooming changes will be lost.', confirmText: 'Discard' }))) return;
    this.stayId.set(id);
    this.draft.set(this.group().rooms.filter(r => r.groupHotelId === id)
      .map(r => ({ roomNumber: r.roomNumber, roomType: r.roomType, genderPolicy: r.genderPolicy, memberIds: [...r.memberIds] })));
    this.dirty.set(false);
  }

  private update(fn: (rooms: TravelRoomingRoom[]) => TravelRoomingRoom[]): void { this.draft.update(fn); this.dirty.set(true); }
  protected patch(i: number, p: Partial<TravelRoomingRoom>): void { this.update(rs => rs.map((r, j) => (j === i ? { ...r, ...p } : r))); }
  protected addRoom(): void { this.update(rs => [...rs, { roomNumber: null, roomType: 'Quad' as TravelRoomType, genderPolicy: 'Male' as TravelGenderPolicy, memberIds: [] }]); }
  protected removeRoom(i: number): void { this.update(rs => rs.filter((_, j) => j !== i)); }
  protected assign(i: number, memberId: number | null): void { if (memberId) this.update(rs => rs.map((r, j) => (j === i ? { ...r, memberIds: [...r.memberIds, memberId] } : r))); }
  protected unassign(i: number, memberId: number): void { this.update(rs => rs.map((r, j) => (j === i ? { ...r, memberIds: r.memberIds.filter(x => x !== memberId) } : r))); }

  protected suggest(): void {
    const id = this.stayId();
    if (!id) return;
    this.busy.set(true);
    this.api.suggestRooming(this.group().id, id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: rooms => { this.busy.set(false); this.draft.set(rooms); this.dirty.set(true); this.toast.info('Suggestion loaded — review it, then save.'); },
      error: err => { this.busy.set(false); this.toast.error(apiErrorMessage(err, 'Could not suggest an allocation.')); }
    });
  }

  protected saveRooming(): void {
    const id = this.stayId();
    if (!id) return;
    this.busy.set(true);
    this.saveError.set(null);
    this.api.saveRooming(this.group().id, id, this.draft().filter(r => r.memberIds.length || r.roomNumber)).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); this.dirty.set(false); this.toast.success('Rooming saved.'); this.changed.emit(); },
      error: err => { this.busy.set(false); this.saveError.set(apiErrorMessage(err, 'The rooming was refused.')); }
    });
  }

  protected openStay(s: TravelGroupStay | null): void {
    this.editingStay.set(s);
    const g = this.group();
    this.stayForm = {
      hotelId: s?.hotelId ?? null, checkIn: toDateInput(s?.checkIn ?? g.departureDate), checkOut: toDateInput(s?.checkOut ?? g.returnDate),
      confirmationNumber: s?.confirmationNumber ?? ''
    };
    this.dialogError.set(null);
    this.stayOpen.set(true);
  }

  protected saveStay(): void {
    const f = this.stayForm;
    this.busy.set(true);
    this.api.saveStay(this.group().id, this.editingStay()?.id ?? null, {
      hotelId: f.hotelId!, checkIn: f.checkIn, checkOut: f.checkOut, confirmationNumber: f.confirmationNumber.trim() || null
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); this.stayOpen.set(false); this.toast.success('Stay saved.'); this.changed.emit(); },
      error: err => { this.busy.set(false); this.dialogError.set(apiErrorMessage(err, 'Could not save the stay.')); }
    });
  }

  protected async deleteStay(s: TravelGroupStay): Promise<void> {
    if (!(await this.confirmDialog.confirmDelete(`Delete the ${s.hotelName} stay and its rooms?`))) return;
    this.api.deleteStay(this.group().id, s.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { if (this.stayId() === s.id) this.stayId.set(null); this.toast.success('Stay deleted.'); this.changed.emit(); },
      error: err => this.toast.error(apiErrorMessage(err, 'Could not delete the stay.'))
    });
  }
}
