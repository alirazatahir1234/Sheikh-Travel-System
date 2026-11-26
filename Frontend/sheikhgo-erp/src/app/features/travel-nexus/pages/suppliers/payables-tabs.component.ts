import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subject, catchError, debounceTime, distinctUntilChanged, forkJoin, of, switchMap, tap } from 'rxjs';
import { UiEmptyStateComponent, UiStatusBadgeComponent } from '../../../../shared/components/ui';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  INVOICE_PAYMENT_STATUS_VARIANT, PAYMENT_METHOD_LABEL, PAYMENT_STATUS_LABEL, PAYMENT_STATUS_VARIANT, SUPPLIER_BILL_PAYMENT_LABEL,
  SUPPLIER_BILL_STATUS_VARIANT, TRAVEL_PAYMENT_STATUSES, TRAVEL_SUPPLIER_BILL_STATUSES, TravelPayablesSummary, TravelPaymentStatus, TravelSupplierBalance,
  TravelSupplierBillListItem, TravelSupplierBillStatus, TravelSupplierPaymentListItem
} from '../../models/travel.models';
import { TravelSupplierFinanceService } from '../../services/travel-supplier-finance.service';

const SKELETON_ROWS = [1, 2, 3];

// ── Bills ───────────────────────────────────────────────────────────────

@Component({
  selector: 'app-payables-bills-tab',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, FormsModule, MatButtonModule, MatIconModule, UiEmptyStateComponent, UiStatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-card">
      <div class="flex flex-col gap-3 border-b border-fleet-border px-5 py-4 lg:flex-row lg:flex-wrap lg:items-end">
        <input type="search" class="tn-input lg:w-64" placeholder="Bill no., their reference, supplier…" aria-label="Search bills"
          [ngModel]="search()" (ngModelChange)="search$.next($event)" />
        <select class="tn-input lg:w-36" aria-label="Bill status" [ngModel]="status()" (ngModelChange)="status.set($event); reset()">
          <option value="">All statuses</option>@for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
        </select>
        <label class="tn-field lg:w-40"><span class="text-xs">Bill date from</span><input type="date" class="tn-input" [ngModel]="from()" (ngModelChange)="from.set($event); reset()" /></label>
        <label class="tn-field lg:w-40"><span class="text-xs">Bill date to</span><input type="date" class="tn-input" [ngModel]="to()" (ngModelChange)="to.set($event); reset()" /></label>
        <button type="button" class="tn-chip warn" [class.active]="overdue()" (click)="overdue.set(!overdue()); reset()"><mat-icon class="!text-[16px]">schedule</mat-icon> Overdue (as of today)</button>
      </div>
      @if (error()) { <div class="tn-card-body"><div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div></div> }
      <div class="overflow-x-auto">
        <table class="w-full min-w-[960px] text-left text-sm">
          <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
            <th class="px-4 py-3">Bill</th><th class="px-4 py-3">Supplier</th><th class="px-4 py-3">Bill date / due</th><th class="px-4 py-3 text-right">Gross</th>
            <th class="px-4 py-3 text-right">Commission</th><th class="px-4 py-3 text-right">Net</th><th class="px-4 py-3 text-right">Balance</th><th class="px-4 py-3">Status</th></tr></thead>
          <tbody class="divide-y divide-fleet-border">
            @if (loading()) { @for (s of skeleton; track s) { <tr>@for (c of [1,2,3,4,5,6,7,8]; track c) { <td class="px-4 py-4"><div class="h-3.5 w-3/4 animate-pulse rounded bg-fleet-surface-muted"></div></td> }</tr> } }
            @else if (!rows().length) { <tr><td colspan="8"><ui-empty-state icon="request_quote" title="No supplier bills" description="Enter a supplier's invoice from the services you booked with them."></ui-empty-state></td></tr> }
            @for (r of rows(); track r.id) {
              <tr class="cursor-pointer hover:bg-fleet-surface-alt/40" (click)="openBill(r)">
                <td class="px-4 py-3"><span class="font-mono text-xs">{{ r.billNumber ?? 'Draft #' + r.id }}</span>
                  @if (r.supplierReference) { <span class="block text-xs text-fleet-text-muted">their ref {{ r.supplierReference }}</span> }</td>
                <td class="px-4 py-3 font-semibold">{{ r.supplierName }}</td>
                <td class="px-4 py-3 whitespace-nowrap">{{ r.billDate ? (r.billDate | date: 'mediumDate': 'UTC') : '—' }}
                  <span class="block text-xs" [class.text-red-600]="r.overdue">due {{ r.dueDate ? (r.dueDate | date: 'mediumDate': 'UTC') : '—' }}</span></td>
                <td class="px-4 py-3 text-right">{{ r.gross | currency: r.currencyCode : 'code' : '1.2-2' }}</td>
                <td class="px-4 py-3 text-right">{{ r.commissionTotal | currency: r.currencyCode : 'code' : '1.2-2' }}</td>
                <td class="px-4 py-3 text-right">{{ r.total | currency: r.currencyCode : 'code' : '1.2-2' }}</td>
                <td class="px-4 py-3 text-right font-semibold">{{ r.status === 'Posted' ? (r.balance | currency: r.currencyCode : 'code' : '1.2-2') : '—' }}</td>
                <td class="px-4 py-3"><ui-status-badge [status]="r.status" [variant]="statusVariant[r.status]"></ui-status-badge>
                  @if (r.paymentStatus) { <ui-status-badge class="ml-1" [status]="r.paymentStatus" [label]="payLabel[r.paymentStatus]" [variant]="payVariant[r.paymentStatus]"></ui-status-badge> }</td>
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
export class PayablesBillsTabComponent {
  private readonly api = inject(TravelSupplierFinanceService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  /** Limits the list to one supplier (supplier detail page). */
  readonly supplierId = input<number | null>(null);
  protected readonly skeleton = SKELETON_ROWS;
  protected readonly statuses = TRAVEL_SUPPLIER_BILL_STATUSES;
  protected readonly statusVariant = SUPPLIER_BILL_STATUS_VARIANT;
  protected readonly payLabel = SUPPLIER_BILL_PAYMENT_LABEL;
  protected readonly payVariant = INVOICE_PAYMENT_STATUS_VARIANT;
  protected readonly rows = signal<TravelSupplierBillListItem[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  protected readonly search = signal('');
  protected readonly status = signal<TravelSupplierBillStatus | ''>('');
  protected readonly from = signal('');
  protected readonly to = signal('');
  /** `?overdue=true` opens the list already filtered (dashboard deep link). */
  protected readonly overdue = signal(inject(ActivatedRoute).snapshot.queryParamMap.get('overdue') === 'true');
  protected readonly search$ = new Subject<string>();
  private readonly load$ = new Subject<void>();

  constructor() {
    this.load$.pipe(
      tap(() => { this.loading.set(true); this.error.set(null); }),
      switchMap(() => this.api.getBills({ page: this.page(), pageSize: 20, search: this.search(), status: this.status(), supplierId: this.supplierId(),
        billFrom: this.from() || undefined, billTo: this.to() || undefined, overdue: this.overdue() })
        .pipe(catchError(err => { this.error.set(apiErrorMessage(err, 'Could not load bills.')); return of(null); }))),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(r => { if (r) { this.rows.set(r.items); this.totalPages.set(r.totalPages); } this.loading.set(false); });
    this.search$.pipe(debounceTime(350), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef)).subscribe(s => { this.search.set(s ?? ''); this.reset(); });
    effect(() => { this.supplierId(); untracked(() => this.reset()); });
  }
  protected reset(): void { this.page.set(1); this.load$.next(); }
  protected goTo(p: number): void { this.page.set(p); this.load$.next(); }
  protected openBill(r: TravelSupplierBillListItem): void { this.router.navigate(['/travel/suppliers/bills', r.id]); }
}

// ── Supplier payments ───────────────────────────────────────────────────

@Component({
  selector: 'app-payables-payments-tab',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, FormsModule, MatButtonModule, MatIconModule, UiEmptyStateComponent, UiStatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-card">
      <div class="flex flex-col gap-3 border-b border-fleet-border px-5 py-4 lg:flex-row lg:flex-wrap lg:items-end">
        <input type="search" class="tn-input lg:w-64" placeholder="Payment no., reference, supplier…" aria-label="Search supplier payments"
          [ngModel]="search()" (ngModelChange)="search$.next($event)" />
        <select class="tn-input lg:w-36" aria-label="Payment status" [ngModel]="status()" (ngModelChange)="status.set($event); reset()">
          <option value="">All statuses</option>@for (s of statuses; track s) { <option [value]="s">{{ statusLabel[s] }}</option> }
        </select>
        <label class="tn-field lg:w-40"><span class="text-xs">Paid from</span><input type="date" class="tn-input" [ngModel]="from()" (ngModelChange)="from.set($event); reset()" /></label>
        <label class="tn-field lg:w-40"><span class="text-xs">Paid to</span><input type="date" class="tn-input" [ngModel]="to()" (ngModelChange)="to.set($event); reset()" /></label>
      </div>
      @if (error()) { <div class="tn-card-body"><div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div></div> }
      <div class="overflow-x-auto">
        <table class="w-full min-w-[900px] text-left text-sm">
          <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
            <th class="px-4 py-3">Payment</th><th class="px-4 py-3">Supplier</th><th class="px-4 py-3">Paid on</th><th class="px-4 py-3">Method</th>
            <th class="px-4 py-3 text-right">Amount</th><th class="px-4 py-3 text-right">Unallocated</th><th class="px-4 py-3">Status</th></tr></thead>
          <tbody class="divide-y divide-fleet-border">
            @if (loading()) { @for (s of skeleton; track s) { <tr>@for (c of [1,2,3,4,5,6,7]; track c) { <td class="px-4 py-4"><div class="h-3.5 w-3/4 animate-pulse rounded bg-fleet-surface-muted"></div></td> }</tr> } }
            @else if (!rows().length) { <tr><td colspan="7"><ui-empty-state icon="payments" title="No supplier payments" description="Payments you make to suppliers appear here."></ui-empty-state></td></tr> }
            @for (r of rows(); track r.id) {
              <tr class="cursor-pointer hover:bg-fleet-surface-alt/40" (click)="openPayment(r)">
                <td class="px-4 py-3 font-mono text-xs">{{ r.paymentNumber }}</td>
                <td class="px-4 py-3 font-semibold">{{ r.supplierName }}</td>
                <td class="px-4 py-3 whitespace-nowrap">{{ r.paidOn | date: 'mediumDate': 'UTC' }}</td>
                <td class="px-4 py-3">{{ methodLabel[r.method] }}@if (r.reference) { <span class="block text-xs text-fleet-text-muted">{{ r.reference }}</span> }</td>
                <td class="px-4 py-3 text-right">{{ r.amount | currency: r.currencyCode : 'code' : '1.2-2' }}</td>
                <td class="px-4 py-3 text-right">{{ r.unallocated | currency: r.currencyCode : 'code' : '1.2-2' }}</td>
                <td class="px-4 py-3"><ui-status-badge [status]="r.status" [label]="statusLabel[r.status]" [variant]="statusVariant[r.status]"></ui-status-badge>
                  @if (r.reconciledAt) { <span class="ml-1 text-xs text-emerald-700">reconciled</span> }</td>
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
export class PayablesPaymentsTabComponent {
  private readonly api = inject(TravelSupplierFinanceService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  readonly supplierId = input<number | null>(null);
  protected readonly skeleton = SKELETON_ROWS;
  protected readonly statuses = TRAVEL_PAYMENT_STATUSES;
  protected readonly statusLabel = PAYMENT_STATUS_LABEL;
  protected readonly statusVariant = PAYMENT_STATUS_VARIANT;
  protected readonly methodLabel = PAYMENT_METHOD_LABEL;
  protected readonly rows = signal<TravelSupplierPaymentListItem[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  protected readonly search = signal('');
  protected readonly status = signal<TravelPaymentStatus | ''>('');
  protected readonly from = signal('');
  protected readonly to = signal('');
  protected readonly search$ = new Subject<string>();
  private readonly load$ = new Subject<void>();

  constructor() {
    this.load$.pipe(
      tap(() => { this.loading.set(true); this.error.set(null); }),
      switchMap(() => this.api.getPayments({ page: this.page(), pageSize: 20, search: this.search(), status: this.status(), supplierId: this.supplierId(),
        paidFrom: this.from() || undefined, paidTo: this.to() || undefined })
        .pipe(catchError(err => { this.error.set(apiErrorMessage(err, 'Could not load supplier payments.')); return of(null); }))),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(r => { if (r) { this.rows.set(r.items); this.totalPages.set(r.totalPages); } this.loading.set(false); });
    this.search$.pipe(debounceTime(350), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef)).subscribe(s => { this.search.set(s ?? ''); this.reset(); });
    effect(() => { this.supplierId(); untracked(() => this.reset()); });
  }
  protected reset(): void { this.page.set(1); this.load$.next(); }
  protected goTo(p: number): void { this.page.set(p); this.load$.next(); }
  protected openPayment(r: TravelSupplierPaymentListItem): void { this.router.navigate(['/travel/suppliers/payments', r.id]); }
}

// ── Payables: summary and balances ──────────────────────────────────────

@Component({
  selector: 'app-payables-balances-tab',
  standalone: true,
  imports: [CurrencyPipe, RouterLink, MatButtonModule, MatIconModule, UiEmptyStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (error()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}<button mat-stroked-button class="ml-auto" (click)="load()">Retry</button></div> }
    <section aria-label="Payables per currency" class="space-y-2">
      @if (!loading() && !summary()?.currencies?.length) { <p class="text-sm text-fleet-text-muted">No posted bills or supplier payments yet.</p> }
      <div class="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        @for (c of summary()?.currencies ?? []; track c.currencyCode) {
          <div class="tn-card tn-card-body">
            <p class="text-xs font-bold uppercase tracking-wider text-fleet-text-muted">{{ c.currencyCode }}</p>
            <dl class="mt-2 grid grid-cols-2 gap-x-4 gap-y-2 text-sm">
              <div><dt class="text-xs text-fleet-text-muted">We owe · all time</dt><dd class="font-semibold">{{ c.outstanding | currency: c.currencyCode : 'code' : '1.0-0' }}</dd></div>
              <div><dt class="text-xs text-fleet-text-muted">Overdue · as of today</dt><dd class="font-semibold" [class.text-red-600]="c.overdue > 0">{{ c.overdue | currency: c.currencyCode : 'code' : '1.0-0' }}</dd></div>
              <div><dt class="text-xs text-fleet-text-muted">Paid · this month</dt><dd class="font-semibold">{{ c.paidThisMonth | currency: c.currencyCode : 'code' : '1.0-0' }}</dd></div>
              <div><dt class="text-xs text-fleet-text-muted">Advances · all time</dt><dd class="font-semibold">{{ c.unallocatedAdvances | currency: c.currencyCode : 'code' : '1.0-0' }}</dd></div>
              @if (c.supplierCreditDue > 0) { <div class="col-span-2"><dt class="text-xs text-fleet-text-muted">Suppliers owe us · all time</dt><dd class="font-semibold text-emerald-700">{{ c.supplierCreditDue | currency: c.currencyCode : 'code' : '1.0-0' }}</dd></div> }
            </dl>
          </div>
        }
      </div>
      @if (summary(); as s) {
        <p class="text-xs text-fleet-text-soft">Bills (all time): {{ s.billCounts.Draft }} draft · {{ s.billCounts.Posted }} posted · {{ s.billCounts.Void }} void · total {{ s.billTotal }}</p>
      }
    </section>

    <div class="tn-card mt-4">
      <div class="border-b border-fleet-border px-5 py-4"><h3 class="tn-section-title">Supplier balances</h3>
        <p class="text-xs text-fleet-text-muted">Per supplier and currency, all time. Negative = the supplier owes you.</p></div>
      <div class="overflow-x-auto">
        <table class="w-full min-w-[860px] text-left text-sm">
          <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
            <th class="px-4 py-3">Supplier</th><th class="px-4 py-3 text-right">Open bills</th><th class="px-4 py-3 text-right">Billed</th><th class="px-4 py-3 text-right">Paid</th>
            <th class="px-4 py-3 text-right">Credited</th><th class="px-4 py-3 text-right">Balance</th><th class="px-4 py-3 text-right">Overdue</th><th class="px-4 py-3 text-right">Advances</th></tr></thead>
          <tbody class="divide-y divide-fleet-border">
            @if (!loading() && !balances().length) { <tr><td colspan="8"><ui-empty-state icon="account_balance" title="Nothing owed" description="Posted bills with a balance appear here."></ui-empty-state></td></tr> }
            @for (b of balances(); track b.supplierId + b.currencyCode) {
              <tr>
                <td class="px-4 py-3"><a class="font-semibold text-fleet-primary hover:underline" [routerLink]="['/travel/suppliers', b.supplierId]">{{ b.supplierName }}</a>
                  <span class="ml-1 font-mono text-xs text-fleet-text-muted">{{ b.currencyCode }}</span></td>
                <td class="px-4 py-3 text-right">{{ b.openBills }}</td>
                <td class="px-4 py-3 text-right">{{ b.billed | currency: b.currencyCode : 'code' : '1.2-2' }}</td>
                <td class="px-4 py-3 text-right">{{ b.paid | currency: b.currencyCode : 'code' : '1.2-2' }}</td>
                <td class="px-4 py-3 text-right">{{ b.credited | currency: b.currencyCode : 'code' : '1.2-2' }}</td>
                <td class="px-4 py-3 text-right font-semibold" [class.text-emerald-700]="b.balance < 0">{{ b.balance | currency: b.currencyCode : 'code' : '1.2-2' }}</td>
                <td class="px-4 py-3 text-right" [class.text-red-600]="b.overdue > 0">{{ b.overdue | currency: b.currencyCode : 'code' : '1.2-2' }}</td>
                <td class="px-4 py-3 text-right">{{ b.unallocatedAdvances | currency: b.currencyCode : 'code' : '1.2-2' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class PayablesBalancesTabComponent {
  private readonly api = inject(TravelSupplierFinanceService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly summary = signal<TravelPayablesSummary | null>(null);
  protected readonly balances = signal<TravelSupplierBalance[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() { this.load(); }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    forkJoin({ summary: this.api.getSummary(), balances: this.api.getBalances(null, true) }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => { this.summary.set(r.summary); this.balances.set(r.balances); this.loading.set(false); },
      error: err => { this.error.set(apiErrorMessage(err, 'Could not load payables.')); this.loading.set(false); }
    });
  }
}
