import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MaintenanceRequest } from '../../../../../core/models/maintenance.model';

@Component({
  selector: 'request-mobile-cards',
  standalone: true,
  imports: [DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cards">
      @for (r of requests(); track r.id) {
        <button type="button" class="card" (click)="cardSelect.emit(r.id)">
          <div class="card-top">
            <strong>{{ r.requestNumber }}</strong>
            <span class="status status--{{ r.status.toLowerCase() }}">{{ r.status }}</span>
          </div>
          <p class="vehicle">{{ r.vehicleName }} @if (r.vehicleRegistration) { · {{ r.vehicleRegistration }} }</p>
          <p class="meta">{{ r.issueCategory }} · {{ r.requestType }} · {{ r.priority }} · {{ r.requestDate | date:'MMM d' }}</p>
        </button>
      } @empty {
        <p class="empty">No requests found.</p>
      }
    </div>
  `,
  styles: [`
    .cards { display: flex; flex-direction: column; gap: 0.625rem; }
    .card {
      text-align: left; background: #fff; border: 1px solid #e2e8f0; border-radius: 12px;
      padding: 1rem; cursor: pointer; width: 100%;
    }
    .card-top { display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.375rem; }
    .vehicle { margin: 0; font-size: 0.875rem; font-weight: 600; }
    .meta { margin: 0.25rem 0 0; font-size: 0.75rem; color: #64748b; }
    .status { font-size: 0.625rem; font-weight: 700; text-transform: uppercase; padding: 0.125rem 0.5rem; border-radius: 999px; }
    .status--open { background: #fef3c7; color: #f59e0b; }
    .status--approved { background: #e8f5f0; color: #0b6b50; }
    .empty { text-align: center; color: #94a3b8; padding: 2rem; }
    @media (min-width: 768px) { :host { display: none; } }
  `]
})
export class RequestMobileCardsComponent {
  readonly requests = input<MaintenanceRequest[]>([]);
  readonly cardSelect = output<number>();
}
