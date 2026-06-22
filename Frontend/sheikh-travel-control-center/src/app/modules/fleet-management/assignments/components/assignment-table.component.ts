import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import {
  assignmentEffectiveStatus,
  FleetAssignment,
  statusBadgeClass,
  TRANSFER_TYPES
} from '../../../../core/models/fleet-assignment.model';

@Component({
  selector: 'assignment-table',
  standalone: true,
  imports: [DatePipe, MatIconModule, MatMenuModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="table-wrap">
      <table class="data-table">
        <thead>
          <tr>
            @if (selectable()) { <th class="th-check"><input type="checkbox" [checked]="allSelected()" (change)="toggleAll.emit($any($event.target).checked)" /></th> }
            <th>Assignment #</th>
            <th>Vehicle</th>
            <th>Driver</th>
            <th>Type</th>
            <th>Purpose</th>
            <th>Duration</th>
            <th>GPS</th>
            <th>Start</th>
            <th>End</th>
            <th>Status</th>
            <th>Created By</th>
            <th class="th-actions">Actions</th>
          </tr>
        </thead>
        <tbody>
          @if (loading()) {
            @for (i of [1,2,3,4,5]; track i) {
              <tr>@for (c of skeletonCols(); track c) { <td><div class="skeleton"></div></td> }</tr>
            }
          } @else if (!rows().length) {
            <tr><td [attr.colspan]="colspan()"><div class="empty"><mat-icon>event_available</mat-icon><p>No assignments found</p></div></td></tr>
          } @else {
            @for (row of rows(); track row.id; let odd = $odd) {
              <tr class="data-row" [class.data-row--alt]="odd" (click)="view.emit(row)">
                @if (selectable()) {
                  <td (click)="$event.stopPropagation()">
                    <input type="checkbox" [checked]="selectedIds().has(row.id)" (change)="toggleRow.emit(row.id)" />
                  </td>
                }
                <td><span class="assignment-no">{{ row.assignmentNo }}</span></td>
                <td>
                  <div class="cell-primary">{{ row.vehicleName }}</div>
                  <div class="cell-sub">{{ row.vehicleRegistration || row.vehicleCode }}</div>
                </td>
                <td>
                  @if (row.driverName) {
                    <div class="cell-primary">{{ row.driverName }}</div>
                    <div class="cell-sub">{{ row.driverCode }}</div>
                  } @else { <span class="cell-muted">Unassigned</span> }
                </td>
                <td>{{ row.assignmentType }}</td>
                <td>{{ row.purpose || '—' }}</td>
                <td>{{ row.durationDays != null ? row.durationDays + 'd' : '—' }}</td>
                <td>
                  <span class="gps-pill" [class.gps-pill--online]="row.gpsOnline">{{ row.gpsOnline ? 'Online' : 'Offline' }}</span>
                </td>
                <td>{{ row.startAt | date:'dd MMM yyyy' }}</td>
                <td>{{ row.endAt ? (row.endAt | date:'dd MMM yyyy') : '—' }}</td>
                <td>
                  <span class="status-badge status-badge--{{ statusBadgeClass(effectiveStatus(row)) }}">
                    {{ effectiveStatus(row) }}
                  </span>
                </td>
                <td>{{ row.createdBy || '—' }}</td>
                <td class="td-actions" (click)="$event.stopPropagation()">
                  <button type="button" class="action-btn" title="View" (click)="view.emit(row)"><mat-icon>visibility</mat-icon></button>
                  @if (isOpen(row)) {
                    <button type="button" class="action-btn" title="Transfer" (click)="transfer.emit(row)"><mat-icon>swap_horiz</mat-icon></button>
                    <button type="button" class="action-btn" title="Complete Assignment" (click)="complete.emit(row)"><mat-icon>task_alt</mat-icon></button>
                  }
                  <button type="button" class="action-btn" [matMenuTriggerFor]="menu" title="More actions"><mat-icon>more_vert</mat-icon></button>
                  <mat-menu #menu="matMenu">
                    <button mat-menu-item (click)="view.emit(row)"><mat-icon>visibility</mat-icon> View</button>
                    @if (isOpen(row)) {
                      <button mat-menu-item (click)="transfer.emit(row)"><mat-icon>swap_horiz</mat-icon> Transfer</button>
                      <button mat-menu-item (click)="complete.emit(row)"><mat-icon>task_alt</mat-icon> Complete</button>
                      <button mat-menu-item (click)="cancel.emit(row)"><mat-icon>cancel</mat-icon> Cancel</button>
                    }
                    <button mat-menu-item (click)="history.emit(row)"><mat-icon>history</mat-icon> History</button>
                  </mat-menu>
                </td>
              </tr>
            }
          }
        </tbody>
      </table>
    </div>

    <!-- Mobile cards -->
    <div class="card-list">
      @for (row of rows(); track row.id) {
        <article class="assignment-card" (click)="view.emit(row)">
          <div class="card-head">
            <strong>{{ row.assignmentNo }}</strong>
            <span class="status-badge status-badge--{{ statusBadgeClass(effectiveStatus(row)) }}">{{ effectiveStatus(row) }}</span>
          </div>
          <p>{{ row.vehicleName }} · {{ row.driverName || 'No driver' }}</p>
          <p class="card-meta">{{ row.startAt | date:'dd MMM yyyy' }} · GPS {{ row.gpsOnline ? 'Online' : 'Offline' }}</p>
        </article>
      }
    </div>
  `,
  styles: [`
    .table-wrap { overflow: auto; border: 1px solid #e2e8f0; border-radius: 10px; background: #fff; }
    .data-table { width: 100%; border-collapse: collapse; min-width: 1100px; }
    thead th { position: sticky; top: 0; z-index: 1; background: #f8fafc; padding: 0.625rem 0.75rem; text-align: left; font-size: 0.6875rem; text-transform: uppercase; letter-spacing: .04em; color: #64748b; border-bottom: 1px solid #e2e8f0; }
    .data-row { cursor: pointer; transition: background .12s; }
    .data-row:hover { background: #f8fafc; }
    .data-row--alt { background: #fcfdfe; }
    .data-row td { padding: 0.625rem 0.75rem; font-size: 0.8125rem; border-bottom: 1px solid #f1f5f9; vertical-align: top; }
    .cell-primary { font-weight: 600; color: #0f172a; }
    .cell-sub, .cell-muted { font-size: 0.75rem; color: #64748b; }
    .assignment-no { font-family: ui-monospace, monospace; font-weight: 700; }
    .gps-pill { font-size: 0.6875rem; font-weight: 700; padding: 2px 8px; border-radius: 99px; background: #f1f5f9; color: #64748b; }
    .gps-pill--online { background: #d1fae5; color: #047857; }
    .status-badge { font-size: 0.6875rem; font-weight: 700; padding: 2px 8px; border-radius: 99px; text-transform: uppercase; }
    .status-badge--success { background: #d1fae5; color: #047857; }
    .status-badge--warning { background: #fef3c7; color: #b45309; }
    .status-badge--error { background: #fee2e2; color: #b91c1c; }
    .status-badge--info { background: #dbeafe; color: #1d4ed8; }
    .status-badge--slate { background: #f1f5f9; color: #475569; }
    .td-actions, .th-actions { text-align: right; white-space: nowrap; }
    .action-btn { border: none; background: transparent; padding: 0.25rem; border-radius: 6px; cursor: pointer; color: #475569; }
    .action-btn:hover { background: #f1f5f9; color: #0f766e; }
    .action-btn mat-icon { font-size: 18px; width: 18px; height: 18px; }
    .skeleton { height: 14px; background: linear-gradient(90deg, #f1f5f9, #e2e8f0, #f1f5f9); border-radius: 4px; }
    .empty { padding: 2rem; text-align: center; color: #94a3b8; }
    .card-list { display: none; gap: 0.75rem; margin-top: 1rem; }
    .assignment-card { border: 1px solid #e2e8f0; border-radius: 10px; padding: 0.875rem; background: #fff; }
    .card-head { display: flex; justify-content: space-between; align-items: center; gap: 0.5rem; }
    .card-meta { font-size: 0.75rem; color: #64748b; margin: 0.25rem 0 0; }
    @media (max-width: 900px) {
      .table-wrap { display: none; }
      .card-list { display: grid; }
    }
  `]
})
export class AssignmentTableComponent {
  readonly rows = input<FleetAssignment[]>([]);
  readonly loading = input(false);
  readonly selectable = input(false);
  readonly selectedIds = input<Set<number>>(new Set());
  readonly allSelected = input(false);

  readonly view = output<FleetAssignment>();
  readonly transfer = output<FleetAssignment>();
  readonly complete = output<FleetAssignment>();
  readonly cancel = output<FleetAssignment>();
  readonly history = output<FleetAssignment>();
  readonly toggleRow = output<number>();
  readonly toggleAll = output<boolean>();

  readonly transferTypes = TRANSFER_TYPES;
  effectiveStatus = assignmentEffectiveStatus;
  statusBadgeClass = statusBadgeClass;

  isOpen(row: FleetAssignment): boolean {
    const s = this.effectiveStatus(row);
    return ['Active', 'Assigned', 'Scheduled', 'PendingApproval', 'Overdue'].includes(s);
  }

  skeletonCols(): number[] {
    return Array.from({ length: this.colspan() }, (_, i) => i);
  }

  colspan(): number {
    return this.selectable() ? 13 : 12;
  }
}
