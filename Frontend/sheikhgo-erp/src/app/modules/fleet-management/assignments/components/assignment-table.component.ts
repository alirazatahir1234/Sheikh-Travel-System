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

    <div class="card-list">
      @if (loading()) {
        @for (i of [1, 2, 3]; track i) {
          <div class="assignment-card assignment-card--skeleton"></div>
        }
      } @else if (!rows().length) {
        <div class="empty empty--card">
          <mat-icon>event_available</mat-icon>
          <p>No assignments found</p>
          <span>Adjust filters or create a new assignment.</span>
        </div>
      } @else {
        @for (row of rows(); track row.id) {
          <article class="assignment-card" (click)="view.emit(row)">
            <div class="card-head">
              <div class="card-head__left">
                @if (selectable()) {
                  <input type="checkbox" class="card-check"
                    [checked]="selectedIds().has(row.id)"
                    (click)="$event.stopPropagation()"
                    (change)="toggleRow.emit(row.id)" />
                }
                <strong class="assignment-no">{{ row.assignmentNo }}</strong>
              </div>
              <span class="status-badge status-badge--{{ statusBadgeClass(effectiveStatus(row)) }}">
                {{ effectiveStatus(row) }}
              </span>
            </div>

            <dl class="card-meta">
              <div>
                <dt>Vehicle</dt>
                <dd>{{ row.vehicleName }}</dd>
              </div>
              <div>
                <dt>Driver</dt>
                <dd>{{ row.driverName || 'Unassigned' }}</dd>
              </div>
              <div>
                <dt>Type</dt>
                <dd>{{ row.assignmentType }}</dd>
              </div>
              <div>
                <dt>GPS</dt>
                <dd>
                  <span class="gps-pill" [class.gps-pill--online]="row.gpsOnline">
                    {{ row.gpsOnline ? 'Online' : 'Offline' }}
                  </span>
                </dd>
              </div>
              <div>
                <dt>Start</dt>
                <dd>{{ row.startAt | date:'dd MMM yyyy' }}</dd>
              </div>
              <div>
                <dt>End</dt>
                <dd>{{ row.endAt ? (row.endAt | date:'dd MMM yyyy') : '—' }}</dd>
              </div>
            </dl>

            <div class="card-actions" (click)="$event.stopPropagation()">
              <button type="button" class="card-action" (click)="view.emit(row)">
                <mat-icon>visibility</mat-icon> View
              </button>
              @if (isOpen(row)) {
                <button type="button" class="card-action" (click)="transfer.emit(row)">
                  <mat-icon>swap_horiz</mat-icon> Transfer
                </button>
                <button type="button" class="card-action card-action--primary" (click)="complete.emit(row)">
                  <mat-icon>task_alt</mat-icon> Complete
                </button>
              }
              <button type="button" class="card-action" (click)="history.emit(row)">
                <mat-icon>history</mat-icon> History
              </button>
            </div>
          </article>
        }
      }
    </div>
  `,
  styles: [`
    :host { display: block; min-width: 0; }

    .table-wrap {
      overflow-x: auto;
      -webkit-overflow-scrolling: touch;
      border: 1px solid #e2e8f0;
      border-radius: 10px;
      background: #fff;
    }

    .data-table {
      width: max-content;
      min-width: 72rem;
      border-collapse: collapse;
    }

    thead th {
      position: sticky;
      top: 0;
      z-index: 2;
      background: #f8fafc;
      padding: 0.75rem;
      text-align: left;
      font-size: clamp(0.75rem, 1.5vw, 0.8125rem);
      text-transform: uppercase;
      letter-spacing: .04em;
      color: #64748b;
      border-bottom: 1px solid #e2e8f0;
      white-space: nowrap;
    }

    .data-row { cursor: pointer; transition: background .12s; }
    .data-row:hover { background: #f8fafc; }
    .data-row--alt { background: #fcfdfe; }
    .data-row td {
      padding: 0.75rem;
      font-size: clamp(0.8125rem, 1.5vw, 0.875rem);
      border-bottom: 1px solid #f1f5f9;
      vertical-align: top;
    }

    .cell-primary { font-weight: 600; color: #0f172a; }
    .cell-sub, .cell-muted { font-size: 0.75rem; color: #64748b; }
    .assignment-no { font-family: ui-monospace, monospace; font-weight: 700; color: #0f766e; }

    .gps-pill {
      display: inline-flex;
      font-size: 0.6875rem;
      font-weight: 700;
      padding: 0.2rem 0.5rem;
      border-radius: 99px;
      background: #f1f5f9;
      color: #64748b;
      white-space: nowrap;
    }
    .gps-pill--online { background: #d1fae5; color: #047857; }

    .status-badge {
      display: inline-flex;
      align-items: center;
      max-width: 100%;
      font-size: 0.6875rem;
      font-weight: 700;
      padding: 0.2rem 0.5rem;
      border-radius: 99px;
      text-transform: uppercase;
      white-space: nowrap;
    }
    .status-badge--success { background: #d1fae5; color: #047857; }
    .status-badge--warning { background: #fef3c7; color: #b45309; }
    .status-badge--error { background: #fee2e2; color: #b91c1c; }
    .status-badge--info { background: #dbeafe; color: #1d4ed8; }
    .status-badge--slate { background: #f1f5f9; color: #475569; }

    .td-actions, .th-actions { text-align: right; white-space: nowrap; }
    .action-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 36px;
      height: 36px;
      border: none;
      background: transparent;
      border-radius: 8px;
      cursor: pointer;
      color: #475569;
    }
    .action-btn:hover { background: #f1f5f9; color: #0f766e; }
    .action-btn mat-icon { font-size: 18px; width: 18px; height: 18px; }

    .skeleton {
      height: 14px;
      background: linear-gradient(90deg, #f1f5f9, #e2e8f0, #f1f5f9);
      border-radius: 4px;
      animation: shimmer 1.2s infinite;
    }

    @keyframes shimmer {
      0% { opacity: 1; }
      50% { opacity: 0.5; }
      100% { opacity: 1; }
    }

    .empty {
      padding: 2rem;
      text-align: center;
      color: #94a3b8;
    }
    .empty mat-icon { font-size: 40px; width: 40px; height: 40px; margin-bottom: 0.5rem; }
    .empty--card {
      border: 1px solid #e2e8f0;
      border-radius: 10px;
      background: #fff;
    }

    .card-list { display: none; gap: 0.75rem; }

    .assignment-card {
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 1rem;
      background: #fff;
      cursor: pointer;
      transition: border-color 0.15s, box-shadow 0.15s;
    }
    .assignment-card:hover {
      border-color: #cbd5e1;
      box-shadow: 0 2px 8px rgba(15, 23, 42, 0.06);
    }
    .assignment-card--skeleton {
      min-height: 9rem;
      background: linear-gradient(90deg, #f8fafc 25%, #f1f5f9 50%, #f8fafc 75%);
      background-size: 200% 100%;
      animation: shimmer 1.2s infinite;
      cursor: default;
    }

    .card-head {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 0.5rem;
      margin-bottom: 0.75rem;
    }
    .card-head__left {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      min-width: 0;
    }
    .card-check { flex-shrink: 0; width: 18px; height: 18px; }

    .card-meta {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.5rem 0.75rem;
      margin: 0 0 0.75rem;
      font-size: 0.8125rem;
    }
    .card-meta dt {
      margin: 0;
      font-size: 0.625rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.03em;
      color: #94a3b8;
    }
    .card-meta dd {
      margin: 0.125rem 0 0;
      font-weight: 600;
      color: #0f172a;
      word-break: break-word;
    }

    .card-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      padding-top: 0.75rem;
      border-top: 1px solid #f1f5f9;
    }
    .card-action {
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
      padding: 0.375rem 0.625rem;
      min-height: 40px;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      background: #fff;
      font-size: 0.75rem;
      font-weight: 600;
      color: #475569;
      cursor: pointer;
    }
    .card-action mat-icon { font-size: 16px; width: 16px; height: 16px; }
    .card-action--primary {
      color: #0f766e;
      border-color: #99f6e4;
      background: #f0fdfa;
    }

    @media (max-width: 767px) {
      .table-wrap { display: none; }
      .card-list { display: grid; grid-template-columns: 1fr; }
      .card-action { flex: 1 1 calc(50% - 0.25rem); justify-content: center; min-height: 44px; }
    }

    @media (min-width: 768px) and (max-width: 1023px) {
      .card-list { display: none; }
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
