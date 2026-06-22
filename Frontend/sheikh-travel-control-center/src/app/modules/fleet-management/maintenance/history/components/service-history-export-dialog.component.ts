import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

export type ServiceHistoryExportFormat = 'pdf' | 'excel';

@Component({
  selector: 'service-history-export-dialog',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (open()) {
      <div class="modal-overlay" (click)="onBackdropClick($event)">
        <div class="modal" role="dialog" aria-labelledby="export-title">
          <header class="modal-header">
            <h3 id="export-title">Export Service History</h3>
            <button type="button" class="modal-close" aria-label="Close" (click)="close.emit()">×</button>
          </header>
          <div class="modal-body">
            <p class="modal-hint">{{ recordCount() }} record(s) will be exported with current filters.</p>
            <div class="export-options">
              <button type="button" class="export-option"
                [class.export-option--selected]="selected() === 'excel'"
                (click)="selected.set('excel')">
                <mat-icon>table_view</mat-icon>
                <strong>Excel</strong>
                <span>.xlsx spreadsheet</span>
              </button>
              <button type="button" class="export-option"
                [class.export-option--selected]="selected() === 'pdf'"
                (click)="selected.set('pdf')">
                <mat-icon>picture_as_pdf</mat-icon>
                <strong>PDF</strong>
                <span>Printable report</span>
              </button>
            </div>
            <div class="modal-actions">
              <button type="button" class="btn btn--outline" (click)="close.emit()">Cancel</button>
              <button type="button" class="btn btn--primary" (click)="confirm()">
                <mat-icon>download</mat-icon> Export
              </button>
            </div>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .modal-overlay {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.5);
      z-index: 1000;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 1rem;
    }
    .modal {
      background: #fff;
      border-radius: 12px;
      box-shadow: 0 25px 50px rgba(0, 0, 0, 0.25);
      width: 100%;
      max-width: 400px;
      overflow: hidden;
      animation: slideUp 0.2s ease;
    }
    @keyframes slideUp {
      from { opacity: 0; transform: translateY(20px); }
      to { opacity: 1; transform: translateY(0); }
    }
    .modal-header {
      padding: 1.25rem 1.5rem 1rem;
      border-bottom: 1px solid #f1f5f9;
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .modal-header h3 { margin: 0; font-size: 1rem; font-weight: 800; color: #0f172a; }
    .modal-close {
      width: 28px;
      height: 28px;
      border-radius: 6px;
      border: none;
      background: #f1f5f9;
      cursor: pointer;
      font-size: 1.125rem;
      color: #64748b;
      line-height: 1;
    }
    .modal-close:hover { background: #e2e8f0; }
    .modal-body { padding: 1.25rem 1.5rem 1.5rem; }
    .modal-hint {
      margin: 0 0 1rem;
      font-size: 0.8125rem;
      color: #64748b;
    }
    .export-options {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.75rem;
      margin-bottom: 1.25rem;
    }
    .export-option {
      border: 2px solid #e2e8f0;
      border-radius: 8px;
      padding: 1rem;
      text-align: center;
      cursor: pointer;
      transition: all 0.15s;
      background: #fff;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.35rem;
    }
    .export-option mat-icon { font-size: 1.75rem; width: 1.75rem; height: 1.75rem; color: #64748b; }
    .export-option strong { font-size: 0.875rem; color: #0f172a; }
    .export-option span { font-size: 0.6875rem; color: #94a3b8; }
    .export-option:hover { border-color: #cbd5e1; background: #f8fafc; }
    .export-option--selected {
      border-color: #0B6B50;
      background: #e8f5f0;
    }
    .export-option--selected mat-icon { color: #0B6B50; }
    .modal-actions {
      display: flex;
      gap: 0.5rem;
      justify-content: flex-end;
    }
    .btn {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      padding: 0.5rem 1rem;
      border-radius: 8px;
      font-size: 0.8125rem;
      font-weight: 600;
      cursor: pointer;
      border: none;
    }
    .btn mat-icon { font-size: 1.125rem; width: 1.125rem; height: 1.125rem; }
    .btn--primary { background: #0B6B50; color: #fff; }
    .btn--primary:hover { background: #095a43; }
    .btn--outline {
      background: #fff;
      color: #475569;
      border: 1.5px solid #cbd5e1;
    }
    .btn--outline:hover { background: #f8fafc; }
  `]
})
export class ServiceHistoryExportDialogComponent {
  readonly open = input(false);
  readonly recordCount = input(0);
  readonly close = output<void>();
  readonly exportFormat = output<ServiceHistoryExportFormat>();

  readonly selected = signal<ServiceHistoryExportFormat>('excel');

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('modal-overlay')) {
      this.close.emit();
    }
  }

  confirm(): void {
    this.exportFormat.emit(this.selected());
    this.close.emit();
  }
}
