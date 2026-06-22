import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { REPORT_CATALOG, ReportCatalogId } from '../utils/report-column.util';

@Component({
  selector: 'report-catalog',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="catalog">
      @for (item of catalog; track item.id) {
        <button type="button" class="catalog-card"
          [class.catalog-card--active]="selected() === item.id"
          (click)="select.emit(item.id)">
          <mat-icon>{{ item.icon }}</mat-icon>
          <strong>{{ item.label }}</strong>
          <span>{{ item.description }}</span>
        </button>
      }
    </div>
  `,
  styles: [`
    .catalog { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 0.75rem; margin-bottom: 1rem; }
    .catalog-card {
      display: flex; flex-direction: column; align-items: flex-start; gap: 0.35rem;
      padding: 1rem; border: 1.5px solid #e2e8f0; border-radius: 12px; background: #fff;
      text-align: left; cursor: pointer; transition: all 0.15s;
    }
    .catalog-card:hover { border-color: #0B6B50; box-shadow: 0 4px 12px rgba(11,107,80,.08); }
    .catalog-card--active { border-color: #0B6B50; background: #f0fdf8; }
    .catalog-card mat-icon { color: #0B6B50; font-size: 1.5rem; width: 1.5rem; height: 1.5rem; }
    .catalog-card strong { font-size: 0.875rem; color: #0f172a; }
    .catalog-card span { font-size: 0.75rem; color: #64748b; line-height: 1.35; }
    @media (max-width: 1100px) { .catalog { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
    @media (max-width: 560px) { .catalog { grid-template-columns: 1fr; } }
  `]
})
export class ReportCatalogComponent {
  readonly catalog = REPORT_CATALOG;
  readonly selected = input<ReportCatalogId>('cost-analysis');
  readonly select = output<ReportCatalogId>();
}
