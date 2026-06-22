import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { UiDrawerComponent } from '../../../../../shared/components/ui/drawer/ui-drawer.component';
import { UiToastService } from '../../../../../shared/components/ui/toast/ui-toast.service';
import { MaintenanceService } from '../../../../../core/services/maintenance.service';
import { CreatePartPayload } from '../../../../../core/models/maintenance.model';
import { apiErrorMessage } from '../../../../../core/utils/api-error.util';

@Component({
  selector: 'part-form-drawer',
  standalone: true,
  imports: [FormsModule, UiDrawerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-drawer [open]="open()" title="Add Part" (closed)="closed.emit()">
      <form class="form" (ngSubmit)="submit()">
        <label>Part Number<input [(ngModel)]="form.partNumber" name="partNumber" required /></label>
        <label>Part Name<input [(ngModel)]="form.partName" name="partName" required /></label>
        <label>Category<input [(ngModel)]="form.category" name="category" /></label>
        <label>Vehicle Compatibility <small>(comma-separated)</small>
          <input [(ngModel)]="compatibilityText" name="compat" placeholder="Toyota Hiace, Ford Transit" />
        </label>
        <label>Unit Cost<input type="number" min="0" step="0.01" [(ngModel)]="form.unitCost" name="unitCost" /></label>
        <label>Minimum Stock<input type="number" min="0" [(ngModel)]="form.minStockLevel" name="minStock" /></label>
        <label>Initial Stock<input type="number" min="0" [(ngModel)]="form.initialStock" name="initialStock" /></label>
        <label>Location<input [(ngModel)]="form.location" name="location" placeholder="Warehouse A" /></label>
        <footer class="footer">
          <button type="button" class="btn-muted" (click)="closed.emit()">Cancel</button>
          <button type="submit" class="btn-primary" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Save Part' }}</button>
        </footer>
      </form>
    </ui-drawer>
  `,
  styles: [`
    .form { display: grid; gap: 0.875rem; }
    label { display: grid; gap: 0.35rem; font-size: 0.8125rem; font-weight: 600; color: #334155; }
    label small { font-weight: 500; color: #94a3b8; }
    input { padding: 0.5rem 0.625rem; border: 1px solid #e2e8f0; border-radius: 8px; }
    .footer { display: flex; justify-content: flex-end; gap: 0.5rem; margin-top: 0.5rem; }
    .btn-primary { background: #0B6B50; color: #fff; border: none; border-radius: 8px; padding: 0.5rem 1rem; font-weight: 600; cursor: pointer; }
    .btn-muted { background: #f1f5f9; color: #475569; border: none; border-radius: 8px; padding: 0.5rem 1rem; cursor: pointer; }
    .btn-primary:disabled { opacity: 0.6; }
  `]
})
export class PartFormDrawerComponent {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly toast = inject(UiToastService);

  readonly open = input(false);
  readonly closed = output<void>();
  readonly saved = output<void>();

  readonly saving = signal(false);
  form: CreatePartPayload = {
    partNumber: '', partName: '', unitCost: 0, minStockLevel: 5, initialStock: 0
  };
  compatibilityText = '';

  constructor() {
    effect(() => {
      if (!this.open()) return;
      this.form = { partNumber: '', partName: '', unitCost: 0, minStockLevel: 5, initialStock: 0 };
      this.compatibilityText = '';
    }, { allowSignalWrites: true });
  }

  submit(): void {
    if (!this.form.partNumber.trim() || !this.form.partName.trim()) return;
    this.saving.set(true);
    const payload: CreatePartPayload = {
      ...this.form,
      vehicleCompatibility: this.compatibilityText
        .split(',').map(s => s.trim()).filter(Boolean)
    };
    this.maintenanceService.createPart(payload).subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success('Part created');
        this.saved.emit();
        this.closed.emit();
      },
      error: err => {
        this.saving.set(false);
        this.toast.error(apiErrorMessage(err, 'Failed to create part'));
      }
    });
  }
}
