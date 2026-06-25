import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { VehicleListItem } from '../../../../core/models/vehicle.model';
import { UiStatusBadgeComponent } from '../../../../shared/components/ui/status-badge/ui-status-badge.component';
import { UiEmptyStateComponent } from '../../../../shared/components/ui/empty-state/ui-empty-state.component';
import { VehiclePagination } from '../../models/vehicle-inventory.model';
import { deriveOperationalStatus } from '../../utils/vehicle-status.util';
import { formatRelativeTime } from '../../../../core/utils/relative-time.util';
import { resolveVehicleImageUrl } from '../../../../core/utils/upload-url.util';

@Component({
  selector: 'vehicle-mobile-cards',
  standalone: true,
  imports: [DatePipe, DecimalPipe, MatIconModule, UiStatusBadgeComponent, UiEmptyStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './vehicle-mobile-cards.component.html',
  styleUrls: ['./vehicle-mobile-cards.component.scss']
})
export class VehicleMobileCardsComponent {
  readonly rows = input<VehicleListItem[]>([]);
  readonly loading = input(false);
  readonly pagination = input.required<VehiclePagination>();

  readonly cardSelect = output<VehicleListItem>();
  readonly track = output<VehicleListItem>();
  readonly assign = output<VehicleListItem>();
  readonly pageChange = output<number>();
  readonly pageSizeChange = output<number>();

  readonly totalPages = computed(() => {
    const p = this.pagination();
    return Math.max(1, Math.ceil(p.total / p.pageSize));
  });

  hasTracker(row: VehicleListItem): boolean {
    return !!(row.hasGpsDevice || row.gpsImei);
  }

  canTrack(row: VehicleListItem): boolean {
    return this.hasTracker(row) || (row.locationLatitude != null && row.locationLongitude != null);
  }

  serviceSubtext(row: VehicleListItem): { text: string; alert: boolean } {
    if (row.nextDueMileage != null) {
      const remaining = row.nextDueMileage - row.currentMileage;
      if (remaining <= 0) return { text: 'Service Due', alert: true };
      return { text: `${Math.round(remaining).toLocaleString()} km remaining`, alert: remaining <= 500 };
    }
    if (row.nextServiceDue) {
      const due = new Date(row.nextServiceDue);
      if (due.getTime() < Date.now()) return { text: 'Service Due', alert: true };
      return { text: `Due ${due.toLocaleDateString()}`, alert: false };
    }
    if (row.serviceAlert?.trim()) {
      const trimmed = row.serviceAlert.trim();
      return { text: trimmed.length > 24 ? `${trimmed.slice(0, 24)}…` : trimmed, alert: true };
    }
    return { text: '—', alert: false };
  }

  operationalStatus(row: VehicleListItem) {
    return deriveOperationalStatus(row);
  }

  vehicleSubtitle(row: VehicleListItem): string {
    const parts: string[] = [];
    if (row.make?.trim()) parts.push(row.make.trim());
    if (row.model?.trim()) parts.push(row.model.trim());
    if (parts.length) {
      let line = parts.join(' ');
      if (row.year) line += ` · ${row.year}`;
      return line;
    }
    return row.name?.trim() || '—';
  }

  locationText(row: VehicleListItem): string {
    if (row.locationLatitude != null && row.locationLongitude != null) {
      return `${row.locationLatitude.toFixed(2)}, ${row.locationLongitude.toFixed(2)}`;
    }
    if (this.hasTracker(row)) return 'No GPS fix';
    return 'No tracker';
  }

  lastPing(row: VehicleListItem): string {
    return formatRelativeTime(row.gpsLastSeenAt ?? row.locationLastUpdate);
  }

  imageUrl(row: VehicleListItem): string | null {
    return resolveVehicleImageUrl(row.imageUrl);
  }

  rangeStart(): number {
    const p = this.pagination();
    return p.total === 0 ? 0 : (p.page - 1) * p.pageSize + 1;
  }

  rangeEnd(): number {
    const p = this.pagination();
    return Math.min(p.page * p.pageSize, p.total);
  }
}
