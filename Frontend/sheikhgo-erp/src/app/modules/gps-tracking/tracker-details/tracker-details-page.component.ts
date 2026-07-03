import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { TrackerAssignment, TrackerDetail } from '../../../core/models/gps-tracking.model';
import { relayOutputLabel } from '../utils/relay-immobilizer.util';
import {
  assignmentBadgeClass,
  assignmentLabel,
  isTrackerInInventory,
  isTrackerInstalled,
  normalizeInventoryStatus
} from '../utils/tracker-status.util';
import { SharedModule } from '../../../shared/shared.module';

@Component({
    selector: 'app-tracker-details-page',
    templateUrl: './tracker-details-page.component.html',
    styleUrls: ['./tracker-details-page.component.scss'],
    standalone: true,
    imports: [SharedModule]
})
export class TrackerDetailsPageComponent implements OnInit {
  private readonly gps = inject(GpsTrackingService);
  private readonly toast = inject(UiToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  tracker: TrackerDetail | null = null;
  assignments: TrackerAssignment[] = [];
  loading = true;
  removing = false;
  trackerId = 0;

  ngOnInit(): void {
    this.trackerId = Number(this.route.snapshot.paramMap.get('id'));
    if (!this.trackerId) {
      void this.router.navigate(['/gps-tracking/devices']);
      return;
    }
    this.load();
  }

  get statusLabel(): string {
    if (!this.tracker) return '—';
    return assignmentLabel(this.tracker);
  }

  get statusClass(): string {
    if (!this.tracker) return 'badge-gray';
    return assignmentBadgeClass(this.tracker);
  }

  get isInstalled(): boolean {
    return !!this.tracker && isTrackerInstalled(this.tracker);
  }

  get isMaintenance(): boolean {
    return normalizeInventoryStatus(this.tracker?.currentStatus) === 'Maintenance';
  }

  get immobilizerLabel(): string {
    if (!this.tracker?.supportsEngineCutoff) return 'Not configured';
    return relayOutputLabel(this.tracker.relayOutput ?? 'output1');
  }

  get trackerModelLine(): string {
    if (!this.tracker) return '—';
    const brand = this.tracker.trackerBrandName || this.tracker.vendor || '';
    const model = this.tracker.modelName || this.tracker.model || '';
    return [brand, model].filter(Boolean).join(' ') || '—';
  }

  assignmentStatus(row: TrackerAssignment): string {
    return row.isActive ? 'Active' : 'Completed';
  }

  formatDate(value?: string | null): string {
    if (!value) return '—';
    return new Date(value).toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' });
  }

  back(): void {
    void this.router.navigate(['/gps-tracking/devices']);
  }

  goTransfer(): void {
    void this.router.navigate(['/gps-tracking/devices', this.trackerId, 'transfer']);
  }

  goEditInventory(): void {
    void this.router.navigate(['/gps-tracking/devices', this.trackerId, 'edit']);
  }

  removeInstallation(): void {
    if (!this.tracker || this.removing) return;
    const reason = prompt('Reason for removal (optional):');
    if (reason === null) return;

    if (!confirm(`Remove "${this.tracker.name}" from ${this.tracker.vehicleName ?? 'vehicle'}?`)) return;

    this.removing = true;
    this.gps.uninstallTracker(this.trackerId, { reason: reason || undefined }).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: () => {
        this.toast.success('Installation removed — tracker is available');
        void this.router.navigate(['/gps-tracking/devices', this.trackerId, 'install']);
      },
      error: err => {
        this.toast.error(err?.error?.message ?? 'Remove failed');
        this.removing = false;
      }
    });
  }

  private load(): void {
    this.loading = true;
    forkJoin({
      tracker: this.gps.getTracker(this.trackerId),
      assignments: this.gps.getTrackerAssignments(this.trackerId)
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: ({ tracker, assignments }) => {
        if (isTrackerInInventory(tracker) && normalizeInventoryStatus(tracker.currentStatus) !== 'Maintenance') {
          void this.router.navigate(['/gps-tracking/devices', this.trackerId, 'install']);
          return;
        }

        this.tracker = tracker;
        this.assignments = assignments;
        this.loading = false;
      },
      error: () => {
        this.toast.error('Tracker not found');
        void this.router.navigate(['/gps-tracking/devices']);
      }
    });
  }
}
