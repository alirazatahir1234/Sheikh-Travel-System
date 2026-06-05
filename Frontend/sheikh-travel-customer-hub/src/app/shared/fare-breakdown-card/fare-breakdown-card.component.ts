import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { PriceBreakdown } from '../../core/models/portal.models';

@Component({
  selector: 'app-fare-breakdown-card',
  standalone: true,
  imports: [DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (breakdown()) {
      <div class="rounded-2xl border border-slate-200 bg-white p-4 text-sm shadow-card">
        <p class="text-xs font-semibold uppercase text-slate-500">Fare breakdown</p>
        <dl class="mt-3 space-y-2">
          <div class="flex justify-between gap-4">
            <dt class="text-slate-600">Base fare</dt>
            <dd class="font-medium">PKR {{ breakdown()!.otherCharges | number : '1.2-2' }}</dd>
          </div>
          <div class="flex justify-between gap-4">
            <dt class="text-slate-600">Fuel</dt>
            <dd class="font-medium">PKR {{ breakdown()!.fuelCost | number : '1.2-2' }}</dd>
          </div>
          <div class="flex justify-between gap-4">
            <dt class="text-slate-600">Driver allowance</dt>
            <dd class="font-medium">PKR {{ breakdown()!.driverAllowance | number : '1.2-2' }}</dd>
          </div>
          <div class="flex justify-between gap-4">
            <dt class="text-slate-600">Tolls &amp; other</dt>
            <dd class="font-medium">PKR {{ breakdown()!.tollCharges | number : '1.2-2' }}</dd>
          </div>
          @if (breakdown()!.isRoundTrip) {
            <div class="flex justify-between gap-4 text-primary-800">
              <dt>Round trip</dt>
              <dd class="font-medium">Included</dd>
            </div>
          }
          @if (discount() > 0) {
            <div class="flex justify-between gap-4 text-emerald-700">
              <dt>Promo discount</dt>
              <dd class="font-medium">− PKR {{ discount() | number : '1.2-2' }}</dd>
            </div>
          }
          <div class="flex justify-between gap-4 border-t border-slate-200 pt-2 text-base font-bold text-slate-900">
            <dt>Total</dt>
            <dd>PKR {{ total() | number : '1.2-2' }}</dd>
          </div>
        </dl>
        @if (busy()) {
          <p class="mt-2 text-xs text-slate-500">Updating fare…</p>
        }
      </div>
    }
  `
})
export class FareBreakdownCardComponent {
  readonly breakdown = input<PriceBreakdown | null>(null);
  readonly discount = input(0);
  readonly busy = input(false);

  total(): number {
    const b = this.breakdown();
    if (!b) return 0;
    return Math.max(0, b.totalAmount - this.discount());
  }
}
