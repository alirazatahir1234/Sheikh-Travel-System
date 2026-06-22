import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { Vehicle, VehicleStatusLabels } from '../../../../../core/models/vehicle.model';
import { resolveVehicleImageUrl } from '../../../../../core/utils/upload-url.util';

@Component({
  selector: 'vehicle-history-profile-card',
  standalone: true,
  imports: [DecimalPipe, DatePipe, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (vehicle(); as v) {
      <div class="profile-card">
        <div class="profile-card__inner">
          <div class="profile-card__photo" [class.profile-card__photo--has-image]="!!resolvedImage()"
            [style.backgroundImage]="resolvedImage() ? 'url(' + resolvedImage() + ')' : null">
            <span class="profile-card__status-glass">
              <span class="status-dot"></span>
              {{ statusLabel(v.status) }}
            </span>
            @if (!resolvedImage()) {
              <mat-icon class="profile-card__placeholder-icon">local_shipping</mat-icon>
            }
          </div>
          <div class="profile-card__info">
            <div class="profile-card__plate-row">
              <span class="plate-badge">{{ v.registrationNumber || '—' }}</span>
            </div>
            <h3 class="profile-card__name">{{ v.name }}</h3>
            <p class="profile-card__sub">{{ subtitle(v) }}</p>
            <div class="profile-card__stats">
              <div class="stat-item">
                <span class="stat-label">VIN</span>
                <span class="stat-value">{{ v.vin || '—' }}</span>
              </div>
              <span class="stat-sep" aria-hidden="true"></span>
              <div class="stat-item">
                <span class="stat-label">Mileage</span>
                <span class="stat-value">{{ v.currentMileage | number:'1.0-0' }} km</span>
              </div>
              <span class="stat-sep" aria-hidden="true"></span>
              <div class="stat-item">
                <span class="stat-label">Last Service</span>
                <span class="stat-value">
                  @if (lastServiceDate()) {
                    {{ lastServiceDate() | date:'mediumDate' }}
                  } @else { — }
                </span>
              </div>
              <span class="stat-sep" aria-hidden="true"></span>
              <div class="stat-item">
                <span class="stat-label">Next Service</span>
                <span class="stat-value stat-value--brand">
                  @if (nextServiceDue()) {
                    {{ nextServiceDue() | date:'mediumDate' }}
                    @if (nextDueMileage()) {
                      <small>· {{ nextDueMileage() | number:'1.0-0' }} km</small>
                    }
                  } @else { — }
                </span>
              </div>
              <span class="stat-sep" aria-hidden="true"></span>
              <div class="stat-item">
                <span class="stat-label">Status</span>
                <span class="status-pill">{{ statusLabel(v.status) }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .profile-card {
      background: #fff;
      border-radius: 12px;
      box-shadow: 0 4px 6px rgba(0, 0, 0, 0.07), 0 2px 4px rgba(0, 0, 0, 0.05);
      overflow: hidden;
      margin-bottom: 1.5rem;
      border: 1px solid #e2e8f0;
    }
    .profile-card__inner {
      display: flex;
      align-items: stretch;
    }
    .profile-card__photo {
      width: 240px;
      min-height: 180px;
      flex-shrink: 0;
      position: relative;
      display: flex;
      align-items: center;
      justify-content: center;
      background: linear-gradient(135deg, #0B6B50 0%, #0d9488 50%, #14b8a6 100%);
      background-size: cover;
      background-position: center;
      overflow: hidden;
    }
    .profile-card__photo--has-image::after {
      content: '';
      position: absolute;
      inset: 0;
      background: linear-gradient(to right, rgba(0,0,0,.15), transparent);
    }
    .profile-card__status-glass {
      position: absolute;
      top: 12px;
      left: 12px;
      z-index: 1;
      display: inline-flex;
      align-items: center;
      gap: 5px;
      padding: 4px 10px;
      border-radius: 20px;
      background: rgba(255, 255, 255, 0.2);
      backdrop-filter: blur(8px);
      border: 1px solid rgba(255, 255, 255, 0.3);
      color: #fff;
      font-size: 0.6875rem;
      font-weight: 600;
    }
    .status-dot {
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: #4ade80;
    }
    .profile-card__placeholder-icon {
      font-size: 4rem;
      width: 4rem;
      height: 4rem;
      color: rgba(255, 255, 255, 0.9);
      position: relative;
      z-index: 0;
    }
    .profile-card__info {
      flex: 1;
      padding: 1.5rem 1.75rem;
      display: flex;
      flex-direction: column;
      justify-content: center;
    }
    .profile-card__plate-row { margin-bottom: 0.25rem; }
    .plate-badge {
      display: inline-block;
      background: #0f172a;
      color: #fff;
      font-size: 1.125rem;
      font-weight: 800;
      padding: 0.35rem 1rem;
      border-radius: 6px;
      letter-spacing: 0.15em;
      font-family: 'Courier New', Courier, monospace;
      border: 3px solid #334155;
    }
    .profile-card__name {
      margin: 0 0 0.25rem;
      font-size: 1.375rem;
      font-weight: 800;
      color: #0f172a;
    }
    .profile-card__sub {
      margin: 0 0 1.25rem;
      font-size: 0.8125rem;
      color: #64748b;
    }
    .profile-card__stats {
      display: flex;
      gap: 2rem;
      flex-wrap: wrap;
      align-items: flex-start;
    }
    .stat-item { display: flex; flex-direction: column; gap: 2px; }
    .stat-label {
      font-size: 0.6875rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: #94a3b8;
    }
    .stat-value {
      font-size: 1rem;
      font-weight: 700;
      color: #1e293b;
    }
    .stat-value--brand { color: #0B6B50; }
    .stat-value small { font-size: 0.75rem; font-weight: 600; color: #64748b; }
    .stat-sep {
      width: 1px;
      background: #e2e8f0;
      align-self: stretch;
      min-height: 2.5rem;
    }
    .status-pill {
      display: inline-block;
      padding: 0.2rem 0.625rem;
      border-radius: 999px;
      background: #e8f5f0;
      color: #0B6B50;
      font-size: 0.8125rem;
      font-weight: 700;
    }
    @media (max-width: 767px) {
      .profile-card__inner { flex-direction: column; }
      .profile-card__photo { width: 100%; min-height: 140px; }
      .stat-sep { display: none; }
      .profile-card__stats { gap: 1rem; }
    }
  `]
})
export class VehicleHistoryProfileCardComponent {
  readonly vehicle = input<Vehicle | null>(null);
  readonly imageUrl = input<string | null>(null);
  readonly lastServiceDate = input<string | null>(null);
  readonly nextServiceDue = input<string | null>(null);
  readonly nextDueMileage = input<number | null>(null);

  resolvedImage(): string | null {
    return resolveVehicleImageUrl(this.imageUrl());
  }

  statusLabel(status: number): string {
    return VehicleStatusLabels[status as keyof typeof VehicleStatusLabels] ?? String(status);
  }

  subtitle(v: Vehicle): string {
    const parts = [v.make, v.model, v.year].filter(Boolean);
    return parts.length ? parts.join(' ') : 'Vehicle details';
  }
}
