import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { PortalRouteDto } from '../../core/models/portal.models';

@Component({
  selector: 'app-route-summary-card',
  standalone: true,
  imports: [DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (route()) {
      <div class="rounded-2xl border border-primary-200 bg-primary-50/50 p-4 text-sm shadow-card">
        <p class="text-xs font-semibold uppercase text-primary-800">Route summary</p>
        <p class="mt-2 text-lg font-bold text-slate-900">{{ route()!.source }} → {{ route()!.destination }}</p>
        <dl class="mt-3 grid grid-cols-2 gap-2 text-slate-700">
          <div>
            <dt class="text-xs text-slate-500">Distance</dt>
            <dd class="font-semibold">{{ route()!.distanceKm | number : '1.0-1' }} km</dd>
          </div>
          <div>
            <dt class="text-xs text-slate-500">Est. duration</dt>
            <dd class="font-semibold">{{ durationLabel() }}</dd>
          </div>
          <div>
            <dt class="text-xs text-slate-500">Base fare</dt>
            <dd class="font-semibold">PKR {{ route()!.basePrice | number : '1.0-0' }}</dd>
          </div>
          @if (seatsRemaining() != null) {
            <div>
              <dt class="text-xs text-slate-500">Seats left</dt>
              <dd class="font-semibold">{{ seatsRemaining() }}</dd>
            </div>
          }
        </dl>
      </div>
    }
  `
})
export class RouteSummaryCardComponent {
  readonly route = input<PortalRouteDto | null>(null);
  readonly seatsRemaining = input<number | null>(null);

  durationLabel(): string {
    const r = this.route();
    if (!r) return '—';
    const mins = r.estimatedDurationMinutes ?? Math.ceil((r.distanceKm / 70) * 60);
    const h = Math.floor(mins / 60);
    const m = mins % 60;
    return h > 0 ? `${h}h ${m}m` : `${m} min`;
  }
}
