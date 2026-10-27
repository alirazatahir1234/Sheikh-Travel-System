import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { FormsModule } from '@angular/forms';
import { FleetAnomalyEvent, FleetEvAnomalyService } from '../../../core/services/fleet-ev-anomaly.service';

@Component({
  selector: 'app-anomaly-review-page',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatButtonModule, MatCheckboxModule, FormsModule],
  template: `
    <div class="anomaly-page">
      <header>
        <h1>Driving anomalies</h1>
        <p>Night driving in unfamiliar areas, sudden speed drops, and off-route alerts.</p>
        <label class="filter">
          <mat-checkbox [(ngModel)]="unreviewedOnly" (change)="reload()">Unreviewed only</mat-checkbox>
        </label>
      </header>
      @if (loading()) {
        <p>Loading…</p>
      } @else {
        <div class="list">
          @for (a of items(); track a.id) {
            <article [class.reviewed]="a.isReviewed">
              <div class="top">
                <span class="type">{{ labelFor(a.anomalyType) }}</span>
                <span class="sev" [attr.data-sev]="a.severity">{{ a.severity }}</span>
              </div>
              <strong>{{ a.vehicleName || ('Vehicle #' + a.vehicleId) }}</strong>
              @if (a.driverName) { <small>{{ a.driverName }}</small> }
              <p>{{ a.details }}</p>
              <small>{{ a.createdAtUtc | date: 'medium' }}</small>
              @if (!a.isReviewed) {
                <button mat-stroked-button type="button" (click)="review(a)">Mark reviewed</button>
              }
            </article>
          } @empty {
            <p>No anomalies recorded yet.</p>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .anomaly-page { padding: 1.25rem; max-width: 900px; }
    header h1 { margin: 0 0 0.25rem; }
    header p { color: #64748b; margin: 0 0 0.75rem; }
    .list { display: grid; gap: 0.75rem; }
    article { border: 1px solid #e2e8f0; border-radius: 10px; padding: 0.85rem; }
    article.reviewed { opacity: 0.72; }
    .top { display: flex; justify-content: space-between; margin-bottom: 0.35rem; }
    .type { font-weight: 600; text-transform: capitalize; }
    .sev { font-size: 0.75rem; text-transform: uppercase; padding: 0.1rem 0.4rem; border-radius: 4px; background: #fef3c7; }
    .sev[data-sev='high'] { background: #fee2e2; color: #991b1b; }
    p { margin: 0.35rem 0; }
  `],
})
export class AnomalyReviewPageComponent implements OnInit {
  private readonly api = inject(FleetEvAnomalyService);
  readonly loading = signal(true);
  readonly items = signal<FleetAnomalyEvent[]>([]);
  unreviewedOnly = true;

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.api.listAnomalies(this.unreviewedOnly).subscribe({
      next: (rows) => {
        this.items.set(rows);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  review(a: FleetAnomalyEvent): void {
    this.api.reviewAnomaly(a.id).subscribe(() => this.reload());
  }

  labelFor(type: string): string {
    return type.replace(/_/g, ' ');
  }
}
