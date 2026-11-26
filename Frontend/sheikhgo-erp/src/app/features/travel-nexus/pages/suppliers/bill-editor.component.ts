import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, catchError, map, of, switchMap } from 'rxjs';
import { UiPageHeaderComponent } from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  SaveTravelSupplierBill, TravelBillableCost, TravelContextType, TravelSupplier, TravelSupplierBillSourceType
} from '../../models/travel.models';
import { TravelContextPickerComponent, TravelContextRef } from '../../components/context-picker.component';
import { TravelSupplierFinanceService } from '../../services/travel-supplier-finance.service';
import { TravelSupplierService } from '../../services/travel-supplier.service';
import { round } from '../finance/finance-documents';
import { billTotals, groupBillableCosts } from './supplier-finance';

interface LineRow {
  description: string; sourceType: TravelSupplierBillSourceType | null; sourceId: number | null; context: TravelContextRef | null;
  amount: number | null; commissionAmount: number | null; suggested: number | null;
}

/** Supplier bill: pick the supplier's unbilled services (commission pre-filled from its rules) and add manual lines tagged to a file / case / group. */
@Component({
  selector: 'app-travel-bill-editor',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, FormsModule, MatButtonModule, MatIconModule, UiPageHeaderComponent, TravelContextPickerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      <ui-page-header [title]="billId ? 'Edit draft bill' : 'New supplier bill'" eyebrow="TravelNexus · Suppliers" icon="request_quote"
        subtitle="Enter the supplier's invoice: each service's cost is billed once; supplier commission is netted off what you pay."
        [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Suppliers', route: '/travel/suppliers' }, { label: billId ? 'Edit draft' : 'New bill' }]">
      </ui-page-header>
      @if (loadError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ loadError() }}</div> }

      <div class="grid gap-5 lg:grid-cols-3">
        <div class="tn-card tn-card-body space-y-4">
          <h3 class="tn-section-title">Supplier invoice</h3>
          <div class="tn-field"><label for="bill-supplier" class="tn-required">Supplier</label>
            @if (fixedSupplier()) { <p class="text-sm font-semibold">{{ supplierName() }}</p> }
            @else {
              <select id="bill-supplier" class="tn-input" [ngModel]="supplierId()" (ngModelChange)="setSupplier($event)">
                <option [ngValue]="null">Choose…</option>
                @for (s of suppliers(); track s.id) { <option [ngValue]="s.id">{{ s.name }}</option> }
              </select>
            }
          </div>
          <div class="tn-field"><label for="bill-cur" class="tn-required">Currency</label>
            @if (billId) { <p class="font-mono text-sm">{{ currency() }}</p> }
            @else {
              <select id="bill-cur" class="tn-input" [ngModel]="currency()" (ngModelChange)="setCurrency($event)">
                <option value="">Choose…</option>
                @for (c of currencyOptions(); track c) { <option [value]="c">{{ c }}</option> }
              </select>
              <input class="tn-input mt-1 uppercase" maxlength="3" aria-label="Other currency" placeholder="Other (ISO code)"
                [ngModel]="otherCurrency" (ngModelChange)="otherCurrency = $event" (blur)="useOther()" />
            }
          </div>
          <div class="tn-field"><label for="bill-ref">Their invoice number</label>
            <input id="bill-ref" class="tn-input" maxlength="60" [(ngModel)]="supplierReference" placeholder="e.g. INV-8812" />
            <span class="tn-hint">The same supplier invoice cannot be entered twice.</span></div>
          <div class="tn-grid">
            <div class="tn-field"><label for="bill-date">Bill date</label><input id="bill-date" type="date" class="tn-input" [(ngModel)]="billDate" (change)="reloadBillable()" /></div>
            <div class="tn-field"><label for="bill-due">Due date</label><input id="bill-due" type="date" class="tn-input" [(ngModel)]="dueDate" />
              <span class="tn-hint">Empty = bill date + payment terms.</span></div>
          </div>
          <div class="tn-field"><label for="bill-notes">Notes</label><textarea id="bill-notes" class="tn-input" rows="3" maxlength="2000" [(ngModel)]="notes"></textarea></div>
        </div>

        <div class="tn-card lg:col-span-2">
          <div class="border-b border-fleet-border px-5 py-4"><h3 class="tn-section-title">Services to bill</h3>
            <p class="text-xs text-fleet-text-muted">This supplier's flights and hotels with a cost, not yet on a bill{{ currency() ? ' · ' + currency() : '' }}.
              Commission is suggested from the supplier's rules on the bill date.</p></div>
          @if (!supplierId()) { <p class="px-5 py-6 text-sm text-fleet-text-muted">Choose the supplier first.</p> }
          @else if (!grouped().length) { <p class="px-5 py-6 text-sm text-fleet-text-muted">Nothing left to bill{{ currency() ? ' in ' + currency() : '' }} — add manual lines below.</p> }
          @for (g of grouped(); track g.bookingId) {
            <div class="border-b border-fleet-border px-5 py-3">
              <p class="text-xs font-bold uppercase tracking-wider text-fleet-text-muted">Booking file · {{ g.label }}</p>
              <ul class="mt-1 space-y-1">
                @for (i of g.items; track i.sourceType + i.sourceId) {
                  <li class="flex items-center gap-3 text-sm">
                    <input type="checkbox" [id]="'bc-' + i.sourceType + i.sourceId" [checked]="isPicked(i)" (change)="toggle(i)" />
                    <label [for]="'bc-' + i.sourceType + i.sourceId" class="flex-1">{{ i.description }}
                      @if (i.serviceDate) { <span class="text-xs text-fleet-text-muted">· {{ i.serviceDate | date: 'mediumDate': 'UTC' }}</span> }
                      @if (i.cancelled) { <span class="text-xs text-amber-700">· cancelled — supplier penalty</span> }</label>
                    <span class="text-xs text-fleet-text-muted">commission {{ i.suggestedCommission | currency: i.currencyCode : 'code' : '1.2-2' }}</span>
                    <span class="font-semibold">{{ i.amount | currency: i.currencyCode : 'code' : '1.2-2' }}</span>
                  </li>
                }
              </ul>
            </div>
          }
        </div>
      </div>

      <div class="tn-card">
        <div class="flex items-center justify-between border-b border-fleet-border px-5 py-4">
          <h3 class="tn-section-title">Lines</h3>
          <button mat-stroked-button (click)="addManual()"><mat-icon>add</mat-icon> Manual line</button>
        </div>
        <div class="overflow-x-auto">
          <table class="w-full min-w-[860px] text-sm">
            <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
              <th class="px-4 py-2 text-left">Description</th><th class="px-4 py-2 text-left">For</th><th class="px-4 py-2 text-right">Amount</th>
              <th class="px-4 py-2 text-right">Commission</th><th class="px-4 py-2 text-right">Net</th><th class="px-4 py-2"></th></tr></thead>
            <tbody class="divide-y divide-fleet-border">
              @if (!lines().length) { <tr><td colspan="6" class="px-4 py-6 text-center text-fleet-text-muted">Pick services above or add a manual line (embassy fees, coaches, ziyarat…).</td></tr> }
              @for (l of lines(); track $index; let i = $index) {
                <tr>
                  <td class="px-4 py-2"><input class="tn-input" maxlength="300" [attr.aria-label]="'Description ' + (i + 1)" [(ngModel)]="l.description" /></td>
                  <td class="px-4 py-2">
                    @if (l.sourceType) { <span class="text-xs text-fleet-text-muted">{{ l.sourceType }} service</span> }
                    @else { <app-travel-context-picker [label]="'Line ' + (i + 1) + ' is for'" [(selected)]="l.context" /> }
                  </td>
                  <td class="px-4 py-2 text-right"><input type="number" min="0" step="0.01" class="tn-input w-32 text-right" [attr.aria-label]="'Amount ' + (i + 1)"
                    [(ngModel)]="l.amount" (ngModelChange)="touch()" /></td>
                  <td class="px-4 py-2 text-right"><input type="number" min="0" step="0.01" class="tn-input w-28 text-right" [attr.aria-label]="'Commission ' + (i + 1)"
                    [(ngModel)]="l.commissionAmount" (ngModelChange)="touch()" />
                    @if (l.suggested !== null && l.suggested !== l.commissionAmount) { <span class="block text-xs text-fleet-text-muted">rule: {{ l.suggested | currency: currency() : 'code' : '1.2-2' }}</span> }</td>
                  <td class="px-4 py-2 text-right font-semibold" [class.text-red-600]="lineNet(l) < 0">{{ lineNet(l) | currency: currency() || 'XXX' : 'code' : '1.2-2' }}</td>
                  <td class="px-4 py-2 text-right"><button mat-icon-button [attr.aria-label]="'Remove line ' + (i + 1)" (click)="removeLine(i)"><mat-icon>delete_outline</mat-icon></button></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <div class="flex flex-col items-end gap-1 px-5 py-4 text-sm">
          <p>Gross <strong class="ml-3">{{ totals().gross | currency: currency() || 'XXX' : 'code' : '1.2-2' }}</strong></p>
          <p>Supplier commission <strong class="ml-3">− {{ totals().commission | currency: currency() || 'XXX' : 'code' : '1.2-2' }}</strong></p>
          <p class="text-base">Net payable <strong class="ml-3">{{ totals().net | currency: currency() || 'XXX' : 'code' : '1.2-2' }}</strong></p>
          @if (totals().invalid) { <p class="text-xs text-red-600">Commission cannot be negative or above a line's amount.</p> }
        </div>
      </div>

      @if (error()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div> }
      <div class="tn-actions">
        <button mat-stroked-button (click)="back()">Cancel</button>
        <button mat-stroked-button [disabled]="!canSave()" (click)="save(false)">{{ busy() ? 'Saving…' : 'Save draft' }}</button>
        <button mat-flat-button color="primary" [disabled]="!canSave() || !(totals().net > 0)" (click)="save(true)">Save and post</button>
      </div>
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelBillEditorComponent {
  private readonly api = inject(TravelSupplierFinanceService);
  private readonly supplierApi = inject(TravelSupplierService);
  private readonly router = inject(Router);
  private readonly toast = inject(UiToastService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);

  protected readonly billId = Number(this.route.snapshot.paramMap.get('id')) || null;
  private readonly presetSupplier = Number(this.route.snapshot.queryParamMap.get('supplierId')) || null;

  protected readonly suppliers = signal<TravelSupplier[]>([]);
  protected readonly supplierId = signal<number | null>(this.presetSupplier);
  protected readonly fixedSupplier = signal(!!this.presetSupplier || !!this.billId);
  protected readonly currency = signal('');
  protected readonly billable = signal<TravelBillableCost[]>([]);
  protected readonly lines = signal<LineRow[]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly loadError = signal<string | null>(null);
  private readonly tick = signal(0);
  private readonly draftSupplierName = signal<string | null>(null);

  protected supplierReference = '';
  protected billDate = '';
  protected dueDate = '';
  protected notes = '';
  protected otherCurrency = '';

  protected readonly supplierName = computed(() => this.draftSupplierName() ?? this.suppliers().find(s => s.id === this.supplierId())?.name ?? '');
  protected readonly currencyOptions = computed(() => {
    const set = new Set(this.billable().map(b => b.currencyCode));
    const supplierCurrency = this.suppliers().find(s => s.id === this.supplierId())?.currencyCode;
    if (supplierCurrency) set.add(supplierCurrency);
    if (this.currency()) set.add(this.currency());
    return [...set].sort();
  });
  protected readonly grouped = computed(() => groupBillableCosts(this.billable().filter(b => !this.currency() || b.currencyCode === this.currency())));
  protected readonly totals = computed(() => { this.tick(); return billTotals(this.lines()); });
  protected readonly canSave = computed(() => { this.tick(); return !this.busy() && !!this.supplierId() && this.currency().length === 3 && this.lines().length > 0 && !this.totals().invalid; });

  constructor() {
    this.supplierApi.getSuppliers().pipe(catchError(() => of([] as TravelSupplier[])), takeUntilDestroyed(this.destroyRef)).subscribe(list => {
      this.suppliers.set(list);
      if (!this.currency() && !this.billId) this.currency.set(list.find(s => s.id === this.supplierId())?.currencyCode ?? '');
    });
    if (this.billId) this.loadDraft(this.billId);
    effect(() => {
      const id = this.supplierId();
      if (!id) return;
      untracked(() => this.reloadBillable());
    });
  }

  private loadDraft(id: number): void {
    this.api.getBill(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: b => {
        if (b.status !== 'Draft') { this.loadError.set('Only drafts can be edited.'); return; }
        this.currency.set(b.currencyCode);
        this.draftSupplierName.set(b.supplierName);
        this.supplierReference = b.supplierReference ?? '';
        this.billDate = b.billDate?.substring(0, 10) ?? '';
        this.dueDate = b.dueDate?.substring(0, 10) ?? '';
        this.notes = b.notes ?? '';
        this.lines.set(b.lines.map(l => ({
          description: l.description, sourceType: l.sourceType, sourceId: l.sourceId, amount: l.amount, commissionAmount: l.commissionAmount, suggested: null,
          context: !l.sourceType && l.contextType && l.contextId ? { type: l.contextType, id: l.contextId, label: l.contextLabel ?? `#${l.contextId}` } : null
        })));
        this.supplierId.set(b.supplierId);
        this.touch();
      },
      error: err => this.loadError.set(apiErrorMessage(err, 'Could not load the bill.'))
    });
  }

  protected reloadBillable(): void {
    const id = this.supplierId();
    if (!id) return;
    this.api.getBillable(id, null, this.billId, this.billDate || null).pipe(catchError(() => of([] as TravelBillableCost[])), takeUntilDestroyed(this.destroyRef))
      .subscribe(items => {
        this.billable.set(items);
        if (!this.currency() && items.length) this.currency.set(items[0].currencyCode);
        // Refresh the suggestion shown next to picked services (the entered commission is kept).
        this.lines.update(ls => ls.map(l => {
          const item = items.find(i => i.sourceType === l.sourceType && i.sourceId === l.sourceId);
          return item ? { ...l, suggested: item.suggestedCommission } : l;
        }));
        this.touch();
      });
  }

  protected touch(): void { this.tick.update(n => n + 1); }
  protected lineNet(l: LineRow): number { return round((Number(l.amount) || 0) - (Number(l.commissionAmount) || 0)); }
  protected isPicked(i: TravelBillableCost): boolean { return this.lines().some(l => l.sourceType === i.sourceType && l.sourceId === i.sourceId); }

  protected setSupplier(id: number | null): void {
    if (id !== this.supplierId() && this.lines().some(l => l.sourceType)) {
      this.lines.update(ls => ls.filter(l => !l.sourceType));
      this.toast.info("The previous supplier's services were removed from the lines.");
    }
    this.supplierId.set(id);
    this.currency.set(this.suppliers().find(s => s.id === id)?.currencyCode ?? '');
    this.touch();
  }

  protected setCurrency(c: string): void {
    if (c !== this.currency() && this.lines().some(l => l.sourceType)) {
      this.lines.update(ls => ls.filter(l => !l.sourceType));
      this.toast.info('Services in the previous currency were removed from the lines.');
    }
    this.currency.set(c);
    this.touch();
  }

  protected useOther(): void {
    const c = this.otherCurrency.trim().toUpperCase();
    if (/^[A-Z]{3}$/.test(c)) this.setCurrency(c);
    this.otherCurrency = '';
  }

  protected toggle(i: TravelBillableCost): void {
    if (this.isPicked(i)) this.lines.update(ls => ls.filter(l => !(l.sourceType === i.sourceType && l.sourceId === i.sourceId)));
    else this.lines.update(ls => [...ls, { description: i.description, sourceType: i.sourceType, sourceId: i.sourceId, context: null, amount: i.amount,
      commissionAmount: i.suggestedCommission, suggested: i.suggestedCommission }]);
    this.touch();
  }

  protected addManual(): void {
    this.lines.update(ls => [...ls, { description: '', sourceType: null, sourceId: null, context: null, amount: null, commissionAmount: 0, suggested: null }]);
    this.touch();
  }
  protected removeLine(i: number): void { this.lines.update(ls => ls.filter((_, j) => j !== i)); this.touch(); }

  protected save(post: boolean): void {
    if (this.lines().some(l => !l.sourceType && !l.description.trim())) { this.error.set('Every manual line needs a description.'); return; }
    const dto: SaveTravelSupplierBill = {
      supplierReference: this.supplierReference.trim() || null, billDate: this.billDate || null, dueDate: this.dueDate || null, notes: this.notes.trim() || null,
      lines: this.lines().map(l => ({
        description: l.description.trim(), sourceType: l.sourceType, sourceId: l.sourceId,
        contextType: l.sourceType ? null : (l.context?.type ?? null) as TravelContextType | null, contextId: l.sourceType ? null : l.context?.id ?? null,
        amount: round(Number(l.amount) || 0), commissionAmount: round(Number(l.commissionAmount) || 0)
      }))
    };
    this.busy.set(true);
    this.error.set(null);
    const save$: Observable<number> = this.billId
      ? this.api.updateBill(this.billId, dto).pipe(map(() => this.billId!))
      : this.api.createBill({ ...dto, supplierId: this.supplierId()!, currencyCode: this.currency() });
    save$.pipe(
      // If posting fails the draft is already saved: open it rather than saving a second draft on retry.
      switchMap(id => post
        ? this.api.postBill(id, dto.billDate, dto.dueDate).pipe(
            map(number => ({ id, number: number as string | null, postError: null as string | null })),
            catchError(err => of({ id, number: null as string | null, postError: apiErrorMessage(err, 'The bill could not be posted.') })))
        : of({ id, number: null as string | null, postError: null as string | null })),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: r => {
        this.busy.set(false);
        if (r.postError) this.toast.error(`Draft saved, but not posted: ${r.postError}`);
        else this.toast.success(r.number ? `Bill ${r.number} posted.` : 'Draft saved.');
        this.router.navigate(['/travel/suppliers/bills', r.id]);
      },
      error: err => { this.busy.set(false); this.error.set(apiErrorMessage(err, 'Could not save the bill.')); }
    });
  }

  protected back(): void { this.router.navigate(this.billId ? ['/travel/suppliers/bills', this.billId] : ['/travel/suppliers']); }
}
