import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { UiDrawerComponent } from '../../../../../shared/components/ui/drawer/ui-drawer.component';
import { UiToastService } from '../../../../../shared/components/ui/toast/ui-toast.service';
import { MaintenanceService } from '../../../../../core/services/maintenance.service';
import { Part } from '../../../../../core/models/maintenance.model';
import { apiErrorMessage } from '../../../../../core/utils/api-error.util';

@Component({
  selector: 'transfer-stock-drawer',
  standalone: true,
  imports: [FormsModule, UiDrawerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-drawer [open]="open()" [title]="'Transfer Stock — ' + (part()?.partName ?? '')" (closed)="closed.emit()">
      @if (part(); as p) {
        <form class="form" (ngSubmit)="submit()">
          <p class="hint">Available at source: <strong>{{ p.stockQuantity }}</strong></p>
          <label>From Location
            <input [(ngModel)]="fromLocation" name="from" [placeholder]="p.location || 'Main warehouse'" />
          </label>
          <label>To Location<input [(ngModel)]="toLocation" name="to" required placeholder="Workshop B" /></label>
          <label>Quantity<input type="number" min="1" [max]="p.stockQuantity" [(ngModel)]="quantity" name="qty" required /></label>
          <label>Notes<textarea [(ngModel)]="notes" name="notes" rows="2"></textarea></label>
          <footer class="footer">
            <button type="button" class="btn-muted" (click)="closed.emit()">Cancel</button>
            <button type="submit" class="btn-primary" [disabled]="saving()">{{ saving() ? 'Transferring…' : 'Transfer' }}</button>
          </footer>
        </form>
      }
    </ui-drawer>
  `,
  styles: [`
    .form { display: grid; gap: 0.875rem; }
    .hint { margin: 0; font-size: 0.8125rem; color: #64748b; }
    label { display: grid; gap: 0.35rem; font-size: 0.8125rem; font-weight: 600; color: #334155; }
    input, textarea { padding: 0.5rem 0.625rem; border: 1px solid #e2e8f0; border-radius: 8px; font-family: inherit; }
    .footer { display: flex; justify-content: flex-end; gap: 0.5rem; margin-top: 0.5rem; }
    .btn-primary { background: #0B6B50; color: #fff; border: none; border-radius: 8px; padding: 0.5rem 1rem; font-weight: 600; cursor: pointer; }
    .btn-muted { background: #f1f5f9; color: #475569; border: none; border-radius: 8px; padding: 0.5rem 1rem; cursor: pointer; }
    .btn-primary:disabled { opacity: 0.6; }
  `]
})
export class TransferStockDrawerComponent {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly toast = inject(UiToastService);

  readonly open = input(false);
  readonly part = input<Part | null>(null);
  readonly closed = output<void>();
  readonly saved = output<void>();

  readonly saving = signal(false);
  fromLocation = '';
  toLocation = '';
  quantity = 1;
  notes = '';

  constructor() {
    effect(() => {
      if (!this.open()) return;
      const p = this.part();
      this.fromLocation = p?.location ?? '';
      this.toLocation = '';
      this.quantity = 1;
      this.notes = '';
    }, { allowSignalWrites: true });
  }

  submit(): void {
    const p = this.part();
    if (!p || this.quantity <= 0 || !this.toLocation.trim()) return;
    this.saving.set(true);
    this.maintenanceService.transferPartStock(p.id, {
      quantity: this.quantity,
      fromLocation: this.fromLocation.trim(),
      toLocation: this.toLocation.trim(),
      notes: this.notes.trim() || undefined
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success('Stock transferred');
        this.saved.emit();
        this.closed.emit();
      },
      error: err => {
        this.saving.set(false);
        this.toast.error(apiErrorMessage(err, 'Failed to transfer stock'));
      }
    });
  }
}
