import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { MaintenanceReport } from '../../../../../core/models/maintenance.model';
import { formatFieldValue } from '../utils/report-column.util';

@Component({
  selector: 'report-preview',
  standalone: true,
  imports: [CurrencyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (!hasPreview()) {
      <div class="empty">Select a report and click Preview to load data.</div>
    } @else if (loading()) {
      <p class="loading">Loading report…</p>
    } @else if (report()) {
      <div class="preview">
        <header class="preview-head">
          <h3>{{ report()!.title }}</h3>
          <div class="summary-chips">
            <span class="chip">Rows: {{ report()!.rows.length }}</span>
            @if (report()!.totalCost > 0) {
              <span class="chip chip--cost">Total: {{ report()!.totalCost | currency }}</span>
            }
          </div>
        </header>
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                @for (col of report()!.columns; track col.key) {
                  <th>{{ col.label }}</th>
                }
              </tr>
            </thead>
            <tbody>
              @for (row of report()!.rows; track row.key) {
                <tr>
                  @for (col of report()!.columns; track col.key) {
                    <td>{{ format(row, col.key, col.format) }}</td>
                  }
                </tr>
              } @empty {
                <tr><td [attr.colspan]="report()!.columns.length" class="no-data">No data for selected filters.</td></tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    }
  `,
  styles: [`
    .empty, .loading { text-align: center; color: #94a3b8; padding: 2.5rem; background: #fff; border: 1px dashed #cbd5e1; border-radius: 12px; }
    .preview { background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; overflow: hidden; }
    .preview-head { display: flex; justify-content: space-between; align-items: center; gap: 1rem; padding: 1rem 1.25rem; border-bottom: 1px solid #f1f5f9; flex-wrap: wrap; }
    .preview-head h3 { margin: 0; font-size: 1rem; font-weight: 800; color: #0f172a; }
    .summary-chips { display: flex; gap: 0.5rem; flex-wrap: wrap; }
    .chip { padding: 0.25rem 0.625rem; border-radius: 999px; background: #f1f5f9; font-size: 0.75rem; font-weight: 600; color: #475569; }
    .chip--cost { background: #e8f5f0; color: #0B6B50; }
    .table-wrap { overflow-x: auto; }
    table { width: 100%; border-collapse: collapse; font-size: 0.8125rem; min-width: 640px; }
    th { text-align: left; padding: 0.625rem 0.75rem; color: #64748b; border-bottom: 1px solid #e2e8f0; white-space: nowrap; }
    td { padding: 0.625rem 0.75rem; border-bottom: 1px solid #f1f5f9; }
    .no-data { text-align: center; color: #94a3b8; padding: 2rem !important; }
  `]
})
export class ReportPreviewComponent {
  readonly report = input<MaintenanceReport | null>(null);
  readonly loading = input(false);
  readonly hasPreview = input(false);

  format(row: { fields: Record<string, unknown> }, key: string, format: string): string {
    return formatFieldValue(row.fields?.[key], format);
  }
}
