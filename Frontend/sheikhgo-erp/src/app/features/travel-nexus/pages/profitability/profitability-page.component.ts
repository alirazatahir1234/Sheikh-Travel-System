import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterLink } from '@angular/router';
import { Subject, catchError, debounceTime, of, switchMap, tap } from 'rxjs';
import { UiEmptyStateComponent, UiPageHeaderComponent } from '../../../../shared/components/ui';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  CONTEXT_TYPE_LABEL, PROFIT_DIMENSION_LABEL, TRAVEL_PROFIT_DIMENSIONS, TravelContextProfit, TravelContextType, TravelLookupItem, TravelProfitDimension,
  TravelProfitabilityReport
} from '../../models/travel.models';
import { TravelConfigService } from '../../services/travel-config.service';
import { TravelProfitabilityService } from '../../services/travel-profitability.service';
import { marginPercentText, marginTone } from '../suppliers/supplier-finance';

const iso = (d: Date): string => d.toISOString().substring(0, 10);
export const CONTEXT_ROUTES: Record<TravelContextType, string> = { Booking: '/travel/bookings', VisaCase: '/travel/visa-cases', Group: '/travel/groups' };

@Component({
  selector: 'app-travel-profitability-page',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, FormsModule, RouterLink, MatButtonModule, MatIconModule, UiPageHeaderComponent, UiEmptyStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      <ui-page-header title="Profitability" eyebrow="TravelNexus" icon="trending_up"
        subtitle="Net invoiced revenue − supplier cost (billed, or estimated until billed) − agent commission, in your reporting currency."
        [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Profitability' }]">
      </ui-page-header>

      <div class="tn-card">
        <div class="flex flex-col gap-3 px-5 py-4 lg:flex-row lg:flex-wrap lg:items-end">
          <label class="tn-field lg:w-44"><span class="text-xs">Show</span>
            <select class="tn-input" [(ngModel)]="dimension" (ngModelChange)="reload()">
              @for (d of dimensions; track d) { <option [value]="d">{{ dimensionLabel[d] }}</option> }</select></label>
          <label class="tn-field lg:w-40"><span class="text-xs">Travel from</span><input type="date" class="tn-input" [(ngModel)]="from" (ngModelChange)="reload()" /></label>
          <label class="tn-field lg:w-40"><span class="text-xs">Travel to</span><input type="date" class="tn-input" [(ngModel)]="to" (ngModelChange)="reload()" /></label>
          <label class="tn-field lg:w-44"><span class="text-xs">Branch</span>
            <select class="tn-input" [(ngModel)]="branchId" (ngModelChange)="reload()"><option [ngValue]="null">All branches</option>
              @for (b of branches(); track b.id) { <option [ngValue]="b.id">{{ b.name }}</option> }</select></label>
          <label class="tn-field lg:w-44"><span class="text-xs">Agent</span>
            <select class="tn-input" [(ngModel)]="agentUserId" (ngModelChange)="reload()"><option [ngValue]="null">All agents</option>
              @for (a of agents(); track a.id) { <option [ngValue]="a.id">{{ a.name }}</option> }</select></label>
        </div>
      </div>

      @if (error()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div> }
      @if (report(); as r) {
        @if (r.missingRates.length) {
          <div class="tn-banner warn"><mat-icon>currency_exchange</mat-icon>
            <span>Some amounts have no exchange rate to {{ r.reportingCurrencyCode }} and are left out:
              @for (m of r.missingRates; track m.currencyCode) { {{ m.currencyCode }} from {{ m.earliestDate | date: 'mediumDate': 'UTC' }} ({{ m.amounts }}){{ $last ? '' : ',' }} }</span>
            <a mat-stroked-button class="ml-auto" routerLink="/travel/settings" [queryParams]="{ tab: 'exchange-rates' }">Add rates</a>
          </div>
        }
        <section class="grid gap-4 md:grid-cols-5" [attr.aria-label]="'Totals for travel ' + from + ' to ' + to">
          <div class="tn-card tn-card-body"><p class="text-xs text-fleet-text-muted">Revenue · travel in range</p>
            <p class="text-lg font-semibold">{{ r.totals.revenue | currency: r.reportingCurrencyCode : 'code' : '1.0-0' }}</p></div>
          <div class="tn-card tn-card-body"><p class="text-xs text-fleet-text-muted">Supplier cost</p>
            <p class="text-lg font-semibold">{{ r.totals.cost | currency: r.reportingCurrencyCode : 'code' : '1.0-0' }}</p>
            @if (r.totals.estimatedCost) { <p class="text-xs text-amber-700">{{ r.totals.estimatedCost | currency: r.reportingCurrencyCode : 'code' : '1.0-0' }} estimated (not billed yet)</p> }</div>
          <div class="tn-card tn-card-body"><p class="text-xs text-fleet-text-muted">Agent commission</p>
            <p class="text-lg font-semibold">{{ r.totals.commission | currency: r.reportingCurrencyCode : 'code' : '1.0-0' }}</p></div>
          <div class="tn-card tn-card-body"><p class="text-xs text-fleet-text-muted">Margin</p>
            <p class="text-lg font-semibold" [class]="tone(r.totals.margin, r.totals.marginPercent)">{{ r.totals.margin | currency: r.reportingCurrencyCode : 'code' : '1.0-0' }}</p></div>
          <div class="tn-card tn-card-body"><p class="text-xs text-fleet-text-muted">Margin %</p>
            <p class="text-lg font-semibold" [class]="tone(r.totals.margin, r.totals.marginPercent)">{{ pct(r.totals.marginPercent) }}</p>
            <p class="text-xs text-fleet-text-muted">{{ r.totals.contexts }} {{ r.totals.contexts === 1 ? 'item' : 'items' }}</p></div>
        </section>

        <div class="tn-card overflow-x-auto">
          <table class="w-full min-w-[900px] text-left text-sm">
            <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
              <th class="px-4 py-3">{{ grouped(r) ? (r.dimension === 'Agent' ? 'Agent' : 'Branch') : dimensionLabel[r.dimension] }}</th>
              @if (!grouped(r)) { <th class="px-4 py-3">Travel</th> } @else { <th class="px-4 py-3 text-right">Items</th> }
              <th class="px-4 py-3 text-right">Revenue</th><th class="px-4 py-3 text-right">Cost</th><th class="px-4 py-3 text-right">Commission</th>
              <th class="px-4 py-3 text-right">Margin</th><th class="px-4 py-3 text-right">%</th></tr></thead>
            <tbody class="divide-y divide-fleet-border">
              @if (loading()) { <tr><td colspan="7" class="px-4 py-6 text-center text-fleet-text-muted">Calculating…</td></tr> }
              @else if (grouped(r) ? !r.groups.length : !r.contexts.length) {
                <tr><td colspan="7"><ui-empty-state icon="trending_up" title="Nothing travels in this range" description="Widen the travel dates or choose another view."></ui-empty-state></td></tr>
              }
              @if (!loading() && grouped(r)) {
                @for (g of r.groups; track g.key) {
                  <tr>
                    <td class="px-4 py-3 font-semibold">{{ g.label }}</td><td class="px-4 py-3 text-right">{{ g.contexts }}</td>
                    <td class="px-4 py-3 text-right">{{ g.revenue | currency: r.reportingCurrencyCode : 'code' : '1.0-0' }}</td>
                    <td class="px-4 py-3 text-right">{{ g.cost | currency: r.reportingCurrencyCode : 'code' : '1.0-0' }}
                      @if (g.estimatedCost) { <span class="block text-xs text-amber-700">incl. {{ g.estimatedCost | currency: r.reportingCurrencyCode : 'code' : '1.0-0' }} est.</span> }</td>
                    <td class="px-4 py-3 text-right">{{ g.commission | currency: r.reportingCurrencyCode : 'code' : '1.0-0' }}</td>
                    <td class="px-4 py-3 text-right font-semibold" [class]="tone(g.margin, g.marginPercent)">{{ g.margin | currency: r.reportingCurrencyCode : 'code' : '1.0-0' }}</td>
                    <td class="px-4 py-3 text-right" [class]="tone(g.margin, g.marginPercent)">{{ pct(g.marginPercent) }}</td>
                  </tr>
                }
              }
              @if (!loading() && !grouped(r)) {
                @for (c of r.contexts; track c.contextType + c.contextId) {
                  <tr class="cursor-pointer hover:bg-fleet-surface-alt/40" (click)="open(c)">
                    <td class="px-4 py-3"><span class="font-semibold">{{ c.label }}</span>
                      <span class="block text-xs text-fleet-text-muted">{{ c.agentName || 'No agent' }}{{ c.branchName ? ' · ' + c.branchName : '' }}</span></td>
                    <td class="px-4 py-3 whitespace-nowrap">{{ c.travelDate ? (c.travelDate | date: 'mediumDate': 'UTC') : '—' }}</td>
                    <td class="px-4 py-3 text-right">{{ c.revenue | currency: r.reportingCurrencyCode : 'code' : '1.0-0' }}</td>
                    <td class="px-4 py-3 text-right">{{ c.cost | currency: r.reportingCurrencyCode : 'code' : '1.0-0' }}
                      @if (c.hasEstimates) { <span class="block text-xs text-amber-700">estimated</span> }</td>
                    <td class="px-4 py-3 text-right">{{ c.commission | currency: r.reportingCurrencyCode : 'code' : '1.0-0' }}</td>
                    <td class="px-4 py-3 text-right font-semibold" [class]="tone(c.margin, c.marginPercent)">{{ c.margin | currency: r.reportingCurrencyCode : 'code' : '1.0-0' }}
                      @if (c.missingRateCount) { <span class="block text-xs font-normal text-amber-700">{{ c.missingRateCount }} amount(s) without a rate</span> }</td>
                    <td class="px-4 py-3 text-right" [class]="tone(c.margin, c.marginPercent)">{{ pct(c.marginPercent) }}</td>
                  </tr>
                }
              }
            </tbody>
          </table>
        </div>
        <p class="text-xs text-fleet-text-soft">{{ contextNote }}</p>
      } @else if (loading()) {
        <p class="text-sm text-fleet-text-muted">Calculating…</p>
      }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelProfitabilityPageComponent {
  private readonly api = inject(TravelProfitabilityService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly dimensions = TRAVEL_PROFIT_DIMENSIONS;
  protected readonly dimensionLabel = PROFIT_DIMENSION_LABEL;
  protected readonly contextLabel = CONTEXT_TYPE_LABEL;
  protected readonly contextNote = 'Booking files owned by an Umrah group, and a group\'s visa case, report under the group. Revenue counts issued invoices net of discounts and credit notes.';
  protected readonly report = signal<TravelProfitabilityReport | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly branches = signal<TravelLookupItem[]>([]);
  protected readonly agents = signal<TravelLookupItem[]>([]);
  private readonly load$ = new Subject<void>();

  protected dimension: TravelProfitDimension = 'Booking';
  protected from = iso(new Date(Date.now() - 90 * 86_400_000));
  protected to = iso(new Date(Date.now() + 90 * 86_400_000));
  protected branchId: number | null = null;
  protected agentUserId: number | null = null;

  constructor() {
    inject(TravelConfigService).getLookups().pipe(catchError(() => of({ branches: [], agents: [] })), takeUntilDestroyed(this.destroyRef))
      .subscribe(l => { this.branches.set(l.branches); this.agents.set(l.agents); });
    this.load$.pipe(
      debounceTime(250),
      tap(() => { this.loading.set(true); this.error.set(null); }),
      switchMap(() => !this.from || !this.to
        ? of(null)
        : this.api.getReport(this.dimension, this.from, this.to, this.branchId, this.agentUserId)
          .pipe(catchError(err => { this.error.set(apiErrorMessage(err, 'Could not calculate profitability.')); return of(null); }))),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(r => { this.report.set(r); this.loading.set(false); if (!this.from || !this.to) this.error.set('Choose both travel dates.'); });
    this.reload();
  }

  protected reload(): void { this.load$.next(); }
  protected grouped(r: TravelProfitabilityReport): boolean { return r.dimension === 'Agent' || r.dimension === 'Branch'; }
  protected pct(p: number | null): string { return marginPercentText(p); }
  protected tone(margin: number, percent: number | null): string { return marginTone(margin, percent); }
  protected open(c: TravelContextProfit): void { this.router.navigate([CONTEXT_ROUTES[c.contextType], c.contextId]); }
}
