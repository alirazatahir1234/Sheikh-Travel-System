import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import {
  UiConfirmService, UiDataTableComponent, UiDrawerComponent, UiStatusBadgeComponent, UiTableCellDirective, UiTableColumn
} from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { SaveTravelHotel, TRAVEL_HOTEL_CITIES, TravelHotel, TravelHotelCity } from '../../models/travel.models';
import { TravelPackageService } from '../../services/travel-package.service';

/** Settings → Hotels: the tenant's hotel catalog used by packages and group stays. */
@Component({
  selector: 'app-hotels-tab',
  standalone: true,
  imports: [ReactiveFormsModule, MatButtonModule, MatIconModule, UiDataTableComponent, UiTableCellDirective, UiDrawerComponent, UiStatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-3">
      <div class="flex items-center justify-between">
        <p class="text-sm text-fleet-text-muted">Hotels used in Umrah packages and group stays.</p>
        <button mat-flat-button color="primary" (click)="edit(null)"><mat-icon>add</mat-icon> Add hotel</button>
      </div>
      @if (error()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}
        <button mat-stroked-button class="ml-auto" (click)="load()">Retry</button></div> }
      <ui-data-table [columns]="columns" [data]="rows()" [loading]="loading()" [searchable]="true" searchPlaceholder="Search hotels…"
        emptyIcon="hotel" emptyTitle="No hotels yet" emptyDescription="Add the Makkah and Madinah hotels you book.">
        <ng-template uiTableCell="starRating" let-row>{{ row.starRating ? row.starRating + '★' : '—' }}</ng-template>
        <ng-template uiTableCell="distanceToHaramMeters" let-row>{{ row.distanceToHaramMeters !== null ? row.distanceToHaramMeters + ' m' : '—' }}</ng-template>
        <ng-template uiTableCell="isActive" let-row><ui-status-badge [status]="row.isActive ? 'active' : 'inactive'"></ui-status-badge></ng-template>
        <ng-template uiTableCell="actions" let-row>
          <button mat-icon-button aria-label="Edit" (click)="edit(row)"><mat-icon>edit</mat-icon></button>
          <button mat-icon-button aria-label="Delete" (click)="remove(row)"><mat-icon>delete_outline</mat-icon></button>
        </ng-template>
      </ui-data-table>
    </div>

    <ui-drawer [(open)]="drawerOpen" [title]="editingId() ? 'Edit hotel' : 'Add hotel'" width="480px">
      <form class="space-y-4" [formGroup]="form" (ngSubmit)="save()" novalidate>
        <div class="tn-field"><label for="h-name" class="tn-required">Name</label>
          <input id="h-name" class="tn-input" formControlName="name" maxlength="150" /></div>
        <div class="tn-grid">
          <div class="tn-field"><label for="h-city" class="tn-required">City</label>
            <select id="h-city" class="tn-input" formControlName="city">
              @for (c of cities; track c) { <option [value]="c">{{ c }}</option> }
            </select></div>
          <div class="tn-field"><label for="h-cc" class="tn-required">Country (ISO)</label>
            <input id="h-cc" class="tn-input uppercase" formControlName="countryCode" maxlength="2" /></div>
          <div class="tn-field"><label for="h-star">Stars</label>
            <input id="h-star" class="tn-input" type="number" min="1" max="5" formControlName="starRating" /></div>
          <div class="tn-field"><label for="h-dist">Distance to Haram (m)</label>
            <input id="h-dist" class="tn-input" type="number" min="0" formControlName="distanceToHaramMeters" /></div>
        </div>
        <div class="tn-field"><label for="h-addr">Address</label><input id="h-addr" class="tn-input" formControlName="address" maxlength="300" /></div>
        <div class="tn-field"><label for="h-phone">Phone</label><input id="h-phone" class="tn-input" formControlName="phone" maxlength="30" /></div>
        <label class="flex items-center gap-2 text-sm"><input type="checkbox" formControlName="isActive" /> Active</label>
        @if (form.invalid && form.touched) { <span class="tn-error">Name, city and a 2-letter country code are required; stars 1–5.</span> }
        @if (saveError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ saveError() }}</div> }
        <div class="tn-actions">
          <button mat-stroked-button type="button" (click)="drawerOpen.set(false)">Cancel</button>
          <button mat-flat-button color="primary" type="submit" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Save' }}</button>
        </div>
      </form>
    </ui-drawer>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class HotelsTabComponent {
  private readonly service = inject(TravelPackageService);
  private readonly fb = inject(FormBuilder);
  private readonly toast = inject(UiToastService);
  private readonly confirm = inject(UiConfirmService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly cities = TRAVEL_HOTEL_CITIES;
  protected readonly columns: UiTableColumn[] = [
    { key: 'name', label: 'Hotel', sortable: true },
    { key: 'city', label: 'City', sortable: true },
    { key: 'starRating', label: 'Stars', align: 'center' },
    { key: 'distanceToHaramMeters', label: 'To Haram', align: 'right' },
    { key: 'isActive', label: 'Status' }
  ];
  protected readonly rows = signal<TravelHotel[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly drawerOpen = signal(false);
  protected readonly editingId = signal<number | null>(null);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);

  protected readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(150)]],
    city: ['Makkah' as TravelHotelCity, Validators.required],
    countryCode: ['SA', [Validators.required, Validators.pattern(/^[A-Za-z]{2}$/)]],
    starRating: [null as number | null, [Validators.min(1), Validators.max(5)]],
    distanceToHaramMeters: [null as number | null, Validators.min(0)],
    address: [''],
    phone: [''],
    isActive: [true]
  });

  constructor() { this.load(); }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.service.getHotels().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: rows => { this.rows.set(rows); this.loading.set(false); },
      error: err => { this.error.set(apiErrorMessage(err, 'Could not load hotels.')); this.loading.set(false); }
    });
  }

  protected edit(row: TravelHotel | null): void {
    this.editingId.set(row?.id ?? null);
    this.saveError.set(null);
    this.form.reset({
      name: row?.name ?? '', city: row?.city ?? 'Makkah', countryCode: row?.countryCode ?? 'SA', starRating: row?.starRating ?? null,
      distanceToHaramMeters: row?.distanceToHaramMeters ?? null, address: row?.address ?? '', phone: row?.phone ?? '', isActive: row?.isActive ?? true
    });
    this.drawerOpen.set(true);
  }

  protected save(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const num = (n: number | null) => (n === null || (n as unknown) === '' ? null : Number(n));
    const dto: SaveTravelHotel = {
      name: v.name!.trim(), city: v.city!, countryCode: v.countryCode!.trim().toUpperCase(), starRating: num(v.starRating),
      distanceToHaramMeters: num(v.distanceToHaramMeters), address: v.address?.trim() || null, phone: v.phone?.trim() || null, isActive: !!v.isActive
    };
    const id = this.editingId();
    this.saving.set(true);
    (id ? this.service.updateHotel(id, dto) : this.service.createHotel(dto)).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.saving.set(false); this.drawerOpen.set(false); this.toast.success('Hotel saved.'); this.load(); },
      error: err => { this.saving.set(false); this.saveError.set(apiErrorMessage(err, 'Could not save the hotel.')); }
    });
  }

  protected async remove(row: TravelHotel): Promise<void> {
    if (!(await this.confirm.confirmDelete(`Delete hotel "${row.name}"?`))) return;
    this.service.deleteHotel(row.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.toast.success('Hotel deleted.'); this.load(); },
      error: err => this.toast.error(apiErrorMessage(err, 'Could not delete the hotel.'))
    });
  }
}
