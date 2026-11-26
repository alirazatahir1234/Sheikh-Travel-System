import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { UiConfirmService, UiModalComponent } from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { LEG_TYPE_LABEL, TRAVEL_PERMISSIONS, TRAVEL_TRANSPORT_LEG_TYPES, TravelGroup, TravelGroupTransport, TravelTransportLegType } from '../../models/travel.models';
import { asUtc, fromDateTimeLocal, toDateTimeLocal } from '../../models/travel-date.util';
import { TravelGroupService } from '../../services/travel-group.service';

@Component({
  selector: 'app-group-transport-tab',
  standalone: true,
  imports: [DatePipe, FormsModule, MatButtonModule, MatIconModule, UiModalComponent, HasPermissionDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-card">
      <div class="flex items-center justify-between border-b border-fleet-border px-5 py-4">
        <div><h3 class="tn-section-title">Transport legs</h3>
          <p class="text-xs text-fleet-text-muted">Airport transfers, Makkah ⇄ Madinah, ziyarat. Optionally link an existing fleet booking (read-only).</p></div>
        @if (editable()) { <button mat-flat-button color="primary" *appHasPermission="perms.groupManage" (click)="open(null)"><mat-icon>add</mat-icon> Add leg</button> }
      </div>
      @if (!legs().length) { <p class="px-5 py-8 text-center text-sm text-fleet-text-muted">No transport legs yet.</p> }
      <ol class="tn-timeline px-5">
        @for (t of legs(); track t.id) {
          <li>
            <span class="tn-timeline-icon"><mat-icon>{{ t.legType === 'AirportTransfer' ? 'flight_land' : t.legType === 'Ziyarat' ? 'mosque' : 'directions_bus' }}</mat-icon></span>
            <div class="flex-1 text-sm">
              <p><strong>{{ legLabel[t.legType] }}</strong> · {{ t.fromPlace }} → {{ t.toPlace }}</p>
              <p class="text-fleet-text-muted">{{ utc(t.departAt) | date: 'medium' }}
                {{ t.providerName ? '· ' + t.providerName : '' }}{{ t.vehicleType ? ' · ' + t.vehicleType : '' }}{{ t.seats ? ' · ' + t.seats + ' seats' : '' }}</p>
              @if (t.fleetBookingId) { <p class="text-xs">Fleet booking: {{ t.fleetBookingNumber || '#' + t.fleetBookingId }}</p> }
              @if (t.notes) { <p class="text-xs text-fleet-text-muted">{{ t.notes }}</p> }
            </div>
            @if (editable()) {
              <ng-container *appHasPermission="perms.groupManage">
                <button mat-icon-button aria-label="Edit leg" (click)="open(t)"><mat-icon>edit</mat-icon></button>
                <button mat-icon-button aria-label="Delete leg" (click)="remove(t)"><mat-icon>delete_outline</mat-icon></button>
              </ng-container>
            }
          </li>
        }
      </ol>
    </div>

    <ui-modal [(open)]="formOpen" [title]="editing() ? 'Edit transport leg' : 'Add transport leg'" size="md">
      <div class="space-y-4">
        <div class="tn-grid">
          <div class="tn-field"><label for="tl-type">Leg</label>
            <select id="tl-type" class="tn-input" [(ngModel)]="f.legType">@for (l of legTypes; track l) { <option [value]="l">{{ legLabel[l] }}</option> }</select></div>
          <div class="tn-field"><label for="tl-at" class="tn-required">Departs</label><input id="tl-at" type="datetime-local" class="tn-input" [(ngModel)]="f.departAt" /></div>
          <div class="tn-field"><label for="tl-from" class="tn-required">From</label><input id="tl-from" class="tn-input" maxlength="150" [(ngModel)]="f.fromPlace" /></div>
          <div class="tn-field"><label for="tl-to" class="tn-required">To</label><input id="tl-to" class="tn-input" maxlength="150" [(ngModel)]="f.toPlace" /></div>
          <div class="tn-field"><label for="tl-prov">Provider</label><input id="tl-prov" class="tn-input" maxlength="150" [(ngModel)]="f.providerName" /></div>
          <div class="tn-field"><label for="tl-veh">Vehicle</label><input id="tl-veh" class="tn-input" maxlength="100" [(ngModel)]="f.vehicleType" placeholder="e.g. 49-seat coach" /></div>
          <div class="tn-field"><label for="tl-seats">Seats</label><input id="tl-seats" type="number" min="1" class="tn-input" [(ngModel)]="f.seats" /></div>
          <div class="tn-field"><label for="tl-fleet">Fleet booking id</label><input id="tl-fleet" type="number" min="1" class="tn-input" [(ngModel)]="f.fleetBookingId" />
            <span class="tn-hint">Optional; must be a booking of your company.</span></div>
        </div>
        <div class="tn-field"><label for="tl-notes">Notes</label><textarea id="tl-notes" class="tn-input" rows="2" maxlength="1000" [(ngModel)]="f.notes"></textarea></div>
        @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
      </div>
      <div modal-footer class="tn-actions">
        <button mat-stroked-button (click)="formOpen.set(false)">Cancel</button>
        <button mat-flat-button color="primary" [disabled]="busy() || !f.departAt || !f.fromPlace.trim() || !f.toPlace.trim()" (click)="save()">Save</button>
      </div>
    </ui-modal>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class GroupTransportTabComponent {
  private readonly api = inject(TravelGroupService);
  private readonly toast = inject(UiToastService);
  private readonly confirm = inject(UiConfirmService);
  private readonly destroyRef = inject(DestroyRef);

  readonly group = input.required<TravelGroup>();
  readonly changed = output<void>();

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly legTypes = TRAVEL_TRANSPORT_LEG_TYPES;
  protected readonly legLabel = LEG_TYPE_LABEL;
  protected readonly utc = asUtc;
  protected readonly legs = computed(() => [...this.group().transport].sort((a, b) => a.departAt.localeCompare(b.departAt)));
  protected readonly editable = computed(() => this.group().allowedActions.includes('ManageTransport'));
  protected readonly busy = signal(false);
  protected readonly dialogError = signal<string | null>(null);
  protected readonly formOpen = signal(false);
  protected readonly editing = signal<TravelGroupTransport | null>(null);
  protected f = this.blank();

  private blank() {
    return { legType: 'AirportTransfer' as TravelTransportLegType, departAt: '', fromPlace: '', toPlace: '', providerName: '', vehicleType: '',
      seats: null as number | null, fleetBookingId: null as number | null, notes: '' };
  }

  protected open(t: TravelGroupTransport | null): void {
    this.editing.set(t);
    this.f = t ? {
      legType: t.legType, departAt: toDateTimeLocal(t.departAt), fromPlace: t.fromPlace, toPlace: t.toPlace, providerName: t.providerName ?? '',
      vehicleType: t.vehicleType ?? '', seats: t.seats, fleetBookingId: t.fleetBookingId, notes: t.notes ?? ''
    } : this.blank();
    this.dialogError.set(null);
    this.formOpen.set(true);
  }

  protected save(): void {
    const f = this.f;
    const num = (n: number | null) => (n === null || (n as unknown) === '' ? null : Number(n));
    this.busy.set(true);
    this.api.saveTransport(this.group().id, this.editing()?.id ?? null, {
      legType: f.legType, departAt: fromDateTimeLocal(f.departAt)!, fromPlace: f.fromPlace.trim(), toPlace: f.toPlace.trim(),
      providerName: f.providerName.trim() || null, vehicleType: f.vehicleType.trim() || null, seats: num(f.seats), fleetBookingId: num(f.fleetBookingId),
      notes: f.notes.trim() || null
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); this.formOpen.set(false); this.toast.success('Transport leg saved.'); this.changed.emit(); },
      error: err => { this.busy.set(false); this.dialogError.set(apiErrorMessage(err, 'Could not save the transport leg.')); }
    });
  }

  protected async remove(t: TravelGroupTransport): Promise<void> {
    if (!(await this.confirm.confirmDelete(`Delete the ${this.legLabel[t.legType].toLowerCase()} ${t.fromPlace} → ${t.toPlace}?`))) return;
    this.api.deleteTransport(this.group().id, t.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.toast.success('Transport leg deleted.'); this.changed.emit(); },
      error: err => this.toast.error(apiErrorMessage(err, 'Could not delete the transport leg.'))
    });
  }
}
