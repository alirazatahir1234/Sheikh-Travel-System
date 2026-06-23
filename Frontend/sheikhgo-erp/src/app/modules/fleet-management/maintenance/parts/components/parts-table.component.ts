import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { Part } from '../../../../../core/models/maintenance.model';

@Component({
  selector: 'parts-table',
  standalone: true,
  imports: [CurrencyPipe, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Part Number</th>
            <th>Part Name</th>
            <th>Category</th>
            <th>Vehicle Compatibility</th>
            <th>Quantity</th>
            <th>Min Stock</th>
            <th>Unit Cost</th>
            <th>Status</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (p of parts(); track p.id) {
            <tr [class.row--low]="p.stockStatus === 'LowStock'" [class.row--out]="p.stockStatus === 'OutOfStock'">
              <td><code>{{ p.partNumber }}</code></td>
              <td><strong>{{ p.partName }}</strong></td>
              <td>{{ p.category || '—' }}</td>
              <td class="compat">
                @if (p.vehicleCompatibility?.length) {
                  @for (v of p.vehicleCompatibility.slice(0, 2); track v) {
                    <span class="chip">{{ v }}</span>
                  }
                  @if (p.vehicleCompatibility.length > 2) {
                    <span class="chip chip--more">+{{ p.vehicleCompatibility.length - 2 }}</span>
                  }
                } @else { — }
              </td>
              <td>{{ p.stockQuantity }}</td>
              <td>{{ p.minStockLevel }}</td>
              <td>{{ p.unitCost | currency }}</td>
              <td>
                <span class="badge" [attr.data-status]="p.stockStatus">{{ statusLabel(p) }}</span>
              </td>
              <td class="actions">
                <button type="button" title="Add Stock" (click)="addStock.emit(p)"><mat-icon>add_box</mat-icon></button>
                <button type="button" title="Issue Part" (click)="issuePart.emit(p)"><mat-icon>output</mat-icon></button>
                <button type="button" title="Transfer Stock" (click)="transferStock.emit(p)"><mat-icon>swap_horiz</mat-icon></button>
              </td>
            </tr>
          } @empty {
            <tr><td colspan="9" class="empty">No parts found.</td></tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: [`
    .table-wrap { overflow-x: auto; background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; }
    table { width: 100%; border-collapse: collapse; font-size: 0.8125rem; min-width: 960px; }
    th { text-align: left; color: #64748b; padding: 0.625rem 0.75rem; border-bottom: 1px solid #e2e8f0; white-space: nowrap; }
    td { padding: 0.625rem 0.75rem; border-bottom: 1px solid #f1f5f9; vertical-align: middle; }
    code { font-family: monospace; font-size: 0.75rem; background: #f1f5f9; padding: 0.1rem 0.35rem; border-radius: 4px; }
    .compat { display: flex; flex-wrap: wrap; gap: 0.25rem; }
    .chip { display: inline-block; padding: 0.1rem 0.4rem; border-radius: 999px; background: #e8f5f0; color: #0B6B50; font-size: 0.6875rem; font-weight: 600; }
    .chip--more { background: #f1f5f9; color: #64748b; }
    .badge { display: inline-block; padding: 0.15rem 0.5rem; border-radius: 999px; font-size: 0.6875rem; font-weight: 700; }
    .badge[data-status="InStock"] { background: #d1fae5; color: #0B6B50; }
    .badge[data-status="LowStock"] { background: #fef3c7; color: #b45309; }
    .badge[data-status="OutOfStock"] { background: #fee2e2; color: #dc2626; }
    .row--low { background: #fffbeb; }
    .row--out { background: #fef2f2; }
    .actions { display: flex; gap: 0.25rem; white-space: nowrap; }
    .actions button { border: 1px solid #e2e8f0; background: #fff; border-radius: 6px; width: 2rem; height: 2rem; cursor: pointer; display: inline-flex; align-items: center; justify-content: center; color: #475569; }
    .actions button:hover { border-color: #0B6B50; color: #0B6B50; }
    .actions mat-icon { font-size: 1rem; width: 1rem; height: 1rem; }
    .empty { text-align: center; color: #94a3b8; padding: 2rem !important; }
  `]
})
export class PartsTableComponent {
  readonly parts = input.required<Part[]>();
  readonly addStock = output<Part>();
  readonly issuePart = output<Part>();
  readonly transferStock = output<Part>();

  statusLabel(p: Part): string {
    if (p.stockStatus === 'OutOfStock') return 'Out Of Stock';
    if (p.stockStatus === 'LowStock') return 'Low Stock';
    return 'In Stock';
  }
}
