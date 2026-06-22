import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { PartsInventoryStats } from '../../../../../core/models/maintenance.model';

@Component({
  selector: 'parts-inventory-stats',
  standalone: true,
  imports: [CurrencyPipe, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="stats">
      <div class="stat-card">
        <div class="stat-icon stat-icon--brand"><mat-icon>inventory_2</mat-icon></div>
        <div>
          <p class="stat-value">{{ stats()?.totalParts ?? 0 }}</p>
          <p class="stat-label">Total Parts</p>
        </div>
      </div>
      <div class="stat-card stat-card--amber">
        <div class="stat-icon stat-icon--amber"><mat-icon>warning</mat-icon></div>
        <div>
          <p class="stat-value">{{ stats()?.lowStock ?? 0 }}</p>
          <p class="stat-label">Low Stock</p>
        </div>
      </div>
      <div class="stat-card stat-card--red">
        <div class="stat-icon stat-icon--red"><mat-icon>remove_shopping_cart</mat-icon></div>
        <div>
          <p class="stat-value">{{ stats()?.outOfStock ?? 0 }}</p>
          <p class="stat-label">Out Of Stock</p>
        </div>
      </div>
      <div class="stat-card stat-card--green">
        <div class="stat-icon stat-icon--green"><mat-icon>payments</mat-icon></div>
        <div>
          <p class="stat-value">{{ stats()?.inventoryValue | currency }}</p>
          <p class="stat-label">Inventory Value</p>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .stats { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 0.75rem; margin-bottom: 1rem; }
    .stat-card {
      display: flex; align-items: center; gap: 0.875rem;
      background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 1rem 1.125rem;
    }
    .stat-card--amber { border-color: #fde68a; background: #fffbeb; }
    .stat-card--red { border-color: #fecaca; background: #fef2f2; }
    .stat-card--red .stat-value { color: #dc2626; }
    .stat-card--green { border-color: #b8e6d4; background: #f0fdf8; }
    .stat-icon { width: 44px; height: 44px; border-radius: 10px; display: grid; place-items: center; flex-shrink: 0; }
    .stat-icon mat-icon { font-size: 22px; width: 22px; height: 22px; }
    .stat-icon--brand { background: #0b6b50; color: #fff; }
    .stat-icon--amber { background: #fef3c7; color: #f59e0b; }
    .stat-icon--red { background: #fee2e2; color: #dc2626; }
    .stat-icon--green { background: #e8f5f0; color: #0b6b50; }
    .stat-value { margin: 0; font-size: 1.375rem; font-weight: 800; color: #0f172a; line-height: 1.1; }
    .stat-label { margin: 0.15rem 0 0; font-size: 0.6875rem; font-weight: 700; text-transform: uppercase; color: #64748b; }
    @media (max-width: 900px) { .stats { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
    @media (max-width: 480px) { .stats { grid-template-columns: 1fr; } }
  `]
})
export class PartsInventoryStatsComponent {
  readonly stats = input<PartsInventoryStats | null>(null);
}
