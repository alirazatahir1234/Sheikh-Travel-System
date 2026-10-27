import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import {
  ChargingStation,
  EvFleetDashboard,
  FleetEvAnomalyService,
} from '../../../core/services/fleet-ev-anomaly.service';

@Component({
  selector: 'app-ev-dashboard-page',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatButtonModule, MatProgressBarModule],
  template: `
    <div class="ev-page">
      <header>
        <h1>Electric fleet</h1>
        <p>Live battery, range alerts, and charging locations for EV vehicles.</p>
      </header>
      @if (loading()) {
        <p>Loading…</p>
      } @else if (error()) {
        <p class="error">{{ error() }}</p>
      } @else if (dashboard()) {
        <section class="dispatch-note">
          <mat-icon>route</mat-icon>
          <div>
            <strong>Range-aware trip assignment</strong>
            <p>
              Smart assignment skips EV drivers when estimated range (rated range × battery %) is below
              trip distance plus a 15% buffer. Keep battery telemetry current for accurate dispatch.
            </p>
          </div>
        </section>
        <section class="kpis">
          <article>
            <span class="label">EV vehicles</span>
            <strong>{{ dashboard()!.electricVehicleCount }}</strong>
          </article>
          <article>
            <span class="label">Range alerts</span>
            <strong [class.warn]="dashboard()!.rangeAlertCount > 0">{{ dashboard()!.rangeAlertCount }}</strong>
          </article>
          <article>
            <span class="label">Avg battery</span>
            <strong>{{ dashboard()!.averageBatteryPercent ?? '—' }}@if (dashboard()!.averageBatteryPercent != null) {%}</strong>
          </article>
          <article>
            <span class="label">Charging sites</span>
            <strong>{{ dashboard()!.chargingStations.length }}</strong>
          </article>
        </section>

        <h2>Fleet battery</h2>
        <div class="vehicle-grid">
          @for (v of dashboard()!.vehicles; track v.vehicleId) {
            <article [class.alert]="v.rangeAlert">
              <div class="row">
                <strong>{{ v.vehicleName }}</strong>
                <span>{{ v.registrationNumber }}</span>
              </div>
              @if (v.batteryPercent != null) {
                <mat-progress-bar mode="determinate" [value]="v.batteryPercent" />
                <small>{{ v.batteryPercent | number: '1.0-0' }}% · est. {{ v.estimatedRangeKm ?? v.electricRangeKm ?? '—' }} km</small>
              } @else {
                <small>No live battery telemetry</small>
              }
              @if (v.rangeAlert) {
                <span class="badge"><mat-icon>battery_alert</mat-icon> Range alert</span>
              }
            </article>
          } @empty {
            <p>No electric vehicles — set Fuel type to Electric on a vehicle record.</p>
          }
        </div>

        <h2>Charging stations</h2>
        <ul class="stations">
          @for (s of dashboard()!.chargingStations; track s.id) {
            <li>
              <mat-icon>ev_station</mat-icon>
              <div>
                <strong>{{ s.name }}</strong>
                <small>{{ s.address || (s.latitude + ', ' + s.longitude) }} · {{ s.connectorType || 'Generic' }}</small>
              </div>
            </li>
          } @empty {
            <li>No charging stations yet — add via API or seed for your tenant.</li>
          }
        </ul>
      }
    </div>
  `,
  styles: [`
    .ev-page { padding: 1.25rem; max-width: 1100px; }
    header h1 { margin: 0 0 0.25rem; font-size: 1.5rem; }
    header p { margin: 0 0 1rem; color: #64748b; }
    .kpis { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 0.75rem; margin-bottom: 1.5rem; }
    .kpis article { background: #f8fafc; border-radius: 10px; padding: 0.75rem 1rem; }
    .kpis .label { display: block; font-size: 0.75rem; color: #64748b; }
    .kpis strong { font-size: 1.35rem; }
    .kpis strong.warn { color: #b45309; }
    .vehicle-grid { display: grid; gap: 0.75rem; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); margin-bottom: 1.5rem; }
    .vehicle-grid article { border: 1px solid #e2e8f0; border-radius: 10px; padding: 0.75rem; }
    .vehicle-grid article.alert { border-color: #fbbf24; background: #fffbeb; }
    .row { display: flex; justify-content: space-between; gap: 0.5rem; margin-bottom: 0.35rem; }
    .badge { display: inline-flex; align-items: center; gap: 0.25rem; color: #b45309; font-size: 0.8rem; margin-top: 0.35rem; }
    .badge mat-icon { font-size: 16px; width: 16px; height: 16px; }
    .stations { list-style: none; padding: 0; margin: 0; display: grid; gap: 0.5rem; }
    .stations li { display: flex; gap: 0.5rem; align-items: flex-start; padding: 0.65rem; background: #f8fafc; border-radius: 8px; }
    .stations mat-icon { color: #0f766e; }
    h2 { font-size: 1.1rem; margin: 1rem 0 0.5rem; }
    .error { color: #b91c1c; background: #fef2f2; border-radius: 8px; padding: 0.75rem 1rem; }
    .dispatch-note {
      display: flex; gap: 0.65rem; align-items: flex-start;
      background: #ecfdf5; border: 1px solid #a7f3d0; border-radius: 10px;
      padding: 0.75rem 1rem; margin-bottom: 1rem;
    }
    .dispatch-note mat-icon { color: #0f766e; margin-top: 0.1rem; }
    .dispatch-note p { margin: 0.25rem 0 0; color: #475569; font-size: 0.9rem; }
  `],
})
export class EvDashboardPageComponent implements OnInit {
  private readonly api = inject(FleetEvAnomalyService);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly dashboard = signal<EvFleetDashboard | null>(null);

  ngOnInit(): void {
    this.api.getEvDashboard().subscribe({
      next: (d) => {
        this.dashboard.set(d);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load electric fleet data. Refresh the page or restart the API if you just deployed.');
        this.loading.set(false);
      },
    });
  }
}
