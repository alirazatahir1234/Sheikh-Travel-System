import { ChangeDetectionStrategy, Component, computed, effect, input, output, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { DatePipe, DecimalPipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { VehicleListItem } from '../../../../core/models/vehicle.model';
import { UiStatusBadgeComponent } from '../../../../shared/components/ui/status-badge/ui-status-badge.component';
import { UiEmptyStateComponent } from '../../../../shared/components/ui/empty-state/ui-empty-state.component';
import { UiButtonComponent } from '../../../../shared/components/ui/button/ui-button.component';
import { VehiclePagination } from '../../models/vehicle-inventory.model';
import { deriveOperationalStatus } from '../../utils/vehicle-status.util';
import { formatRelativeTime } from '../../../../core/utils/relative-time.util';
import { resolveVehicleImageUrl } from '../../../../core/utils/upload-url.util';

@Component({
  selector: 'vehicle-table',
  standalone: true,
  imports: [
    RouterModule,
    DatePipe,
    DecimalPipe,
    MatIconModule,
    UiStatusBadgeComponent,
    UiEmptyStateComponent,
    UiButtonComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './vehicle-table.component.html',
  styleUrls: ['./vehicle-table.component.scss']
})
export class VehicleTableComponent {
  readonly rows = input<VehicleListItem[]>([]);
  readonly loading = input(false);
  readonly error = input<string | null>(null);
  readonly pagination = input.required<VehiclePagination>();
  readonly selectedIds = input<ReadonlySet<number>>(new Set());

  readonly view = output<VehicleListItem>();
  readonly edit = output<VehicleListItem>();
  readonly track = output<VehicleListItem>();
  readonly assign = output<VehicleListItem>();
  readonly delete = output<VehicleListItem>();
  readonly rowClick = output<VehicleListItem>();
  readonly selectionChange = output<ReadonlySet<number>>();
  readonly pageChange = output<number>();
  readonly pageSizeChange = output<number>();
  readonly retry = output<void>();

  readonly allSelected = computed(() => {
    const rows = this.rows();
    if (!rows.length) return false;
    const sel = this.selectedIds();
    return rows.every(r => sel.has(r.id));
  });

  trackById(_: number, row: VehicleListItem): number {
    return row.id;
  }

  operationalStatus(row: VehicleListItem) {
    return deriveOperationalStatus(row);
  }

  hasTracker(row: VehicleListItem): boolean {
    return !!(row.hasGpsDevice || row.gpsImei);
  }

  lastPing(row: VehicleListItem): string {
    return formatRelativeTime(row.gpsLastSeenAt ?? row.locationLastUpdate);
  }

  locationText(row: VehicleListItem): string {
    if (row.locationLatitude != null && row.locationLongitude != null) {
      return `${row.locationLatitude.toFixed(2)}, ${row.locationLongitude.toFixed(2)}`;
    }
    if (this.hasTracker(row)) {
      return 'No GPS fix';
    }
    return 'No tracker';
  }

  vehicleSubtitle(row: VehicleListItem): string {
    const parts: string[] = [];
    if (row.make?.trim()) parts.push(row.make.trim());
    if (row.model?.trim()) parts.push(row.model.trim());
    if (parts.length) {
      let line = parts.join(' ');
      if (row.year) line += ` • ${row.year}`;
      return line;
    }
    if (row.name?.trim()) return row.name.trim();
    return '—';
  }

  canTrack(row: VehicleListItem): boolean {
    return this.hasTracker(row) || (row.locationLatitude != null && row.locationLongitude != null);
  }

  driverInitials(name: string): string {
    return name.split(' ').map(p => p[0]).join('').slice(0, 2).toUpperCase();
  }

  vehicleImageUrl(row: VehicleListItem): string | null {
    return resolveVehicleImageUrl(row.imageUrl);
  }

  serviceSubtext(row: VehicleListItem): { text: string; alert: boolean } {
    if (row.nextDueMileage != null) {
      const remaining = row.nextDueMileage - row.currentMileage;
      if (remaining <= 0) {
        return { text: 'Service Due', alert: true };
      }
      return { text: `${Math.round(remaining).toLocaleString()} km remaining`, alert: remaining <= 500 };
    }
    if (row.nextServiceDue) {
      const due = new Date(row.nextServiceDue);
      if (due.getTime() < Date.now()) {
        return { text: 'Service Due', alert: true };
      }
      return { text: `Due ${due.toLocaleDateString()}`, alert: false };
    }
    if (row.serviceAlert?.trim()) {
      const trimmed = row.serviceAlert.trim();
      const friendly = trimmed.length > 24 ? `${trimmed.slice(0, 24)}…` : trimmed;
      const isGeneric = /^service due$/i.test(trimmed);
      return { text: isGeneric ? 'Service Due' : friendly, alert: true };
    }
    return { text: '—', alert: false };
  }

  toggleAll(checked: boolean): void {
    const next = new Set(this.selectedIds());
    if (checked) {
      this.rows().forEach(r => next.add(r.id));
    } else {
      this.rows().forEach(r => next.delete(r.id));
    }
    this.selectionChange.emit(next);
  }

  toggleRow(id: number, checked: boolean): void {
    const next = new Set(this.selectedIds());
    if (checked) next.add(id);
    else next.delete(id);
    this.selectionChange.emit(next);
  }

  rangeStart(): number {
    const p = this.pagination();
    return p.total === 0 ? 0 : (p.page - 1) * p.pageSize + 1;
  }

  rangeEnd(): number {
    const p = this.pagination();
    return Math.min(p.page * p.pageSize, p.total);
  }

  totalPages(): number {
    const p = this.pagination();
    return Math.max(1, Math.ceil(p.total / p.pageSize));
  }
}
