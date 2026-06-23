import { Component, Input } from '@angular/core';

@Component({
  selector: 'fleet-expiry-card',
  template: `
    <article class="expiry-card" [attr.data-tone]="tone">
      <div class="expiry-card__head">
        <mat-icon class="expiry-card__icon">{{ icon }}</mat-icon>
        <fleet-status-badge [status]="statusLabel" [toneOverride]="tone"></fleet-status-badge>
      </div>
      <h3 class="expiry-card__title">{{ documentType }}</h3>
      <p class="expiry-card__entity">{{ entityName }}</p>
      <div class="expiry-card__meta">
        <span>Expires</span>
        <strong>{{ expiryDate ? (expiryDate | date: 'mediumDate') : '—' }}</strong>
      </div>
      <p class="expiry-card__days" *ngIf="daysRemaining !== null">
        {{ daysRemaining >= 0 ? daysRemaining + ' days remaining' : (-daysRemaining) + ' days overdue' }}
      </p>
    </article>
  `,
  styleUrls: ['./fleet-expiry-card.component.scss']
})
export class FleetExpiryCardComponent {
  @Input() documentType = '';
  @Input() entityName = '';
  @Input() icon = 'verified';
  @Input() expiryDate?: string | Date | null;

  get daysRemaining(): number | null {
    if (!this.expiryDate) return null;
    const expiry = new Date(this.expiryDate).getTime();
    const now = Date.now();
    return Math.ceil((expiry - now) / (1000 * 60 * 60 * 24));
  }

  get tone(): 'success' | 'warning' | 'danger' | 'muted' {
    const days = this.daysRemaining;
    if (days === null) return 'muted';
    if (days < 0) return 'danger';
    if (days <= 30) return 'warning';
    return 'success';
  }

  get statusLabel(): string {
    const days = this.daysRemaining;
    if (days === null) return 'Unknown';
    if (days < 0) return 'Expired';
    if (days <= 30) return 'Expiring';
    return 'Valid';
  }
}
