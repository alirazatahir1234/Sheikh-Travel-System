import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { UiDrawerComponent } from '../../../../../shared/components/ui/drawer/ui-drawer.component';
import { UiToastService } from '../../../../../shared/components/ui/toast/ui-toast.service';
import { MaintenanceService } from '../../../../../core/services/maintenance.service';
import { Part } from '../../../../../core/models/maintenance.model';
import { apiErrorMessage } from '../../../../../core/utils/api-error.util';

@Component({
  selector: 'add-stock-drawer',
  standalone: true,
  imports: [FormsModule, UiDrawerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-drawer [open]="open()" [title]="'Add Stock — ' + (part()?.partName ?? '')" (closed)="closed.emit()">
      @if (part(); as p) {
        <form class="form" (ngSubmit)="submit()">
          <p class="hint">Current stock: <strong>{{ p.stockQuantity }}</strong></p>
          <label>Quantity to add<input type="number" min="1" [(ngModel)]="quantity" name="qty" required /></label>
          <label>Location <small>(optional update)</small>
            <input [(ngModel)]="location" name="location" [placeholder]="p.location || 'Warehouse'" />
          </label>
          <label>Notes<textarea [(ngModel)]="notes" name="notes" rows="2"></textarea></label>
          <footer class="footer">
            <button type="button" class="btn-muted" (click)="closed.emit()">Cancel</button>
            <button type="submit" class="btn-primary" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Add Stock' }}</button>
          </footer>
        </form>
      }
    </ui-drawer>
  `,
  styles: [`
    .form { display: grid; gap: 0.875rem; }
    .hint { margin: 0; font-size: 0.8125rem; color: #64748b; }
    label { display: grid; gap: 0.35rem; font-size: 0.8125rem; font-weight: 600; color: #334155; }
    label small { font-weight: 500; color: #94a3b8; }
    input, textarea { padding: 0.5rem 0.625rem; border: 1px solid #e2e8f0; border-radius: 8px; font-family: inherit; }
    .footer { display: flex; justify-content: flex-end; gap: 0.5rem; margin-top: 0.5rem; }
    .btn-primary { background: #0B6B50; color: #fff; border: none; border-radius: 8px; padding: 0.5rem 1rem; font-weight: 600; cursor: pointer; }
    .btn-muted { background: #f1f5f9; color: #475569; border: none; border-radius: 8px; padding: 0.5rem 1rem; cursor: pointer; }
    .btn-primary:disabled { opacity: 0.6; }
  `]
})
export class AddStockDrawerComponent {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly toast = inject(UiToastService);

  readonly open = input(false);
  readonly part = input<Part | null>(null);
  readonly closed = output<void>();
  readonly saved = output<void>();

  readonly saving = signal(false);
  quantity = 1;
  location = '';
  notes = '';

  constructor() {
    effect(() => {
      if (!this.open()) return;
      this.quantity = 1;
      this.location = this.part()?.location ?? '';
      this.notes = '';
    }, { allowSignalWrites: true });
  }

  submit(): void {
    const p = this.part();
    if (!p || this.quantity <= 0) return;
    this.saving.set(true);
    this.maintenanceService.addPartStock(p.id, {
      quantity: this.quantity,
      location: this.location.trim() || undefined,
      notes: this.notes.trim() || undefined
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success('Stock added');
        this.saved.emit();
        this.closed.emit();
      },
      error: err => {
        this.saving.set(false);
        this.toast.error(apiErrorMessage(err, 'Failed to add stock'));
      }
    });
  }
}
