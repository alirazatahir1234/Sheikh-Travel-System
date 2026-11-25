import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { catchError, of } from 'rxjs';
import {
  UiConfirmService, UiDataTableComponent, UiDrawerComponent, UiStatusBadgeComponent, UiTableCellDirective, UiTableColumn
} from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  SaveTravelVisaType, TRAVEL_VISA_CATEGORIES, TRAVEL_VISA_ENTRY_TYPES, TravelVisaCategory, TravelVisaEntryType, TravelVisaType
} from '../../models/travel.models';
import { TravelConfigService } from '../../services/travel-config.service';

@Component({
  selector: 'app-visa-types-tab',
  standalone: true,
  imports: [CurrencyPipe, ReactiveFormsModule, MatButtonModule, MatIconModule, UiDataTableComponent, UiTableCellDirective,
    UiDrawerComponent, UiStatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-3">
      <div class="flex items-center justify-between">
        <p class="text-sm text-fleet-text-muted">Visa products you process, per destination country.</p>
        <button mat-flat-button color="primary" (click)="edit(null)"><mat-icon>add</mat-icon> Add visa type</button>
      </div>
      @if (error()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}
        <button mat-stroked-button class="ml-auto" (click)="load()">Retry</button></div> }
      <ui-data-table [columns]="columns" [data]="rows()" [loading]="loading()" [searchable]="true" searchPlaceholder="Search visa types…"
        emptyIcon="flight" emptyTitle="No visa types yet" emptyDescription="Add the visa types you offer, e.g. Saudi Arabia · Umrah.">
        <ng-template uiTableCell="countryName" let-row>{{ row.countryName }} <span class="text-xs text-fleet-text-muted">({{ row.countryCode }})</span></ng-template>
        <ng-template uiTableCell="defaultFee" let-row>
          {{ row.defaultFee !== null ? (row.defaultFee | currency: row.currencyCode : 'code' : '1.0-2') : '—' }}
        </ng-template>
        <ng-template uiTableCell="isActive" let-row><ui-status-badge [status]="row.isActive ? 'active' : 'inactive'"></ui-status-badge></ng-template>
        <ng-template uiTableCell="actions" let-row>
          <button mat-icon-button aria-label="Edit" (click)="edit(row)"><mat-icon>edit</mat-icon></button>
          <button mat-icon-button aria-label="Delete" (click)="remove(row)"><mat-icon>delete_outline</mat-icon></button>
        </ng-template>
      </ui-data-table>
    </div>

    <ui-drawer [(open)]="drawerOpen" [title]="editingId() ? 'Edit visa type' : 'Add visa type'" width="480px">
      <form class="space-y-4" [formGroup]="form" (ngSubmit)="save()" novalidate>
        <div class="tn-field">
          <label for="vt-country" class="tn-required">Country</label>
          <select id="vt-country" class="tn-input" formControlName="countryCode">
            <option [ngValue]="null">Choose…</option>
            @for (c of countries(); track c.code) { <option [ngValue]="c.code">{{ c.name }}</option> }
          </select>
        </div>
        <div class="tn-field">
          <label for="vt-name" class="tn-required">Name</label>
          <input id="vt-name" class="tn-input" formControlName="name" maxlength="150" placeholder="e.g. Umrah visa (30 days)" />
        </div>
        <div class="tn-grid">
          <div class="tn-field">
            <label for="vt-cat">Category</label>
            <select id="vt-cat" class="tn-input" formControlName="category">
              @for (c of categories; track c) { <option [value]="c">{{ c }}</option> }
            </select>
          </div>
          <div class="tn-field">
            <label for="vt-entry">Entries</label>
            <select id="vt-entry" class="tn-input" formControlName="entryType">
              @for (e of entryTypes; track e) { <option [value]="e">{{ e }}</option> }
            </select>
          </div>
          <div class="tn-field"><label for="vt-val">Validity (days)</label><input id="vt-val" class="tn-input" type="number" min="1" formControlName="validityDays" /></div>
          <div class="tn-field"><label for="vt-stay">Stay (days)</label><input id="vt-stay" class="tn-input" type="number" min="1" formControlName="stayDays" /></div>
          <div class="tn-field"><label for="vt-proc">Processing (days)</label><input id="vt-proc" class="tn-input" type="number" min="0" formControlName="processingDays" /></div>
          <div class="tn-field"><label for="vt-fee">Default fee</label><input id="vt-fee" class="tn-input" type="number" min="0" step="0.01" formControlName="defaultFee" /></div>
          <div class="tn-field">
            <label for="vt-cur">Currency (ISO)</label>
            <input id="vt-cur" class="tn-input uppercase" formControlName="currencyCode" maxlength="3" placeholder="SAR" />
            @if (form.hasError('feeCurrency')) { <span class="tn-error">A fee needs a 3-letter currency code.</span> }
          </div>
        </div>
        <label class="flex items-center gap-2 text-sm"><input type="checkbox" formControlName="isActive" /> Active</label>
        <div class="tn-field"><label for="vt-notes">Notes</label><textarea id="vt-notes" class="tn-input" formControlName="notes" maxlength="1000"></textarea></div>
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
export class VisaTypesTabComponent {
  private readonly service = inject(TravelConfigService);
  private readonly fb = inject(FormBuilder);
  private readonly toast = inject(UiToastService);
  private readonly confirm = inject(UiConfirmService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly categories = TRAVEL_VISA_CATEGORIES;
  protected readonly entryTypes = TRAVEL_VISA_ENTRY_TYPES;
  protected readonly columns: UiTableColumn[] = [
    { key: 'countryName', label: 'Country', sortable: true },
    { key: 'name', label: 'Visa type', sortable: true },
    { key: 'category', label: 'Category', sortable: true },
    { key: 'entryType', label: 'Entries' },
    { key: 'processingDays', label: 'Processing (days)', align: 'center' },
    { key: 'defaultFee', label: 'Default fee', align: 'right' },
    { key: 'isActive', label: 'Status' }
  ];

  protected readonly countries = toSignal(this.service.countries$.pipe(catchError(() => of([]))), { initialValue: [] });
  protected readonly rows = signal<TravelVisaType[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly drawerOpen = signal(false);
  protected readonly editingId = signal<number | null>(null);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);

  protected readonly form = this.fb.group({
    countryCode: [null as string | null, Validators.required],
    name: ['', [Validators.required, Validators.maxLength(150)]],
    category: ['Tourist' as TravelVisaCategory],
    entryType: ['Single' as TravelVisaEntryType],
    validityDays: [null as number | null, Validators.min(1)],
    stayDays: [null as number | null, Validators.min(1)],
    processingDays: [null as number | null, Validators.min(0)],
    defaultFee: [null as number | null, Validators.min(0)],
    currencyCode: [''],
    isActive: [true],
    notes: ['']
  }, {
    validators: g => {
      const fee = g.get('defaultFee')?.value;
      const cur = (g.get('currencyCode')?.value ?? '').trim();
      return (fee !== null && fee !== '' && !/^[A-Za-z]{3}$/.test(cur)) || (cur && !/^[A-Za-z]{3}$/.test(cur)) ? { feeCurrency: true } : null;
    }
  });

  constructor() { this.load(); }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.service.getVisaTypes().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: rows => { this.rows.set(rows); this.loading.set(false); },
      error: err => { this.error.set(apiErrorMessage(err, 'Could not load visa types.')); this.loading.set(false); }
    });
  }

  protected edit(row: TravelVisaType | null): void {
    this.editingId.set(row?.id ?? null);
    this.saveError.set(null);
    this.form.reset({
      countryCode: row?.countryCode ?? null, name: row?.name ?? '', category: row?.category ?? 'Tourist',
      entryType: row?.entryType ?? 'Single', validityDays: row?.validityDays ?? null, stayDays: row?.stayDays ?? null,
      processingDays: row?.processingDays ?? null, defaultFee: row?.defaultFee ?? null, currencyCode: row?.currencyCode ?? '',
      isActive: row?.isActive ?? true, notes: row?.notes ?? ''
    });
    this.drawerOpen.set(true);
  }

  protected save(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const num = (n: number | null) => (n === null || (n as unknown) === '' ? null : Number(n));
    const dto: SaveTravelVisaType = {
      countryCode: v.countryCode!, name: v.name!.trim(), category: v.category!, entryType: v.entryType!,
      validityDays: num(v.validityDays), stayDays: num(v.stayDays), processingDays: num(v.processingDays),
      defaultFee: num(v.defaultFee), currencyCode: v.currencyCode?.trim() ? v.currencyCode.trim().toUpperCase() : null,
      isActive: !!v.isActive, notes: v.notes?.trim() || null
    };
    const id = this.editingId();
    this.saving.set(true);
    (id ? this.service.updateVisaType(id, dto) : this.service.createVisaType(dto))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => { this.saving.set(false); this.drawerOpen.set(false); this.toast.success('Visa type saved.'); this.load(); },
        error: err => { this.saving.set(false); this.saveError.set(apiErrorMessage(err, 'Could not save the visa type.')); }
      });
  }

  protected async remove(row: TravelVisaType): Promise<void> {
    if (!(await this.confirm.confirmDelete(`Delete visa type "${row.name}" (${row.countryName})?`))) return;
    this.service.deleteVisaType(row.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.toast.success('Visa type deleted.'); this.load(); },
      error: err => this.toast.error(apiErrorMessage(err, 'Could not delete the visa type.'))
    });
  }
}
