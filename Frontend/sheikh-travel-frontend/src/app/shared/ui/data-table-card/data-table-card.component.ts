import { Component, EventEmitter, Input, Output } from '@angular/core';
import { DataTableColumn } from '../ui.types';

@Component({
  selector: 'stb-data-table-card',
  templateUrl: './data-table-card.component.html',
  styleUrls: ['./data-table-card.component.scss']
})
export class DataTableCardComponent<T = any> {
  @Input() title?: string;
  @Input() icon?: string;
  @Input() columns: DataTableColumn<T>[] = [];
  @Input() rows: T[] = [];
  @Input() actionLabel = 'View all';
  @Input() emptyMessage = 'No records';

  @Output() rowClick = new EventEmitter<T>();
  @Output() action = new EventEmitter<void>();

  valueFor(row: T, col: DataTableColumn<T>): string {
    if (col.cell) return col.cell(row);
    const raw = (row as unknown as Record<string, unknown>)[col.key];
    return raw == null ? '' : String(raw);
  }
}
