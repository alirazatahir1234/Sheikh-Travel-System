import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, catchError, of } from 'rxjs';
import { UiConfirmService, UiModalComponent, UiPageHeaderComponent, UiStatusBadgeComponent } from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  PAYMENT_METHOD_LABEL, PAYMENT_STATUS_LABEL, PAYMENT_STATUS_VARIANT, SUPPLIER_FINANCE_EVENT_LABEL, TRAVEL_PAYMENT_METHODS, TRAVEL_PERMISSIONS,
  TravelPaymentAction, TravelPaymentMethod, TravelSupplierAllocation, TravelSupplierBillListItem, TravelSupplierPayment
} from '../../models/travel.models';
import { asUtc } from '../../models/travel-date.util';
import { newRequestKey } from '../../services/travel-finance.service';
import { TravelSupplierFinanceService } from '../../services/travel-supplier-finance.service';
import { round } from '../finance/finance-documents';
import { suggestBillAllocations } from './supplier-finance';

interface AllocateRow { bill: TravelSupplierBillListItem; selected: boolean; amount: number | null; rate: number | null; }

@Component({
  selector: 'app-travel-supplier-payment-detail',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, FormsModule, RouterLink, MatButtonModule, MatIconModule, UiPageHeaderComponent, UiStatusBadgeComponent, UiModalComponent,
    HasPermissionDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      @if (error()) {
        <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div>
      } @else if (payment(); as p) {
        <ui-page-header [title]="p.paymentNumber" eyebrow="TravelNexus · Suppliers" icon="payments"
          [subtitle]="p.supplierName + ' · ' + (p.amount | currency: p.currencyCode : 'code' : '1.2-2')"
          [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Suppliers', route: '/travel/suppliers' }, { label: p.paymentNumber }]">
          <ui-status-badge [status]="p.status" [label]="statusLabel[p.status]" [variant]="statusVariant[p.status]"></ui-status-badge>
          <ng-container *appHasPermission="perms.supplierPaymentManage">
            @if (can('Clear')) { <button mat-flat-button color="primary" [disabled]="busy()" (click)="clear()"><mat-icon>check</mat-icon> Mark cleared</button> }
            @if (can('Fail')) { <button mat-stroked-button color="warn" (click)="failOpen.set(true)">Mark failed</button> }
            @if (can('Allocate')) { <button mat-stroked-button (click)="openAllocate()"><mat-icon>call_split</mat-icon> Apply to bills</button> }
          </ng-container>
          @if (can('Refund')) { <button mat-stroked-button *appHasPermission="perms.financeAdjust" (click)="openRefund()"><mat-icon>undo</mat-icon> Advance returned</button> }
          @if (can('Reconcile')) { <button mat-stroked-button *appHasPermission="perms.financeReconcile" (click)="reconcileOpen.set(true)"><mat-icon>account_balance</mat-icon> Reconcile</button> }
        </ui-page-header>

        <div class="grid gap-5 lg:grid-cols-3">
          <div class="tn-card tn-card-body">
            <dl class="tn-kv">
              <div><dt>Paid to</dt><dd><a class="text-fleet-primary underline" [routerLink]="['/travel/suppliers', p.supplierId]">{{ p.supplierName }}</a></dd></div>
              <div><dt>Paid on</dt><dd>{{ p.paidOn | date: 'mediumDate': 'UTC' }}</dd></div>
              <div><dt>Method</dt><dd>{{ methodLabel[p.method] }}{{ p.reference ? ' · ' + p.reference : '' }}</dd></div>
              @if (p.clearedAt) { <div><dt>Cleared</dt><dd>{{ utc(p.clearedAt) | date: 'medium' }}</dd></div> }
              <div><dt>Reconciled</dt><dd>{{ p.reconciledAt ? (utc(p.reconciledAt) | date: 'mediumDate') + ' · ' + p.bankReference : 'No' }}</dd></div>
            </dl>
            @if (p.notes) { <p class="mt-3 text-sm text-fleet-text-muted">{{ p.notes }}</p> }
          </div>
          <div class="tn-card tn-card-body lg:col-span-2">
            <div class="grid grid-cols-3 gap-4">
              <div><p class="text-xs text-fleet-text-muted">Amount</p><p class="text-lg font-semibold">{{ p.amount | currency: p.currencyCode : 'code' : '1.2-2' }}</p></div>
              <div><p class="text-xs text-fleet-text-muted">Applied to bills</p><p class="text-lg font-semibold">{{ p.allocated | currency: p.currencyCode : 'code' : '1.2-2' }}</p></div>
              <div><p class="text-xs text-fleet-text-muted">Advance (unallocated)</p><p class="text-lg font-semibold">{{ p.unallocated | currency: p.currencyCode : 'code' : '1.2-2' }}</p></div>
            </div>
            @if (p.status === 'Pending') { <p class="mt-3 text-xs text-amber-700">Not cleared yet: its allocations reserve the bills but do not reduce their balance.</p> }
            @if (p.status === 'Failed') { <p class="mt-3 text-xs text-red-600">Failed: its allocations no longer count.</p> }
          </div>
        </div>

        <div class="tn-card tn-card-body">
          <h3 class="tn-section-title">Applied to</h3>
          @if (!p.allocations.length) { <p class="mt-2 text-sm text-fleet-text-muted">Not applied to any bill.</p> }
          <ul class="mt-2 divide-y divide-fleet-border text-sm">
            @for (a of p.allocations; track a.billId) {
              <li class="flex flex-wrap items-center justify-between gap-2 py-2">
                <a class="text-fleet-primary underline" [routerLink]="['/travel/suppliers/bills', a.billId]">{{ a.billNumber }}</a>
                <span>{{ a.amountFromPayment | currency: a.paymentCurrencyCode : 'code' : '1.2-2' }}
                  @if (a.paymentCurrencyCode !== a.billCurrencyCode) { at {{ a.exchangeRate }} = {{ a.amountApplied | currency: a.billCurrencyCode : 'code' : '1.2-2' }} }</span>
                @if (can('Unallocate')) {
                  <button mat-button *appHasPermission="perms.supplierPaymentManage" (click)="unallocate(a)"><mat-icon>link_off</mat-icon> Remove</button>
                }
              </li>
            }
          </ul>
          @if (p.refunds.length) {
            <h3 class="tn-section-title mt-4">Returned by the supplier</h3>
            <ul class="mt-2 divide-y divide-fleet-border text-sm">
              @for (r of p.refunds; track r.id) {
                <li class="flex justify-between py-2"><span class="font-mono text-xs">{{ r.refundNumber }}</span><span>{{ r.receivedOn | date: 'mediumDate': 'UTC' }}</span>
                  <span class="font-semibold">{{ r.amount | currency: r.currencyCode : 'code' : '1.2-2' }}</span></li>
              }
            </ul>
          }
        </div>

        <div class="tn-card tn-card-body">
          <h3 class="tn-section-title">History</h3>
          <ol class="tn-timeline">
            @for (h of p.history; track h.id) {
              <li><div class="flex-1 text-sm">
                <p><strong>{{ eventLabel[h.eventType] }}</strong>
                  @if (h.fromValue || h.toValue) { <span class="text-fleet-text-muted"> {{ h.fromValue || '' }}{{ h.fromValue && h.toValue ? ' → ' : '' }}{{ h.toValue || '' }}</span> }
                  @if (h.amount !== null && h.currencyCode) { · {{ h.amount | currency: h.currencyCode : 'code' : '1.2-2' }} }</p>
                @if (h.note) { <p class="text-fleet-text-muted">{{ h.note }}</p> }
                <p class="text-xs text-fleet-text-soft">{{ utc(h.changedAt) | date: 'medium' }} · {{ h.changedByName || 'System' }}</p>
              </div></li>
            }
          </ol>
        </div>

        <ui-modal [(open)]="failOpen" title="Mark payment failed" size="sm">
          <div class="space-y-3">
            <p class="text-sm">The bills it was applied to go back to owing that amount.</p>
            <div class="tn-field"><label for="spf-note" class="tn-required">Reason</label><textarea id="spf-note" class="tn-input" rows="2" maxlength="500" [(ngModel)]="note"></textarea></div>
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          </div>
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="failOpen.set(false)">Back</button>
            <button mat-flat-button color="warn" [disabled]="busy() || !note.trim()" (click)="fail()">Mark failed</button>
          </div>
        </ui-modal>

        <ui-modal [(open)]="reconcileOpen" title="Reconcile with the bank" size="sm">
          <div class="space-y-3">
            <div class="tn-field"><label for="src-ref" class="tn-required">Bank statement reference</label><input id="src-ref" class="tn-input" maxlength="100" [(ngModel)]="bankReference" /></div>
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          </div>
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="reconcileOpen.set(false)">Back</button>
            <button mat-flat-button color="primary" [disabled]="busy() || !bankReference.trim()" (click)="reconcile()">Reconcile</button>
          </div>
        </ui-modal>

        <ui-modal [(open)]="allocateOpen" title="Apply to bills" size="lg">
          <div class="space-y-3">
            <div class="flex items-center justify-between"><p class="text-xs text-fleet-text-muted">{{ p.unallocated | currency: p.currencyCode : 'code' : '1.2-2' }} left on this payment.</p>
              <button mat-button (click)="suggest()"><mat-icon>auto_fix_high</mat-icon> Suggest</button></div>
            @if (!rows().length) { <p class="text-sm text-fleet-text-muted">The supplier has no open bills.</p> }
            <table class="w-full text-sm">
              @for (r of rows(); track r.bill.id) {
                <tr class="border-b border-fleet-border">
                  <td class="py-1.5"><input type="checkbox" [attr.aria-label]="'Apply to ' + r.bill.billNumber" [(ngModel)]="r.selected" /></td>
                  <td class="py-1.5 font-mono text-xs">{{ r.bill.billNumber }}</td>
                  <td class="py-1.5 text-right">{{ openOf(r.bill) | currency: r.bill.currencyCode : 'code' : '1.2-2' }} open</td>
                  <td class="py-1.5 pl-3"><input type="number" min="0" step="0.01" class="tn-input w-32" [attr.aria-label]="'Amount for ' + r.bill.billNumber" [(ngModel)]="r.amount" [disabled]="!r.selected" /> {{ p.currencyCode }}</td>
                  <td class="py-1.5 pl-3">@if (r.bill.currencyCode !== p.currencyCode) {
                    <label class="text-xs">rate <input type="number" min="0" step="0.000001" class="tn-input inline w-28" [attr.aria-label]="'Rate for ' + r.bill.billNumber" [(ngModel)]="r.rate" [disabled]="!r.selected" /></label> }</td>
                </tr>
              }
            </table>
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          </div>
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="allocateOpen.set(false)">Cancel</button>
            <button mat-flat-button color="primary" [disabled]="busy()" (click)="allocate()">Apply</button>
          </div>
        </ui-modal>

        <ui-modal [(open)]="refundOpen" title="Advance returned by supplier" size="sm">
          <div class="space-y-3">
            <div class="tn-grid">
              <div class="tn-field"><label for="sra-amount" class="tn-required">Amount ({{ p.currencyCode }})</label><input id="sra-amount" type="number" min="0" step="0.01" class="tn-input" [(ngModel)]="refundAmount" /></div>
              <div class="tn-field"><label for="sra-date" class="tn-required">Received on</label><input id="sra-date" type="date" class="tn-input" [(ngModel)]="refundDate" /></div>
              <div class="tn-field"><label for="sra-method">Method</label>
                <select id="sra-method" class="tn-input" [(ngModel)]="refundMethod">@for (m of methods; track m) { <option [value]="m">{{ methodLabel[m] }}</option> }</select></div>
              <div class="tn-field"><label for="sra-ref">Reference</label><input id="sra-ref" class="tn-input" maxlength="100" [(ngModel)]="refundReference" /></div>
            </div>
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          </div>
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="refundOpen.set(false)">Cancel</button>
            <button mat-flat-button color="primary" [disabled]="busy() || !(refundAmount > 0) || !refundDate" (click)="refund()">Record</button>
          </div>
        </ui-modal>
      } @else {
        <p class="text-sm text-fleet-text-muted">Loading payment…</p>
      }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelSupplierPaymentDetailComponent {
  private readonly api = inject(TravelSupplierFinanceService);
  private readonly toast = inject(UiToastService);
  private readonly confirm = inject(UiConfirmService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly id = Number(inject(ActivatedRoute).snapshot.paramMap.get('id'));

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly statusLabel = PAYMENT_STATUS_LABEL;
  protected readonly statusVariant = PAYMENT_STATUS_VARIANT;
  protected readonly methodLabel = PAYMENT_METHOD_LABEL;
  protected readonly methods = TRAVEL_PAYMENT_METHODS;
  protected readonly eventLabel = SUPPLIER_FINANCE_EVENT_LABEL;
  protected readonly utc = asUtc;

  protected readonly payment = signal<TravelSupplierPayment | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly dialogError = signal<string | null>(null);
  protected readonly failOpen = signal(false);
  protected readonly reconcileOpen = signal(false);
  protected readonly allocateOpen = signal(false);
  protected readonly refundOpen = signal(false);
  protected readonly rows = signal<AllocateRow[]>([]);
  private key = newRequestKey();
  protected note = '';
  protected bankReference = '';
  protected refundAmount = 0;
  protected refundDate = '';
  protected refundMethod: TravelPaymentMethod = 'BankTransfer';
  protected refundReference = '';

  constructor() { this.load(); }

  load(): void {
    this.api.getPayment(this.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: p => { this.payment.set(p); this.error.set(null); },
      error: err => this.error.set(apiErrorMessage(err, 'Could not load the payment.'))
    });
  }

  protected can(a: TravelPaymentAction): boolean { return this.payment()?.allowedActions.includes(a) ?? false; }
  protected openOf(b: TravelSupplierBillListItem): number { return round(Math.max(0, b.balance - b.pendingPaid)); }

  private run(obs: Observable<unknown>, ok: string, close?: () => void): void {
    this.busy.set(true);
    this.dialogError.set(null);
    obs.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); close?.(); this.toast.success(ok); this.load(); },
      error: err => { this.busy.set(false); const m = apiErrorMessage(err, 'The change was refused.'); if (close) this.dialogError.set(m); else this.toast.error(m); }
    });
  }

  protected clear(): void { this.run(this.api.changePaymentStatus(this.id, 'Completed', null), 'Payment cleared.'); }
  protected fail(): void { this.run(this.api.changePaymentStatus(this.id, 'Failed', this.note.trim()), 'Payment marked failed.', () => this.failOpen.set(false)); }
  protected reconcile(): void { this.run(this.api.reconcile(this.id, this.bankReference.trim(), null), 'Payment reconciled.', () => this.reconcileOpen.set(false)); }

  protected async unallocate(a: TravelSupplierAllocation): Promise<void> {
    if (!(await this.confirm.open({ variant: 'warning', title: 'Remove allocation', message: `Take this payment off ${a.billNumber}? The amount becomes an advance again.`, confirmText: 'Remove' }))) return;
    this.run(this.api.unallocate(this.id, a.billId), 'Allocation removed.');
  }

  protected openAllocate(): void {
    const p = this.payment()!;
    this.dialogError.set(null);
    this.rows.set([]);
    this.allocateOpen.set(true);
    this.api.getOpenBills(p.supplierId).pipe(catchError(() => of([] as TravelSupplierBillListItem[])), takeUntilDestroyed(this.destroyRef))
      .subscribe(list => this.rows.set(list.map(b => ({ bill: b, selected: false, amount: null, rate: null }))));
  }

  protected suggest(): void {
    const p = this.payment()!;
    const rates: Record<string, number | null> = {};
    for (const r of this.rows()) if (r.rate) rates[r.bill.currencyCode] = Number(r.rate);
    const plan = suggestBillAllocations(p.unallocated, p.currencyCode, this.rows().map(r => r.bill), rates);
    this.rows.update(rows => rows.map(r => {
      const s = plan.find(x => x.invoiceId === r.bill.id);
      return { ...r, selected: !!s, amount: s?.amountFromPayment ?? null, rate: s?.exchangeRate ?? r.rate };
    }));
  }

  protected allocate(): void {
    const p = this.payment()!;
    const allocations = this.rows().filter(r => r.selected && Number(r.amount) > 0).map(r => ({
      invoiceId: r.bill.id, amountFromPayment: round(Number(r.amount)), exchangeRate: r.bill.currencyCode === p.currencyCode ? null : (Number(r.rate) || null)
    }));
    if (!allocations.length) { this.dialogError.set('Choose at least one bill and an amount.'); return; }
    this.run(this.api.allocate(this.id, allocations), 'Payment applied.', () => this.allocateOpen.set(false));
  }

  protected openRefund(): void {
    this.key = newRequestKey();
    this.refundAmount = this.payment()!.unallocated;
    this.refundDate = new Date().toISOString().substring(0, 10);
    this.refundReference = '';
    this.dialogError.set(null);
    this.refundOpen.set(true);
  }

  protected refund(): void {
    this.run(this.api.recordRefund({ idempotencyKey: this.key, billId: null, paymentId: this.id, amount: round(Number(this.refundAmount)), method: this.refundMethod,
      reference: this.refundReference.trim() || null, receivedOn: this.refundDate, cancellationId: null, notes: null }), 'Refund recorded.', () => this.refundOpen.set(false));
  }
}
