import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, model, output, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { catchError, of } from 'rxjs';
import { UiModalComponent } from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  PAYMENT_METHOD_LABEL, TRAVEL_PAYMENT_METHODS, TravelCustomerListItem, TravelInvoiceListItem, TravelPaymentMethod
} from '../../models/travel.models';
import { TravelCustomerPickerComponent } from '../../components/customer-picker.component';
import { TravelFinanceService, newRequestKey } from '../../services/travel-finance.service';
import { round, suggestAllocations } from './finance-documents';

interface AllocationRow { invoice: TravelInvoiceListItem; selected: boolean; amount: number | null; rate: number | null; }

/** Record a receipt and (optionally) apply it to the customer's open invoices. One request key per dialog: retries never double-post. */
@Component({
  selector: 'app-record-payment-dialog',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, FormsModule, MatButtonModule, MatIconModule, UiModalComponent, TravelCustomerPickerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-modal [(open)]="open" title="Record payment" size="lg">
      <div class="space-y-4">
        @if (!customerId()) {
          <div class="tn-field"><label for="rp-customer" class="tn-required">Received from</label>
            <app-travel-customer-picker inputId="rp-customer" [(selected)]="picked" /></div>
        }
        <div class="tn-grid">
          <div class="tn-field"><label for="rp-amount" class="tn-required">Amount</label>
            <input id="rp-amount" type="number" min="0" step="0.01" class="tn-input" [(ngModel)]="amount" (ngModelChange)="touch()" /></div>
          <div class="tn-field"><label for="rp-cur" class="tn-required">Currency</label>
            <input id="rp-cur" class="tn-input uppercase" maxlength="3" [(ngModel)]="currency" (ngModelChange)="touch()" /></div>
          <div class="tn-field"><label for="rp-date" class="tn-required">Received on</label>
            <input id="rp-date" type="date" class="tn-input" [(ngModel)]="receivedOn" /></div>
          <div class="tn-field"><label for="rp-method">Method</label>
            <select id="rp-method" class="tn-input" [ngModel]="method" (ngModelChange)="setMethod($event)">
              @for (m of methods; track m) { <option [value]="m">{{ methodLabel[m] }}</option> }
            </select></div>
          <div class="tn-field"><label for="rp-ref">Reference</label>
            <input id="rp-ref" class="tn-input" maxlength="100" [(ngModel)]="reference" placeholder="Cheque no., transfer id…" /></div>
          <label class="flex items-center gap-2 self-end pb-2 text-sm"><input type="checkbox" [(ngModel)]="cleared" /> Cleared (money is in the account)</label>
        </div>
        @if (!cleared) { <p class="text-xs text-fleet-text-muted">An uncleared payment reserves the invoice amount but does not reduce the balance until you mark it cleared.</p> }

        <section class="space-y-2">
          <div class="flex items-center justify-between">
            <h4 class="tn-section-title">Apply to invoices</h4>
            @if (rows().length) { <button mat-button type="button" (click)="suggest()"><mat-icon>auto_fix_high</mat-icon> Suggest (oldest due first)</button> }
          </div>
          @if (loadingInvoices()) { <p class="text-sm text-fleet-text-muted">Loading open invoices…</p> }
          @else if (!rows().length) { <p class="text-sm text-fleet-text-muted">No open invoices — the payment is kept on account as an advance.</p> }
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              @for (r of rows(); track r.invoice.id) {
                <tr class="border-b border-fleet-border">
                  <td class="py-1.5"><input type="checkbox" [attr.aria-label]="'Apply to ' + r.invoice.invoiceNumber" [(ngModel)]="r.selected" (ngModelChange)="touch()" /></td>
                  <td class="py-1.5"><span class="font-mono text-xs">{{ r.invoice.invoiceNumber }}</span>
                    <span class="block text-xs text-fleet-text-muted">due {{ (r.invoice.nextDueDate ?? r.invoice.dueDate) | date: 'mediumDate': 'UTC' }}</span></td>
                  <td class="py-1.5 text-right">{{ openAmount(r.invoice) | currency: r.invoice.currencyCode : 'code' : '1.2-2' }} open</td>
                  <td class="py-1.5 pl-3"><input type="number" min="0" step="0.01" class="tn-input w-32" [attr.aria-label]="'Amount for ' + r.invoice.invoiceNumber"
                    [(ngModel)]="r.amount" (ngModelChange)="touch()" [disabled]="!r.selected" />
                    <span class="text-xs text-fleet-text-muted"> {{ currencyCode() }}</span></td>
                  <td class="py-1.5 pl-3">
                    @if (r.invoice.currencyCode !== currencyCode()) {
                      <label class="text-xs">1 {{ currencyCode() }} =
                        <input type="number" min="0" step="0.000001" class="tn-input inline w-28" [attr.aria-label]="'Rate for ' + r.invoice.invoiceNumber"
                          [(ngModel)]="r.rate" (ngModelChange)="touch()" [disabled]="!r.selected" /> {{ r.invoice.currencyCode }}</label>
                      @if (r.selected && r.amount && r.rate) { <span class="block text-xs">= {{ applied(r) | currency: r.invoice.currencyCode : 'code' : '1.2-2' }}</span> }
                    }
                  </td>
                </tr>
              }
            </table>
          </div>
          <p class="text-xs" [class.text-red-600]="leftOver() < 0">Applied {{ used() | currency: currencyCode() || 'XXX' : 'code' : '1.2-2' }} ·
            on account {{ leftOver() | currency: currencyCode() || 'XXX' : 'code' : '1.2-2' }}</p>
        </section>
        <div class="tn-field"><label for="rp-notes">Notes</label><textarea id="rp-notes" class="tn-input" rows="2" maxlength="1000" [(ngModel)]="notes"></textarea></div>
        @if (error()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div> }
      </div>
      <div modal-footer class="tn-actions">
        <button mat-stroked-button (click)="open.set(false)">Cancel</button>
        <button mat-flat-button color="primary" [disabled]="busy() || !effectiveCustomer() || !(amount > 0) || currencyCode().length !== 3 || leftOver() < 0" (click)="save()">
          {{ busy() ? 'Saving…' : 'Record payment' }}</button>
      </div>
    </ui-modal>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class RecordPaymentDialogComponent {
  private readonly api = inject(TravelFinanceService);
  private readonly toast = inject(UiToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly open = model(false);
  /** Fixed customer (invoice / customer pages); otherwise a picker is shown. */
  readonly customerId = input<number | null>(null);
  readonly presetInvoice = input<TravelInvoiceListItem | null>(null);
  readonly saved = output<number>();

  protected readonly methods = TRAVEL_PAYMENT_METHODS;
  protected readonly methodLabel = PAYMENT_METHOD_LABEL;
  protected readonly picked = signal<TravelCustomerListItem | null>(null);
  protected readonly rows = signal<AllocationRow[]>([]);
  protected readonly loadingInvoices = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  private readonly tick = signal(0);
  private key = newRequestKey();

  protected amount = 0;
  protected currency = '';
  protected receivedOn = new Date().toISOString().substring(0, 10);
  protected method: TravelPaymentMethod = 'Cash';
  protected reference = '';
  protected cleared = true;
  protected notes = '';

  protected readonly effectiveCustomer = computed(() => this.customerId() ?? this.picked()?.customerId ?? null);
  protected readonly currencyCode = computed(() => { this.tick(); return (this.currency || '').trim().toUpperCase(); });
  protected readonly used = computed(() => { this.tick(); return round(this.rows().filter(r => r.selected).reduce((n, r) => n + (Number(r.amount) || 0), 0)); });
  protected readonly leftOver = computed(() => { this.tick(); return round((Number(this.amount) || 0) - this.used()); });

  constructor() {
    effect(() => {
      if (!this.open()) return;
      untracked(() => this.reset());
    });
    effect(() => {
      const id = this.effectiveCustomer();
      if (!this.open() || !id) return;
      untracked(() => this.loadInvoices(id));
    });
  }

  private reset(): void {
    this.key = newRequestKey();
    this.error.set(null);
    this.picked.set(null);
    this.rows.set([]);
    const preset = this.presetInvoice();
    this.amount = preset ? round(Math.max(0, preset.balance - preset.pendingPaid)) : 0;
    this.currency = preset?.currencyCode ?? '';
    this.receivedOn = new Date().toISOString().substring(0, 10);
    this.method = 'Cash';
    this.cleared = true;
    this.reference = '';
    this.notes = '';
    this.touch();
  }

  private loadInvoices(customerId: number): void {
    this.loadingInvoices.set(true);
    this.api.getOpenInvoices(customerId).pipe(catchError(() => of([] as TravelInvoiceListItem[])), takeUntilDestroyed(this.destroyRef)).subscribe(list => {
      this.loadingInvoices.set(false);
      const preset = this.presetInvoice();
      if (!this.currency && list.length) this.currency = list[0].currencyCode;
      this.rows.set(list.map(i => ({ invoice: i, selected: preset?.id === i.id, amount: preset?.id === i.id ? this.amount : null, rate: null })));
      this.touch();
    });
  }

  protected touch(): void { this.tick.update(n => n + 1); }
  protected openAmount(i: TravelInvoiceListItem): number { return round(Math.max(0, i.balance - i.pendingPaid)); }
  protected applied(r: AllocationRow): number { return round((Number(r.amount) || 0) * (Number(r.rate) || 0)); }
  protected setMethod(m: TravelPaymentMethod): void {
    this.method = m;
    this.cleared = m === 'Cash' || m === 'Card';
  }

  protected suggest(): void {
    const rates: Record<string, number | null> = {};
    for (const r of this.rows()) if (r.rate) rates[r.invoice.currencyCode] = Number(r.rate);
    const plan = suggestAllocations(Number(this.amount) || 0, this.currencyCode(), this.rows().map(r => r.invoice), rates);
    this.rows.update(rows => rows.map(r => {
      const p = plan.find(x => x.invoiceId === r.invoice.id);
      return { ...r, selected: !!p, amount: p?.amountFromPayment ?? null, rate: p?.exchangeRate ?? r.rate };
    }));
    this.touch();
  }

  protected save(): void {
    const allocations = this.rows().filter(r => r.selected && Number(r.amount) > 0).map(r => ({
      invoiceId: r.invoice.id, amountFromPayment: round(Number(r.amount)),
      exchangeRate: r.invoice.currencyCode === this.currencyCode() ? null : (Number(r.rate) || null)
    }));
    this.busy.set(true);
    this.error.set(null);
    this.api.recordPayment({
      idempotencyKey: this.key, customerId: this.effectiveCustomer()!, receivedOn: this.receivedOn, method: this.method,
      reference: this.reference.trim() || null, amount: round(Number(this.amount)), currencyCode: this.currencyCode(), cleared: this.cleared,
      notes: this.notes.trim() || null, allocations
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: id => { this.busy.set(false); this.open.set(false); this.toast.success('Payment recorded.'); this.saved.emit(id); },
      error: err => { this.busy.set(false); this.error.set(apiErrorMessage(err, 'Could not record the payment.')); }
    });
  }
}
