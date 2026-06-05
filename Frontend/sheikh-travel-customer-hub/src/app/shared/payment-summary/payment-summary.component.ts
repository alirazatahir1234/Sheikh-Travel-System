import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { PortalPayState } from '../../core/models/portal.models';
import { StatusBadgeComponent } from '../status-badge/status-badge.component';

@Component({
  selector: 'app-payment-summary',
  standalone: true,
  imports: [DecimalPipe, StatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section
      class="rounded-2xl border border-slate-200 bg-white p-5 shadow-card"
      role="region"
      aria-label="Payment summary"
    >
      <h3 class="text-xs font-semibold uppercase tracking-wide text-slate-500">Payment summary</h3>
      <dl class="mt-4 space-y-3 text-sm">
        <div class="flex justify-between gap-4">
          <dt class="text-slate-600">Total</dt>
          <dd class="font-semibold text-slate-900">PKR {{ total() | number : '1.2-2' }}</dd>
        </div>
        <div class="flex justify-between gap-4">
          <dt class="text-slate-600">Paid</dt>
          <dd class="font-semibold text-emerald-700">PKR {{ paid() | number : '1.2-2' }}</dd>
        </div>
        <div class="flex justify-between gap-4 border-t border-slate-100 pt-3">
          <dt class="font-medium text-slate-800">Remaining</dt>
          <dd
            class="font-bold tabular-nums"
            [class.text-rose-600]="remaining() > 0"
            [class.text-emerald-700]="remaining() <= 0"
          >
            PKR {{ remaining() | number : '1.2-2' }}
          </dd>
        </div>
      </dl>
      <div class="mt-4 flex flex-wrap items-center gap-2">
        <span class="text-xs text-slate-500">Status</span>
        <app-status-badge [payState]="payState()" />
      </div>
    </section>
  `
})
export class PaymentSummaryComponent {
  readonly total = input.required<number>();
  readonly paid = input.required<number>();
  readonly remaining = input.required<number>();
  readonly payState = input.required<PortalPayState>();
}
