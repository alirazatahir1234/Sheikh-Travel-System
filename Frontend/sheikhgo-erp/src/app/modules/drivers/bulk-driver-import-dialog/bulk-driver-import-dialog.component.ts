import { Component, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { DriverService } from '../../../core/services/driver.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import {
  DRIVER_IMPORT_HEADERS,
  DRIVER_IMPORT_SAMPLE_ROWS,
  ParsedDriverImportRow,
  parseDriverImportRecords
} from '../driver-import-format';

interface PreviewRow extends ParsedDriverImportRow {
  serverError?: string | null;
  serverSkipped?: string | null;
  created?: boolean;
  dryRunOk?: boolean;
}

@Component({
  selector: 'app-bulk-driver-import-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, MatDialogModule, MatButtonModule, MatCheckboxModule, MatIconModule],
  template: `
    <h2 mat-dialog-title>Import drivers</h2>
    <mat-dialog-content class="dialog-body">
      <p class="hint">CSV/XLSX with columns: {{ headers }}</p>
      <div class="actions-row">
        <button mat-stroked-button type="button" (click)="downloadTemplate()">
          <mat-icon>download</mat-icon> Template
        </button>
        <button mat-stroked-button type="button" (click)="fileInput.click()">
          <mat-icon>upload_file</mat-icon> Choose file
        </button>
        <input #fileInput type="file" hidden accept=".csv,.xlsx,.xls" (change)="onFile($event)" />
        <span class="file-name">{{ fileName || 'No file' }}</span>
      </div>
      <div class="opts">
        <mat-checkbox [(ngModel)]="skipDuplicates">Skip duplicates</mat-checkbox>
        <mat-checkbox [(ngModel)]="dryRun">Dry run</mat-checkbox>
      </div>
      @if (rows.length) {
        <p class="summary">{{ readyCount }} ready · {{ errorCount }} errors · {{ rows.length }} total</p>
        <table class="preview">
          <thead>
            <tr><th>#</th><th>Phone</th><th>Name</th><th>Status</th><th>Notes</th></tr>
          </thead>
          <tbody>
            @for (r of rows; track r.row) {
              <tr [class.err]="r.severity === 'error' || r.serverError" [class.ok]="r.created || r.dryRunOk">
                <td>{{ r.row }}</td>
                <td>{{ r.phone }}</td>
                <td>{{ r.dto ? (r.dto.firstName + ' ' + r.dto.lastName) : '—' }}</td>
                <td>{{ r.created ? 'Created' : r.dryRunOk ? 'Dry-run OK' : r.serverSkipped || r.severity }}</td>
                <td>{{ r.serverError || r.serverSkipped || r.messages.join('; ') }}</td>
              </tr>
            }
          </tbody>
        </table>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="dialogRef.close()">Close</button>
      <button mat-flat-button color="primary" type="button" [disabled]="!importableCount || submitting" (click)="submit()">
        {{ dryRun ? 'Dry run' : 'Import' }} ({{ importableCount }})
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .dialog-body { min-width: min(720px, 92vw); max-height: 70vh; }
    .hint, .summary, .file-name { color: #64748b; font-size: 13px; }
    .actions-row, .opts { display: flex; flex-wrap: wrap; gap: 12px; align-items: center; margin: 12px 0; }
    .preview { width: 100%; border-collapse: collapse; font-size: 12px; }
    .preview th, .preview td { border-bottom: 1px solid #e2e8f0; padding: 6px 8px; text-align: left; }
    .preview tr.err { background: #fff1f2; }
    .preview tr.ok { background: #ecfdf5; }
  `]
})
export class BulkDriverImportDialogComponent {
  @ViewChild('fileInput') fileInput?: ElementRef<HTMLInputElement>;

  fileName = '';
  rows: PreviewRow[] = [];
  submitting = false;
  skipDuplicates = true;
  dryRun = false;
  readonly headers = DRIVER_IMPORT_HEADERS.join(', ');

  constructor(
    public dialogRef: MatDialogRef<BulkDriverImportDialogComponent, { created: number }>,
    private driverService: DriverService,
    private toast: UiToastService
  ) {}

  get readyCount(): number {
    return this.rows.filter(r => r.severity !== 'error' && r.dto).length;
  }

  get errorCount(): number {
    return this.rows.filter(r => r.severity === 'error' || !r.dto).length;
  }

  get importableCount(): number {
    return this.rows.filter(r => r.dto && r.severity !== 'error').length;
  }

  async downloadTemplate(): Promise<void> {
    const XLSX = await import('xlsx');
    const ws = XLSX.utils.aoa_to_sheet([
      [...DRIVER_IMPORT_HEADERS],
      ...DRIVER_IMPORT_SAMPLE_ROWS
    ]);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Drivers');
    XLSX.writeFile(wb, 'driver-import-template.xlsx');
  }

  onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.fileName = file.name;
    const reader = new FileReader();
    reader.onload = () => {
      void (async () => {
        const XLSX = await import('xlsx');
        const data = new Uint8Array(reader.result as ArrayBuffer);
        const wb = XLSX.read(data, { type: 'array' });
        const sheet = wb.Sheets[wb.SheetNames[0]];
        const records = XLSX.utils.sheet_to_json<Record<string, unknown>>(sheet, { defval: '' });
        this.rows = parseDriverImportRecords(records);
      })();
    };
    reader.readAsArrayBuffer(file);
  }

  submit(): void {
    const drivers = this.rows.filter(r => r.dto && r.severity !== 'error').map(r => r.dto!);
    if (!drivers.length) return;
    this.submitting = true;
    this.driverService.bulkCreate({ drivers, options: { dryRun: this.dryRun, skipDuplicates: this.skipDuplicates } })
      .subscribe({
        next: result => {
          this.submitting = false;
          for (const c of result.created ?? []) {
            const row = this.rows.find(r => r.row === c.row);
            if (!row) continue;
            if (result.dryRun || c.dryRun) row.dryRunOk = true;
            else row.created = true;
          }
          for (const e of result.errors ?? []) {
            const row = this.rows.find(r => r.row === e.row);
            if (row) row.serverError = e.error;
          }
          for (const s of result.skippedRows ?? []) {
            const row = this.rows.find(r => r.row === s.row);
            if (row) row.serverSkipped = s.reason;
          }
          this.toast.success(
            result.dryRun
              ? `Dry run: ${result.created?.length ?? 0} would create`
              : `Imported ${result.succeeded} driver(s)`
          );
          if (!result.dryRun && result.succeeded > 0) {
            this.dialogRef.close({ created: result.succeeded });
          }
        },
        error: err => {
          this.submitting = false;
          this.toast.error(err?.error?.message || err?.message || 'Import failed');
        }
      });
  }
}
