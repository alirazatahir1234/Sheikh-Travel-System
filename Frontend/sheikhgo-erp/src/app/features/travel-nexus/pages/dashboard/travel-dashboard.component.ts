import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { ChartData } from 'chart.js';
import { UiChartComponent, UiEmptyStateComponent, UiPageHeaderComponent } from '../../../../shared/components/ui';
import { AuthService } from '../../../../core/services/auth.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  REMINDER_ENTITY_ROUTE, TRAVEL_BOOKING_STATUSES, TRAVEL_ENQUIRY_STATUSES, TRAVEL_GROUP_STATUSES, TRAVEL_PERMISSIONS, TRAVEL_VISA_CASE_STATUSES,
  TravelDashboard, TravelDashboardProfit, TravelPipeline, TravelUpcoming, TravelUpcomingItem
} from '../../models/travel.models';
import { TravelDashboardService } from '../../services/travel-dashboard.service';
import { pipelineConsistent } from '../reports/report-format';
import { marginPercentText, marginTone } from '../suppliers/supplier-finance';

interface AttentionCard { label: string; window: string; value: number; icon: string; link: string; query?: Record<string, string>; warn: boolean; }

@Component({
  selector: 'app-travel-dashboard',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, RouterLink, MatButtonModule, MatIconModule, UiPageHeaderComponent, UiChartComponent, UiEmptyStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      <ui-page-header title="Dashboard" eyebrow="TravelNexus" icon="dashboard"
        [subtitle]="data() ? 'As of ' + (data()!.asOf | date: 'medium') + ' · every figure names its window' : 'Loading…'"
        [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Dashboard' }]">
        <button mat-stroked-button (click)="load()"><mat-icon>refresh</mat-icon> Refresh</button>
      </ui-page-header>
      @if (error()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}<button mat-stroked-button class="ml-auto" (click)="load()">Retry</button></div> }

      @if (data(); as d) {
        <section aria-label="Needs attention" class="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          @for (c of cards(); track c.label) {
            <a class="tn-card tn-card-body block hover:bg-fleet-surface-alt/40" [routerLink]="c.link" [queryParams]="c.query ?? null">
              <div class="flex items-start justify-between">
                <div><p class="text-xs font-bold uppercase tracking-wider text-fleet-text-muted">{{ c.label }}</p>
                  <p class="text-2xl font-semibold" [class.text-red-600]="c.warn && c.value > 0">{{ c.value }}</p>
                  <p class="text-xs text-fleet-text-soft">{{ c.window }}</p></div>
                <mat-icon class="text-fleet-text-muted">{{ c.icon }}</mat-icon>
              </div>
            </a>
          }
        </section>
        @if (d.attention.overdueInvoiceAmounts.length) {
          <p class="text-xs text-fleet-text-muted">Overdue receivables · as of today:
            @for (a of d.attention.overdueInvoiceAmounts; track a.currencyCode) { <strong class="ml-1">{{ a.amount | currency: a.currencyCode : 'code' : '1.0-0' }}</strong> }</p>
        }

        <section aria-label="Pipelines" class="grid gap-4 lg:grid-cols-2">
          @for (p of pipelines(); track p.title) {
            <div class="tn-card tn-card-body">
              <div class="mb-2 flex items-center justify-between"><h3 class="tn-section-title">{{ p.title }}</h3>
                <span class="text-xs text-fleet-text-soft">all time · total {{ p.consistent ? p.pipeline.total : 'unavailable' }}</span></div>
              <div class="tn-pipeline">
                @for (s of p.statuses; track s) {
                  <a [routerLink]="p.link" [queryParams]="{ status: s }"><button type="button"><small>{{ s }}</small><strong>{{ p.pipeline.counts[s] }}</strong></button></a>
                }
              </div>
            </div>
          }
        </section>

        <section aria-label="Money" class="grid gap-4 lg:grid-cols-2">
          <div class="tn-card tn-card-body">
            <h3 class="tn-section-title">Receivables</h3>
            @if (!d.receivables.length) { <p class="mt-2 text-sm text-fleet-text-muted">No issued invoices yet.</p> }
            @for (c of d.receivables; track c.currencyCode) {
              <dl class="mt-2 grid grid-cols-3 gap-2 text-sm"><dt class="col-span-3 font-mono text-xs text-fleet-text-muted">{{ c.currencyCode }}</dt>
                <div><dt class="text-xs text-fleet-text-muted">Outstanding · all time</dt><dd class="font-semibold">{{ c.outstanding | currency: c.currencyCode : 'code' : '1.0-0' }}</dd></div>
                <div><dt class="text-xs text-fleet-text-muted">Overdue · as of today</dt><dd class="font-semibold" [class.text-red-600]="c.overdue > 0">{{ c.overdue | currency: c.currencyCode : 'code' : '1.0-0' }}</dd></div>
                <div><dt class="text-xs text-fleet-text-muted">Collected · this month</dt><dd class="font-semibold">{{ c.collectedThisMonth | currency: c.currencyCode : 'code' : '1.0-0' }}</dd></div></dl>
            }
            <a class="mt-2 inline-block text-sm text-fleet-primary underline" routerLink="/travel/finance">Invoices & Payments</a>
          </div>
          <div class="tn-card tn-card-body">
            <h3 class="tn-section-title">Payables</h3>
            @if (!d.payables.length) { <p class="mt-2 text-sm text-fleet-text-muted">No posted supplier bills yet.</p> }
            @for (c of d.payables; track c.currencyCode) {
              <dl class="mt-2 grid grid-cols-3 gap-2 text-sm"><dt class="col-span-3 font-mono text-xs text-fleet-text-muted">{{ c.currencyCode }}</dt>
                <div><dt class="text-xs text-fleet-text-muted">We owe · all time</dt><dd class="font-semibold">{{ c.outstanding | currency: c.currencyCode : 'code' : '1.0-0' }}</dd></div>
                <div><dt class="text-xs text-fleet-text-muted">Overdue · as of today</dt><dd class="font-semibold" [class.text-red-600]="c.overdue > 0">{{ c.overdue | currency: c.currencyCode : 'code' : '1.0-0' }}</dd></div>
                <div><dt class="text-xs text-fleet-text-muted">Paid · this month</dt><dd class="font-semibold">{{ c.paidThisMonth | currency: c.currencyCode : 'code' : '1.0-0' }}</dd></div></dl>
            }
            <a class="mt-2 inline-block text-sm text-fleet-primary underline" routerLink="/travel/suppliers">Suppliers & payables</a>
          </div>
        </section>

        @if (canProfit) {
          <section class="tn-card tn-card-body" aria-label="Margin">
            <div class="flex items-center justify-between"><h3 class="tn-section-title">Margin · travel this month vs last month</h3>
              <a class="text-sm text-fleet-primary underline" routerLink="/travel/profitability">Profitability</a></div>
            @if (profitError()) { <p class="mt-2 text-sm text-fleet-text-muted">{{ profitError() }}</p> }
            @else if (profit(); as p) {
              <div class="mt-2 grid gap-4 sm:grid-cols-2">
                @for (m of [p.thisMonth, p.lastMonth]; track m.label) {
                  <div><p class="text-xs text-fleet-text-muted">{{ m.label }} · {{ m.contexts }} item(s)</p>
                    <p class="text-xl font-semibold" [class]="tone(m.margin, m.marginPercent)">{{ m.margin | currency: p.reportingCurrencyCode : 'code' : '1.0-0' }} · {{ pct(m.marginPercent) }}</p>
                    <p class="text-xs text-fleet-text-soft">revenue {{ m.revenue | currency: p.reportingCurrencyCode : 'code' : '1.0-0' }} · cost {{ m.cost | currency: p.reportingCurrencyCode : 'code' : '1.0-0' }}
                      @if (m.estimatedCost) { ({{ m.estimatedCost | currency: p.reportingCurrencyCode : 'code' : '1.0-0' }} estimated) }</p></div>
                }
              </div>
              @if (p.missingRates.length) { <p class="mt-2 text-xs text-amber-700">{{ p.missingRates.length }} currency(ies) without an exchange rate are left out.</p> }
            } @else { <p class="mt-2 text-sm text-fleet-text-muted">Calculating…</p> }
          </section>
        }

        <section aria-label="Trends" class="grid gap-4 lg:grid-cols-3">
          <div class="tn-card tn-card-body"><h3 class="tn-section-title">Enquiries · last {{ d.trends.enquiryWeeks }} weeks</h3>
            <ui-chart type="bar" [data]="enquiryChart()" height="220px" /></div>
          <div class="tn-card tn-card-body"><h3 class="tn-section-title">Invoiced vs collected · last {{ d.trends.months }} months{{ invoicingCurrency() ? ' · ' + invoicingCurrency() : '' }}</h3>
            @if (invoicingCurrencies().length > 1) { <p class="text-xs text-fleet-text-muted">Showing {{ invoicingCurrency() }}; other currencies: {{ otherCurrencies() }}.</p> }
            <ui-chart type="line" [data]="invoicingChart()" height="220px" /></div>
          <div class="tn-card tn-card-body"><h3 class="tn-section-title">Visa decisions · last {{ d.trends.months }} months</h3>
            <ui-chart type="bar" [data]="decisionChart()" height="220px" /></div>
        </section>

        <section class="tn-card" aria-label="Upcoming deadlines">
          <div class="flex items-center justify-between border-b border-fleet-border px-5 py-4">
            <div><h3 class="tn-section-title">Upcoming · next {{ upcomingDays }} days</h3><p class="text-xs text-fleet-text-muted">The same deadlines the reminders watch.</p></div>
            <a class="text-sm text-fleet-primary underline" routerLink="/travel/reminders">Reminder rules</a>
          </div>
          @if (!upcoming()) { <p class="px-5 py-6 text-sm text-fleet-text-muted">Loading…</p> }
          @else if (!upcoming()!.items.length) { <ui-empty-state icon="event_available" title="Nothing due" [description]="'No deadlines in the next ' + upcomingDays + ' days.'"></ui-empty-state> }
          @else {
            <div class="overflow-x-auto"><table class="w-full min-w-[720px] text-left text-sm">
              <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                <th class="px-4 py-2">Due</th><th class="px-4 py-2">What</th><th class="px-4 py-2">Customer</th><th class="px-4 py-2 text-right">Amount</th></tr></thead>
              <tbody class="divide-y divide-fleet-border">
                @for (u of upcoming()!.items; track u.ruleType + u.entityType + u.entityId) {
                  <tr class="cursor-pointer hover:bg-fleet-surface-alt/40" [routerLink]="link(u)">
                    <td class="px-4 py-2 whitespace-nowrap">{{ u.dueAt | date: 'd MMM, HH:mm' }}</td>
                    <td class="px-4 py-2"><span class="font-semibold">{{ u.label }}</span><span class="block text-xs text-fleet-text-muted">{{ ruleLabel(u.ruleType) }}</span></td>
                    <td class="px-4 py-2">{{ u.customerName || '—' }}</td>
                    <td class="px-4 py-2 text-right">{{ u.amount !== null && u.currencyCode ? (u.amount | currency: u.currencyCode : 'code' : '1.2-2') : '' }}</td>
                  </tr>
                }
              </tbody></table></div>
          }
        </section>
      } @else if (!error()) {
        <div class="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">@for (s of [1,2,3,4]; track s) { <div class="tn-card h-24 animate-pulse"></div> }</div>
      }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelDashboardComponent {
  private readonly api = inject(TravelDashboardService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly canProfit = inject(AuthService).hasAnyPermission([TRAVEL_PERMISSIONS.profitabilityView]);
  protected readonly upcomingDays = 14;
  protected readonly data = signal<TravelDashboard | null>(null);
  protected readonly upcoming = signal<TravelUpcoming | null>(null);
  protected readonly profit = signal<TravelDashboardProfit | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly profitError = signal<string | null>(null);

  protected readonly cards = computed<AttentionCard[]>(() => {
    const a = this.data()?.attention;
    if (!a) return [];
    const cards: AttentionCard[] = [
      { label: 'Follow-ups due', window: 'today or missed', value: a.followUpsDue, icon: 'contact_phone', link: '/travel/enquiries', query: { followUpDue: 'true' }, warn: true },
      { label: 'Missing documents', window: 'open cases · now', value: a.missingDocumentCases, icon: 'description', link: '/travel/visa-cases', query: { missingDocs: 'true' }, warn: true },
      { label: 'Ticketing due', window: 'next 72 h', value: a.ticketingDueNext72Hours, icon: 'airplane_ticket', link: '/travel/bookings', query: { ticketingDue: 'true' }, warn: true },
      { label: 'Passports expiring', window: `next ${a.passportWarningDays} days`, value: a.passportsExpiring, icon: 'badge', link: '/travel/customers', warn: false },
      { label: 'Visa appointments', window: 'next 7 days', value: a.appointmentsNext7Days, icon: 'event', link: '/travel/visa-cases', warn: false },
      { label: 'Departures', window: 'next 7 days · flights + groups', value: a.departuresNext7Days, icon: 'flight_takeoff', link: '/travel/bookings', warn: false },
      { label: 'Overdue invoices', window: 'as of today', value: a.overdueInvoices, icon: 'receipt_long', link: '/travel/finance', query: { overdue: 'true' }, warn: true },
      { label: 'Overdue supplier bills', window: 'as of today', value: a.overdueSupplierBills, icon: 'request_quote', link: '/travel/suppliers', query: { overdue: 'true' }, warn: true },
      { label: 'Reminders failed', window: 'last 7 days', value: a.remindersFailedLast7Days, icon: 'notification_important', link: '/travel/reminders', query: { status: 'Failed' }, warn: true },
      { label: 'No agent to remind', window: 'last 7 days', value: a.remindersSkippedNoAgentLast7Days, icon: 'person_off', link: '/travel/reminders', query: { status: 'Skipped' }, warn: false }
    ];
    return cards;
  });

  protected readonly pipelines = computed(() => {
    const d = this.data();
    if (!d) return [];
    const make = (title: string, pipeline: TravelPipeline, statuses: readonly string[], link: string) => ({ title, pipeline, statuses, link, consistent: pipelineConsistent(pipeline) });
    return [
      make('Enquiries', d.enquiries, TRAVEL_ENQUIRY_STATUSES, '/travel/enquiries'),
      make('Visa cases', d.visaCases, TRAVEL_VISA_CASE_STATUSES, '/travel/visa-cases'),
      make('Umrah groups', d.groups, TRAVEL_GROUP_STATUSES, '/travel/groups'),
      make('Booking files', d.bookings, TRAVEL_BOOKING_STATUSES, '/travel/bookings')
    ];
  });

  protected readonly enquiryChart = computed<ChartData>(() => {
    const t = this.data()?.trends.enquiries ?? [];
    return { labels: t.map(p => p.label.substring(5)), datasets: [
      { label: 'Enquiries', data: t.map(p => p.total), backgroundColor: '#0f766e' },
      { label: 'Won', data: t.map(p => p.won), backgroundColor: '#10b981' }] };
  });
  protected readonly invoicingCurrencies = computed(() => [...new Set((this.data()?.trends.invoicing ?? []).map(p => p.currencyCode))].sort());
  protected readonly invoicingCurrency = computed(() => this.invoicingCurrencies()[0] ?? null);
  protected readonly otherCurrencies = computed(() => this.invoicingCurrencies().slice(1).join(', '));
  protected readonly invoicingChart = computed<ChartData>(() => {
    const currency = this.invoicingCurrency();
    const t = (this.data()?.trends.invoicing ?? []).filter(p => p.currencyCode === currency);
    return { labels: t.map(p => p.label), datasets: [
      { label: 'Invoiced', data: t.map(p => p.issued), borderColor: '#0f766e', backgroundColor: '#0f766e' },
      { label: 'Collected', data: t.map(p => p.collected), borderColor: '#10b981', backgroundColor: '#10b981' }] };
  });
  protected readonly decisionChart = computed<ChartData>(() => {
    const t = this.data()?.trends.visaDecisions ?? [];
    return { labels: t.map(p => p.label), datasets: [
      { label: 'Approved', data: t.map(p => p.approved), backgroundColor: '#10b981' },
      { label: 'Rejected', data: t.map(p => p.rejected), backgroundColor: '#dc2626' }] };
  });

  constructor() { this.load(); }

  protected load(): void {
    this.error.set(null);
    this.api.getDashboard().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: d => this.data.set(d),
      error: err => this.error.set(apiErrorMessage(err, 'Could not load the dashboard.'))
    });
    this.api.getUpcoming(this.upcomingDays).pipe(catchError(() => of({ days: this.upcomingDays, items: [] } as TravelUpcoming)), takeUntilDestroyed(this.destroyRef))
      .subscribe(u => this.upcoming.set(u));
    if (this.canProfit)
      this.api.getProfit().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: p => this.profit.set(p),
        error: err => this.profitError.set(apiErrorMessage(err, 'Profitability is not available.'))
      });
  }

  protected link(u: TravelUpcomingItem): string[] {
    // Flight and hotel reminders point at the service; the file page is where they live.
    return u.entityType === 'FlightBooking' || u.entityType === 'HotelBooking' ? ['/travel/bookings'] : [REMINDER_ENTITY_ROUTE[u.entityType], String(u.entityId)];
  }
  protected ruleLabel(ruleType: string): string { return ruleType.replace(/([a-z])([A-Z])/g, '$1 $2'); }
  protected pct(p: number | null): string { return marginPercentText(p); }
  protected tone(margin: number, percent: number | null): string { return marginTone(margin, percent); }
}
