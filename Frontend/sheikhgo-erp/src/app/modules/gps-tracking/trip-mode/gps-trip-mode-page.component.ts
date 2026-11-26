import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { FleetTripCreationMode } from '../../../core/models/gps-tracking.model';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { AuthService } from '../../../core/services/auth.service';
import { DEFAULT_TIME_ZONE } from '../../../core/constants/time-zone.constants';

@Component({
  selector: 'app-gps-trip-mode-page',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule, MatButtonModule],
  template: `
    <div class="page">
      <header>
        <h1><mat-icon aria-hidden="true">alt_route</mat-icon> Trip creation mode</h1>
        <p>
          Choose how this company records trips. Booking mode keeps manual/booking trips on
          <strong>/trips</strong>. Auto-trip mode creates one GPS trip per vehicle per day on
          <strong>/gps/trips</strong> — no manual trip required.
        </p>
      </header>

      @if (loading()) {
        <p>Loading…</p>
      } @else {
        <section class="card">
          <label class="mode">
            <input
              type="radio"
              name="tripMode"
              [value]="modes.Booking"
              [(ngModel)]="selectedMode"
              (change)="dirty.set(true)" />
            <span>
              <strong>Booking mode</strong>
              <small>For agencies (e.g. Jonesy): bookings drive operational trips; GPS uses stop-based segments.</small>
            </span>
          </label>
          <label class="mode">
            <input
              type="radio"
              name="tripMode"
              [value]="modes.AutoGpsDaily"
              [(ngModel)]="selectedMode"
              (change)="dirty.set(true)" />
            <span>
              <strong>Auto-trip (GPS daily)</strong>
              <small>When a vehicle moves, one trip per day from first movement to last stop — distance, time, and speed captured automatically.</small>
            </span>
          </label>
          <p class="tz">Company timezone: <code>{{ timeZoneId() }}</code></p>
          @if (message()) {
            <p class="msg" [class.error]="error()">{{ message() }}</p>
          }
          @if (canManage()) {
            <button mat-flat-button color="primary" type="button" [disabled]="!dirty() || saving()" (click)="save()">
              Save mode
            </button>
          } @else {
            <p class="hint">You need GPS device manage permission to change this setting.</p>
          }
        </section>
      }
    </div>
  `,
  styles: [`
    .page { padding: 1.25rem; max-width: 720px; }
    header h1 { display: flex; align-items: center; gap: 0.35rem; margin: 0 0 0.25rem; }
    header p { color: #64748b; margin: 0 0 1rem; line-height: 1.45; }
    .card { border: 1px solid #e2e8f0; border-radius: 12px; padding: 1rem; display: grid; gap: 0.85rem; }
    .mode { display: flex; gap: 0.65rem; align-items: flex-start; cursor: pointer; }
    .mode strong { display: block; }
    .mode small { display: block; color: #64748b; margin-top: 0.15rem; }
    .tz { margin: 0; font-size: 0.85rem; color: #475569; }
    .msg { margin: 0; color: #15803d; }
    .msg.error { color: #b91c1c; }
    .hint { color: #64748b; margin: 0; font-size: 0.85rem; }
  `]
})
export class GpsTripModePageComponent implements OnInit {
  private readonly api = inject(GpsTrackingService);
  private readonly auth = inject(AuthService);

  readonly modes = FleetTripCreationMode;
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly dirty = signal(false);
  readonly timeZoneId = signal(DEFAULT_TIME_ZONE);
  readonly message = signal('');
  readonly error = signal(false);

  selectedMode: FleetTripCreationMode = FleetTripCreationMode.Booking;

  canManage(): boolean {
    return this.auth.hasPermission('Gps.DeviceManage');
  }

  ngOnInit(): void {
    this.api.getTripCreationMode().subscribe({
      next: s => {
        this.selectedMode = s.tripCreationMode;
        this.timeZoneId.set(s.timeZoneId);
        this.loading.set(false);
      },
      error: () => {
        this.message.set('Could not load trip mode settings.');
        this.error.set(true);
        this.loading.set(false);
      }
    });
  }

  save(): void {
    this.saving.set(true);
    this.message.set('');
    this.error.set(false);
    this.api.saveTripCreationMode(this.selectedMode).subscribe({
      next: s => {
        this.selectedMode = s.tripCreationMode;
        this.timeZoneId.set(s.timeZoneId);
        this.dirty.set(false);
        this.message.set('Trip creation mode saved.');
        this.saving.set(false);
      },
      error: () => {
        this.message.set('Save failed.');
        this.error.set(true);
        this.saving.set(false);
      }
    });
  }
}
