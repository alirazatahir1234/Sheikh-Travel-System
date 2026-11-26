import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import {
  UiConfirmService, UiDataTableComponent, UiDrawerComponent, UiPageHeaderComponent, UiStatusBadgeComponent, UiTableCellDirective, UiTableColumn
} from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  SUPPLIER_TYPE_LABEL, SaveTravelSupplier, TRAVEL_PERMISSIONS, TRAVEL_SUPPLIER_TYPES, TravelSupplier, TravelSupplierType
} from '../../models/travel.models';
import { TravelSupplierService } from '../../services/travel-supplier.service';

@Component({
  selector: 'app-travel-supplier-list',
  standalone: true,
  imports: [ReactiveFormsModule, MatButtonModule, MatIconModule, UiPageHeaderComponent, UiDataTableComponent, UiTableCellDirective, UiDrawerComponent,
    UiStatusBadgeComponent, HasPermissionDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      <ui-page-header title="Suppliers" eyebrow="TravelNexus" icon="storefront"
        subtitle="Airlines, hotels, bed banks, ticket consolidators and transport providers you buy from."
        [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Suppliers' }]">
        <button mat-flat-button color="primary" *appHasPermission="perms.supplierManage" (click)="edit(null)"><mat-icon>add</mat-icon> Add supplier</button>
      </ui-page-header>
      @if (error()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}
        <button mat-stroked-button class="ml-auto" (click)="load()">Retry</button></div> }
      <ui-data-table [columns]="columns" [data]="rows()" [loading]="loading()" [searchable]="true" searchPlaceholder="Search suppliers…"
        emptyIcon="storefront" emptyTitle="No suppliers yet" emptyDescription="Add the airlines and hotels you book with.">
        <ng-template uiTableCell="supplierType" let-row>{{ label(row) }}</ng-template>
        <ng-template uiTableCell="contactName" let-row>{{ row.contactName || '—' }}{{ row.phone ? ' · ' + row.phone : '' }}</ng-template>
        <ng-template uiTableCell="isActive" let-row><ui-status-badge [status]="row.isActive ? 'active' : 'inactive'"></ui-status-badge></ng-template>
        <ng-template uiTableCell="actions" let-row>
          <ng-container *appHasPermission="perms.supplierManage">
            <button mat-icon-button aria-label="Edit" (click)="edit(row)"><mat-icon>edit</mat-icon></button>
            <button mat-icon-button aria-label="Delete" (click)="remove(row)"><mat-icon>delete_outline</mat-icon></button>
          </ng-container>
        </ng-template>
      </ui-data-table>
    </div>

    <ui-drawer [(open)]="drawerOpen" [title]="editingId() ? 'Edit supplier' : 'Add supplier'" width="480px">
      <form class="space-y-4" [formGroup]="form" (ngSubmit)="save()" novalidate>
        <div class="tn-field"><label for="s-name" class="tn-required">Name</label><input id="s-name" class="tn-input" formControlName="name" maxlength="150" /></div>
        <div class="tn-grid">
          <div class="tn-field"><label for="s-type" class="tn-required">Type</label>
            <select id="s-type" class="tn-input" formControlName="supplierType">
              @for (t of types; track t) { <option [value]="t">{{ typeLabel[t] }}</option> }
            </select></div>
          <div class="tn-field"><label for="s-iata">IATA code</label><input id="s-iata" class="tn-input uppercase" formControlName="iataCode" maxlength="3" placeholder="e.g. PK" /></div>
          <div class="tn-field"><label for="s-contact">Contact</label><input id="s-contact" class="tn-input" formControlName="contactName" maxlength="150" /></div>
          <div class="tn-field"><label for="s-phone">Phone</label><input id="s-phone" class="tn-input" formControlName="phone" maxlength="30" /></div>
          <div class="tn-field"><label for="s-email">Email</label><input id="s-email" type="email" class="tn-input" formControlName="email" maxlength="200" /></div>
          <div class="tn-field"><label for="s-cur">Billing currency</label><input id="s-cur" class="tn-input uppercase" formControlName="currencyCode" maxlength="3" /></div>
        </div>
        <label class="flex items-center gap-2 text-sm"><input type="checkbox" formControlName="isActive" /> Active</label>
        <div class="tn-field"><label for="s-notes">Notes</label><textarea id="s-notes" class="tn-input" rows="3" formControlName="notes" maxlength="1000"></textarea></div>
        @if (form.invalid && form.touched) { <span class="tn-error">Name and type are required; IATA 2–3 characters; currency 3 letters.</span> }
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
export class TravelSupplierListComponent {
  private readonly service = inject(TravelSupplierService);
  private readonly fb = inject(FormBuilder);
  private readonly toast = inject(UiToastService);
  private readonly confirm = inject(UiConfirmService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly types = TRAVEL_SUPPLIER_TYPES;
  protected readonly typeLabel = SUPPLIER_TYPE_LABEL;
  protected readonly columns: UiTableColumn[] = [
    { key: 'name', label: 'Supplier', sortable: true },
    { key: 'supplierType', label: 'Type', sortable: true },
    { key: 'iataCode', label: 'IATA' },
    { key: 'contactName', label: 'Contact' },
    { key: 'currencyCode', label: 'Currency' },
    { key: 'usageCount', label: 'Services', align: 'center' },
    { key: 'isActive', label: 'Status' }
  ];
  protected readonly rows = signal<TravelSupplier[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly drawerOpen = signal(false);
  protected readonly editingId = signal<number | null>(null);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);

  protected readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(150)]],
    supplierType: ['Airline' as TravelSupplierType, Validators.required],
    iataCode: ['', Validators.pattern(/^[A-Za-z0-9]{2,3}$/)],
    contactName: [''],
    phone: [''],
    email: ['', Validators.email],
    currencyCode: ['', Validators.pattern(/^[A-Za-z]{3}$/)],
    isActive: [true],
    notes: ['']
  });

  constructor() { this.load(); }

  protected label(row: TravelSupplier): string { return SUPPLIER_TYPE_LABEL[row.supplierType] ?? row.supplierType; }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.service.getSuppliers().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => { this.rows.set(r); this.loading.set(false); },
      error: err => { this.error.set(apiErrorMessage(err, 'Could not load suppliers.')); this.loading.set(false); }
    });
  }

  protected edit(row: TravelSupplier | null): void {
    this.editingId.set(row?.id ?? null);
    this.saveError.set(null);
    this.form.reset({
      name: row?.name ?? '', supplierType: row?.supplierType ?? 'Airline', iataCode: row?.iataCode ?? '', contactName: row?.contactName ?? '',
      phone: row?.phone ?? '', email: row?.email ?? '', currencyCode: row?.currencyCode ?? '', isActive: row?.isActive ?? true, notes: row?.notes ?? ''
    });
    this.drawerOpen.set(true);
  }

  protected save(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const t = (x: string | null | undefined) => x?.trim() || null;
    const dto: SaveTravelSupplier = {
      name: v.name!.trim(), supplierType: v.supplierType!, iataCode: t(v.iataCode)?.toUpperCase() ?? null, contactName: t(v.contactName),
      phone: t(v.phone), email: t(v.email), currencyCode: t(v.currencyCode)?.toUpperCase() ?? null, isActive: !!v.isActive, notes: t(v.notes)
    };
    const id = this.editingId();
    this.saving.set(true);
    (id ? this.service.update(id, dto) : this.service.create(dto)).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.saving.set(false); this.drawerOpen.set(false); this.toast.success('Supplier saved.'); this.load(); },
      error: err => { this.saving.set(false); this.saveError.set(apiErrorMessage(err, 'Could not save the supplier.')); }
    });
  }

  protected async remove(row: TravelSupplier): Promise<void> {
    if (!(await this.confirm.confirmDelete(`Delete supplier "${row.name}"?`))) return;
    this.service.delete(row.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.toast.success('Supplier deleted.'); this.load(); },
      error: err => this.toast.error(apiErrorMessage(err, 'Could not delete the supplier.'))
    });
  }
}
