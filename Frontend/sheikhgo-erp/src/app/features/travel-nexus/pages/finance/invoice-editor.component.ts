import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, catchError, map, of, switchMap } from 'rxjs';
import { UiPageHeaderComponent } from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  SOURCE_TYPE_LABEL, SaveTravelInvoice, TravelBillableItem, TravelCustomerListItem, TravelInvoiceSourceType, TravelLookupItem, TravelSubAgent
} from '../../models/travel.models';
import { TravelCustomerPickerComponent } from '../../components/customer-picker.component';
import { TravelCommissionService } from '../../services/travel-commission.service';
import { TravelConfigService } from '../../services/travel-config.service';
import { TravelCustomerService } from '../../services/travel-customer.service';
import { TravelFinanceService } from '../../services/travel-finance.service';
import { groupBillable, round } from './finance-documents';

interface LineRow { description: string; quantity: number; unitPrice: number; sourceType: TravelInvoiceSourceType | null; sourceId: number | null; sourceAmount: number | null; }
interface InstallmentRow { dueDate: string; amount: number; }

/** Splits a total into n installments; the rounding difference goes on the last one. */
export function splitEvenly(total: number, count: number, firstDue: string, monthsApart = 1): InstallmentRow[] {
  if (count < 1 || total <= 0) return [];
  const part = Math.floor((total / count) * 100) / 100;
  const rows: InstallmentRow[] = [];
  const start = new Date(`${firstDue}T00:00:00Z`);
  for (let i = 0; i < count; i++) {
    // Same day each month, clamped to the month's last day (31 Jan → 28 Feb → 31 Mar).
    const y = start.getUTCFullYear(), m = start.getUTCMonth() + i * monthsApart;
    const lastDay = new Date(Date.UTC(y, m + 1, 0)).getUTCDate();
    const d = new Date(Date.UTC(y, m, Math.min(start.getUTCDate(), lastDay)));
    rows.push({ dueDate: d.toISOString().substring(0, 10), amount: i === count - 1 ? round(total - part * (count - 1)) : part });
  }
  return rows;
}

@Component({
  selector: 'app-travel-invoice-editor',
  standalone: true,
  imports: [CurrencyPipe, FormsModule, MatButtonModule, MatIconModule, UiPageHeaderComponent, TravelCustomerPickerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      <ui-page-header [title]="invoiceId ? 'Edit draft invoice' : 'New invoice'" eyebrow="TravelNexus · Invoices & Payments" icon="receipt_long"
        subtitle="Pick sold items (each can be invoiced once), add manual lines, set installments, then issue."
        [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Invoices & Payments', route: '/travel/finance' }, { label: invoiceId ? 'Edit draft' : 'New invoice' }]">
      </ui-page-header>
      @if (loadError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ loadError() }}</div> }

      <div class="grid gap-5 lg:grid-cols-3">
        <div class="tn-card tn-card-body space-y-4">
          <h3 class="tn-section-title">Bill to</h3>
          @if (customerName()) {
            <p class="text-sm font-semibold">{{ customerName() }}</p>
          } @else {
            <div class="tn-field"><label for="inv-customer" class="tn-required">Customer</label>
              <app-travel-customer-picker inputId="inv-customer" [(selected)]="picked" /></div>
          }
          <div class="tn-field"><label for="inv-cur" class="tn-required">Currency</label>
            @if (invoiceId) { <p class="font-mono text-sm">{{ currency() }}</p> }
            @else {
              <select id="inv-cur" class="tn-input" [ngModel]="currency()" (ngModelChange)="setCurrency($event)">
                <option value="">Choose…</option>
                @for (c of currencyOptions(); track c) { <option [value]="c">{{ c }}</option> }
              </select>
              <span class="tn-hint">Items in another currency go on a separate invoice.</span>
            }
          </div>
          <div class="tn-grid">
            <div class="tn-field"><label for="inv-issue">Issue date</label><input id="inv-issue" type="date" class="tn-input" [(ngModel)]="issueDate" /></div>
            <div class="tn-field"><label for="inv-due">Due date</label><input id="inv-due" type="date" class="tn-input" [(ngModel)]="dueDate" [disabled]="installments().length > 0" />
              <span class="tn-hint">{{ installments().length ? 'Taken from the last installment.' : 'Empty = issue date + ' + terms() + ' days.' }}</span></div>
          </div>
          <div class="tn-field"><label for="inv-notes">Notes (printed)</label><textarea id="inv-notes" class="tn-input" rows="3" maxlength="2000" [(ngModel)]="notes"></textarea></div>
          <h3 class="tn-section-title">Commission</h3>
          <div class="tn-grid">
            <div class="tn-field"><label for="inv-agent">Staff agent</label>
              <select id="inv-agent" class="tn-input" [(ngModel)]="agentUserId"><option [ngValue]="null">{{ invoiceId ? 'Keep current' : 'Default (file / customer agent)' }}</option>
                @for (a of agents(); track a.id) { <option [ngValue]="a.id">{{ a.name }}</option> }</select></div>
            <div class="tn-field"><label for="inv-sub">Sub-agent</label>
              <select id="inv-sub" class="tn-input" [(ngModel)]="subAgentId"><option [ngValue]="null">{{ invoiceId ? 'Keep current' : 'Default (customer referred by)' }}</option>
                @for (a of subAgents(); track a.id) { <option [ngValue]="a.id">{{ a.name }}</option> }</select></div>
          </div>
          <span class="tn-hint">Clearing an agent is done from the invoice page (Commissions permission).</span>
        </div>

        <div class="tn-card lg:col-span-2">
          <div class="border-b border-fleet-border px-5 py-4"><h3 class="tn-section-title">Billable items</h3>
            <p class="text-xs text-fleet-text-muted">Sold and not yet on any invoice{{ currency() ? ' · ' + currency() : '' }}.</p></div>
          @if (!effectiveCustomer()) { <p class="px-5 py-6 text-sm text-fleet-text-muted">Choose the customer first.</p> }
          @else if (!grouped().length) { <p class="px-5 py-6 text-sm text-fleet-text-muted">Nothing left to invoice{{ currency() ? ' in ' + currency() : '' }} — add manual lines below.</p> }
          @for (g of grouped(); track g.currency) {
            @for (ctx of g.contexts; track ctx.label) {
              <div class="border-b border-fleet-border px-5 py-3">
                <p class="text-xs font-bold uppercase tracking-wider text-fleet-text-muted">{{ ctx.type === 'VisaCase' ? 'Visa case' : ctx.type }} · {{ ctx.label }}</p>
                <ul class="mt-1 space-y-1">
                  @for (i of ctx.items; track i.sourceType + i.sourceId) {
                    <li class="flex items-center gap-3 text-sm">
                      <input type="checkbox" [id]="'bi-' + i.sourceType + i.sourceId" [checked]="isPicked(i)" (change)="toggle(i)" />
                      <label [for]="'bi-' + i.sourceType + i.sourceId" class="flex-1">{{ i.description }}
                        <span class="text-xs text-fleet-text-muted">· {{ sourceLabel[i.sourceType] }}</span></label>
                      <span class="font-semibold">{{ i.amount | currency: i.currencyCode : 'code' : '1.2-2' }}</span>
                    </li>
                  }
                </ul>
              </div>
            }
          }
        </div>
      </div>

      <div class="tn-card">
        <div class="flex items-center justify-between border-b border-fleet-border px-5 py-4">
          <h3 class="tn-section-title">Lines</h3>
          <button mat-stroked-button (click)="addManual()"><mat-icon>add</mat-icon> Manual line</button>
        </div>
        <div class="overflow-x-auto">
          <table class="w-full min-w-[760px] text-sm">
            <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
              <th class="px-4 py-2 text-left">Description</th><th class="px-4 py-2 text-right">Qty</th><th class="px-4 py-2 text-right">Unit price</th>
              <th class="px-4 py-2 text-right">Amount</th><th class="px-4 py-2"></th></tr></thead>
            <tbody class="divide-y divide-fleet-border">
              @if (!lines().length) { <tr><td colspan="5" class="px-4 py-6 text-center text-fleet-text-muted">Pick items above or add a manual line.</td></tr> }
              @for (l of lines(); track $index; let i = $index) {
                <tr>
                  <td class="px-4 py-2"><input class="tn-input" maxlength="300" [attr.aria-label]="'Description ' + (i + 1)" [(ngModel)]="l.description" (ngModelChange)="touch()" />
                    @if (l.sourceType) {
                      <span class="text-xs text-fleet-text-muted">{{ sourceLabel[l.sourceType] }}
                        @if (l.sourceAmount !== null && l.sourceAmount !== l.unitPrice) { · sold at {{ l.sourceAmount | currency: currency() : 'code' : '1.2-2' }} }</span>
                    }</td>
                  <td class="px-4 py-2 text-right"><input type="number" min="0.01" step="0.01" class="tn-input w-20 text-right" [attr.aria-label]="'Quantity ' + (i + 1)"
                    [(ngModel)]="l.quantity" (ngModelChange)="touch()" [disabled]="!!l.sourceType" /></td>
                  <td class="px-4 py-2 text-right"><input type="number" min="0" step="0.01" class="tn-input w-32 text-right" [attr.aria-label]="'Unit price ' + (i + 1)"
                    [(ngModel)]="l.unitPrice" (ngModelChange)="touch()" /></td>
                  <td class="px-4 py-2 text-right font-semibold">{{ lineTotal(l) | currency: currency() || 'XXX' : 'code' : '1.2-2' }}</td>
                  <td class="px-4 py-2 text-right"><button mat-icon-button [attr.aria-label]="'Remove line ' + (i + 1)" (click)="removeLine(i)"><mat-icon>delete_outline</mat-icon></button></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <div class="flex flex-col items-end gap-1 px-5 py-4 text-sm">
          <p>Subtotal <strong class="ml-3">{{ subtotal() | currency: currency() || 'XXX' : 'code' : '1.2-2' }}</strong></p>
          <label class="flex items-center gap-3">Discount
            <input type="number" min="0" step="0.01" class="tn-input w-32 text-right" [(ngModel)]="discount" (ngModelChange)="touch()" /></label>
          <p class="text-base">Total <strong class="ml-3">{{ total() | currency: currency() || 'XXX' : 'code' : '1.2-2' }}</strong></p>
        </div>
      </div>

      <div class="tn-card tn-card-body space-y-3">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <div><h3 class="tn-section-title">Installments</h3>
            <p class="text-xs text-fleet-text-muted">Optional. They must add up to the total; payments cover them oldest first.</p></div>
          <div class="flex flex-wrap items-center gap-2">
            <label class="text-xs">Split into <input type="number" min="2" max="24" class="tn-input inline w-16" [(ngModel)]="splitCount" /> monthly from
              <input type="date" class="tn-input inline w-40" [(ngModel)]="splitFrom" /></label>
            <button mat-stroked-button [disabled]="!(total() > 0) || !splitFrom" (click)="split()">Split evenly</button>
            <button mat-button (click)="addInstallment()"><mat-icon>add</mat-icon> Row</button>
          </div>
        </div>
        @for (r of installments(); track $index; let i = $index) {
          <div class="flex items-end gap-3">
            <div class="tn-field w-44"><label [for]="'ins-d-' + i">Due</label><input [id]="'ins-d-' + i" type="date" class="tn-input" [(ngModel)]="r.dueDate" /></div>
            <div class="tn-field w-40"><label [for]="'ins-a-' + i">Amount</label><input [id]="'ins-a-' + i" type="number" min="0" step="0.01" class="tn-input" [(ngModel)]="r.amount" (ngModelChange)="touch()" /></div>
            <button mat-icon-button [attr.aria-label]="'Remove installment ' + (i + 1)" (click)="removeInstallment(i)"><mat-icon>delete_outline</mat-icon></button>
          </div>
        }
        @if (installments().length) {
          <p class="text-xs" [class.text-red-600]="installmentGap() !== 0">Installments {{ installmentSum() | currency: currency() || 'XXX' : 'code' : '1.2-2' }}
            @if (installmentGap() !== 0) { — {{ installmentGap() > 0 ? 'short by' : 'over by' }} {{ abs(installmentGap()) | currency: currency() || 'XXX' : 'code' : '1.2-2' }} }</p>
        }
      </div>

      @if (error()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div> }
      <div class="tn-actions">
        <button mat-stroked-button (click)="back()">Cancel</button>
        <button mat-stroked-button [disabled]="!canSave()" (click)="save(false)">{{ busy() ? 'Saving…' : 'Save draft' }}</button>
        <button mat-flat-button color="primary" [disabled]="!canSave()" (click)="save(true)">Save and issue</button>
      </div>
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelInvoiceEditorComponent {
  private readonly api = inject(TravelFinanceService);
  private readonly customers = inject(TravelCustomerService);
  private readonly router = inject(Router);
  private readonly toast = inject(UiToastService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);

  protected readonly sourceLabel = SOURCE_TYPE_LABEL;
  protected readonly invoiceId = Number(this.route.snapshot.paramMap.get('id')) || null;
  private readonly presetCustomer = Number(this.route.snapshot.queryParamMap.get('customerId')) || null;

  protected readonly picked = signal<TravelCustomerListItem | null>(null);
  protected readonly fixedCustomer = signal<{ id: number; name: string } | null>(null);
  protected readonly currency = signal((this.route.snapshot.queryParamMap.get('currency') ?? '').toUpperCase());
  protected readonly billable = signal<TravelBillableItem[]>([]);
  protected readonly lines = signal<LineRow[]>([]);
  protected readonly installments = signal<InstallmentRow[]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly agents = toSignal(inject(TravelConfigService).getLookups().pipe(map(l => l.agents), catchError(() => of([] as TravelLookupItem[]))),
    { initialValue: [] as TravelLookupItem[] });
  protected readonly subAgents = toSignal(inject(TravelCommissionService).getSubAgents(true).pipe(catchError(() => of([] as TravelSubAgent[]))),
    { initialValue: [] as TravelSubAgent[] });
  protected readonly terms = toSignal(inject(TravelConfigService).getSettings().pipe(map(s => s.defaultPaymentTermsDays ?? 7), catchError(() => of(7))), { initialValue: 7 });
  private readonly tick = signal(0);

  protected issueDate = '';
  protected dueDate = '';
  protected discount = 0;
  protected notes = '';
  protected agentUserId: number | null = null;
  protected subAgentId: number | null = null;
  protected splitCount = 2;
  protected splitFrom = '';

  protected readonly effectiveCustomer = computed(() => this.fixedCustomer()?.id ?? this.picked()?.customerId ?? null);
  protected readonly customerName = computed(() => this.fixedCustomer()?.name ?? null);
  protected readonly currencyOptions = computed(() => {
    const set = new Set(this.billable().map(b => b.currencyCode));
    if (this.currency()) set.add(this.currency());
    return [...set].sort();
  });
  protected readonly grouped = computed(() => groupBillable(this.billable().filter(b => !this.currency() || b.currencyCode === this.currency())));
  protected readonly subtotal = computed(() => { this.tick(); return round(this.lines().reduce((n, l) => n + this.lineTotal(l), 0)); });
  protected readonly total = computed(() => { this.tick(); return round(this.subtotal() - (Number(this.discount) || 0)); });
  protected readonly installmentSum = computed(() => { this.tick(); return round(this.installments().reduce((n, r) => n + (Number(r.amount) || 0), 0)); });
  protected readonly installmentGap = computed(() => round(this.total() - this.installmentSum()));
  protected readonly canSave = computed(() => { this.tick(); return !this.busy() && !!this.effectiveCustomer() && this.currency().length === 3 && this.lines().length > 0 && this.total() >= 0
    && (this.installments().length === 0 || this.installmentGap() === 0); });

  constructor() {
    if (this.invoiceId) this.loadDraft(this.invoiceId);
    else if (this.presetCustomer) {
      this.customers.getCustomer(this.presetCustomer).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: c => this.fixedCustomer.set({ id: c.customerId, name: c.fullName }),
        error: err => this.loadError.set(apiErrorMessage(err, 'Could not load the customer.'))
      });
    }
    effect(() => {
      const customerId = this.effectiveCustomer();
      if (!customerId) return;
      untracked(() => this.loadBillable(customerId));
    });
  }

  private loadDraft(id: number): void {
    this.api.getInvoice(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: inv => {
        if (inv.status !== 'Draft') { this.loadError.set('Only drafts can be edited.'); return; }
        this.currency.set(inv.currencyCode);
        this.issueDate = inv.issueDate?.substring(0, 10) ?? '';
        this.dueDate = inv.dueDate?.substring(0, 10) ?? '';
        this.discount = inv.discountAmount;
        this.notes = inv.notes ?? '';
        this.agentUserId = inv.agentUserId;
        this.subAgentId = inv.subAgentId;
        this.lines.set(inv.lines.map(l => ({ description: l.description, quantity: l.quantity, unitPrice: l.unitPrice, sourceType: l.sourceType,
          sourceId: l.sourceId, sourceAmount: l.currentSourceAmount })));
        this.installments.set(inv.installments.map(i => ({ dueDate: i.dueDate.substring(0, 10), amount: i.amount })));
        this.fixedCustomer.set({ id: inv.customerId, name: inv.customerName });
        this.touch();
      },
      error: err => this.loadError.set(apiErrorMessage(err, 'Could not load the invoice.'))
    });
  }

  private loadBillable(customerId: number): void {
    this.api.getBillable(customerId, null, this.invoiceId).pipe(catchError(() => of([] as TravelBillableItem[])), takeUntilDestroyed(this.destroyRef))
      .subscribe(items => {
        this.billable.set(items);
        if (!this.currency() && items.length) this.currency.set(items[0].currencyCode);
      });
  }

  protected touch(): void { this.tick.update(n => n + 1); }
  protected abs(n: number): number { return Math.abs(n); }
  protected lineTotal(l: LineRow): number { return round((Number(l.quantity) || 0) * (Number(l.unitPrice) || 0)); }
  protected isPicked(i: TravelBillableItem): boolean { return this.lines().some(l => l.sourceType === i.sourceType && l.sourceId === i.sourceId); }

  protected setCurrency(c: string): void {
    if (c !== this.currency() && this.lines().some(l => l.sourceType)) {
      this.lines.update(ls => ls.filter(l => !l.sourceType));
      this.toast.info('Items in the previous currency were removed from the lines.');
    }
    this.currency.set(c);
    this.touch();
  }

  protected toggle(i: TravelBillableItem): void {
    if (this.isPicked(i)) this.lines.update(ls => ls.filter(l => !(l.sourceType === i.sourceType && l.sourceId === i.sourceId)));
    else this.lines.update(ls => [...ls, { description: i.description, quantity: 1, unitPrice: i.amount, sourceType: i.sourceType, sourceId: i.sourceId, sourceAmount: i.amount }]);
    this.touch();
  }

  protected addManual(): void { this.lines.update(ls => [...ls, { description: '', quantity: 1, unitPrice: 0, sourceType: null, sourceId: null, sourceAmount: null }]); this.touch(); }
  protected removeLine(i: number): void { this.lines.update(ls => ls.filter((_, j) => j !== i)); this.touch(); }
  protected addInstallment(): void { this.installments.update(r => [...r, { dueDate: '', amount: 0 }]); this.touch(); }
  protected removeInstallment(i: number): void { this.installments.update(r => r.filter((_, j) => j !== i)); this.touch(); }
  protected split(): void { this.installments.set(splitEvenly(this.total(), Number(this.splitCount) || 2, this.splitFrom)); this.touch(); }

  protected save(issue: boolean): void {
    if (this.installments().some(r => !r.dueDate || !(Number(r.amount) > 0))) { this.error.set('Every installment needs a due date and an amount.'); return; }
    const dto: SaveTravelInvoice = {
      issueDate: this.issueDate || null, dueDate: this.installments().length ? null : this.dueDate || null, discountAmount: round(Number(this.discount) || 0),
      notes: this.notes.trim() || null,
      lines: this.lines().map(l => ({ description: l.description.trim(), quantity: Number(l.quantity), unitPrice: round(Number(l.unitPrice)),
        sourceType: l.sourceType, sourceId: l.sourceId })),
      installments: this.installments().map(r => ({ dueDate: r.dueDate, amount: round(Number(r.amount)) })),
      agentUserId: this.agentUserId, subAgentId: this.subAgentId
    };
    this.busy.set(true);
    this.error.set(null);
    const save$: Observable<number> = this.invoiceId
      ? this.api.updateInvoice(this.invoiceId, dto).pipe(map(() => this.invoiceId!))
      : this.api.createInvoice({ ...dto, customerId: this.effectiveCustomer()!, currencyCode: this.currency() });
    save$.pipe(
      // If issuing fails the draft is already saved: open it rather than saving a second draft on retry.
      switchMap(id => issue
        ? this.api.issueInvoice(id, dto.issueDate, dto.dueDate).pipe(
            map(number => ({ id, number: number as string | null, issueError: null as string | null })),
            catchError(err => of({ id, number: null as string | null, issueError: apiErrorMessage(err, 'The invoice could not be issued.') })))
        : of({ id, number: null as string | null, issueError: null as string | null })),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: r => {
        this.busy.set(false);
        if (r.issueError) this.toast.error(`Draft saved, but not issued: ${r.issueError}`);
        else this.toast.success(r.number ? `Invoice ${r.number} issued.` : 'Draft saved.');
        this.router.navigate(['/travel/finance/invoices', r.id]);
      },
      error: err => { this.busy.set(false); this.error.set(apiErrorMessage(err, 'Could not save the invoice.')); }
    });
  }

  protected back(): void { this.router.navigate(this.invoiceId ? ['/travel/finance/invoices', this.invoiceId] : ['/travel/finance']); }
}
