import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, OnInit, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { UiDrawerComponent } from '../../../../../shared/components/ui/drawer/ui-drawer.component';
import { UiToastService } from '../../../../../shared/components/ui/toast/ui-toast.service';
import { MaintenanceService } from '../../../../../core/services/maintenance.service';
import { VehicleService } from '../../../../../core/services/vehicle.service';
import { Part } from '../../../../../core/models/maintenance.model';
import { VehicleListItem } from '../../../../../core/models/vehicle.model';
import { apiErrorMessage } from '../../../../../core/utils/api-error.util';

@Component({
  selector: 'issue-part-drawer',
  standalone: true,
  imports: [FormsModule, UiDrawerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-drawer [open]="open()" [title]="'Issue Part — ' + (part()?.partName ?? '')" (closed)="closed.emit()">
      @if (part(); as p) {
        <form class="form" (ngSubmit)="submit()">
          <p class="hint">Available stock: <strong>{{ p.stockQuantity }}</strong></p>
          <label>Vehicle
            <select [(ngModel)]="vehicleId" name="vehicleId" required>
              <option [ngValue]="null">Select vehicle</option>
              @for (v of vehicles(); track v.id) {
                <option [ngValue]="v.id">{{ v.name }} ({{ v.registrationNumber }})</option>
              }
            </select>
          </label>
          <label>Quantity<input type="number" min="1" [max]="p.stockQuantity" [(ngModel)]="quantity" name="qty" required /></label>
          <label>Work Order ID <small>(optional)</small>
            <input type="number" min="1" [(ngModel)]="workOrderId" name="wo" placeholder="Link to WO #" />
          </label>
          <label>Notes<textarea [(ngModel)]="notes" name="notes" rows="2"></textarea></label>
          <footer class="footer">
            <button type="button" class="btn-muted" (click)="closed.emit()">Cancel</button>
            <button type="submit" class="btn-primary" [disabled]="saving() || !vehicleId">{{ saving() ? 'Issuing…' : 'Issue Part' }}</button>
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
    input, select, textarea { padding: 0.5rem 0.625rem; border: 1px solid #e2e8f0; border-radius: 8px; font-family: inherit; }
    .footer { display: flex; justify-content: flex-end; gap: 0.5rem; margin-top: 0.5rem; }
    .btn-primary { background: #0B6B50; color: #fff; border: none; border-radius: 8px; padding: 0.5rem 1rem; font-weight: 600; cursor: pointer; }
    .btn-muted { background: #f1f5f9; color: #475569; border: none; border-radius: 8px; padding: 0.5rem 1rem; cursor: pointer; }
    .btn-primary:disabled { opacity: 0.6; }
  `]
})
export class IssuePartDrawerComponent implements OnInit {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly vehicleService = inject(VehicleService);
  private readonly toast = inject(UiToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly open = input(false);
  readonly part = input<Part | null>(null);
  readonly closed = output<void>();
  readonly saved = output<void>();

  readonly vehicles = signal<VehicleListItem[]>([]);
  readonly saving = signal(false);
  vehicleId: number | null = null;
  quantity = 1;
  workOrderId: number | null = null;
  notes = '';

  constructor() {
    effect(() => {
      if (!this.open()) return;
      this.vehicleId = null;
      this.quantity = 1;
      this.workOrderId = null;
      this.notes = '';
    }, { allowSignalWrites: true });
  }

  ngOnInit(): void {
    this.vehicleService.getAll(1, 500).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => this.vehicles.set(r.items),
      error: () => this.vehicles.set([])
    });
  }

  submit(): void {
    const p = this.part();
    if (!p || !this.vehicleId || this.quantity <= 0) return;
    this.saving.set(true);
    this.maintenanceService.issuePart(p.id, {
      vehicleId: this.vehicleId,
      quantity: this.quantity,
      workOrderId: this.workOrderId ?? undefined,
      notes: this.notes.trim() || undefined
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success('Part issued');
        this.saved.emit();
        this.closed.emit();
      },
      error: err => {
        this.saving.set(false);
        this.toast.error(apiErrorMessage(err, 'Failed to issue part'));
      }
    });
  }
}
