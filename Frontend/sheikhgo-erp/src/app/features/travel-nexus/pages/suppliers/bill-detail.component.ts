import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { UiConfirmService, UiModalComponent, UiPageHeaderComponent, UiStatusBadgeComponent } from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  CONTEXT_TYPE_LABEL, INVOICE_PAYMENT_STATUS_VARIANT, PAYMENT_METHOD_LABEL, PAYMENT_STATUS_LABEL, PAYMENT_STATUS_VARIANT, SUPPLIER_BILL_PAYMENT_LABEL,
  SUPPLIER_BILL_STATUS_VARIANT, SUPPLIER_FINANCE_EVENT_LABEL, TRAVEL_PAYMENT_METHODS, TRAVEL_PERMISSIONS, TravelContextType, TravelPaymentMethod,
  TravelSupplierBill, TravelSupplierBillAction, TravelSupplierCredit
} from '../../models/travel.models';
import { asUtc } from '../../models/travel-date.util';
import { newRequestKey } from '../../services/travel-finance.service';
import { TravelSupplierFinanceService } from '../../services/travel-supplier-finance.service';
import { round } from '../finance/finance-documents';
import { SupplierPaymentDialogComponent } from './supplier-payment-dialog.component';

const CONTEXT_ROUTE: Record<TravelContextType, string> = { Booking: '/travel/bookings', VisaCase: '/travel/visa-cases', Group: '/travel/groups' };

@Component({
  selector: 'app-travel-bill-detail',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, FormsModule, RouterLink, MatButtonModule, MatIconModule, UiPageHeaderComponent, UiStatusBadgeComponent, UiModalComponent,
    HasPermissionDirective, SupplierPaymentDialogComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      @if (error()) {
        <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div>
      } @else if (bill(); as b) {
        <ui-page-header [title]="b.billNumber ?? 'Draft bill'" eyebrow="TravelNexus · Suppliers" icon="request_quote"
          [subtitle]="b.supplierName + ' · ' + b.currencyCode + (b.supplierReference ? ' · their ref ' + b.supplierReference : '')"
          [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Suppliers', route: '/travel/suppliers' }, { label: b.billNumber ?? 'Draft' }]">
          <ui-status-badge [status]="b.status" [variant]="statusVariant[b.status]"></ui-status-badge>
          @if (b.paymentStatus) { <ui-status-badge [status]="b.paymentStatus" [label]="payLabel[b.paymentStatus]" [variant]="payVariant[b.paymentStatus]"></ui-status-badge> }
          <ng-container *appHasPermission="perms.supplierBillManage">
            @if (can('Edit')) { <a mat-stroked-button [routerLink]="['/travel/suppliers/bills', b.id, 'edit']"><mat-icon>edit</mat-icon> Edit</a> }
            @if (can('Post')) { <button mat-flat-button color="primary" [disabled]="busy()" (click)="post()"><mat-icon>task_alt</mat-icon> Post</button> }
            @if (can('Delete')) { <button mat-stroked-button [disabled]="busy()" (click)="remove()"><mat-icon>delete_outline</mat-icon> Delete draft</button> }
          </ng-container>
          @if (can('RecordPayment')) { <button mat-flat-button color="primary" *appHasPermission="perms.supplierPaymentManage" (click)="paymentOpen.set(true)"><mat-icon>payments</mat-icon> Pay</button> }
          <ng-container *appHasPermission="perms.financeAdjust">
            @if (can('Credit')) { <button mat-stroked-button (click)="openCredit()"><mat-icon>remove_circle_outline</mat-icon> Supplier credit</button> }
            @if (can('Refund')) { <button mat-stroked-button (click)="openRefund()"><mat-icon>undo</mat-icon> Refund received</button> }
            @if (can('Void')) { <button mat-stroked-button color="warn" (click)="voidOpen.set(true)"><mat-icon>block</mat-icon> Void</button> }
          </ng-container>
        </ui-page-header>

        <div class="grid gap-5 lg:grid-cols-3">
          <div class="tn-card tn-card-body">
            <dl class="tn-kv">
              <div><dt>Supplier</dt><dd><a class="text-fleet-primary underline" [routerLink]="['/travel/suppliers', b.supplierId]">{{ b.supplierName }}</a></dd></div>
              <div><dt>Their invoice</dt><dd>{{ b.supplierReference || '—' }}</dd></div>
              <div><dt>Bill date</dt><dd>{{ b.billDate ? (b.billDate | date: 'mediumDate': 'UTC') : '—' }}</dd></div>
              <div><dt>Due</dt><dd [class.text-red-600]="b.overdue">{{ b.dueDate ? (b.dueDate | date: 'mediumDate': 'UTC') : '—' }}{{ b.overdue ? ' · overdue' : '' }}</dd></div>
              @if (b.voidReason) { <div><dt>Void reason</dt><dd>{{ b.voidReason }}</dd></div> }
            </dl>
            @if (b.notes) { <p class="mt-3 whitespace-pre-line text-sm text-fleet-text-muted">{{ b.notes }}</p> }
          </div>
          <div class="tn-card tn-card-body lg:col-span-2">
            <div class="grid grid-cols-2 gap-4 md:grid-cols-4">
              <div><p class="text-xs text-fleet-text-muted">Gross</p><p class="text-lg font-semibold">{{ b.gross | currency: b.currencyCode : 'code' : '1.2-2' }}</p></div>
              <div><p class="text-xs text-fleet-text-muted">Supplier commission</p><p class="text-lg font-semibold">{{ b.commissionTotal | currency: b.currencyCode : 'code' : '1.2-2' }}</p></div>
              <div><p class="text-xs text-fleet-text-muted">Paid (cleared, net of refunds)</p><p class="text-lg font-semibold">{{ b.paid - b.refunded | currency: b.currencyCode : 'code' : '1.2-2' }}</p></div>
              <div><p class="text-xs text-fleet-text-muted">{{ b.balance < 0 ? 'Supplier owes us' : 'We owe' }}</p>
                <p class="text-lg font-semibold" [class.text-emerald-700]="b.balance <= 0">{{ abs(b.status === 'Posted' ? b.balance : b.total) | currency: b.currencyCode : 'code' : '1.2-2' }}</p></div>
            </div>
            <p class="mt-2 text-xs text-fleet-text-muted">Net payable {{ b.total | currency: b.currencyCode : 'code' : '1.2-2' }}
              @if (b.credited > 0) { · credited {{ b.credited | currency: b.currencyCode : 'code' : '1.2-2' }} }</p>
            @if (b.pendingPaid > 0) { <p class="mt-1 text-xs text-amber-700">{{ b.pendingPaid | currency: b.currencyCode : 'code' : '1.2-2' }} more is paid but not yet cleared.</p> }
          </div>
        </div>

        <div class="tn-card overflow-x-auto">
          <table class="w-full min-w-[760px] text-sm">
            <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
              <th class="px-4 py-2 text-left">#</th><th class="px-4 py-2 text-left">Description</th><th class="px-4 py-2 text-left">For</th>
              <th class="px-4 py-2 text-right">Amount</th><th class="px-4 py-2 text-right">Commission</th><th class="px-4 py-2 text-right">Net</th></tr></thead>
            <tbody class="divide-y divide-fleet-border">
              @for (l of b.lines; track l.id) {
                <tr><td class="px-4 py-2">{{ l.sequence }}</td>
                  <td class="px-4 py-2">{{ l.description }}@if (l.sourceType) { <span class="block text-xs text-fleet-text-muted">{{ l.sourceType }} service</span> }</td>
                  <td class="px-4 py-2">
                    @if (l.contextType && l.contextId) { <a class="text-fleet-primary underline" [routerLink]="[contextRoute[l.contextType], l.contextId]">{{ contextLabel[l.contextType] }} {{ l.contextLabel }}</a> }
                    @else { <span class="text-xs text-fleet-text-muted">Not tagged — not in profitability</span> }</td>
                  <td class="px-4 py-2 text-right">{{ l.amount | currency: b.currencyCode : 'code' : '1.2-2' }}</td>
                  <td class="px-4 py-2 text-right">{{ l.commissionAmount | currency: b.currencyCode : 'code' : '1.2-2' }}</td>
                  <td class="px-4 py-2 text-right font-semibold">{{ l.netAmount | currency: b.currencyCode : 'code' : '1.2-2' }}</td></tr>
              }
            </tbody>
          </table>
        </div>

        <div class="grid gap-5 lg:grid-cols-2">
          <div class="tn-card tn-card-body">
            <h3 class="tn-section-title">Payments applied</h3>
            @if (!b.allocations.length) { <p class="mt-2 text-sm text-fleet-text-muted">None yet.</p> }
            <ul class="mt-2 divide-y divide-fleet-border text-sm">
              @for (a of b.allocations; track a.paymentId) {
                <li class="flex flex-wrap items-center justify-between gap-2 py-2">
                  <a class="text-fleet-primary underline" [routerLink]="['/travel/suppliers/payments', a.paymentId]">{{ a.paymentNumber }}</a>
                  <span>{{ a.paidOn | date: 'mediumDate': 'UTC' }} · {{ methodLabel[a.method] }}</span>
                  <span class="font-semibold">{{ a.amountApplied | currency: b.currencyCode : 'code' : '1.2-2' }}
                    @if (a.paymentCurrencyCode !== b.currencyCode) { <span class="block text-xs font-normal text-fleet-text-muted">{{ a.amountFromPayment | currency: a.paymentCurrencyCode : 'code' : '1.2-2' }} at {{ a.exchangeRate }}</span> }</span>
                  <ui-status-badge [status]="a.paymentStatus" [label]="paymentStatusLabel[a.paymentStatus]" [variant]="paymentStatusVariant[a.paymentStatus]"></ui-status-badge>
                </li>
              }
            </ul>
          </div>
          @if (b.credits.length || b.refunds.length) {
            <div class="tn-card tn-card-body">
              <h3 class="tn-section-title">Supplier credits and refunds received</h3>
              <ul class="mt-2 divide-y divide-fleet-border text-sm">
                @for (c of b.credits; track c.id) {
                  <li class="flex flex-wrap items-center justify-between gap-2 py-2" [class.opacity-60]="c.status === 'Void'">
                    <span><span class="font-mono text-xs">{{ c.creditNumber }}</span> · {{ c.issuedOn | date: 'mediumDate': 'UTC' }} · {{ c.reason }}</span>
                    <span class="font-semibold">− {{ c.amount | currency: c.currencyCode : 'code' : '1.2-2' }}{{ c.status === 'Void' ? ' (void)' : '' }}</span>
                    @if (c.status === 'Issued' && !b.refunds.length) {
                      <button mat-icon-button *appHasPermission="perms.financeAdjust" aria-label="Void supplier credit" (click)="voidCredit(c)"><mat-icon>block</mat-icon></button>
                    }
                  </li>
                }
                @for (r of b.refunds; track r.id) {
                  <li class="flex flex-wrap items-center justify-between gap-2 py-2">
                    <span><span class="font-mono text-xs">{{ r.refundNumber }}</span> · {{ r.receivedOn | date: 'mediumDate': 'UTC' }} · {{ methodLabel[r.method] }}{{ r.reference ? ' · ' + r.reference : '' }}</span>
                    <span class="font-semibold">{{ r.amount | currency: r.currencyCode : 'code' : '1.2-2' }} received</span>
                  </li>
                }
              </ul>
            </div>
          }
          <div class="tn-card tn-card-body">
            <h3 class="tn-section-title">History</h3>
            <ol class="tn-timeline">
              @for (h of b.history; track h.id) {
                <li><div class="flex-1 text-sm">
                  <p><strong>{{ eventLabel[h.eventType] }}</strong>
                    @if (h.amount !== null && h.currencyCode) { · {{ h.amount | currency: h.currencyCode : 'code' : '1.2-2' }} }
                    @if (h.toValue) { <span class="text-fleet-text-muted"> · {{ h.toValue }}</span> }</p>
                  @if (h.note) { <p class="text-fleet-text-muted">{{ h.note }}</p> }
                  <p class="text-xs text-fleet-text-soft">{{ utc(h.changedAt) | date: 'medium' }} · {{ h.changedByName || 'System' }}</p>
                </div></li>
              }
            </ol>
          </div>
        </div>

        <app-supplier-payment-dialog [(open)]="paymentOpen" [supplierId]="b.supplierId" [presetBill]="b" (saved)="load()" />

        <ui-modal [(open)]="creditOpen" title="Supplier credit" size="sm">
          <div class="space-y-3">
            <p class="text-xs text-fleet-text-muted">Up to {{ b.total - b.credited | currency: b.currencyCode : 'code' : '1.2-2' }} can be credited. If you already paid, the credit becomes money the supplier owes you back.</p>
            <div class="tn-field"><label for="sc-amount" class="tn-required">Amount ({{ b.currencyCode }})</label>
              <input id="sc-amount" type="number" min="0" step="0.01" class="tn-input" [(ngModel)]="creditAmount" /></div>
            <div class="tn-field"><label for="sc-reason" class="tn-required">Reason</label>
              <textarea id="sc-reason" class="tn-input" rows="2" maxlength="500" [(ngModel)]="creditReason" placeholder="Their credit note no., room downgrade…"></textarea></div>
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          </div>
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="creditOpen.set(false)">Cancel</button>
            <button mat-flat-button color="primary" [disabled]="busy() || !(creditAmount > 0) || !creditReason.trim()" (click)="issueCredit()">Record credit</button>
          </div>
        </ui-modal>

        <ui-modal [(open)]="refundOpen" title="Refund received from supplier" size="sm">
          <div class="space-y-3">
            <p class="text-xs text-fleet-text-muted">The supplier owes {{ abs(b.balance) | currency: b.currencyCode : 'code' : '1.2-2' }} back on this bill.</p>
            <div class="tn-grid">
              <div class="tn-field"><label for="sr-amount" class="tn-required">Amount ({{ b.currencyCode }})</label>
                <input id="sr-amount" type="number" min="0" step="0.01" class="tn-input" [(ngModel)]="refundAmount" /></div>
              <div class="tn-field"><label for="sr-date" class="tn-required">Received on</label><input id="sr-date" type="date" class="tn-input" [(ngModel)]="refundDate" /></div>
              <div class="tn-field"><label for="sr-method">Method</label>
                <select id="sr-method" class="tn-input" [(ngModel)]="refundMethod">@for (m of methods; track m) { <option [value]="m">{{ methodLabel[m] }}</option> }</select></div>
              <div class="tn-field"><label for="sr-ref">Reference</label><input id="sr-ref" class="tn-input" maxlength="100" [(ngModel)]="refundReference" /></div>
            </div>
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          </div>
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="refundOpen.set(false)">Cancel</button>
            <button mat-flat-button color="primary" [disabled]="busy() || !(refundAmount > 0) || !refundDate" (click)="refund()">Record refund</button>
          </div>
        </ui-modal>

        <ui-modal [(open)]="voidOpen" title="Void bill" size="sm">
          <div class="space-y-3">
            <p class="text-sm">Voiding cancels the bill and frees its services to be billed again. It keeps its number.</p>
            <div class="tn-field"><label for="bv-reason" class="tn-required">Reason</label>
              <textarea id="bv-reason" class="tn-input" rows="2" maxlength="500" [(ngModel)]="voidReason"></textarea></div>
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          </div>
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="voidOpen.set(false)">Back</button>
            <button mat-flat-button color="warn" [disabled]="busy() || !voidReason.trim()" (click)="voidBill()">Void bill</button>
          </div>
        </ui-modal>
      } @else {
        <p class="text-sm text-fleet-text-muted">Loading bill…</p>
      }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelBillDetailComponent {
  private readonly api = inject(TravelSupplierFinanceService);
  private readonly router = inject(Router);
  private readonly toast = inject(UiToastService);
  private readonly confirm = inject(UiConfirmService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly id = Number(inject(ActivatedRoute).snapshot.paramMap.get('id'));

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly statusVariant = SUPPLIER_BILL_STATUS_VARIANT;
  protected readonly payLabel = SUPPLIER_BILL_PAYMENT_LABEL;
  protected readonly payVariant = INVOICE_PAYMENT_STATUS_VARIANT;
  protected readonly paymentStatusLabel = PAYMENT_STATUS_LABEL;
  protected readonly paymentStatusVariant = PAYMENT_STATUS_VARIANT;
  protected readonly methodLabel = PAYMENT_METHOD_LABEL;
  protected readonly methods = TRAVEL_PAYMENT_METHODS;
  protected readonly eventLabel = SUPPLIER_FINANCE_EVENT_LABEL;
  protected readonly contextLabel = CONTEXT_TYPE_LABEL;
  protected readonly contextRoute = CONTEXT_ROUTE;
  protected readonly utc = asUtc;

  protected readonly bill = signal<TravelSupplierBill | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly dialogError = signal<string | null>(null);
  protected readonly paymentOpen = signal(false);
  protected readonly creditOpen = signal(false);
  protected readonly refundOpen = signal(false);
  protected readonly voidOpen = signal(false);
  private key = newRequestKey();
  protected creditAmount = 0;
  protected creditReason = '';
  protected refundAmount = 0;
  protected refundDate = '';
  protected refundMethod: TravelPaymentMethod = 'BankTransfer';
  protected refundReference = '';
  protected voidReason = '';

  constructor() { this.load(); }

  load(): void {
    this.api.getBill(this.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: b => { this.bill.set(b); this.error.set(null); },
      error: err => this.error.set(apiErrorMessage(err, 'Could not load the bill.'))
    });
  }

  protected can(a: TravelSupplierBillAction): boolean { return this.bill()?.allowedActions.includes(a) ?? false; }
  protected abs(n: number): number { return Math.abs(n); }

  private run(obs: Observable<unknown>, ok: string, close?: () => void): void {
    this.busy.set(true);
    this.dialogError.set(null);
    obs.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); close?.(); this.toast.success(ok); this.load(); },
      error: err => { this.busy.set(false); const m = apiErrorMessage(err, 'The change was refused.'); if (close) this.dialogError.set(m); else this.toast.error(m); }
    });
  }

  protected post(): void { this.run(this.api.postBill(this.id, null, null), 'Bill posted.'); }
  protected async remove(): Promise<void> {
    if (!(await this.confirm.confirmDelete('Delete this draft? Its services can be billed again.'))) return;
    this.api.deleteBill(this.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.toast.success('Draft deleted.'); this.router.navigate(['/travel/suppliers']); },
      error: err => this.toast.error(apiErrorMessage(err, 'Could not delete the draft.'))
    });
  }
  protected openCredit(): void {
    const b = this.bill()!;
    this.key = newRequestKey();
    this.creditAmount = round(b.total - b.credited);
    this.creditReason = '';
    this.dialogError.set(null);
    this.creditOpen.set(true);
  }
  protected issueCredit(): void {
    this.run(this.api.issueCredit({ idempotencyKey: this.key, billId: this.id, amount: round(Number(this.creditAmount)), reason: this.creditReason.trim(),
      cancellationId: null, issuedOn: null }), 'Supplier credit recorded.', () => this.creditOpen.set(false));
  }
  protected openRefund(): void {
    this.key = newRequestKey();
    this.refundAmount = round(Math.abs(this.bill()!.balance));
    this.refundDate = new Date().toISOString().substring(0, 10);
    this.refundReference = '';
    this.dialogError.set(null);
    this.refundOpen.set(true);
  }
  protected refund(): void {
    this.run(this.api.recordRefund({ idempotencyKey: this.key, billId: this.id, paymentId: null, amount: round(Number(this.refundAmount)), method: this.refundMethod,
      reference: this.refundReference.trim() || null, receivedOn: this.refundDate, cancellationId: null, notes: null }), 'Refund recorded.',
      () => this.refundOpen.set(false));
  }
  protected voidBill(): void { this.run(this.api.voidBill(this.id, this.voidReason.trim()), 'Bill voided.', () => this.voidOpen.set(false)); }
  protected async voidCredit(c: TravelSupplierCredit): Promise<void> {
    if (!(await this.confirm.open({ variant: 'warning', title: 'Void supplier credit', message: `Void ${c.creditNumber}? The bill balance goes back up.`, confirmText: 'Void' }))) return;
    this.run(this.api.voidCredit(c.id, 'Voided from the bill'), 'Supplier credit voided.');
  }
}
