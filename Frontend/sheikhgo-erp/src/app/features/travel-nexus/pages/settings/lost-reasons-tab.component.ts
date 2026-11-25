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
import { TravelLostReason } from '../../models/travel.models';
import { TravelConfigService } from '../../services/travel-config.service';

@Component({
  selector: 'app-lost-reasons-tab',
  standalone: true,
  imports: [ReactiveFormsModule, MatButtonModule, MatIconModule, UiDataTableComponent, UiTableCellDirective,
    UiDrawerComponent, UiStatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-3">
      <div class="flex items-center justify-between">
        <p class="text-sm text-fleet-text-muted">Why enquiries are lost (required when marking an enquiry lost).</p>
        <button mat-flat-button color="primary" (click)="edit(null)"><mat-icon>add</mat-icon> Add lost reason</button>
      </div>
      @if (error()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}
        <button mat-stroked-button class="ml-auto" (click)="load()">Retry</button></div> }
      <ui-data-table [columns]="columns" [data]="rows()" [loading]="loading()"
        emptyIcon="block" emptyTitle="No lost reasons yet" emptyDescription="e.g. Price, Chose competitor, Visa refused, No response.">
        <ng-template uiTableCell="isActive" let-row><ui-status-badge [status]="row.isActive ? 'active' : 'inactive'"></ui-status-badge></ng-template>
        <ng-template uiTableCell="actions" let-row>
          <button mat-icon-button aria-label="Edit" (click)="edit(row)"><mat-icon>edit</mat-icon></button>
          <button mat-icon-button aria-label="Delete" (click)="remove(row)"><mat-icon>delete_outline</mat-icon></button>
        </ng-template>
      </ui-data-table>
    </div>

    <ui-drawer [(open)]="drawerOpen" [title]="editingId() ? 'Edit lost reason' : 'Add lost reason'" width="400px">
      <form class="space-y-4" [formGroup]="form" (ngSubmit)="save()" novalidate>
        <div class="tn-field">
          <label for="ls-name" class="tn-required">Name</label>
          <input id="ls-name" class="tn-input" formControlName="name" maxlength="100" />
        </div>
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
export class LostReasonsTabComponent {
  private readonly service = inject(TravelConfigService);
  private readonly fb = inject(FormBuilder);
  private readonly toast = inject(UiToastService);
  private readonly confirm = inject(UiConfirmService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly columns: UiTableColumn[] = [
    { key: 'name', label: 'Name', sortable: true },
    { key: 'isActive', label: 'Status' }
  ];
  protected readonly rows = signal<TravelLostReason[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly drawerOpen = signal(false);
  protected readonly editingId = signal<number | null>(null);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);

  protected readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    isActive: [true]
  });

  constructor() { this.load(); }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.service.getLostReasons().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: rows => { this.rows.set(rows); this.loading.set(false); },
      error: err => { this.error.set(apiErrorMessage(err, 'Could not load lost reasons.')); this.loading.set(false); }
    });
  }

  protected edit(row: TravelLostReason | null): void {
    this.editingId.set(row?.id ?? null);
    this.saveError.set(null);
    this.form.reset({ name: row?.name ?? '', isActive: row?.isActive ?? true });
    this.drawerOpen.set(true);
  }

  protected save(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const dto = { name: v.name!.trim(), isActive: !!v.isActive };
    const id = this.editingId();
    this.saving.set(true);
    (id ? this.service.updateLostReason(id, dto) : this.service.createLostReason(dto))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => { this.saving.set(false); this.drawerOpen.set(false); this.toast.success('Lost reason saved.'); this.load(); },
        error: err => { this.saving.set(false); this.saveError.set(apiErrorMessage(err, 'Could not save the lost reason.')); }
      });
  }

  protected async remove(row: TravelLostReason): Promise<void> {
    if (!(await this.confirm.confirmDelete(`Delete lost reason "${row.name}"?`))) return;
    this.service.deleteLostReason(row.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.toast.success('Lost reason deleted.'); this.load(); },
      error: err => this.toast.error(apiErrorMessage(err, 'Could not delete the lost reason.'))
    });
  }
}
