import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import {
  FleetEvAnomalyService,
  VehicleDocumentTrackerItem,
} from '../../../core/services/fleet-ev-anomaly.service';

@Component({
  selector: 'app-documents-tracker-page',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  template: `
    <div class="tracker-page">
      <header>
        <h1>Insurance &amp; documents</h1>
        <p>Registration, insurance, fitness certificate, and compliance uploads with renewal status.</p>
      </header>
      @if (loading()) {
        <p>Loading…</p>
      } @else {
        <table>
          <thead>
            <tr>
              <th>Vehicle</th>
              <th>Document</th>
              <th>Expiry</th>
              <th>Status</th>
              <th>Source</th>
            </tr>
          </thead>
          <tbody>
            @for (d of items(); track trackRow(d)) {
              <tr [class.expired]="d.status === 'Expired'" [class.expiring]="d.status === 'Expiring'">
                <td>{{ d.vehicleName }}</td>
                <td>{{ d.documentType }}</td>
                <td>{{ d.expiryDate ? (d.expiryDate | date: 'mediumDate') : '—' }}</td>
                <td><span class="pill">{{ d.status }}</span></td>
                <td>{{ d.source }}</td>
              </tr>
            } @empty {
              <tr><td colspan="5">No document expiry dates on file.</td></tr>
            }
          </tbody>
        </table>
      }
    </div>
  `,
  styles: [`
    .tracker-page { padding: 1.25rem; }
    table { width: 100%; border-collapse: collapse; font-size: 0.9rem; }
    th, td { text-align: left; padding: 0.5rem 0.65rem; border-bottom: 1px solid #e2e8f0; }
    tr.expired { background: #fef2f2; }
    tr.expiring { background: #fffbeb; }
    .pill { font-size: 0.75rem; font-weight: 600; }
  `],
})
export class DocumentsTrackerPageComponent implements OnInit {
  private readonly api = inject(FleetEvAnomalyService);
  readonly loading = signal(true);
  readonly items = signal<VehicleDocumentTrackerItem[]>([]);

  ngOnInit(): void {
    this.api.getDocumentTracker().subscribe({
      next: (rows) => {
        this.items.set(rows);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  trackRow(d: VehicleDocumentTrackerItem): string {
    return `${d.vehicleId}-${d.documentType}-${d.complianceDocumentId ?? d.source}`;
  }
}
