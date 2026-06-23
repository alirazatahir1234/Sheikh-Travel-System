import { Directive, inject, input, TemplateRef } from '@angular/core';

/**
 * Marks an <ng-template> as the custom renderer for a given column key.
 * Use `uiTableCell="actions"` to render a trailing row-actions column.
 *
 * Template context: `$implicit` and `row` are the row data; `index` is the row index.
 */
@Directive({
  selector: '[uiTableCell]',
  standalone: true
})
export class UiTableCellDirective {
  readonly key = input.required<string>({ alias: 'uiTableCell' });
  readonly template = inject<TemplateRef<unknown>>(TemplateRef);
}
