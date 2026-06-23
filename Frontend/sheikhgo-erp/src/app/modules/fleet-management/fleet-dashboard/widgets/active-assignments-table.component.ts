import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  UiDataTableComponent,
  UiStatusBadgeComponent,
  UiTableCellDirective,
  UiTableColumn
} from '../../../../shared/components/ui';
import { AssignmentItem } from '../fleet-dashboard.model';

@Component({
  selector: 'fleet-active-assignments-table',
  standalone: true,
  imports: [RouterLink, UiDataTableComponent, UiStatusBadgeComponent, UiTableCellDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="rounded-xl border border-fleet-border bg-white">
      <div class="flex items-center justify-between border-b border-fleet-border px-6 py-4">
        <h3 class="text-lg font-bold text-fleet-text">Active Assignments</h3>
        <a routerLink="/fleet/assignments" class="text-[13px] font-semibold text-fleet-primary hover:underline">View All</a>
      </div>

      <ui-data-table [columns]="columns" [data]="rows()" [loading]="loading()" [pageSize]="5">
        <ng-template uiTableCell="vehicleId" let-row>
          <p class="text-[13px] font-semibold text-fleet-text">{{ row.vehicleId }}</p>
          <p class="text-[11px] text-fleet-text-muted">{{ row.vehicleModel }}</p>
        </ng-template>

        <ng-template uiTableCell="driver" let-row>
          <div class="flex items-center gap-3">
            <span class="flex h-8 w-8 items-center justify-center rounded-full bg-fleet-primary/15 text-[11px] font-bold text-fleet-primary">
              {{ initials(row.driver) }}
            </span>
            <span class="text-[13px] font-semibold text-fleet-text">{{ row.driver }}</span>
          </div>
        </ng-template>

        <ng-template uiTableCell="status" let-row>
          <ui-status-badge [status]="row.status"></ui-status-badge>
        </ng-template>
      </ui-data-table>
    </section>
  `,
  styles: [`:host { display: block; }`]
})
export class ActiveAssignmentsTableComponent {
  readonly rows = input<AssignmentItem[]>([]);
  readonly loading = input(false);

  protected readonly columns: UiTableColumn[] = [
    { key: 'vehicleId', label: 'Vehicle ID', sortable: true },
    { key: 'driver', label: 'Driver', sortable: true },
    { key: 'route', label: 'Route' },
    { key: 'status', label: 'Status' },
    { key: 'eta', label: 'ETA' }
  ];

  initials(name: string): string {
    return name
      .split(' ')
      .map((part) => part[0])
      .filter(Boolean)
      .slice(0, 2)
      .join('')
      .toUpperCase();
  }
}
