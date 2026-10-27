import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { FuelFraudEvent, FuelFraudService } from '../../../core/services/fuel-fraud.service';

@Component({
  selector: 'app-fuel-fraud-platform-page',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  template: `
    <div class="page">
      <header>
        <h1><mat-icon aria-hidden="true">gpp_bad</mat-icon> Fuel fraud — all companies</h1>
        <p>Super admin view across tenants (fraud alerts first).</p>
      </header>
      @if (loading()) {
        <p>Loading…</p>
      } @else {
        <table>
          <thead>
            <tr>
              <th>Tenant</th>
              <th>Vehicle</th>
              <th>Driver</th>
              <th>Ordered</th>
              <th>OBD</th>
              <th>Short</th>
              <th>Status</th>
              <th>When</th>
            </tr>
          </thead>
          <tbody>
            @for (e of items(); track e.id) {
              <tr [class.alert]="e.status === 'FraudAlert'">
                <td>{{ e.tenantName || e.tenantId }}</td>
                <td>{{ e.vehicleRegistration || e.vehicleName }}</td>
                <td>{{ e.driverName }}</td>
                <td>{{ e.orderedLiters | number:'1.1-1' }} L</td>
                <td>{{ e.actualIncreaseLiters != null ? (e.actualIncreaseLiters | number:'1.1-1') + ' L' : '—' }}</td>
                <td>{{ e.shortfallLiters != null && e.shortfallLiters > 0 ? (e.shortfallLiters | number:'1.1-1') + ' L' : '—' }}</td>
                <td>{{ e.status }}</td>
                <td>{{ e.createdAtUtc | date: 'short' }}</td>
              </tr>
            } @empty {
              <tr><td colspan="8">No events</td></tr>
            }
          </tbody>
        </table>
      }
    </div>
  `,
  styles: [`
    .page { padding: 1.25rem; }
    table { width: 100%; border-collapse: collapse; font-size: 0.9rem; }
    th, td { text-align: left; padding: 0.5rem; border-bottom: 1px solid #e2e8f0; }
    tr.alert { background: #fff7f7; }
  `],
})
export class FuelFraudPlatformPageComponent implements OnInit {
  private readonly api = inject(FuelFraudService);
  readonly loading = signal(true);
  readonly items = signal<FuelFraudEvent[]>([]);

  ngOnInit(): void {
    this.api.listPlatformEvents(undefined, 'FraudAlert').subscribe({
      next: (rows) => {
        this.items.set(rows);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
