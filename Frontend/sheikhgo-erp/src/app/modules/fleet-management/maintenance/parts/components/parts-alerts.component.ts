import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { Part } from '../../../../../core/models/maintenance.model';

@Component({
  selector: 'parts-alerts',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (lowStockParts().length || outOfStockParts().length) {
      <div class="alerts">
        @if (outOfStockParts().length) {
          <div class="alert alert--danger">
            <mat-icon>error</mat-icon>
            <div>
              <strong>{{ outOfStockParts().length }} part(s) out of stock</strong>
              <p>{{ outOfStockNames() }}</p>
            </div>
          </div>
        }
        @if (lowStockParts().length) {
          <div class="alert alert--warning">
            <mat-icon>warning</mat-icon>
            <div>
              <strong>{{ lowStockParts().length }} part(s) below minimum stock</strong>
              <p>{{ lowStockNames() }}</p>
            </div>
          </div>
        }
      </div>
    }
  `,
  styles: [`
    .alerts { display: grid; gap: 0.625rem; margin-bottom: 1rem; }
    .alert {
      display: flex; align-items: flex-start; gap: 0.625rem;
      padding: 0.75rem 1rem; border-radius: 10px; font-size: 0.8125rem;
    }
    .alert mat-icon { font-size: 1.25rem; width: 1.25rem; height: 1.25rem; flex-shrink: 0; margin-top: 1px; }
    .alert strong { display: block; font-weight: 700; margin-bottom: 0.15rem; }
    .alert p { margin: 0; color: inherit; opacity: 0.9; }
    .alert--danger { background: #fef2f2; border: 1px solid #fecaca; color: #991b1b; }
    .alert--warning { background: #fffbeb; border: 1px solid #fde68a; color: #92400e; }
  `]
})
export class PartsAlertsComponent {
  readonly parts = input.required<Part[]>();

  readonly lowStockParts = computed(() =>
    this.parts().filter(p => p.stockStatus === 'LowStock'));
  readonly outOfStockParts = computed(() =>
    this.parts().filter(p => p.stockStatus === 'OutOfStock'));

  lowStockNames(): string {
    return this.lowStockParts().map(p => p.partName).slice(0, 5).join(', ')
      + (this.lowStockParts().length > 5 ? '…' : '');
  }

  outOfStockNames(): string {
    return this.outOfStockParts().map(p => p.partName).slice(0, 5).join(', ')
      + (this.outOfStockParts().length > 5 ? '…' : '');
  }
}
