import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { FormsModule } from '@angular/forms';
import { FuelFraudEvent, FuelFraudService } from '../../../core/services/fuel-fraud.service';

@Component({
  selector: 'app-fuel-fraud-events-page',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatButtonModule, FormsModule],
  template: `
    <div class="page">
      <header>
        <h1><mat-icon aria-hidden="true">gpp_bad</mat-icon> Fuel fraud events</h1>
        <p>Pump liters ordered by drivers vs OBD fuel-level increase after leaving the station.</p>
        <label>
          Status
          <select [(ngModel)]="statusFilter" (change)="reload()">
            <option value="">All</option>
            <option>FraudAlert</option>
            <option>PendingVerification</option>
            <option>Matched</option>
            <option>NoObdData</option>
            <option>Resolved</option>
          </select>
        </label>
      </header>
      @if (loading()) {
        <p>Loading…</p>
      } @else {
        <div class="list">
          @for (e of items(); track e.id) {
            <article [class.alert]="e.status === 'FraudAlert'">
              <div class="top">
                <strong>{{ e.vehicleRegistration || e.vehicleName }}</strong>
                <span class="status">{{ e.status }}</span>
              </div>
              <small>{{ e.driverName }} · {{ e.stationName || 'Station not named' }}</small>
              <p>
                Ordered {{ e.orderedLiters | number:'1.1-1' }} L
                @if (e.actualIncreaseLiters != null) {
                  · OBD +{{ e.actualIncreaseLiters | number:'1.1-1' }} L
                }
                @if (e.shortfallLiters != null && e.shortfallLiters > 0) {
                  · <strong class="short">{{ e.shortfallLiters | number:'1.1-1' }} L short</strong>
                }
              </p>
              <small>{{ e.createdAtUtc | date: 'medium' }}</small>
              @if (e.latitude != null && e.longitude != null) {
                <small class="loc">{{ e.latitude | number:'1.4-4' }}, {{ e.longitude | number:'1.4-4' }}</small>
              }
              @if (e.status === 'FraudAlert') {
                <button mat-stroked-button type="button" (click)="resolve(e)">Mark resolved</button>
              }
            </article>
          } @empty {
            <p>No fuel fraud events yet.</p>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .page { padding: 1.25rem; max-width: 960px; }
    header h1 { display: flex; align-items: center; gap: 0.35rem; margin: 0 0 0.25rem; }
    header p { color: #64748b; margin: 0 0 0.75rem; }
    .list { display: grid; gap: 0.75rem; }
    article { border: 1px solid #e2e8f0; border-radius: 10px; padding: 0.85rem; }
    article.alert { border-color: #fecaca; background: #fff7f7; }
    .top { display: flex; justify-content: space-between; gap: 0.5rem; }
    .status { font-size: 0.75rem; text-transform: uppercase; color: #b45309; }
    .short { color: #b91c1c; }
    .loc { display: block; }
  `],
})
export class FuelFraudEventsPageComponent implements OnInit {
  private readonly api = inject(FuelFraudService);
  readonly loading = signal(true);
  readonly items = signal<FuelFraudEvent[]>([]);
  statusFilter = 'FraudAlert';

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.api.listEvents(this.statusFilter || undefined).subscribe({
      next: (rows) => {
        this.items.set(rows);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  resolve(e: FuelFraudEvent): void {
    this.api.resolve(e.id).subscribe(() => this.reload());
  }
}
