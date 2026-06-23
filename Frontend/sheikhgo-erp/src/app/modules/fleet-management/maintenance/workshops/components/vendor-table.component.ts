import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { Vendor } from '../../../../../core/models/maintenance.model';

@Component({
  selector: 'vendor-table',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Vendor</th><th>Category</th><th>Contact</th><th>Products</th><th>Rating</th><th>Status</th><th></th>
          </tr>
        </thead>
        <tbody>
          @for (v of vendors(); track v.id) {
            <tr>
              <td><strong>{{ v.name }}</strong></td>
              <td>{{ v.category }}</td>
              <td>
                {{ v.contactPerson || '—' }}
                @if (v.contactPhone) { <span class="muted">{{ v.contactPhone }}</span> }
              </td>
              <td>
                <div class="chips">
                  @for (p of v.products.slice(0, 3); track p) {
                    <span class="chip">{{ p }}</span>
                  }
                  @if (v.products.length > 3) { <span class="chip chip--more">+{{ v.products.length - 3 }}</span> }
                  @if (!v.products.length) { — }
                </div>
              </td>
              <td>{{ v.rating != null ? v.rating + '/5' : '—' }}</td>
              <td>
                <span class="badge" [class.badge--inactive]="!v.isActive">{{ v.isActive ? 'Active' : 'Inactive' }}</span>
                @if (v.isPreferred) { <span class="badge badge--pref">Preferred</span> }
              </td>
              <td class="actions">
                <button type="button" title="Edit" (click)="edit.emit(v)"><mat-icon>edit</mat-icon></button>
                @if (v.isActive) {
                  <button type="button" title="Deactivate" (click)="deactivate.emit(v)"><mat-icon>block</mat-icon></button>
                } @else {
                  <button type="button" title="Activate" (click)="activate.emit(v)"><mat-icon>check_circle</mat-icon></button>
                }
              </td>
            </tr>
          } @empty {
            <tr><td colspan="7" class="empty">No vendors yet.</td></tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: [`
    .table-wrap { overflow-x: auto; background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; }
    table { width: 100%; border-collapse: collapse; font-size: 0.8125rem; }
    th { text-align: left; color: #64748b; padding: 0.625rem 0.75rem; border-bottom: 1px solid #e2e8f0; }
    td { padding: 0.625rem 0.75rem; border-bottom: 1px solid #f1f5f9; vertical-align: middle; }
    .muted { display: block; font-size: 0.75rem; color: #94a3b8; }
    .chips { display: flex; flex-wrap: wrap; gap: 0.25rem; }
    .chip { padding: 0.1rem 0.45rem; border-radius: 999px; background: #f1f5f9; font-size: 0.6875rem; font-weight: 600; color: #475569; }
    .chip--more { background: #e2e8f0; }
    .badge { display: inline-block; padding: 0.15rem 0.5rem; border-radius: 999px; background: #d1fae5; color: #0B6B50; font-size: 0.6875rem; font-weight: 700; margin-right: 0.25rem; }
    .badge--inactive { background: #f1f5f9; color: #64748b; }
    .badge--pref { background: #fef3c7; color: #b45309; }
    .actions { display: flex; gap: 0.25rem; }
    .actions button { border: 1px solid #e2e8f0; background: #fff; border-radius: 6px; width: 2rem; height: 2rem; cursor: pointer; display: inline-flex; align-items: center; justify-content: center; color: #475569; }
    .actions button:hover { border-color: #0B6B50; color: #0B6B50; }
    .actions mat-icon { font-size: 1rem; width: 1rem; height: 1rem; }
    .empty { text-align: center; color: #94a3b8; padding: 2rem !important; }
  `]
})
export class VendorTableComponent {
  readonly vendors = input.required<Vendor[]>();
  readonly edit = output<Vendor>();
  readonly activate = output<Vendor>();
  readonly deactivate = output<Vendor>();
}
