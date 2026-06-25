import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterModule } from '@angular/router';
import { DriverListItem, DriverStatusLabels, driverDisplayName } from '../../../../core/models/driver.model';
import { UiStatusBadgeComponent } from '../../../../shared/components/ui/status-badge/ui-status-badge.component';
import { UiEmptyStateComponent } from '../../../../shared/components/ui/empty-state/ui-empty-state.component';
import { DriverPagination, DRIVER_PAGE_SIZE_OPTIONS, computeDriverScore, scoreTone } from '../../models/driver-inventory.model';
import { licenseExpiryLabel, licenseExpiryState, availabilityBucketLabel } from '../../utils/driver-status.util';
import { resolveDriverPhotoUrl } from '../../../../core/utils/upload-url.util';

@Component({
  selector: 'driver-table',
  standalone: true,
  imports: [MatIconModule, RouterModule, UiStatusBadgeComponent, UiEmptyStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './driver-table.component.html',
  styleUrls: ['./driver-table.component.scss']
})
export class DriverTableComponent {
  readonly rows = input<DriverListItem[]>([]);
  readonly loading = input(false);
  readonly error = input<string | null>(null);
  readonly pagination = input.required<DriverPagination>();

  readonly rowClick = output<DriverListItem>();
  readonly view = output<DriverListItem>();
  readonly edit = output<DriverListItem>();
  readonly delete = output<DriverListItem>();
  readonly pageChange = output<number>();
  readonly pageSizeChange = output<number>();
  readonly retry = output<void>();

  readonly pageSizeOptions = DRIVER_PAGE_SIZE_OPTIONS;

  displayName(row: DriverListItem): string {
    return driverDisplayName(row);
  }

  licenseLabel(row: DriverListItem): string {
    return licenseExpiryLabel(licenseExpiryState(row.licenseExpired, row.licenseExpiringSoon));
  }

  licenseVariant(row: DriverListItem): 'success' | 'warning' | 'error' {
    const state = licenseExpiryState(row.licenseExpired, row.licenseExpiringSoon);
    if (state === 'expired') return 'error';
    if (state === 'expiring') return 'warning';
    return 'success';
  }

  statusLabel(row: DriverListItem): string {
    return DriverStatusLabels[row.status] ?? 'Unknown';
  }

  availabilityLabel(row: DriverListItem): string {
    return availabilityBucketLabel(row.availabilityBucket);
  }

  assignedVehicle(row: DriverListItem): string {
    return row.assignedVehicleRegistration || row.assignedVehicleCode || 'Unassigned';
  }

  driverScore(row: DriverListItem): number {
    return computeDriverScore(row);
  }

  scoreVariant(row: DriverListItem): 'success' | 'warning' | 'error' {
    return scoreTone(this.driverScore(row));
  }

  initials(row: DriverListItem): string {
    const name = this.displayName(row);
    return name.split(' ').slice(0, 2).map(w => w[0] ?? '').join('').toUpperCase();
  }

  private readonly brokenImages = signal<Set<number>>(new Set());

  resolvePhoto(row: DriverListItem): string | null {
    if (this.brokenImages().has(row.id)) return null;
    return resolveDriverPhotoUrl(row.photoUrl);
  }

  onImgError(row: DriverListItem): void {
    this.brokenImages.update(s => new Set(s).add(row.id));
  }

  formatExpiryDate(dateStr: string | Date | null | undefined): string {
    if (!dateStr) return '—';
    const d = new Date(dateStr);
    if (isNaN(d.getTime())) return '—';
    return d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
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
