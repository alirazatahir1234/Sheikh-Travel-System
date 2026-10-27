import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { FatigueDetectionService, FatigueEvent } from '../../../core/services/fatigue-detection.service';

@Component({
  selector: 'app-fatigue-platform-page',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  template: `
    <div class="page">
      <header>
        <h1><mat-icon aria-hidden="true">bedtime</mat-icon> Fatigue — all companies</h1>
        <p>Super admin view across tenants, sorted by composite risk.</p>
      </header>
      @if (loading()) {
        <p>Loading…</p>
      } @else {
        <table>
          <thead>
            <tr>
              <th>Tenant</th>
              <th>Driver</th>
              <th>Signal</th>
              <th>Risk</th>
              <th>Status</th>
              <th>When</th>
            </tr>
          </thead>
          <tbody>
            @for (e of items(); track e.id) {
              <tr>
                <td>{{ e.tenantName || e.tenantId }}</td>
                <td>{{ e.driverName }}</td>
                <td>{{ e.source }} / {{ e.signalType }}</td>
                <td>{{ e.compositeRiskScore != null ? (e.compositeRiskScore | percent:'1.0-0') : '—' }}</td>
                <td>{{ e.status }}</td>
                <td>{{ e.createdAtUtc | date: 'short' }}</td>
              </tr>
            } @empty {
              <tr><td colspan="6">No events</td></tr>
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
  `],
})
export class FatiguePlatformPageComponent implements OnInit {
  private readonly api = inject(FatigueDetectionService);
  readonly loading = signal(true);
  readonly items = signal<FatigueEvent[]>([]);

  ngOnInit(): void {
    this.api.listPlatformEvents().subscribe({
      next: (rows) => {
        this.items.set(rows);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
