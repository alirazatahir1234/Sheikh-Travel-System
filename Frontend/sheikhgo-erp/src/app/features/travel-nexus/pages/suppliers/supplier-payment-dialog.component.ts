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
import { PAYMENT_METHOD_LABEL, TRAVEL_PAYMENT_METHODS, TravelPaymentMethod, TravelSupplier, TravelSupplierBillListItem } from '../../models/travel.models';
import { newRequestKey } from '../../services/travel-finance.service';
import { TravelSupplierFinanceService } from '../../services/travel-supplier-finance.service';
import { TravelSupplierService } from '../../services/travel-supplier.service';
import { round } from '../finance/finance-documents';
import { suggestBillAllocations } from './supplier-finance';

interface AllocationRow { bill: TravelSupplierBillListItem; selected: boolean; amount: number | null; rate: number | null; }

/** Pay a supplier and (optionally) apply the payment to their open bills. One request key per dialog: retries never double-post. */
@Component({
  selector: 'app-supplier-payment-dialog',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, FormsModule, MatButtonModule, MatIconModule, UiModalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-modal [(open)]="open" title="Pay supplier" size="lg">
      <div class="space-y-4">
        @if (!supplierId()) {
          <div class="tn-field"><label for="sp-supplier" class="tn-required">Supplier</label>
            <select id="sp-supplier" class="tn-input" [ngModel]="picked()" (ngModelChange)="picked.set($event)">
              <option [ngValue]="null">Choose…</option>
              @for (s of suppliers(); track s.id) { <option [ngValue]="s.id">{{ s.name }}</option> }
            </select></div>
        }
        <div class="tn-grid">
          <div class="tn-field"><label for="sp-amount" class="tn-required">Amount</label>
            <input id="sp-amount" type="number" min="0" step="0.01" class="tn-input" [(ngModel)]="amount" (ngModelChange)="touch()" /></div>
          <div class="tn-field"><label for="sp-cur" class="tn-required">Currency</label>
            <input id="sp-cur" class="tn-input uppercase" maxlength="3" [(ngModel)]="currency" (ngModelChange)="touch()" /></div>
          <div class="tn-field"><label for="sp-date" class="tn-required">Paid on</label>
            <input id="sp-date" type="date" class="tn-input" [(ngModel)]="paidOn" /></div>
          <div class="tn-field"><label for="sp-method">Method</label>
            <select id="sp-method" class="tn-input" [ngModel]="method" (ngModelChange)="setMethod($event)">
              @for (m of methods; track m) { <option [value]="m">{{ methodLabel[m] }}</option> }
            </select></div>
          <div class="tn-field"><label for="sp-ref">Reference</label>
            <input id="sp-ref" class="tn-input" maxlength="100" [(ngModel)]="reference" placeholder="Transfer id, cheque no.…" /></div>
          <label class="flex items-center gap-2 self-end pb-2 text-sm"><input type="checkbox" [(ngModel)]="cleared" /> Cleared (money has left the account)</label>
        </div>

        <section class="space-y-2">
          <div class="flex items-center justify-between">
            <h4 class="tn-section-title">Apply to bills</h4>
            @if (rows().length) { <button mat-button type="button" (click)="suggest()"><mat-icon>auto_fix_high</mat-icon> Suggest (oldest due first)</button> }
          </div>
          @if (loadingBills()) { <p class="text-sm text-fleet-text-muted">Loading open bills…</p> }
          @else if (!rows().length) { <p class="text-sm text-fleet-text-muted">No open bills — the payment is kept as an advance to the supplier.</p> }
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              @for (r of rows(); track r.bill.id) {
                <tr class="border-b border-fleet-border">
                  <td class="py-1.5"><input type="checkbox" [attr.aria-label]="'Apply to ' + r.bill.billNumber" [(ngModel)]="r.selected" (ngModelChange)="touch()" /></td>
                  <td class="py-1.5"><span class="font-mono text-xs">{{ r.bill.billNumber }}</span>
                    @if (r.bill.supplierReference) { <span class="text-xs text-fleet-text-muted"> · {{ r.bill.supplierReference }}</span> }
                    <span class="block text-xs text-fleet-text-muted">due {{ r.bill.dueDate | date: 'mediumDate': 'UTC' }}</span></td>
                  <td class="py-1.5 text-right">{{ openAmount(r.bill) | currency: r.bill.currencyCode : 'code' : '1.2-2' }} open</td>
                  <td class="py-1.5 pl-3"><input type="number" min="0" step="0.01" class="tn-input w-32" [attr.aria-label]="'Amount for ' + r.bill.billNumber"
                    [(ngModel)]="r.amount" (ngModelChange)="touch()" [disabled]="!r.selected" />
                    <span class="text-xs text-fleet-text-muted"> {{ currencyCode() }}</span></td>
                  <td class="py-1.5 pl-3">
                    @if (r.bill.currencyCode !== currencyCode()) {
                      <label class="text-xs">1 {{ currencyCode() }} =
                        <input type="number" min="0" step="0.000001" class="tn-input inline w-28" [attr.aria-label]="'Rate for ' + r.bill.billNumber"
                          [(ngModel)]="r.rate" (ngModelChange)="touch()" [disabled]="!r.selected" /> {{ r.bill.currencyCode }}</label>
                    }
                  </td>
                </tr>
              }
            </table>
          </div>
          <p class="text-xs" [class.text-red-600]="leftOver() < 0">Applied {{ used() | currency: currencyCode() || 'XXX' : 'code' : '1.2-2' }} ·
            advance {{ leftOver() | currency: currencyCode() || 'XXX' : 'code' : '1.2-2' }}</p>
        </section>
        <div class="tn-field"><label for="sp-notes">Notes</label><textarea id="sp-notes" class="tn-input" rows="2" maxlength="1000" [(ngModel)]="notes"></textarea></div>
        @if (error()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div> }
      </div>
      <div modal-footer class="tn-actions">
        <button mat-stroked-button (click)="open.set(false)">Cancel</button>
        <button mat-flat-button color="primary" [disabled]="busy() || !effectiveSupplier() || !(amount > 0) || currencyCode().length !== 3 || leftOver() < 0" (click)="save()">
          {{ busy() ? 'Saving…' : 'Record payment' }}</button>
      </div>
    </ui-modal>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class SupplierPaymentDialogComponent {
  private readonly api = inject(TravelSupplierFinanceService);
  private readonly supplierApi = inject(TravelSupplierService);
  private readonly toast = inject(UiToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly open = model(false);
  /** Fixed supplier (bill / supplier pages); otherwise a supplier select is shown. */
  readonly supplierId = input<number | null>(null);
  readonly presetBill = input<TravelSupplierBillListItem | null>(null);
  readonly saved = output<number>();

  protected readonly methods = TRAVEL_PAYMENT_METHODS;
  protected readonly methodLabel = PAYMENT_METHOD_LABEL;
  protected readonly suppliers = signal<TravelSupplier[]>([]);
  protected readonly picked = signal<number | null>(null);
  protected readonly rows = signal<AllocationRow[]>([]);
  protected readonly loadingBills = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  private readonly tick = signal(0);
  private key = newRequestKey();

  protected amount = 0;
  protected currency = '';
  protected paidOn = new Date().toISOString().substring(0, 10);
  protected method: TravelPaymentMethod = 'BankTransfer';
  protected reference = '';
  protected cleared = false;
  protected notes = '';

  protected readonly effectiveSupplier = computed(() => this.supplierId() ?? this.picked());
  protected readonly currencyCode = computed(() => { this.tick(); return (this.currency || '').trim().toUpperCase(); });
  protected readonly used = computed(() => { this.tick(); return round(this.rows().filter(r => r.selected).reduce((n, r) => n + (Number(r.amount) || 0), 0)); });
  protected readonly leftOver = computed(() => { this.tick(); return round((Number(this.amount) || 0) - this.used()); });

  constructor() {
    effect(() => {
      if (!this.open()) return;
      untracked(() => this.reset());
    });
    effect(() => {
      const id = this.effectiveSupplier();
      if (!this.open() || !id) return;
      untracked(() => this.loadBills(id));
    });
  }

  private reset(): void {
    this.key = newRequestKey();
    this.error.set(null);
    this.picked.set(null);
    this.rows.set([]);
    const preset = this.presetBill();
    this.amount = preset ? this.openAmount(preset) : 0;
    this.currency = preset?.currencyCode ?? '';
    this.paidOn = new Date().toISOString().substring(0, 10);
    this.method = 'BankTransfer';
    this.cleared = false;
    this.reference = '';
    this.notes = '';
    if (!this.supplierId() && !this.suppliers().length)
      this.supplierApi.getSuppliers(undefined, true).pipe(catchError(() => of([] as TravelSupplier[])), takeUntilDestroyed(this.destroyRef))
        .subscribe(list => this.suppliers.set(list));
    this.touch();
  }

  private loadBills(supplierId: number): void {
    this.loadingBills.set(true);
    this.api.getOpenBills(supplierId).pipe(catchError(() => of([] as TravelSupplierBillListItem[])), takeUntilDestroyed(this.destroyRef)).subscribe(list => {
      this.loadingBills.set(false);
      const preset = this.presetBill();
      if (!this.currency) this.currency = list[0]?.currencyCode ?? this.suppliers().find(s => s.id === supplierId)?.currencyCode ?? '';
      this.rows.set(list.map(b => ({ bill: b, selected: preset?.id === b.id, amount: preset?.id === b.id ? this.amount : null, rate: null })));
      this.touch();
    });
  }

  protected touch(): void { this.tick.update(n => n + 1); }
  protected openAmount(b: TravelSupplierBillListItem): number { return round(Math.max(0, b.balance - b.pendingPaid)); }
  protected setMethod(m: TravelPaymentMethod): void {
    this.method = m;
    this.cleared = m === 'Cash' || m === 'Card';
  }

  protected suggest(): void {
    const rates: Record<string, number | null> = {};
    for (const r of this.rows()) if (r.rate) rates[r.bill.currencyCode] = Number(r.rate);
    const plan = suggestBillAllocations(Number(this.amount) || 0, this.currencyCode(), this.rows().map(r => r.bill), rates);
    this.rows.update(rows => rows.map(r => {
      const p = plan.find(x => x.invoiceId === r.bill.id);
      return { ...r, selected: !!p, amount: p?.amountFromPayment ?? null, rate: p?.exchangeRate ?? r.rate };
    }));
    this.touch();
  }

  protected save(): void {
    const allocations = this.rows().filter(r => r.selected && Number(r.amount) > 0).map(r => ({
      invoiceId: r.bill.id, amountFromPayment: round(Number(r.amount)),
      exchangeRate: r.bill.currencyCode === this.currencyCode() ? null : (Number(r.rate) || null)
    }));
    this.busy.set(true);
    this.error.set(null);
    this.api.recordPayment({
      idempotencyKey: this.key, supplierId: this.effectiveSupplier()!, paidOn: this.paidOn, method: this.method, reference: this.reference.trim() || null,
      amount: round(Number(this.amount)), currencyCode: this.currencyCode(), cleared: this.cleared, notes: this.notes.trim() || null, allocations
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: id => { this.busy.set(false); this.open.set(false); this.toast.success('Supplier payment recorded.'); this.saved.emit(id); },
      error: err => { this.busy.set(false); this.error.set(apiErrorMessage(err, 'Could not record the payment.')); }
    });
  }
}
