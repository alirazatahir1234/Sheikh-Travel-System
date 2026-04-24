import { Component } from '@angular/core';
import { MatDialogRef } from '@angular/material/dialog';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

import { RouteService } from '../../../core/services/route.service';
import { CreateRouteDto } from '../../../core/models/route.model';

export interface BulkRouteAddDialogResult {
  created: number;
  failed: number;
}

interface ParsedRow {
  rowNumber: number;
  raw: string;
  dto?: CreateRouteDto;
  error?: string;
  /** Populated after submission. */
  submissionError?: string;
  submitted?: boolean;
}

const COLUMN_ORDER = ['name', 'source', 'destination', 'distance', 'estimatedMinutes', 'basePrice'] as const;

@Component({
  selector: 'app-bulk-route-add-dialog',
  templateUrl: './bulk-route-add-dialog.component.html',
  styleUrls: ['./bulk-route-add-dialog.component.scss']
})
export class BulkRouteAddDialogComponent {
  input = '';
  parsed: ParsedRow[] = [];
  submitting = false;

  readonly sampleText =
`Lahore – Multan, Lahore, Multan, 340, 240, 6000
Karachi – Hyderabad, Karachi, Hyderabad, 165, 150, 3500
Islamabad – Peshawar, Islamabad, Peshawar, 190, 165, 4000`;

  constructor(
    private dialogRef: MatDialogRef<BulkRouteAddDialogComponent, BulkRouteAddDialogResult>,
    private routeService: RouteService
  ) {}

  // ---------- Parsing --------------------------------------------------------

  parse(): void {
    this.parsed = (this.input || '')
      .split(/\r?\n/)
      .map(line => line.trim())
      .filter(line => line.length > 0 && !line.startsWith('#'))
      .map((line, idx) => this.parseRow(line, idx + 1));
  }

  useSample(): void {
    this.input = this.sampleText;
    this.parse();
  }

  get validCount(): number  { return this.parsed.filter(p => !p.error).length; }
  get invalidCount(): number { return this.parsed.filter(p =>  p.error).length; }

  private parseRow(line: string, rowNumber: number): ParsedRow {
    const cells = this.splitRow(line);

    if (cells.length < 3) {
      return { rowNumber, raw: line, error: 'Need at least: Name, Origin, Destination.' };
    }

    const [name, source, destination, distanceStr, durationStr, priceStr] = [
      cells[0] ?? '', cells[1] ?? '', cells[2] ?? '',
      cells[3] ?? '', cells[4] ?? '', cells[5] ?? ''
    ];

    if (!source.trim() || !destination.trim()) {
      return { rowNumber, raw: line, error: 'Origin and destination are required.' };
    }

    const distance = this.parseNumber(distanceStr);
    if (distance == null || distance <= 0) {
      return { rowNumber, raw: line, error: 'Distance (km) must be a positive number.' };
    }

    const estimatedMinutes = durationStr.trim() ? this.parseNumber(durationStr) : null;
    if (durationStr.trim() && (estimatedMinutes == null || estimatedMinutes < 0)) {
      return { rowNumber, raw: line, error: 'Duration must be a number of minutes.' };
    }

    const basePrice = this.parseNumber(priceStr);
    if (basePrice == null || basePrice < 0) {
      return { rowNumber, raw: line, error: 'Base price must be a non-negative number.' };
    }

    const dto: CreateRouteDto = {
      name: name.trim() || null,
      source: source.trim(),
      destination: destination.trim(),
      distance,
      estimatedMinutes,
      basePrice
    };

    return { rowNumber, raw: line, dto };
  }

  /**
   * Accepts comma, tab, or pipe delimited input and tolerates quoted cells
   * with commas inside. Not a full CSV parser — enough for paste-in usage.
   */
  private splitRow(line: string): string[] {
    if (line.includes('\t')) return line.split('\t').map(s => s.trim());
    if (line.includes('|'))  return line.split('|').map(s => s.trim());

    const out: string[] = [];
    let current = '';
    let inQuotes = false;
    for (const ch of line) {
      if (ch === '"') { inQuotes = !inQuotes; continue; }
      if (ch === ',' && !inQuotes) { out.push(current.trim()); current = ''; continue; }
      current += ch;
    }
    out.push(current.trim());
    return out;
  }

  private parseNumber(value: string): number | null {
    if (value == null) return null;
    const cleaned = value.replace(/[, ]/g, '').trim();
    if (!cleaned) return null;
    const n = Number(cleaned);
    return Number.isFinite(n) ? n : null;
  }

  // ---------- Submission -----------------------------------------------------

  submit(): void {
    const ready = this.parsed.filter(p => p.dto);
    if (ready.length === 0) return;

    this.submitting = true;
    const calls = ready.map(row =>
      this.routeService.create({ route: row.dto! }).pipe(
        map(() => ({ row, ok: true as const })),
        catchError(err => {
          row.submissionError = this.extractError(err);
          return of({ row, ok: false as const });
        })
      )
    );

    forkJoin(calls).subscribe(results => {
      this.submitting = false;

      for (const r of results) r.row.submitted = r.ok;
      const created = results.filter(r => r.ok).length;
      const failed  = results.length - created;

      if (failed === 0) {
        this.dialogRef.close({ created, failed });
      }
      // If some failed, keep the dialog open so the user can see which rows bombed.
      else {
        this.parsed = [...this.parsed];
      }
    });
  }

  closeWithSummary(): void {
    const created = this.parsed.filter(p => p.submitted).length;
    const failed  = this.parsed.filter(p => p.submissionError).length;
    this.dialogRef.close({ created, failed });
  }

  cancel(): void { this.dialogRef.close(); }

  private extractError(err: any): string {
    if (!err) return 'Unknown error';
    if (typeof err === 'string') return err;

    const data = err.error;
    if (data) {
      if (typeof data === 'string') return data;
      if (data.message) return data.message;

      // ProblemDetails / ModelState validation errors
      if (data.errors && typeof data.errors === 'object') {
        const messages: string[] = [];
        for (const key of Object.keys(data.errors)) {
          const value = data.errors[key];
          if (Array.isArray(value)) messages.push(...value);
          else if (typeof value === 'string') messages.push(value);
        }
        if (messages.length) return messages.join('; ');
      }
    }
    return err.message ?? 'Request failed';
  }

  /** Used by the header tooltip to describe the expected column order. */
  readonly columnOrder = COLUMN_ORDER;
}
