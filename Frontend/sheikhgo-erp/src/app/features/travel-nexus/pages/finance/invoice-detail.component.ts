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
  FINANCE_EVENT_LABEL, INSTALLMENT_STATUS_VARIANT, INVOICE_PAYMENT_STATUS_LABEL, INVOICE_PAYMENT_STATUS_VARIANT, INVOICE_STATUS_VARIANT,
  PAYMENT_METHOD_LABEL, PAYMENT_STATUS_LABEL, PAYMENT_STATUS_VARIANT, SOURCE_TYPE_LABEL, TRAVEL_PAYMENT_METHODS, TRAVEL_PERMISSIONS,
  TravelCreditNote, TravelInvoice, TravelInvoiceAction, TravelPaymentMethod
} from '../../models/travel.models';
import { asUtc } from '../../models/travel-date.util';
import { TravelFinanceService, newRequestKey } from '../../services/travel-finance.service';
import { buildCreditNotePdf, buildInvoicePdf, round } from './finance-documents';
import { RecordPaymentDialogComponent } from './record-payment-dialog.component';

@Component({
  selector: 'app-travel-invoice-detail',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, FormsModule, RouterLink, MatButtonModule, MatIconModule, UiPageHeaderComponent, UiStatusBadgeComponent, UiModalComponent,
    HasPermissionDirective, RecordPaymentDialogComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      @if (error()) {
        <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div>
      } @else if (invoice(); as v) {
        <ui-page-header [title]="v.invoiceNumber ?? 'Draft invoice'" eyebrow="TravelNexus · Invoices & Payments" icon="receipt_long"
          [subtitle]="v.customerName + ' · ' + v.currencyCode"
          [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Invoices & Payments', route: '/travel/finance' }, { label: v.invoiceNumber ?? 'Draft' }]">
          <ui-status-badge [status]="v.status" [variant]="statusVariant[v.status]"></ui-status-badge>
          @if (v.paymentStatus) { <ui-status-badge [status]="v.paymentStatus" [label]="payLabel[v.paymentStatus]" [variant]="payVariant[v.paymentStatus]"></ui-status-badge> }
          <button mat-stroked-button (click)="pdf()"><mat-icon>picture_as_pdf</mat-icon> PDF</button>
          <ng-container *appHasPermission="perms.invoiceManage">
            @if (can('Edit')) { <a mat-stroked-button [routerLink]="['/travel/finance/invoices', v.id, 'edit']"><mat-icon>edit</mat-icon> Edit</a> }
            @if (can('Issue')) { <button mat-flat-button color="primary" [disabled]="busy()" (click)="issue()"><mat-icon>send</mat-icon> Issue</button> }
            @if (can('Delete')) { <button mat-stroked-button [disabled]="busy()" (click)="remove()"><mat-icon>delete_outline</mat-icon> Delete draft</button> }
          </ng-container>
          @if (can('RecordPayment')) { <button mat-flat-button color="primary" *appHasPermission="perms.paymentManage" (click)="paymentOpen.set(true)"><mat-icon>payments</mat-icon> Record payment</button> }
          <ng-container *appHasPermission="perms.financeAdjust">
            @if (can('CreditNote')) { <button mat-stroked-button (click)="openCredit()"><mat-icon>remove_circle_outline</mat-icon> Credit note</button> }
            @if (can('Refund')) { <button mat-stroked-button (click)="openRefund()"><mat-icon>undo</mat-icon> Refund</button> }
            @if (can('Void')) { <button mat-stroked-button color="warn" (click)="voidOpen.set(true)"><mat-icon>block</mat-icon> Void</button> }
          </ng-container>
        </ui-page-header>

        <div class="grid gap-5 lg:grid-cols-3">
          <div class="tn-card tn-card-body">
            <dl class="tn-kv">
              <div><dt>Customer</dt><dd><a class="text-fleet-primary underline" [routerLink]="['/travel/customers', v.customerId]">{{ v.customerName }}</a></dd></div>
              <div><dt>Issued</dt><dd>{{ v.issueDate ? (v.issueDate | date: 'mediumDate': 'UTC') : '—' }}</dd></div>
              <div><dt>Due</dt><dd>{{ v.dueDate ? (v.dueDate | date: 'mediumDate': 'UTC') : '—' }}</dd></div>
              @if (v.bookingId) { <div><dt>Booking</dt><dd><a class="text-fleet-primary underline" [routerLink]="['/travel/bookings', v.bookingId]">{{ v.bookingNumber }}</a></dd></div> }
              @if (v.visaCaseId) { <div><dt>Visa case</dt><dd><a class="text-fleet-primary underline" [routerLink]="['/travel/visa-cases', v.visaCaseId]">{{ v.visaCaseNumber }}</a></dd></div> }
              @if (v.groupId) { <div><dt>Umrah group</dt><dd><a class="text-fleet-primary underline" [routerLink]="['/travel/groups', v.groupId]">{{ v.groupNumber }}</a></dd></div> }
              <div><dt>Branch</dt><dd>{{ v.branchName || '—' }}</dd></div>
              @if (v.voidReason) { <div><dt>Void reason</dt><dd>{{ v.voidReason }}</dd></div> }
            </dl>
            @if (v.notes) { <p class="mt-3 whitespace-pre-line text-sm text-fleet-text-muted">{{ v.notes }}</p> }
          </div>
          <div class="tn-card tn-card-body lg:col-span-2">
            <div class="grid grid-cols-2 gap-4 md:grid-cols-4">
              <div><p class="text-xs text-fleet-text-muted">Total</p><p class="text-lg font-semibold">{{ v.total | currency: v.currencyCode : 'code' : '1.2-2' }}</p></div>
              <div><p class="text-xs text-fleet-text-muted">Paid (cleared, net of refunds)</p><p class="text-lg font-semibold">{{ v.paid - v.refunded | currency: v.currencyCode : 'code' : '1.2-2' }}</p></div>
              <div><p class="text-xs text-fleet-text-muted">Credited</p><p class="text-lg font-semibold">{{ v.credited | currency: v.currencyCode : 'code' : '1.2-2' }}</p></div>
              <div><p class="text-xs text-fleet-text-muted">{{ v.balance < 0 ? 'Credit due to customer' : 'Balance' }}</p>
                <p class="text-lg font-semibold" [class.text-emerald-700]="v.balance <= 0">{{ abs(v.balance) | currency: v.currencyCode : 'code' : '1.2-2' }}</p></div>
            </div>
            @if (v.pendingPaid > 0) { <p class="mt-3 text-xs text-amber-700">{{ v.pendingPaid | currency: v.currencyCode : 'code' : '1.2-2' }} more is received but not yet cleared.</p> }
            @if (v.overdueAmount > 0) { <p class="mt-1 text-xs font-semibold text-red-600">Overdue (as of today): {{ v.overdueAmount | currency: v.currencyCode : 'code' : '1.2-2' }}</p> }
          </div>
        </div>

        <div class="tn-card overflow-x-auto">
          <table class="w-full min-w-[720px] text-sm">
            <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
              <th class="px-4 py-2 text-left">#</th><th class="px-4 py-2 text-left">Description</th><th class="px-4 py-2 text-right">Qty</th>
              <th class="px-4 py-2 text-right">Unit price</th><th class="px-4 py-2 text-right">Amount</th></tr></thead>
            <tbody class="divide-y divide-fleet-border">
              @for (l of v.lines; track l.id) {
                <tr><td class="px-4 py-2">{{ l.sequence }}</td>
                  <td class="px-4 py-2">{{ l.description }}
                    @if (l.sourceType) { <span class="block text-xs text-fleet-text-muted">{{ sourceLabel[l.sourceType] }}</span> }
                    @if (v.status === 'Issued' && l.sourceChanged) {
                      <span class="block text-xs text-amber-700">{{ l.currentSourceAmount === null ? 'The service was cancelled since invoicing — consider a credit note.'
                        : 'The service is now priced ' + (l.currentSourceAmount | currency: v.currencyCode : 'code' : '1.2-2') + '.' }}</span>
                    }</td>
                  <td class="px-4 py-2 text-right">{{ l.quantity }}</td>
                  <td class="px-4 py-2 text-right">{{ l.unitPrice | currency: v.currencyCode : 'code' : '1.2-2' }}</td>
                  <td class="px-4 py-2 text-right font-semibold">{{ l.lineTotal | currency: v.currencyCode : 'code' : '1.2-2' }}</td></tr>
              }
            </tbody>
            <tfoot class="text-sm">
              @if (v.discountAmount > 0) { <tr><td colspan="4" class="px-4 py-1 text-right">Discount</td><td class="px-4 py-1 text-right">− {{ v.discountAmount | currency: v.currencyCode : 'code' : '1.2-2' }}</td></tr> }
              <tr><td colspan="4" class="px-4 py-2 text-right font-semibold">Total</td><td class="px-4 py-2 text-right font-semibold">{{ v.total | currency: v.currencyCode : 'code' : '1.2-2' }}</td></tr>
            </tfoot>
          </table>
        </div>

        <div class="grid gap-5 lg:grid-cols-2">
          @if (v.installments.length) {
            <div class="tn-card tn-card-body">
              <h3 class="tn-section-title">Installments</h3>
              <ul class="mt-2 divide-y divide-fleet-border text-sm">
                @for (i of v.installments; track i.sequence) {
                  <li class="flex items-center justify-between py-2">
                    <span>{{ i.dueDate | date: 'mediumDate': 'UTC' }} · {{ i.amount | currency: v.currencyCode : 'code' : '1.2-2' }}
                      @if (i.covered > 0 && i.covered < i.amount) { <span class="text-xs text-fleet-text-muted"> ({{ i.covered | currency: v.currencyCode : 'code' : '1.2-2' }} covered)</span> }</span>
                    <ui-status-badge [status]="i.status" [variant]="installmentVariant[i.status]"></ui-status-badge>
                  </li>
                }
              </ul>
            </div>
          }
          <div class="tn-card tn-card-body">
            <h3 class="tn-section-title">Payments applied</h3>
            @if (!v.allocations.length) { <p class="mt-2 text-sm text-fleet-text-muted">None yet.</p> }
            <ul class="mt-2 divide-y divide-fleet-border text-sm">
              @for (a of v.allocations; track a.paymentId) {
                <li class="flex flex-wrap items-center justify-between gap-2 py-2">
                  <a class="text-fleet-primary underline" [routerLink]="['/travel/finance/payments', a.paymentId]">{{ a.receiptNumber }}</a>
                  <span>{{ a.receivedOn | date: 'mediumDate': 'UTC' }} · {{ methodLabel[a.method] }}</span>
                  <span class="font-semibold">{{ a.amountApplied | currency: v.currencyCode : 'code' : '1.2-2' }}
                    @if (a.paymentCurrencyCode !== v.currencyCode) { <span class="block text-xs font-normal text-fleet-text-muted">{{ a.amountFromPayment | currency: a.paymentCurrencyCode : 'code' : '1.2-2' }} at {{ a.exchangeRate }}</span> }</span>
                  <ui-status-badge [status]="a.paymentStatus" [label]="paymentStatusLabel[a.paymentStatus]" [variant]="paymentStatusVariant[a.paymentStatus]"></ui-status-badge>
                </li>
              }
            </ul>
          </div>
          @if (v.creditNotes.length || v.refunds.length) {
            <div class="tn-card tn-card-body">
              <h3 class="tn-section-title">Credit notes and refunds</h3>
              <ul class="mt-2 divide-y divide-fleet-border text-sm">
                @for (c of v.creditNotes; track c.id) {
                  <li class="flex flex-wrap items-center justify-between gap-2 py-2" [class.opacity-60]="c.status === 'Void'">
                    <span><span class="font-mono text-xs">{{ c.creditNoteNumber }}</span> · {{ c.issuedOn | date: 'mediumDate': 'UTC' }} · {{ c.reason }}</span>
                    <span class="font-semibold">− {{ c.amount | currency: c.currencyCode : 'code' : '1.2-2' }}{{ c.status === 'Void' ? ' (void)' : '' }}</span>
                    <span class="flex gap-1">
                      <button mat-icon-button aria-label="Credit note PDF" (click)="creditPdf(c)"><mat-icon>picture_as_pdf</mat-icon></button>
                      @if (c.status === 'Issued' && !v.refunds.length) {
                        <button mat-icon-button *appHasPermission="perms.financeAdjust" aria-label="Void credit note" (click)="voidCredit(c)"><mat-icon>block</mat-icon></button>
                      }
                    </span>
                  </li>
                }
                @for (r of v.refunds; track r.id) {
                  <li class="flex flex-wrap items-center justify-between gap-2 py-2">
                    <span><span class="font-mono text-xs">{{ r.refundNumber }}</span> · {{ r.paidOn | date: 'mediumDate': 'UTC' }} · {{ methodLabel[r.method] }}{{ r.reference ? ' · ' + r.reference : '' }}</span>
                    <span class="font-semibold">{{ r.amount | currency: r.currencyCode : 'code' : '1.2-2' }} refunded</span>
                  </li>
                }
              </ul>
            </div>
          }
          <div class="tn-card tn-card-body">
            <h3 class="tn-section-title">History</h3>
            <ol class="tn-timeline">
              @for (h of v.history; track h.id) {
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

        <app-record-payment-dialog [(open)]="paymentOpen" [customerId]="v.customerId" [presetInvoice]="v" (saved)="load()" />

        <ui-modal [(open)]="creditOpen" title="Issue credit note" size="sm">
          <div class="space-y-3">
            <p class="text-xs text-fleet-text-muted">Up to {{ v.total - v.credited | currency: v.currencyCode : 'code' : '1.2-2' }} can be credited. If the customer already paid, the credit becomes money owed back (refund it next).</p>
            <div class="tn-field"><label for="cn-amount" class="tn-required">Amount ({{ v.currencyCode }})</label>
              <input id="cn-amount" type="number" min="0" step="0.01" class="tn-input" [(ngModel)]="creditAmount" /></div>
            <div class="tn-field"><label for="cn-reason" class="tn-required">Reason</label>
              <textarea id="cn-reason" class="tn-input" rows="2" maxlength="500" [(ngModel)]="creditReason"></textarea></div>
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          </div>
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="creditOpen.set(false)">Cancel</button>
            <button mat-flat-button color="primary" [disabled]="busy() || !(creditAmount > 0) || !creditReason.trim()" (click)="issueCredit()">Issue credit note</button>
          </div>
        </ui-modal>

        <ui-modal [(open)]="refundOpen" title="Refund to customer" size="sm">
          <div class="space-y-3">
            <p class="text-xs text-fleet-text-muted">{{ abs(v.balance) | currency: v.currencyCode : 'code' : '1.2-2' }} is owed back on this invoice.</p>
            <div class="tn-grid">
              <div class="tn-field"><label for="rf-amount" class="tn-required">Amount ({{ v.currencyCode }})</label>
                <input id="rf-amount" type="number" min="0" step="0.01" class="tn-input" [(ngModel)]="refundAmount" /></div>
              <div class="tn-field"><label for="rf-date" class="tn-required">Paid on</label><input id="rf-date" type="date" class="tn-input" [(ngModel)]="refundDate" /></div>
              <div class="tn-field"><label for="rf-method">Method</label>
                <select id="rf-method" class="tn-input" [(ngModel)]="refundMethod">@for (m of methods; track m) { <option [value]="m">{{ methodLabel[m] }}</option> }</select></div>
              <div class="tn-field"><label for="rf-ref">Reference</label><input id="rf-ref" class="tn-input" maxlength="100" [(ngModel)]="refundReference" /></div>
            </div>
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          </div>
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="refundOpen.set(false)">Cancel</button>
            <button mat-flat-button color="primary" [disabled]="busy() || !(refundAmount > 0) || !refundDate" (click)="refund()">Record refund</button>
          </div>
        </ui-modal>

        <ui-modal [(open)]="voidOpen" title="Void invoice" size="sm">
          <div class="space-y-3">
            <p class="text-sm">Voiding cancels the invoice and frees its items to be invoiced again. It keeps its number.</p>
            <div class="tn-field"><label for="vd-reason" class="tn-required">Reason</label>
              <textarea id="vd-reason" class="tn-input" rows="2" maxlength="500" [(ngModel)]="voidReason"></textarea></div>
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          </div>
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="voidOpen.set(false)">Back</button>
            <button mat-flat-button color="warn" [disabled]="busy() || !voidReason.trim()" (click)="voidInvoice()">Void invoice</button>
          </div>
        </ui-modal>
      } @else {
        <p class="text-sm text-fleet-text-muted">Loading invoice…</p>
      }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelInvoiceDetailComponent {
  private readonly api = inject(TravelFinanceService);
  private readonly router = inject(Router);
  private readonly toast = inject(UiToastService);
  private readonly confirm = inject(UiConfirmService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly id = Number(inject(ActivatedRoute).snapshot.paramMap.get('id'));

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly statusVariant = INVOICE_STATUS_VARIANT;
  protected readonly payLabel = INVOICE_PAYMENT_STATUS_LABEL;
  protected readonly payVariant = INVOICE_PAYMENT_STATUS_VARIANT;
  protected readonly installmentVariant = INSTALLMENT_STATUS_VARIANT;
  protected readonly paymentStatusLabel = PAYMENT_STATUS_LABEL;
  protected readonly paymentStatusVariant = PAYMENT_STATUS_VARIANT;
  protected readonly methodLabel = PAYMENT_METHOD_LABEL;
  protected readonly methods = TRAVEL_PAYMENT_METHODS;
  protected readonly sourceLabel = SOURCE_TYPE_LABEL;
  protected readonly eventLabel = FINANCE_EVENT_LABEL;
  protected readonly utc = asUtc;

  protected readonly invoice = signal<TravelInvoice | null>(null);
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
    this.api.getInvoice(this.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: v => { this.invoice.set(v); this.error.set(null); },
      error: err => this.error.set(apiErrorMessage(err, 'Could not load the invoice.'))
    });
  }

  protected can(a: TravelInvoiceAction): boolean { return this.invoice()?.allowedActions.includes(a) ?? false; }
  protected abs(n: number): number { return Math.abs(n); }

  private run(obs: Observable<unknown>, ok: string, close?: () => void): void {
    this.busy.set(true);
    this.dialogError.set(null);
    obs.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); close?.(); this.toast.success(ok); this.load(); },
      error: err => { this.busy.set(false); const m = apiErrorMessage(err, 'The change was refused.'); if (close) this.dialogError.set(m); else this.toast.error(m); }
    });
  }

  protected issue(): void { this.run(this.api.issueInvoice(this.id, null, null), 'Invoice issued.'); }
  protected async remove(): Promise<void> {
    if (!(await this.confirm.confirmDelete('Delete this draft? Its items become billable again.'))) return;
    this.api.deleteInvoice(this.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.toast.success('Draft deleted.'); this.router.navigate(['/travel/finance']); },
      error: err => this.toast.error(apiErrorMessage(err, 'Could not delete the draft.'))
    });
  }
  protected openCredit(): void {
    const v = this.invoice()!;
    this.key = newRequestKey();
    this.creditAmount = round(v.total - v.credited);
    this.creditReason = '';
    this.dialogError.set(null);
    this.creditOpen.set(true);
  }
  protected issueCredit(): void {
    this.run(this.api.issueCreditNote({ idempotencyKey: this.key, invoiceId: this.id, amount: round(Number(this.creditAmount)), reason: this.creditReason.trim(),
      cancellationId: null, issuedOn: null }), 'Credit note issued.', () => this.creditOpen.set(false));
  }
  protected openRefund(): void {
    this.key = newRequestKey();
    this.refundAmount = round(Math.abs(this.invoice()!.balance));
    this.refundDate = new Date().toISOString().substring(0, 10);
    this.refundReference = '';
    this.dialogError.set(null);
    this.refundOpen.set(true);
  }
  protected refund(): void {
    this.run(this.api.recordRefund({ idempotencyKey: this.key, invoiceId: this.id, paymentId: null, amount: round(Number(this.refundAmount)), method: this.refundMethod,
      reference: this.refundReference.trim() || null, paidOn: this.refundDate, cancellationId: null, notes: null }), 'Refund recorded.', () => this.refundOpen.set(false));
  }
  protected voidInvoice(): void { this.run(this.api.voidInvoice(this.id, this.voidReason.trim()), 'Invoice voided.', () => this.voidOpen.set(false)); }
  protected async voidCredit(c: TravelCreditNote): Promise<void> {
    if (!(await this.confirm.open({ variant: 'warning', title: 'Void credit note', message: `Void ${c.creditNoteNumber}? The invoice balance goes back up.`, confirmText: 'Void' }))) return;
    this.run(this.api.voidCreditNote(c.id, 'Voided from the invoice'), 'Credit note voided.');
  }
  protected pdf(): void {
    const v = this.invoice()!;
    try { buildInvoicePdf(v).save(`${v.invoiceNumber ?? 'draft-invoice-' + v.id}.pdf`); } catch { this.toast.error('Could not build the PDF.'); }
  }
  protected creditPdf(c: TravelCreditNote): void {
    try { buildCreditNotePdf(c).save(`${c.creditNoteNumber}.pdf`); } catch { this.toast.error('Could not build the PDF.'); }
  }
}
