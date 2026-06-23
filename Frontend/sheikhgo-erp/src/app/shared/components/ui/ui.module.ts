import { NgModule } from '@angular/core';

import { UiButtonComponent } from './button/ui-button.component';
import { UiInputComponent } from './input/ui-input.component';
import { UiSelectComponent } from './select/ui-select.component';
import { UiStatusBadgeComponent } from './status-badge/ui-status-badge.component';
import { UiPageHeaderComponent } from './page-header/ui-page-header.component';
import { UiModalComponent } from './modal/ui-modal.component';
import { UiDrawerComponent } from './drawer/ui-drawer.component';
import { UiTabsComponent } from './tabs/ui-tabs.component';
import { UiEmptyStateComponent } from './empty-state/ui-empty-state.component';
import { UiChartComponent } from './chart/ui-chart.component';
import { UiDataTableComponent } from './data-table/ui-data-table.component';
import { UiTableCellDirective } from './data-table/ui-table-cell.directive';
import { UiConfirmDialogComponent } from './confirm-dialog/ui-confirm-dialog.component';

const UI = [
  UiButtonComponent,
  UiInputComponent,
  UiSelectComponent,
  UiStatusBadgeComponent,
  UiPageHeaderComponent,
  UiModalComponent,
  UiDrawerComponent,
  UiTabsComponent,
  UiEmptyStateComponent,
  UiChartComponent,
  UiDataTableComponent,
  UiTableCellDirective,
  UiConfirmDialogComponent
];

/**
 * Aggregates the standalone UI primitives so NgModule-based feature modules
 * can import the whole library in one line via `imports: [UiModule]`.
 */
@NgModule({
  imports: [...UI],
  exports: [...UI]
})
export class UiModule {}
