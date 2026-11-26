import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { TravelContextProfitDetail, TravelContextType } from '../../models/travel.models';
import { TravelProfitabilityService } from '../../services/travel-profitability.service';
import { marginPercentText, marginTone } from '../suppliers/supplier-finance';

/** Margin of one booking file, visa case or group (place behind `Travel.Profitability.View`). */
@Component({
  selector: 'app-travel-profit-card',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-card tn-card-body">
      <div class="flex items-center justify-between">
        <h3 class="tn-section-title">Profitability</h3>
        @if (profit()) { <button mat-button (click)="expanded.set(!expanded())">{{ expanded() ? 'Hide lines' : 'Show lines' }}</button> }
      </div>
      @if (error()) { <p class="mt-2 text-sm text-fleet-text-muted">{{ error() }}</p> }
      @else if (profit(); as p) {
        <dl class="mt-2 grid grid-cols-2 gap-3 text-sm md:grid-cols-4">
          <div><dt class="text-xs text-fleet-text-muted">Revenue (net)</dt><dd class="font-semibold">{{ p.revenue | currency: p.reportingCurrencyCode : 'code' : '1.0-0' }}</dd></div>
          <div><dt class="text-xs text-fleet-text-muted">Supplier cost</dt><dd class="font-semibold">{{ p.cost | currency: p.reportingCurrencyCode : 'code' : '1.0-0' }}</dd>
            @if (p.hasEstimates) { <dd class="text-xs text-amber-700">{{ p.estimatedCost | currency: p.reportingCurrencyCode : 'code' : '1.0-0' }} estimated</dd> }</div>
          <div><dt class="text-xs text-fleet-text-muted">Commission</dt><dd class="font-semibold">{{ p.commission | currency: p.reportingCurrencyCode : 'code' : '1.0-0' }}</dd></div>
          <div><dt class="text-xs text-fleet-text-muted">Margin</dt><dd class="font-semibold" [class]="tone(p.margin, p.marginPercent)">
            {{ p.margin | currency: p.reportingCurrencyCode : 'code' : '1.0-0' }} · {{ pct(p.marginPercent) }}</dd></div>
        </dl>
        @if (p.missingRateCount) { <p class="mt-2 text-xs text-amber-700">{{ p.missingRateCount }} amount(s) have no exchange rate and are left out.</p> }
        @if (expanded()) {
          <table class="mt-3 w-full text-sm">
            @for (l of p.lines; track $index) {
              <tr class="border-t border-fleet-border">
                <td class="py-1.5 text-xs text-fleet-text-muted">{{ l.kind }}</td>
                <td class="py-1.5">{{ l.description }} <span class="text-xs text-fleet-text-muted">{{ l.reference }} · {{ l.date | date: 'mediumDate': 'UTC' }}</span>
                  @if (l.estimated) { <span class="text-xs text-amber-700"> · estimated</span> }</td>
                <td class="py-1.5 text-right">{{ l.amount | currency: l.currencyCode : 'code' : '1.2-2' }}</td>
                <td class="py-1.5 text-right">{{ l.converted === null ? 'no rate' : (l.converted | currency: p.reportingCurrencyCode : 'code' : '1.0-0') }}</td>
              </tr>
            }
          </table>
        }
      } @else { <p class="mt-2 text-sm text-fleet-text-muted">Calculating…</p> }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelProfitCardComponent {
  private readonly api = inject(TravelProfitabilityService);
  private readonly destroyRef = inject(DestroyRef);
  readonly contextType = input.required<TravelContextType>();
  readonly contextId = input.required<number>();
  protected readonly profit = signal<TravelContextProfitDetail | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly expanded = signal(false);

  constructor() {
    effect(() => {
      const type = this.contextType(), id = this.contextId();
      untracked(() => this.load(type, id));
    });
  }

  /** Reload after a change on the host page (new invoice, bill, cancellation). */
  load(type = this.contextType(), id = this.contextId()): void {
    this.api.getContext(type, id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: p => { this.profit.set(p); this.error.set(null); },
      error: err => this.error.set(apiErrorMessage(err, 'Profitability is not available yet.'))
    });
  }

  protected pct(p: number | null): string { return marginPercentText(p); }
  protected tone(margin: number, percent: number | null): string { return marginTone(margin, percent); }
}
