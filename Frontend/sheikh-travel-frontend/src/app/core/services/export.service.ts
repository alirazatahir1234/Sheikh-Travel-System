import { Injectable } from '@angular/core';
import * as XLSX from 'xlsx';
import { jsPDF } from 'jspdf';
import autoTable from 'jspdf-autotable';

/**
 * Describes a single export column for both Excel and PDF outputs.
 * `accessor` receives the source row and must return a primitive that both
 * formats can render (string, number, or nullish).
 *
 * `excelWidth` is in Excel character units. `pdfWidth` is in PDF points.
 * These have to be separate because autoTable interprets them very differently
 * from a spreadsheet column width — using one value for both yielded 8-point
 * columns and one-character-per-line wrapping.
 */
export interface ExportColumn<T> {
  header: string;
  accessor: (row: T) => string | number | null | undefined;
  align?: 'left' | 'center' | 'right';
  /** Excel column width in character units (wch). Default: header length + padding. */
  excelWidth?: number;
  /** Optional PDF column width in points. If omitted, autoTable auto-distributes. */
  pdfWidth?: number;
  /**
   * Relative weight hint used when auto-sizing PDF columns to fill the page
   * width. Larger numbers get proportionally more room. Defaults to 1.
   */
  pdfWeight?: number;
}

export interface ExportMeta {
  /** File name without extension — sanitised by the service. */
  filename: string;
  /** Title printed at the top of the PDF. Ignored by Excel. */
  title?: string;
  /** Optional subtitle (e.g. active filters summary). */
  subtitle?: string;
  /** Excel sheet name (defaults to 'Sheet1'). */
  sheetName?: string;
  /** PDF orientation — defaults to 'landscape' since these tables are wide. */
  orientation?: 'portrait' | 'landscape';
}

const PAGE_MARGIN_PT = 32;

/**
 * Centralises Excel (.xlsx) and PDF exports so every list page exports in a
 * consistent style. Callers just provide rows, column definitions, and metadata.
 */
@Injectable({ providedIn: 'root' })
export class ExportService {
  exportExcel<T>(rows: T[], columns: ExportColumn<T>[], meta: ExportMeta): void {
    const data = rows.map(row => {
      const obj: Record<string, string | number | null> = {};
      for (const col of columns) {
        obj[col.header] = this.toCell(col.accessor(row));
      }
      return obj;
    });

    const sheet = XLSX.utils.json_to_sheet(data, {
      header: columns.map(c => c.header)
    });

    sheet['!cols'] = columns.map(c => ({
      wch: c.excelWidth ?? Math.max(c.header.length + 2, 14)
    }));

    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, sheet, meta.sheetName || 'Sheet1');

    XLSX.writeFile(book, `${this.sanitize(meta.filename)}.xlsx`, { compression: true });
  }

  exportPdf<T>(rows: T[], columns: ExportColumn<T>[], meta: ExportMeta): void {
    const orientation = meta.orientation ?? 'landscape';
    const doc = new jsPDF({ orientation, unit: 'pt', format: 'a4' });

    const pageWidth  = doc.internal.pageSize.getWidth();
    const pageHeight = doc.internal.pageSize.getHeight();
    const usableWidth = pageWidth - PAGE_MARGIN_PT * 2;

    // --- Header block (title / subtitle / timestamp) ----------------------
    if (meta.title) {
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(16);
      doc.setTextColor(30);
      doc.text(meta.title, PAGE_MARGIN_PT, 40);
    }
    if (meta.subtitle) {
      doc.setFont('helvetica', 'normal');
      doc.setFontSize(9);
      doc.setTextColor(110);
      // Wrap long subtitles (e.g. filter summaries) so they never overflow the page.
      const lines = doc.splitTextToSize(meta.subtitle, usableWidth - 160);
      doc.text(lines, PAGE_MARGIN_PT, 58);
    }

    const stamp = new Date().toLocaleString();
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(9);
    doc.setTextColor(140);
    doc.text(`Generated ${stamp}`, pageWidth - PAGE_MARGIN_PT, 40, { align: 'right' });

    // --- Column widths ----------------------------------------------------
    const columnWidths = this.resolvePdfWidths(columns, usableWidth);

    const columnStyles = columns.reduce((acc, col, idx) => {
      acc[idx] = {
        halign: col.align ?? 'left',
        cellWidth: columnWidths[idx]
      };
      return acc;
    }, {} as Record<number, { halign: 'left' | 'center' | 'right'; cellWidth: number }>);

    // --- Auto-pick a readable font size for this column count --------------
    // (small tables breathe, wide tables shrink gracefully)
    const bodyFontSize = this.pickFontSize(columns.length);

    autoTable(doc, {
      startY: meta.subtitle ? 78 : (meta.title ? 58 : PAGE_MARGIN_PT),
      margin: { left: PAGE_MARGIN_PT, right: PAGE_MARGIN_PT, bottom: 40 },
      head: [columns.map(c => c.header)],
      body: rows.map(row => columns.map(col => this.toCell(col.accessor(row)))),
      tableWidth: usableWidth,
      styles: {
        font: 'helvetica',
        fontSize: bodyFontSize,
        cellPadding: { top: 4, right: 5, bottom: 4, left: 5 },
        overflow: 'linebreak',
        valign: 'middle',
        lineColor: [229, 233, 239],
        lineWidth: 0.5
      },
      headStyles: {
        fillColor: [27, 127, 117], // --stb-primary
        textColor: 255,
        fontStyle: 'bold',
        fontSize: bodyFontSize + 0.5,
        halign: 'left'
      },
      alternateRowStyles: { fillColor: [246, 248, 250] },
      columnStyles,
      didDrawPage: (hookData) => {
        const page = doc.getNumberOfPages();
        doc.setFontSize(8);
        doc.setTextColor(140);
        doc.text(
          `Page ${hookData.pageNumber} of ${page}`,
          pageWidth - PAGE_MARGIN_PT,
          pageHeight - 16,
          { align: 'right' }
        );
      }
    });

    doc.save(`${this.sanitize(meta.filename)}.pdf`);
  }

  // --- helpers -------------------------------------------------------------

  /**
   * Distributes the available page width across columns.
   *
   * - Columns with an explicit `pdfWidth` are honored first.
   * - Remaining space is split across the rest using `pdfWeight` (default 1).
   */
  private resolvePdfWidths<T>(columns: ExportColumn<T>[], usableWidth: number): number[] {
    const widths: number[] = new Array(columns.length);
    let fixedTotal = 0;
    let weightTotal = 0;

    columns.forEach((col, i) => {
      if (col.pdfWidth != null) {
        widths[i] = col.pdfWidth;
        fixedTotal += col.pdfWidth;
      } else {
        weightTotal += col.pdfWeight ?? 1;
      }
    });

    const remaining = Math.max(usableWidth - fixedTotal, 0);
    columns.forEach((col, i) => {
      if (widths[i] == null) {
        const weight = col.pdfWeight ?? 1;
        widths[i] = weightTotal > 0 ? (remaining * weight) / weightTotal : remaining / columns.length;
      }
    });

    return widths;
  }

  /**
   * Picks a body font size that stays readable as the column count grows.
   * 1–6 columns → 10pt, 7–9 → 9pt, 10–12 → 8pt, 13+ → 7pt.
   */
  private pickFontSize(colCount: number): number {
    if (colCount <= 6) return 10;
    if (colCount <= 9) return 9;
    if (colCount <= 12) return 8;
    return 7;
  }

  private toCell(value: string | number | null | undefined): string | number {
    if (value == null) return '';
    return value;
  }

  private sanitize(name: string): string {
    return (name || 'export').replace(/[\\/:*?"<>|]+/g, '_').trim() || 'export';
  }
}
