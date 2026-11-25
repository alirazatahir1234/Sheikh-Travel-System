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
import { SaveTravelDocumentType, TravelDocumentType } from '../../models/travel.models';
import { TravelConfigService } from '../../services/travel-config.service';

@Component({
  selector: 'app-document-types-tab',
  standalone: true,
  imports: [ReactiveFormsModule, MatButtonModule, MatIconModule, UiDataTableComponent, UiTableCellDirective,
    UiDrawerComponent, UiStatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-3">
      <div class="flex items-center justify-between">
        <p class="text-sm text-fleet-text-muted">Documents you collect from applicants (used by visa checklists).</p>
        <button mat-flat-button color="primary" (click)="edit(null)"><mat-icon>add</mat-icon> Add document type</button>
      </div>
      @if (error()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}
        <button mat-stroked-button class="ml-auto" (click)="load()">Retry</button></div> }
      <ui-data-table [columns]="columns" [data]="rows()" [loading]="loading()" [searchable]="true"
        emptyIcon="description" emptyTitle="No document types yet" emptyDescription="e.g. PASSPORT, PHOTO, BANK_STATEMENT.">
        <ng-template uiTableCell="code" let-row><span class="font-mono text-xs">{{ row.code }}</span></ng-template>
        <ng-template uiTableCell="requiresExpiryDate" let-row>{{ row.requiresExpiryDate ? 'Yes' : 'No' }}</ng-template>
        <ng-template uiTableCell="isActive" let-row><ui-status-badge [status]="row.isActive ? 'active' : 'inactive'"></ui-status-badge></ng-template>
        <ng-template uiTableCell="actions" let-row>
          <button mat-icon-button aria-label="Edit" (click)="edit(row)"><mat-icon>edit</mat-icon></button>
          <button mat-icon-button aria-label="Delete" (click)="remove(row)"><mat-icon>delete_outline</mat-icon></button>
        </ng-template>
      </ui-data-table>
    </div>

    <ui-drawer [(open)]="drawerOpen" [title]="editingId() ? 'Edit document type' : 'Add document type'" width="440px">
      <form class="space-y-4" [formGroup]="form" (ngSubmit)="save()" novalidate>
        <div class="tn-field">
          <label for="dt-code" class="tn-required">Code</label>
          <input id="dt-code" class="tn-input uppercase font-mono" formControlName="code" maxlength="40" placeholder="PASSPORT" />
          <span class="tn-hint">A–Z, 0–9 and underscore. Unique per company.</span>
        </div>
        <div class="tn-field">
          <label for="dt-name" class="tn-required">Name</label>
          <input id="dt-name" class="tn-input" formControlName="name" maxlength="150" />
        </div>
        <div class="tn-field">
          <label for="dt-desc">Description</label>
          <textarea id="dt-desc" class="tn-input" formControlName="description" maxlength="500"></textarea>
        </div>
        <label class="flex items-center gap-2 text-sm"><input type="checkbox" formControlName="requiresExpiryDate" /> Has an expiry date</label>
        <label class="flex items-center gap-2 text-sm"><input type="checkbox" formControlName="isActive" /> Active</label>
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
export class DocumentTypesTabComponent {
  private readonly service = inject(TravelConfigService);
  private readonly fb = inject(FormBuilder);
  private readonly toast = inject(UiToastService);
  private readonly confirm = inject(UiConfirmService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly columns: UiTableColumn[] = [
    { key: 'code', label: 'Code', sortable: true },
    { key: 'name', label: 'Name', sortable: true },
    { key: 'requiresExpiryDate', label: 'Expiry' },
    { key: 'isActive', label: 'Status' }
  ];
  protected readonly rows = signal<TravelDocumentType[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly drawerOpen = signal(false);
  protected readonly editingId = signal<number | null>(null);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);

  protected readonly form = this.fb.group({
    code: ['', [Validators.required, Validators.maxLength(40), Validators.pattern(/^[A-Za-z0-9_]+$/)]],
    name: ['', [Validators.required, Validators.maxLength(150)]],
    description: [''],
    requiresExpiryDate: [false],
    isActive: [true]
  });

  constructor() { this.load(); }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.service.getDocumentTypes().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: rows => { this.rows.set(rows); this.loading.set(false); },
      error: err => { this.error.set(apiErrorMessage(err, 'Could not load document types.')); this.loading.set(false); }
    });
  }

  protected edit(row: TravelDocumentType | null): void {
    this.editingId.set(row?.id ?? null);
    this.saveError.set(null);
    this.form.reset({
      code: row?.code ?? '', name: row?.name ?? '', description: row?.description ?? '',
      requiresExpiryDate: row?.requiresExpiryDate ?? false, isActive: row?.isActive ?? true
    });
    this.drawerOpen.set(true);
  }

  protected save(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const dto: SaveTravelDocumentType = {
      code: v.code!.trim().toUpperCase(), name: v.name!.trim(), description: v.description?.trim() || null,
      requiresExpiryDate: !!v.requiresExpiryDate, isActive: !!v.isActive
    };
    const id = this.editingId();
    this.saving.set(true);
    (id ? this.service.updateDocumentType(id, dto) : this.service.createDocumentType(dto))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => { this.saving.set(false); this.drawerOpen.set(false); this.toast.success('Document type saved.'); this.load(); },
        error: err => { this.saving.set(false); this.saveError.set(apiErrorMessage(err, 'Could not save the document type.')); }
      });
  }

  protected async remove(row: TravelDocumentType): Promise<void> {
    if (!(await this.confirm.confirmDelete(`Delete document type "${row.name}"?`))) return;
    this.service.deleteDocumentType(row.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.toast.success('Document type deleted.'); this.load(); },
      error: err => this.toast.error(apiErrorMessage(err, 'Could not delete the document type.'))
    });
  }
}
