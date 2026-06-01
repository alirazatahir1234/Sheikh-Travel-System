import { Component, EventEmitter, Input, Output } from '@angular/core';
import { DataTableColumn } from '../ui.types';

@Component({
  selector: 'stb-data-table-card',
  templateUrl: './data-table-card.component.html',
  styleUrls: ['./data-table-card.component.scss']
})
export class DataTableCardComponent<T = any> {
  selectedRow: T | null = null;

  @Input() title?: string;
  @Input() icon?: string;
  @Input() columns: DataTableColumn<T>[] = [];
  @Input() rows: T[] = [];
  @Input() actionLabel = 'View all';
  @Input() emptyMessage = 'No records';

  @Input() showRowActions = false;

  @Output() rowClick = new EventEmitter<T>();
  @Output() viewRow = new EventEmitter<T>();
  @Output() editRow = new EventEmitter<T>();
  @Output() assignRow = new EventEmitter<T>();
  @Output() action = new EventEmitter<void>();

  valueFor(row: T, col: DataTableColumn<T>): string {
    if (col.cell) return col.cell(row);
    const raw = (row as unknown as Record<string, unknown>)[col.key];
    return raw == null ? '' : String(raw);
  }

  statusKey(row: T, col: DataTableColumn<T>): string {
    return this.valueFor(row, col).toLowerCase().replace(/\s+/g, '-');
  }

  statusIcon(row: T, col: DataTableColumn<T>): string {
    const k = this.statusKey(row, col);
    if (k.includes('pending') || k.includes('confirm')) return 'schedule';
    if (k.includes('cancel')) return 'cancel';
    if (k.includes('complete')) return 'check_circle';
    if (k.includes('start') || k.includes('active')) return 'play_circle';
    return 'info';
  }

  onRowClick(row: T): void {
    this.rowClick.emit(row);
  }

  selectRow(row: T, event: Event): void {
    event.stopPropagation();
    this.selectedRow = row;
  }
}
