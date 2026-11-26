import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterLink } from '@angular/router';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap, tap } from 'rxjs';
import { UiEmptyStateComponent, UiStatusBadgeComponent } from '../../../../shared/components/ui';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  INVOICE_PAYMENT_STATUS_LABEL, INVOICE_PAYMENT_STATUS_VARIANT, INVOICE_STATUS_VARIANT, PAYMENT_METHOD_LABEL, PAYMENT_STATUS_LABEL,
  PAYMENT_STATUS_VARIANT, SOURCE_TYPE_LABEL, TRAVEL_INVOICE_PAYMENT_STATUSES, TRAVEL_INVOICE_STATUSES, TRAVEL_PAYMENT_METHODS, TRAVEL_PAYMENT_STATUSES,
  TravelBillableItem, TravelChangedInvoiceLine, TravelCreditNote, TravelCustomerBalance, TravelInvoiceListItem, TravelInvoicePaymentStatus,
  TravelInvoiceStatus, TravelPaymentListItem, TravelPaymentMethod, TravelPaymentStatus, TravelRefund
} from '../../models/travel.models';
import { TravelFinanceService } from '../../services/travel-finance.service';

// ── Invoices ────────────────────────────────────────────────────────────

@Component({
  selector: 'app-finance-invoices-tab',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, FormsModule, MatButtonModule, MatIconModule, UiEmptyStateComponent, UiStatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-card">
      <div class="flex flex-col gap-3 border-b border-fleet-border px-5 py-4 lg:flex-row lg:flex-wrap lg:items-end">
        <input type="search" class="tn-input lg:w-64" placeholder="Invoice no., customer, booking…" aria-label="Search invoices"
          [ngModel]="search()" (ngModelChange)="search$.next($event)" />
        <select class="tn-input lg:w-36" aria-label="Invoice status" [ngModel]="status()" (ngModelChange)="status.set($event); reset()">
          <option value="">All statuses</option>@for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
        </select>
        <select class="tn-input lg:w-40" aria-label="Payment status" [ngModel]="paymentStatus()" (ngModelChange)="paymentStatus.set($event); reset()">
          <option value="">Any payment</option>@for (s of paymentStatuses; track s) { <option [value]="s">{{ payLabel[s] }}</option> }
        </select>
        <label class="tn-field lg:w-40"><span class="text-xs">Issued from</span><input type="date" class="tn-input" [ngModel]="from()" (ngModelChange)="from.set($event); reset()" /></label>
        <label class="tn-field lg:w-40"><span class="text-xs">Issued to</span><input type="date" class="tn-input" [ngModel]="to()" (ngModelChange)="to.set($event); reset()" /></label>
        <button type="button" class="tn-chip warn" [class.active]="overdue()" (click)="overdue.set(!overdue()); reset()"><mat-icon class="!text-[16px]">schedule</mat-icon> Overdue (as of today)</button>
      </div>
      @if (error()) { <div class="tn-card-body"><div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div></div> }
      <div class="overflow-x-auto">
        <table class="w-full min-w-[960px] text-left text-sm">
          <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
            <th class="px-4 py-3">Invoice</th><th class="px-4 py-3">Customer</th><th class="px-4 py-3">Issued / due</th><th class="px-4 py-3 text-right">Total</th>
            <th class="px-4 py-3 text-right">Balance</th><th class="px-4 py-3">Status</th><th class="px-4 py-3">For</th></tr></thead>
          <tbody class="divide-y divide-fleet-border">
            @if (loading()) { @for (s of [1,2,3]; track s) { <tr>@for (c of [1,2,3,4,5,6,7]; track c) { <td class="px-4 py-4"><div class="h-3.5 w-3/4 animate-pulse rounded bg-fleet-surface-muted"></div></td> }</tr> } }
            @else if (!rows().length) { <tr><td colspan="7"><ui-empty-state icon="receipt_long" title="No invoices" description="Create an invoice from a customer's bookings, visa cases or pilgrims."></ui-empty-state></td></tr> }
            @for (r of rows(); track r.id) {
              <tr class="cursor-pointer hover:bg-fleet-surface-alt/40" (click)="open(r)">
                <td class="px-4 py-3 font-mono text-xs">{{ r.invoiceNumber ?? 'Draft #' + r.id }}</td>
                <td class="px-4 py-3 font-semibold">{{ r.customerName }}</td>
                <td class="px-4 py-3 whitespace-nowrap">{{ r.issueDate ? (r.issueDate | date: 'mediumDate': 'UTC') : '—' }}
                  <span class="block text-xs" [class.text-red-600]="r.overdueAmount > 0">due {{ (r.nextDueDate ?? r.dueDate) ? ((r.nextDueDate ?? r.dueDate) | date: 'mediumDate': 'UTC') : '—' }}</span></td>
                <td class="px-4 py-3 text-right">{{ r.total | currency: r.currencyCode : 'code' : '1.2-2' }}</td>
                <td class="px-4 py-3 text-right font-semibold">{{ r.balance | currency: r.currencyCode : 'code' : '1.2-2' }}
                  @if (r.overdueAmount > 0) { <span class="block text-xs font-normal text-red-600">{{ r.overdueAmount | currency: r.currencyCode : 'code' : '1.2-2' }} overdue</span> }</td>
                <td class="px-4 py-3"><ui-status-badge [status]="r.status" [variant]="statusVariant[r.status]"></ui-status-badge>
                  @if (r.paymentStatus) { <ui-status-badge class="ml-1" [status]="r.paymentStatus" [label]="payLabel[r.paymentStatus]" [variant]="payVariant[r.paymentStatus]"></ui-status-badge> }</td>
                <td class="px-4 py-3 text-xs">{{ r.bookingNumber ?? r.visaCaseNumber ?? r.groupNumber ?? '—' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      @if (!loading() && totalPages() > 1) {
        <div class="flex items-center justify-between border-t border-fleet-border px-5 py-3 text-sm">
          <span class="text-fleet-text-muted">Page {{ page() }} of {{ totalPages() }}</span>
          <div class="flex gap-2"><button mat-stroked-button [disabled]="page() <= 1" (click)="goTo(page() - 1)">Previous</button>
            <button mat-stroked-button [disabled]="page() >= totalPages()" (click)="goTo(page() + 1)">Next</button></div>
        </div>
      }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class FinanceInvoicesTabComponent {
  private readonly api = inject(TravelFinanceService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly statuses = TRAVEL_INVOICE_STATUSES;
  protected readonly paymentStatuses = TRAVEL_INVOICE_PAYMENT_STATUSES;
  protected readonly statusVariant = INVOICE_STATUS_VARIANT;
  protected readonly payLabel = INVOICE_PAYMENT_STATUS_LABEL;
  protected readonly payVariant = INVOICE_PAYMENT_STATUS_VARIANT;
  protected readonly rows = signal<TravelInvoiceListItem[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  protected readonly search = signal('');
  protected readonly status = signal<TravelInvoiceStatus | ''>('');
  protected readonly paymentStatus = signal<TravelInvoicePaymentStatus | ''>('');
  protected readonly from = signal('');
  protected readonly to = signal('');
  protected readonly overdue = signal(false);
  protected readonly search$ = new Subject<string>();
  private readonly load$ = new Subject<void>();

  constructor() {
    this.load$.pipe(
      tap(() => { this.loading.set(true); this.error.set(null); }),
      switchMap(() => this.api.getInvoices({ page: this.page(), pageSize: 20, search: this.search(), status: this.status(), paymentStatus: this.paymentStatus(),
        issueFrom: this.from() || undefined, issueTo: this.to() || undefined, overdue: this.overdue() })
        .pipe(catchError(err => { this.error.set(apiErrorMessage(err, 'Could not load invoices.')); return of(null); }))),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(r => { if (r) { this.rows.set(r.items); this.totalPages.set(r.totalPages); } this.loading.set(false); });
    this.search$.pipe(debounceTime(350), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef)).subscribe(s => { this.search.set(s ?? ''); this.reset(); });
    this.reset();
  }
  protected reset(): void { this.page.set(1); this.load$.next(); }
  protected goTo(p: number): void { this.page.set(p); this.load$.next(); }
  protected open(r: TravelInvoiceListItem): void { this.router.navigate(['/travel/finance/invoices', r.id]); }
}

// ── Payments ────────────────────────────────────────────────────────────

@Component({
  selector: 'app-finance-payments-tab',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, FormsModule, MatButtonModule, MatIconModule, UiEmptyStateComponent, UiStatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-card">
      <div class="flex flex-col gap-3 border-b border-fleet-border px-5 py-4 lg:flex-row lg:flex-wrap lg:items-end">
        <input type="search" class="tn-input lg:w-64" placeholder="Receipt, reference, customer…" aria-label="Search payments"
          [ngModel]="search()" (ngModelChange)="search$.next($event)" />
        <select class="tn-input lg:w-36" aria-label="Payment status" [ngModel]="status()" (ngModelChange)="status.set($event); reset()">
          <option value="">All statuses</option>@for (s of statuses; track s) { <option [value]="s">{{ statusLabel[s] }}</option> }
        </select>
        <select class="tn-input lg:w-40" aria-label="Method" [ngModel]="method()" (ngModelChange)="method.set($event); reset()">
          <option value="">All methods</option>@for (m of methods; track m) { <option [value]="m">{{ methodLabel[m] }}</option> }
        </select>
        <label class="tn-field lg:w-40"><span class="text-xs">Received from</span><input type="date" class="tn-input" [ngModel]="from()" (ngModelChange)="from.set($event); reset()" /></label>
        <label class="tn-field lg:w-40"><span class="text-xs">Received to</span><input type="date" class="tn-input" [ngModel]="to()" (ngModelChange)="to.set($event); reset()" /></label>
      </div>
      @if (error()) { <div class="tn-card-body"><div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div></div> }
      <div class="overflow-x-auto">
        <table class="w-full min-w-[900px] text-left text-sm">
          <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
            <th class="px-4 py-3">Receipt</th><th class="px-4 py-3">Customer</th><th class="px-4 py-3">Received</th><th class="px-4 py-3">Method</th>
            <th class="px-4 py-3 text-right">Amount</th><th class="px-4 py-3 text-right">On account</th><th class="px-4 py-3">Status</th></tr></thead>
          <tbody class="divide-y divide-fleet-border">
            @if (loading()) { @for (s of [1,2,3]; track s) { <tr>@for (c of [1,2,3,4,5,6,7]; track c) { <td class="px-4 py-4"><div class="h-3.5 w-3/4 animate-pulse rounded bg-fleet-surface-muted"></div></td> }</tr> } }
            @else if (!rows().length) { <tr><td colspan="7"><ui-empty-state icon="payments" title="No payments" description="Recorded receipts appear here."></ui-empty-state></td></tr> }
            @for (r of rows(); track r.id) {
              <tr class="cursor-pointer hover:bg-fleet-surface-alt/40" (click)="open(r)">
                <td class="px-4 py-3 font-mono text-xs">{{ r.receiptNumber }}</td>
                <td class="px-4 py-3 font-semibold">{{ r.customerName }}</td>
                <td class="px-4 py-3">{{ r.receivedOn | date: 'mediumDate': 'UTC' }}</td>
                <td class="px-4 py-3">{{ methodLabel[r.method] }}{{ r.reference ? ' · ' + r.reference : '' }}</td>
                <td class="px-4 py-3 text-right">{{ r.amount | currency: r.currencyCode : 'code' : '1.2-2' }}</td>
                <td class="px-4 py-3 text-right">{{ r.unallocated | currency: r.currencyCode : 'code' : '1.2-2' }}</td>
                <td class="px-4 py-3"><ui-status-badge [status]="r.status" [label]="statusLabel[r.status]" [variant]="statusVariant[r.status]"></ui-status-badge>
                  @if (r.reconciledAt) { <mat-icon class="!text-[16px] align-middle text-emerald-600" title="Reconciled">verified</mat-icon> }</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      @if (!loading() && totalPages() > 1) {
        <div class="flex items-center justify-between border-t border-fleet-border px-5 py-3 text-sm">
          <span class="text-fleet-text-muted">Page {{ page() }} of {{ totalPages() }}</span>
          <div class="flex gap-2"><button mat-stroked-button [disabled]="page() <= 1" (click)="goTo(page() - 1)">Previous</button>
            <button mat-stroked-button [disabled]="page() >= totalPages()" (click)="goTo(page() + 1)">Next</button></div>
        </div>
      }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class FinancePaymentsTabComponent {
  private readonly api = inject(TravelFinanceService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly statuses = TRAVEL_PAYMENT_STATUSES;
  protected readonly methods = TRAVEL_PAYMENT_METHODS;
  protected readonly statusLabel = PAYMENT_STATUS_LABEL;
  protected readonly statusVariant = PAYMENT_STATUS_VARIANT;
  protected readonly methodLabel = PAYMENT_METHOD_LABEL;
  protected readonly rows = signal<TravelPaymentListItem[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  protected readonly search = signal('');
  protected readonly status = signal<TravelPaymentStatus | ''>('');
  protected readonly method = signal<TravelPaymentMethod | ''>('');
  protected readonly from = signal('');
  protected readonly to = signal('');
  protected readonly search$ = new Subject<string>();
  private readonly load$ = new Subject<void>();

  constructor() {
    this.load$.pipe(
      tap(() => { this.loading.set(true); this.error.set(null); }),
      switchMap(() => this.api.getPayments({ page: this.page(), pageSize: 20, search: this.search(), status: this.status(), method: this.method(),
        receivedFrom: this.from() || undefined, receivedTo: this.to() || undefined })
        .pipe(catchError(err => { this.error.set(apiErrorMessage(err, 'Could not load payments.')); return of(null); }))),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(r => { if (r) { this.rows.set(r.items); this.totalPages.set(r.totalPages); } this.loading.set(false); });
    this.search$.pipe(debounceTime(350), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef)).subscribe(s => { this.search.set(s ?? ''); this.reset(); });
    this.reset();
  }
  reload(): void { this.load$.next(); }
  protected reset(): void { this.page.set(1); this.load$.next(); }
  protected goTo(p: number): void { this.page.set(p); this.load$.next(); }
  protected open(r: TravelPaymentListItem): void { this.router.navigate(['/travel/finance/payments', r.id]); }
}

// ── Credit notes and refunds ────────────────────────────────────────────

@Component({
  selector: 'app-finance-adjustments-tab',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, RouterLink, MatIconModule, UiStatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (error()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div> }
    <div class="grid gap-5 lg:grid-cols-2">
      <div class="tn-card">
        <div class="border-b border-fleet-border px-5 py-4"><h3 class="tn-section-title">Credit notes</h3></div>
        @if (!creditNotes().length) { <p class="px-5 py-6 text-sm text-fleet-text-muted">No credit notes.</p> }
        <ul class="divide-y divide-fleet-border text-sm">
          @for (c of creditNotes(); track c.id) {
            <li class="flex flex-wrap items-center justify-between gap-2 px-5 py-3" [class.opacity-60]="c.status === 'Void'">
              <span><span class="font-mono text-xs">{{ c.creditNoteNumber }}</span> · {{ c.customerName }}
                <span class="block text-xs text-fleet-text-muted">{{ c.issuedOn | date: 'mediumDate': 'UTC' }} · on
                  <a class="text-fleet-primary underline" [routerLink]="['/travel/finance/invoices', c.invoiceId]">{{ c.invoiceNumber }}</a> · {{ c.reason }}</span></span>
              <span class="font-semibold">{{ c.amount | currency: c.currencyCode : 'code' : '1.2-2' }}</span>
              @if (c.status === 'Void') { <ui-status-badge status="Void" variant="inactive"></ui-status-badge> }
            </li>
          }
        </ul>
      </div>
      <div class="tn-card">
        <div class="border-b border-fleet-border px-5 py-4"><h3 class="tn-section-title">Refunds paid</h3></div>
        @if (!refunds().length) { <p class="px-5 py-6 text-sm text-fleet-text-muted">No refunds.</p> }
        <ul class="divide-y divide-fleet-border text-sm">
          @for (r of refunds(); track r.id) {
            <li class="flex flex-wrap items-center justify-between gap-2 px-5 py-3">
              <span><span class="font-mono text-xs">{{ r.refundNumber }}</span> · {{ r.customerName }}
                <span class="block text-xs text-fleet-text-muted">{{ r.paidOn | date: 'mediumDate': 'UTC' }} ·
                  @if (r.invoiceId) { <a class="text-fleet-primary underline" [routerLink]="['/travel/finance/invoices', r.invoiceId]">{{ r.invoiceNumber }}</a> }
                  @else { advance from <a class="text-fleet-primary underline" [routerLink]="['/travel/finance/payments', r.paymentId]">{{ r.receiptNumber }}</a> }</span></span>
              <span class="font-semibold">{{ r.amount | currency: r.currencyCode : 'code' : '1.2-2' }}</span>
            </li>
          }
        </ul>
      </div>
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class FinanceAdjustmentsTabComponent {
  private readonly api = inject(TravelFinanceService);
  protected readonly creditNotes = signal<TravelCreditNote[]>([]);
  protected readonly refunds = signal<TravelRefund[]>([]);
  protected readonly error = signal<string | null>(null);

  constructor() {
    const destroyRef = inject(DestroyRef);
    this.api.getCreditNotes(1, 50).pipe(takeUntilDestroyed(destroyRef)).subscribe({
      next: r => this.creditNotes.set(r.items), error: err => this.error.set(apiErrorMessage(err, 'Could not load credit notes.'))
    });
    this.api.getRefunds(1, 50).pipe(takeUntilDestroyed(destroyRef)).subscribe({
      next: r => this.refunds.set(r.items), error: err => this.error.set(apiErrorMessage(err, 'Could not load refunds.'))
    });
  }
}

// ── Balances ────────────────────────────────────────────────────────────

@Component({
  selector: 'app-finance-balances-tab',
  standalone: true,
  imports: [CurrencyPipe, RouterLink, MatIconModule, UiEmptyStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-card overflow-x-auto">
      @if (error()) { <div class="tn-card-body"><div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div></div> }
      <table class="w-full min-w-[900px] text-left text-sm">
        <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
          <th class="px-4 py-3">Customer</th><th class="px-4 py-3">Currency</th><th class="px-4 py-3 text-right">Invoiced</th>
          <th class="px-4 py-3 text-right">Balance</th><th class="px-4 py-3 text-right">Overdue (as of today)</th><th class="px-4 py-3 text-right">On account</th>
          <th class="px-4 py-3"></th></tr></thead>
        <tbody class="divide-y divide-fleet-border">
          @if (!rows().length && !error()) { <tr><td colspan="7"><ui-empty-state icon="account_balance_wallet" title="Nothing outstanding" description="No customer owes or is owed money."></ui-empty-state></td></tr> }
          @for (r of rows(); track r.customerId + r.currencyCode) {
            <tr>
              <td class="px-4 py-3 font-semibold"><a class="text-fleet-primary underline" [routerLink]="['/travel/customers', r.customerId]">{{ r.customerName }}</a></td>
              <td class="px-4 py-3 font-mono">{{ r.currencyCode }}</td>
              <td class="px-4 py-3 text-right">{{ r.invoiced | currency: r.currencyCode : 'code' : '1.2-2' }}</td>
              <td class="px-4 py-3 text-right font-semibold" [class.text-emerald-700]="r.balance < 0">{{ r.balance | currency: r.currencyCode : 'code' : '1.2-2' }}</td>
              <td class="px-4 py-3 text-right" [class.text-red-600]="r.overdue > 0">{{ r.overdue | currency: r.currencyCode : 'code' : '1.2-2' }}</td>
              <td class="px-4 py-3 text-right">{{ r.unallocatedAdvances | currency: r.currencyCode : 'code' : '1.2-2' }}</td>
              <td class="px-4 py-3 text-right"><a class="text-fleet-primary underline" [routerLink]="['/travel/finance/statements', r.customerId]" [queryParams]="{ currency: r.currencyCode }">Statement</a></td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class FinanceBalancesTabComponent {
  protected readonly rows = signal<TravelCustomerBalance[]>([]);
  protected readonly error = signal<string | null>(null);
  constructor() {
    inject(TravelFinanceService).getBalances().pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe({
      next: r => this.rows.set(r), error: err => this.error.set(apiErrorMessage(err, 'Could not load balances.'))
    });
  }
}

// ── Reconciliation ──────────────────────────────────────────────────────

@Component({
  selector: 'app-finance-reconciliation-tab',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, RouterLink, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (error()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div> }
    <div class="tn-card">
      <div class="border-b border-fleet-border px-5 py-4"><h3 class="tn-section-title">Payments to reconcile</h3>
        <p class="text-xs text-fleet-text-muted">Cleared non-cash payments not yet matched to a bank statement line.</p></div>
      @if (!unreconciled().length) { <p class="px-5 py-6 text-sm text-fleet-text-muted">Everything is reconciled.</p> }
      <ul class="divide-y divide-fleet-border text-sm">
        @for (p of unreconciled(); track p.id) {
          <li><a class="flex flex-wrap justify-between gap-2 px-5 py-3 hover:bg-fleet-surface-alt/40" [routerLink]="['/travel/finance/payments', p.id]">
            <span><span class="font-mono text-xs">{{ p.receiptNumber }}</span> · {{ p.customerName }} · {{ p.receivedOn | date: 'mediumDate': 'UTC' }}{{ p.reference ? ' · ' + p.reference : '' }}</span>
            <span class="font-semibold">{{ p.amount | currency: p.currencyCode : 'code' : '1.2-2' }}</span></a></li>
        }
      </ul>
    </div>
    <div class="tn-card">
      <div class="border-b border-fleet-border px-5 py-4"><h3 class="tn-section-title">Sold but not invoiced</h3>
        <p class="text-xs text-fleet-text-muted">Priced services, visa fees and pilgrims that are on no live invoice (newest 500).</p></div>
      @if (!uninvoiced().length) { <p class="px-5 py-6 text-sm text-fleet-text-muted">Everything sold is invoiced.</p> }
      <ul class="divide-y divide-fleet-border text-sm">
        @for (i of uninvoiced(); track i.sourceType + i.sourceId) {
          <li class="flex flex-wrap items-center justify-between gap-2 px-5 py-3">
            <span><strong>{{ i.customerName }}</strong> · {{ i.description }}<span class="block text-xs text-fleet-text-muted">{{ sourceLabel[i.sourceType] }} · {{ i.contextLabel }}</span></span>
            <span class="flex items-center gap-3"><span class="font-semibold">{{ i.amount | currency: i.currencyCode : 'code' : '1.2-2' }}</span>
              <a class="text-fleet-primary underline" [routerLink]="['/travel/finance/invoices/new']" [queryParams]="{ customerId: i.customerId, currency: i.currencyCode }">Invoice</a></span>
          </li>
        }
      </ul>
    </div>
    <div class="tn-card">
      <div class="border-b border-fleet-border px-5 py-4"><h3 class="tn-section-title">Changed since invoiced</h3>
        <p class="text-xs text-fleet-text-muted">Issued invoice lines whose service price changed or was cancelled — usually needs a credit note.</p></div>
      @if (!changed().length) { <p class="px-5 py-6 text-sm text-fleet-text-muted">No differences.</p> }
      <ul class="divide-y divide-fleet-border text-sm">
        @for (c of changed(); track $index) {
          <li><a class="flex flex-wrap justify-between gap-2 px-5 py-3 hover:bg-fleet-surface-alt/40" [routerLink]="['/travel/finance/invoices', c.invoiceId]">
            <span><span class="font-mono text-xs">{{ c.invoiceNumber }}</span> · {{ c.customerName }} · {{ c.description }}</span>
            <span>invoiced {{ c.invoiced | currency: c.currencyCode : 'code' : '1.2-2' }} → {{ c.current === null ? 'cancelled' : (c.current | currency: c.currencyCode : 'code' : '1.2-2') }}</span></a></li>
        }
      </ul>
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class FinanceReconciliationTabComponent {
  protected readonly sourceLabel = SOURCE_TYPE_LABEL;
  protected readonly unreconciled = signal<TravelPaymentListItem[]>([]);
  protected readonly uninvoiced = signal<TravelBillableItem[]>([]);
  protected readonly changed = signal<TravelChangedInvoiceLine[]>([]);
  protected readonly error = signal<string | null>(null);

  constructor() {
    const api = inject(TravelFinanceService);
    const destroyRef = inject(DestroyRef);
    const fail = (err: unknown) => this.error.set(apiErrorMessage(err, 'Could not load the reconciliation lists.'));
    api.getPayments({ page: 1, pageSize: 100, reconciled: false }).pipe(takeUntilDestroyed(destroyRef)).subscribe({ next: r => this.unreconciled.set(r.items), error: fail });
    api.getUninvoiced().pipe(takeUntilDestroyed(destroyRef)).subscribe({ next: r => this.uninvoiced.set(r), error: fail });
    api.getChangedLines().pipe(takeUntilDestroyed(destroyRef)).subscribe({ next: r => this.changed.set(r), error: fail });
  }
}
