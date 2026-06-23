import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { Workshop } from '../../../../../core/models/maintenance.model';

@Component({
  selector: 'workshop-table',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Name</th><th>Contact</th><th>Mobile</th><th>Email</th><th>Address</th><th>Rating</th><th>Status</th><th></th>
          </tr>
        </thead>
        <tbody>
          @for (w of workshops(); track w.id) {
            <tr>
              <td><strong>{{ w.name }}</strong></td>
              <td>{{ w.contactPerson || '—' }}</td>
              <td>{{ w.contactPhone || '—' }}</td>
              <td>{{ w.contactEmail || '—' }}</td>
              <td>{{ w.location || '—' }}</td>
              <td>{{ w.rating != null ? w.rating + '/5' : '—' }}</td>
              <td><span class="badge" [class.badge--inactive]="!w.isActive">{{ w.isActive ? 'Active' : 'Inactive' }}</span></td>
              <td class="actions">
                <button type="button" title="Edit" (click)="edit.emit(w)"><mat-icon>edit</mat-icon></button>
                @if (w.isActive) {
                  <button type="button" title="Deactivate" (click)="deactivate.emit(w)"><mat-icon>block</mat-icon></button>
                } @else {
                  <button type="button" title="Activate" (click)="activate.emit(w)"><mat-icon>check_circle</mat-icon></button>
                }
              </td>
            </tr>
          } @empty {
            <tr><td colspan="8" class="empty">No workshops yet.</td></tr>
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
    .badge { display: inline-block; padding: 0.15rem 0.5rem; border-radius: 999px; background: #d1fae5; color: #0B6B50; font-size: 0.6875rem; font-weight: 700; }
    .badge--inactive { background: #f1f5f9; color: #64748b; }
    .actions { display: flex; gap: 0.25rem; }
    .actions button { border: 1px solid #e2e8f0; background: #fff; border-radius: 6px; width: 2rem; height: 2rem; cursor: pointer; display: inline-flex; align-items: center; justify-content: center; color: #475569; }
    .actions button:hover { border-color: #0B6B50; color: #0B6B50; }
    .actions mat-icon { font-size: 1rem; width: 1rem; height: 1rem; }
    .empty { text-align: center; color: #94a3b8; padding: 2rem !important; }
  `]
})
export class WorkshopTableComponent {
  readonly workshops = input.required<Workshop[]>();
  readonly edit = output<Workshop>();
  readonly activate = output<Workshop>();
  readonly deactivate = output<Workshop>();
}
