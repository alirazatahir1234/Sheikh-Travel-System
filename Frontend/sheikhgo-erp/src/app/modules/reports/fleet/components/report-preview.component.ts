import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FleetReport } from '../../../../core/models/fleet-report.model';
import { formatFieldValue } from '../utils/report-column.util';
import { AppBrandLoaderComponent } from '../../../../shared/components/app-brand-loader/app-brand-loader.component';
import { APP_LOGO_PATH, COMPANY_NAME, COMPANY_ADDRESS } from '../../../../core/constants/app-brand';

@Component({
  selector: 'fleet-report-preview',
  standalone: true,
  imports: [AppBrandLoaderComponent, DatePipe, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (!hasPreview()) {
      <div class="empty">Select a report and click Preview to load data.</div>
    } @else if (loading()) {
      <app-brand-loader message="Loading report…" />
    } @else if (report()) {
      <div class="preview" id="fleet-report-print-area">
        <header class="print-letterhead">
          <img [src]="logo" alt="" />
          <div>
            <strong>{{ companyName }}</strong>
            @if (companyAddress) { <span>{{ companyAddress }}</span> }
          </div>
          <div class="print-meta">
            <span>Generated {{ generatedAt | date: 'medium' }}</span>
          </div>
        </header>

        <header class="preview-head">
          <h3>{{ report()!.title }}</h3>
          <div class="summary-chips">
            <span class="chip">Rows: {{ report()!.rows.length }}</span>
            @if (report()!.totalValue > 0) {
              <span class="chip chip--cost">Total: {{ report()!.totalValue | number: '1.0-2' }}</span>
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

        <footer class="print-footer">
          <span>{{ companyName }} — Confidential</span>
        </footer>
      </div>
    }
  `,
  styles: [`
    .empty, .loading { text-align: center; color: #94a3b8; padding: 2.5rem; background: #fff; border: 1px dashed #cbd5e1; border-radius: 12px; }
    .preview { background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; overflow: hidden; }
    .print-letterhead, .print-footer { display: none; }
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

    @media print {
      .print-letterhead {
        display: flex; align-items: center; gap: 0.75rem; padding: 1rem 1.25rem; border-bottom: 2px solid #0B6B50;
      }
      .print-letterhead img { height: 40px; }
      .print-letterhead strong { display: block; font-size: 1rem; }
      .print-letterhead span { display: block; font-size: 0.75rem; color: #64748b; }
      .print-meta { margin-left: auto; font-size: 0.75rem; color: #64748b; }
      .print-footer { display: block; padding: 0.75rem 1.25rem; font-size: 0.7rem; color: #94a3b8; border-top: 1px solid #e2e8f0; }
      table { min-width: 0; }
    }
  `]
})
export class FleetReportPreviewComponent {
  readonly report = input<FleetReport | null>(null);
  readonly loading = input(false);
  readonly hasPreview = input(false);

  readonly logo = APP_LOGO_PATH;
  readonly companyName = COMPANY_NAME;
  readonly companyAddress = COMPANY_ADDRESS;
  readonly generatedAt = new Date();

  format(row: { fields: Record<string, unknown> }, key: string, format: string): string {
    return formatFieldValue(row.fields?.[key], format);
  }
}
