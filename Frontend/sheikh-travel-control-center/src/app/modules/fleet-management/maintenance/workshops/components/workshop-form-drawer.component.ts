import { ChangeDetectionStrategy, Component, effect, inject, input, OnInit, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { UiToastService } from '../../../../../shared/components/ui/toast/ui-toast.service';
import { UiDrawerComponent } from '../../../../../shared/components/ui/drawer/ui-drawer.component';
import { MaintenanceService } from '../../../../../core/services/maintenance.service';
import { CreateWorkshopPayload, Workshop } from '../../../../../core/models/maintenance.model';
import { apiErrorMessage } from '../../../../../core/utils/api-error.util';

@Component({
  selector: 'workshop-form-drawer',
  standalone: true,
  imports: [FormsModule, UiDrawerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-drawer [open]="open()" [title]="workshop() ? 'Edit Workshop' : 'Add Workshop'" (closed)="closed.emit()">
      <form class="form" (ngSubmit)="submit()">
        <label>Workshop Name<input [(ngModel)]="form.name" name="name" required /></label>
        <label>Contact Person<input [(ngModel)]="form.contactPerson" name="contactPerson" /></label>
        <label>Mobile<input [(ngModel)]="form.contactPhone" name="contactPhone" /></label>
        <label>Email<input type="email" [(ngModel)]="form.contactEmail" name="contactEmail" /></label>
        <label>Address<input [(ngModel)]="form.location" name="location" /></label>
        <label>Rating (1–5)<input type="number" min="1" max="5" step="0.1" [(ngModel)]="form.rating" name="rating" /></label>
        <label>Type
          <select [(ngModel)]="form.workshopType" name="workshopType">
            <option>Internal</option><option>External</option>
          </select>
        </label>
        <label>Capacity<input type="number" [(ngModel)]="form.capacity" name="capacity" /></label>
        <footer class="footer">
          <button type="button" class="btn-muted" (click)="closed.emit()">Cancel</button>
          <button type="submit" class="btn-primary" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Save' }}</button>
        </footer>
      </form>
    </ui-drawer>
  `,
  styles: [`
    .form { display: grid; gap: 0.875rem; }
    label { display: grid; gap: 0.35rem; font-size: 0.8125rem; font-weight: 600; color: #334155; }
    input, select { padding: 0.5rem 0.625rem; border: 1px solid #e2e8f0; border-radius: 8px; }
    .footer { display: flex; justify-content: flex-end; gap: 0.5rem; margin-top: 0.5rem; }
    .btn-primary { background: #0B6B50; color: #fff; border: none; border-radius: 8px; padding: 0.5rem 1rem; font-weight: 600; cursor: pointer; }
    .btn-muted { background: #f1f5f9; color: #475569; border: none; border-radius: 8px; padding: 0.5rem 1rem; cursor: pointer; }
    .btn-primary:disabled { opacity: 0.6; }
  `]
})
export class WorkshopFormDrawerComponent implements OnInit {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly toast = inject(UiToastService);

  readonly open = input(false);
  readonly workshop = input<Workshop | null>(null);
  readonly closed = output<void>();
  readonly saved = output<void>();

  readonly saving = signal(false);
  form: CreateWorkshopPayload = { name: '', workshopType: 'Internal' };

  constructor() {
    effect(() => {
      if (!this.open()) return;
      const w = this.workshop();
      this.form = w ? {
        name: w.name,
        workshopType: w.workshopType,
        location: w.location ?? undefined,
        contactPerson: w.contactPerson ?? undefined,
        contactPhone: w.contactPhone ?? undefined,
        contactEmail: w.contactEmail ?? undefined,
        capacity: w.capacity ?? undefined,
        rating: w.rating ?? undefined
      } : { name: '', workshopType: 'Internal' };
    }, { allowSignalWrites: true });
  }

  ngOnInit(): void {}

  submit(): void {
    if (!this.form.name.trim()) return;
    this.saving.set(true);
    const w = this.workshop();
    if (w) {
      this.maintenanceService.updateWorkshop(w.id, this.form).subscribe({
        next: () => this.onSuccess(),
        error: (err: unknown) => this.onError(err, 'Failed to save workshop')
      });
    } else {
      this.maintenanceService.createWorkshop(this.form).subscribe({
        next: () => this.onSuccess(),
        error: (err: unknown) => this.onError(err, 'Failed to save workshop')
      });
    }
  }

  private onSuccess(): void {
    this.saving.set(false);
    this.saved.emit();
    this.closed.emit();
  }

  private onError(err: unknown, msg: string): void {
    this.saving.set(false);
    this.toast.error(apiErrorMessage(err, msg));
  }
}
