import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { VehicleServiceHistoryItem } from '../../../../../core/models/maintenance.model';
import { serviceTypeBadgeClass, serviceTypeIcon, serviceTypeTone } from '../utils/service-type.util';

@Component({
  selector: 'service-history-timeline',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
  <section class="timeline-section">
    <header class="section-header">
      <h3 class="section-title">
        Service Timeline
        <span class="section-count">{{ items().length }}</span>
      </h3>
    </header>

    @if (!items().length) {
      <p class="empty">No service records in this range.</p>
    } @else {
      <div class="timeline">
        @for (item of items(); track item.source + '-' + item.id) {
          <article class="timeline-item">
            <span class="timeline-dot" [attr.data-tone]="tone(item.serviceType)"></span>
            <div class="service-card">
              <header class="service-card__header">
                <div class="service-type-icon" [attr.data-tone]="tone(item.serviceType)">
                  <mat-icon>{{ iconFor(item.serviceType) }}</mat-icon>
                </div>
                <div class="service-card__title">
                  <h4>{{ item.serviceType }}</h4>
                  <p>{{ subtitle(item) }}</p>
                </div>
                <span class="service-badge" [class]="badgeClass(item.serviceType)">{{ item.serviceType }}</span>
              </header>
              <div class="service-card__body">
                <div class="info-item">
                  <span class="info-label"><mat-icon>event</mat-icon> Date</span>
                  <span class="info-value">{{ item.serviceDate | date:'mediumDate' }}</span>
                </div>
                <div class="info-item">
                  <span class="info-label"><mat-icon>store</mat-icon> Workshop</span>
                  <span class="info-value">{{ item.workshopName || '—' }}</span>
                </div>
                <div class="info-item">
                  <span class="info-label"><mat-icon>engineering</mat-icon> Technician</span>
                  <span class="info-value">{{ item.technicianName || '—' }}</span>
                </div>
                <div class="info-item">
                  <span class="info-label"><mat-icon>payments</mat-icon> Total Cost</span>
                  <span class="info-value info-value--price">{{ item.totalCost | currency }}</span>
                </div>
              </div>
              <footer class="service-card__footer">
                <span class="notes-text">
                  <mat-icon>sticky_note_2</mat-icon>
                  {{ item.notes || 'No notes recorded.' }}
                </span>
                @if (item.invoiceUrl) {
                  <a class="invoice-btn" [href]="item.invoiceUrl" target="_blank" rel="noopener">
                    <mat-icon>receipt_long</mat-icon> Invoice
                  </a>
                } @else {
                  <span class="invoice-btn invoice-btn--disabled">
                    <mat-icon>receipt_long</mat-icon> No invoice
                  </span>
                }
              </footer>
            </div>
          </article>
        }
      </div>
    }
  </section>
  `,
  styles: [`
    .timeline-section { margin-bottom: 1.5rem; }
    .section-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 1rem;
    }
    .section-title {
      margin: 0;
      font-size: 1rem;
      font-weight: 800;
      color: #0f172a;
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    .section-count {
      background: #e8f5f0;
      color: #0B6B50;
      font-size: 0.75rem;
      font-weight: 700;
      padding: 0.15rem 0.625rem;
      border-radius: 20px;
    }
    .empty {
      text-align: center;
      color: #94a3b8;
      padding: 2rem;
      background: #fff;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
    }
    .timeline {
      position: relative;
      padding-left: 2.25rem;
    }
    .timeline::before {
      content: '';
      position: absolute;
      left: 0.9375rem;
      top: 8px;
      bottom: 8px;
      width: 2px;
      background: linear-gradient(to bottom, #0B6B50, #e8f5f0);
      border-radius: 2px;
    }
    .timeline-item {
      position: relative;
      margin-bottom: 1.25rem;
    }
    .timeline-dot {
      position: absolute;
      left: -1.6875rem;
      top: 1.125rem;
      width: 16px;
      height: 16px;
      border-radius: 50%;
      border: 3px solid #fff;
      box-shadow: 0 0 0 2px #0B6B50;
      z-index: 1;
      background: #0B6B50;
    }
    .timeline-dot[data-tone="warning"] {
      background: #f59e0b;
      box-shadow: 0 0 0 2px #f59e0b;
    }
    .timeline-dot[data-tone="danger"] {
      background: #dc2626;
      box-shadow: 0 0 0 2px #dc2626;
    }
    .service-card {
      background: #fff;
      border-radius: 12px;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.08), 0 1px 2px rgba(0, 0, 0, 0.04);
      border: 1.5px solid transparent;
      overflow: hidden;
      transition: box-shadow 0.2s, transform 0.2s, border-color 0.2s;
    }
    .service-card:hover {
      box-shadow: 0 10px 15px rgba(0, 0, 0, 0.08), 0 4px 6px rgba(0, 0, 0, 0.04);
      transform: translateY(-1px);
      border-color: #e8f5f0;
    }
    .service-card__header {
      padding: 1rem 1.25rem 0.875rem;
      display: flex;
      align-items: center;
      gap: 0.875rem;
      border-bottom: 1px solid #f1f5f9;
    }
    .service-type-icon {
      width: 42px;
      height: 42px;
      border-radius: 10px;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      background: #e8f5f0;
      color: #0B6B50;
    }
    .service-type-icon[data-tone="warning"] { background: #fef3c7; color: #b45309; }
    .service-type-icon[data-tone="danger"] { background: #fee2e2; color: #dc2626; }
    .service-type-icon mat-icon { font-size: 1.25rem; width: 1.25rem; height: 1.25rem; }
    .service-card__title { flex: 1; min-width: 0; }
    .service-card__title h4 {
      margin: 0;
      font-size: 0.9375rem;
      font-weight: 800;
      color: #0f172a;
    }
    .service-card__title p {
      margin: 0.15rem 0 0;
      font-size: 0.75rem;
      color: #64748b;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .service-badge {
      padding: 0.25rem 0.625rem;
      border-radius: 20px;
      font-size: 0.6875rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.03em;
      white-space: nowrap;
      flex-shrink: 0;
    }
    .badge--primary { background: #e8f5f0; color: #0B6B50; }
    .badge--warning { background: #fef3c7; color: #b45309; }
    .badge--danger { background: #fee2e2; color: #dc2626; }
    .service-card__body {
      padding: 1rem 1.25rem;
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 1rem;
    }
    .info-item { display: flex; flex-direction: column; gap: 0.2rem; }
    .info-label {
      font-size: 0.625rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      color: #94a3b8;
      display: flex;
      align-items: center;
      gap: 0.25rem;
    }
    .info-label mat-icon { font-size: 0.75rem; width: 0.75rem; height: 0.75rem; }
    .info-value { font-size: 0.8125rem; font-weight: 600; color: #334155; }
    .info-value--price { color: #0B6B50; font-size: 0.9375rem; font-weight: 800; }
    .service-card__footer {
      padding: 0.75rem 1.25rem;
      background: #f8fafc;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.75rem;
      border-top: 1px solid #f1f5f9;
    }
    .notes-text {
      font-size: 0.75rem;
      color: #64748b;
      flex: 1;
      min-width: 0;
      display: flex;
      align-items: flex-start;
      gap: 0.35rem;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .notes-text mat-icon { font-size: 0.875rem; width: 0.875rem; height: 0.875rem; flex-shrink: 0; margin-top: 1px; }
    .invoice-btn {
      display: inline-flex;
      align-items: center;
      gap: 0.3rem;
      padding: 0.3rem 0.75rem;
      background: #fff;
      border: 1.5px solid #e2e8f0;
      border-radius: 6px;
      font-size: 0.6875rem;
      font-weight: 600;
      color: #475569;
      text-decoration: none;
      cursor: pointer;
      transition: all 0.15s;
      flex-shrink: 0;
    }
    .invoice-btn mat-icon { font-size: 0.875rem; width: 0.875rem; height: 0.875rem; }
    .invoice-btn:hover { border-color: #0B6B50; color: #0B6B50; }
    .invoice-btn--disabled {
      opacity: 0.6;
      cursor: default;
      pointer-events: none;
    }
    @media (max-width: 900px) {
      .service-card__body { grid-template-columns: repeat(2, 1fr); }
    }
    @media (max-width: 560px) {
      .service-card__header { flex-wrap: wrap; }
      .service-badge { width: 100%; text-align: center; }
      .service-card__body { grid-template-columns: 1fr; }
      .service-card__footer { flex-direction: column; align-items: flex-start; }
    }
  `]
})
export class ServiceHistoryTimelineComponent {
  readonly items = input.required<VehicleServiceHistoryItem[]>();

  iconFor(serviceType: string): string {
    return serviceTypeIcon(serviceType);
  }

  tone(serviceType: string): string {
    return serviceTypeTone(serviceType);
  }

  badgeClass(serviceType: string): string {
    return serviceTypeBadgeClass(serviceType);
  }

  subtitle(item: VehicleServiceHistoryItem): string {
    if (item.notes) {
      return item.notes.length > 80 ? item.notes.slice(0, 80) + '…' : item.notes;
    }
    return item.workshopName || 'Service record';
  }
}
