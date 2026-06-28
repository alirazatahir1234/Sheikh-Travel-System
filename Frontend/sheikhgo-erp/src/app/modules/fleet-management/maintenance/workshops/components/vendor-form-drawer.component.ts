import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { UiToastService } from '../../../../../shared/components/ui/toast/ui-toast.service';
import { UiDrawerComponent } from '../../../../../shared/components/ui/drawer/ui-drawer.component';
import { PhoneDigitsOnlyDirective } from '../../../../../shared/directives/phone-digits-only.directive';
import { MaintenanceService } from '../../../../../core/services/maintenance.service';
import { CreateVendorPayload, Vendor } from '../../../../../core/models/maintenance.model';
import { apiErrorMessage } from '../../../../../core/utils/api-error.util';

const VENDOR_CATEGORIES = ['Parts', 'Fluids', 'Tires', 'Electrical', 'Other'] as const;

@Component({
  selector: 'vendor-form-drawer',
  standalone: true,
  imports: [FormsModule, UiDrawerComponent, PhoneDigitsOnlyDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-drawer [open]="open()" [title]="vendor() ? 'Edit Vendor' : 'Add Vendor'" (closed)="closed.emit()">
      <form class="form" (ngSubmit)="submit()">
        <label>Vendor Name<input [(ngModel)]="form.name" name="name" required /></label>
        <label>Category
          <select [(ngModel)]="form.category" name="category">
            @for (c of categories; track c) { <option [value]="c">{{ c }}</option> }
          </select>
        </label>
        <label>Contact Person<input [(ngModel)]="form.contactPerson" name="contactPerson" /></label>
        <label>Mobile<input type="tel" [(ngModel)]="form.contactPhone" name="contactPhone" placeholder="501234567" /></label>
        <label>Email<input type="email" [(ngModel)]="form.contactEmail" name="contactEmail" /></label>
        <label>Products <span class="hint">(comma-separated)</span>
          <input [(ngModel)]="productsText" name="products" placeholder="Brake pads, Oil filters" />
        </label>
        <label>Rating (1–5)<input type="number" min="1" max="5" step="0.1" [(ngModel)]="form.rating" name="rating" /></label>
        <label class="check"><input type="checkbox" [(ngModel)]="form.isPreferred" name="isPreferred" /> Preferred vendor</label>
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
    .check { display: flex; align-items: center; gap: 0.5rem; flex-direction: row; }
    .hint { font-weight: 400; color: #94a3b8; }
    input, select { padding: 0.5rem 0.625rem; border: 1px solid #e2e8f0; border-radius: 8px; }
    .footer { display: flex; justify-content: flex-end; gap: 0.5rem; margin-top: 0.5rem; }
    .btn-primary { background: #0B6B50; color: #fff; border: none; border-radius: 8px; padding: 0.5rem 1rem; font-weight: 600; cursor: pointer; }
    .btn-muted { background: #f1f5f9; color: #475569; border: none; border-radius: 8px; padding: 0.5rem 1rem; cursor: pointer; }
    .btn-primary:disabled { opacity: 0.6; }
  `]
})
export class VendorFormDrawerComponent {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly toast = inject(UiToastService);

  readonly categories = VENDOR_CATEGORIES;
  readonly open = input(false);
  readonly vendor = input<Vendor | null>(null);
  readonly closed = output<void>();
  readonly saved = output<void>();

  readonly saving = signal(false);
  form: CreateVendorPayload = { name: '', category: 'Parts', isPreferred: false };
  productsText = '';

  constructor() {
    effect(() => {
      if (!this.open()) return;
      const v = this.vendor();
      if (v) {
        this.form = {
          name: v.name,
          category: v.category,
          contactPerson: v.contactPerson ?? undefined,
          contactPhone: v.contactPhone ?? undefined,
          contactEmail: v.contactEmail ?? undefined,
          rating: v.rating ?? undefined,
          isPreferred: v.isPreferred,
          products: v.products
        };
        this.productsText = v.products.join(', ');
      } else {
        this.form = { name: '', category: 'Parts', isPreferred: false };
        this.productsText = '';
      }
    }, { allowSignalWrites: true });
  }

  submit(): void {
    if (!this.form.name.trim()) return;
    this.saving.set(true);
    const products = this.productsText.split(',').map(p => p.trim()).filter(Boolean);
    const body = { ...this.form, products };
    const v = this.vendor();
    if (v) {
      this.maintenanceService.updateVendor(v.id, body).subscribe({
        next: () => this.onSuccess(),
        error: (err: unknown) => this.onError(err)
      });
    } else {
      this.maintenanceService.createVendor(body).subscribe({
        next: () => this.onSuccess(),
        error: (err: unknown) => this.onError(err)
      });
    }
  }

  private onSuccess(): void {
    this.saving.set(false);
    this.saved.emit();
    this.closed.emit();
  }

  private onError(err: unknown): void {
    this.saving.set(false);
    this.toast.error(apiErrorMessage(err, 'Failed to save vendor'));
  }
}
