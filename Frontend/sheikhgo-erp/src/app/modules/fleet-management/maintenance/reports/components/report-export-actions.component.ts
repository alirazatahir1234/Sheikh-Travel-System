import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'report-export-actions',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="actions-bar">
      <button type="button" class="btn btn--primary" [disabled]="loading()" (click)="preview.emit()">
        <mat-icon>visibility</mat-icon> Preview
      </button>
      <button type="button" class="btn btn--outline" [disabled]="!canExport()" (click)="exportPdf.emit()">
        <mat-icon>picture_as_pdf</mat-icon> Export PDF
      </button>
      <button type="button" class="btn btn--outline" [disabled]="!canExport()" (click)="exportExcel.emit()">
        <mat-icon>table_view</mat-icon> Export Excel
      </button>
      <button type="button" class="btn btn--schedule" (click)="schedule.emit()">
        <mat-icon>schedule_send</mat-icon> Schedule Email
      </button>
    </div>
  `,
  styles: [`
    .actions-bar { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 1rem; }
    .btn { display: inline-flex; align-items: center; gap: 0.35rem; padding: 0.5rem 1rem; border-radius: 8px; font-size: 0.8125rem; font-weight: 600; cursor: pointer; border: none; }
    .btn:disabled { opacity: 0.5; cursor: not-allowed; }
    .btn mat-icon { font-size: 1.125rem; width: 1.125rem; height: 1.125rem; }
    .btn--primary { background: #0B6B50; color: #fff; }
    .btn--outline { background: #fff; color: #475569; border: 1.5px solid #cbd5e1; }
    .btn--schedule { background: #eff6ff; color: #1d4ed8; border: 1.5px solid #bfdbfe; }
  `]
})
export class ReportExportActionsComponent {
  readonly loading = input(false);
  readonly canExport = input(false);
  readonly preview = output<void>();
  readonly exportPdf = output<void>();
  readonly exportExcel = output<void>();
  readonly schedule = output<void>();
}
