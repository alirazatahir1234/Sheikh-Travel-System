import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { FormsModule } from '@angular/forms';
import { FatigueDetectionService, FatigueEvent } from '../../../core/services/fatigue-detection.service';

@Component({
  selector: 'app-fatigue-events-page',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatButtonModule, MatSelectModule, FormsModule],
  template: `
    <div class="page">
      <header>
        <h1><mat-icon aria-hidden="true">bedtime</mat-icon> Fatigue events</h1>
        <p>Tenant fleet fatigue alerts — camera/dashcam signals, OBD context, ack status, and composite risk.</p>
        <label>
          Status
          <select [(ngModel)]="statusFilter" (change)="reload()">
            <option value="">All</option>
            <option>AwaitingAck</option>
            <option>Escalating</option>
            <option>Acknowledged</option>
            <option>AutoStopped</option>
            <option>Resolved</option>
          </select>
        </label>
      </header>
      @if (loading()) {
        <p>Loading…</p>
      } @else {
        <div class="list">
          @for (e of items(); track e.id) {
            <article [attr.data-risk]="riskBand(e.compositeRiskScore)">
              <div class="top">
                <strong>{{ e.driverName }}</strong>
                <span class="status">{{ e.status }}</span>
              </div>
              <small>{{ e.vehicleName || ('Vehicle #' + e.vehicleId) }} · {{ e.source }} · {{ e.signalType }}</small>
              @if (e.compositeRiskScore != null) {
                <p class="risk">Composite risk {{ e.compositeRiskScore | percent:'1.0-0' }}
                  @if (e.driverRiskScore != null) { · Driver risk {{ e.driverRiskScore | percent:'1.0-0' }} }
                </p>
              }
              @if (e.lastEscalationNote) { <p class="note">{{ e.lastEscalationNote }}</p> }
              <small>{{ e.createdAtUtc | date: 'medium' }}</small>
              @if (e.latitude != null && e.longitude != null) {
                <small class="loc">{{ e.latitude | number:'1.4-4' }}, {{ e.longitude | number:'1.4-4' }}</small>
              }
              @if (e.status !== 'Resolved' && e.status !== 'Acknowledged') {
                <button mat-stroked-button type="button" (click)="resolve(e)">Mark resolved</button>
              }
            </article>
          } @empty {
            <p>No fatigue events yet.</p>
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
    article[data-risk='high'] { border-color: #fecaca; background: #fff7f7; }
    .top { display: flex; justify-content: space-between; }
    .status { font-size: 0.75rem; text-transform: uppercase; color: #b45309; }
    .risk { font-weight: 600; margin: 0.35rem 0; }
    .note { margin: 0.25rem 0; color: #475569; }
    .loc { display: block; }
  `],
})
export class FatigueEventsPageComponent implements OnInit {
  private readonly api = inject(FatigueDetectionService);
  readonly loading = signal(true);
  readonly items = signal<FatigueEvent[]>([]);
  statusFilter = '';

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

  resolve(e: FatigueEvent): void {
    this.api.resolve(e.id).subscribe(() => this.reload());
  }

  riskBand(score?: number | null): string {
    if (score == null) return 'normal';
    return score >= 0.65 ? 'high' : 'normal';
  }
}
