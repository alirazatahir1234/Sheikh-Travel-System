import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { UiConfirmService, UiDrawerComponent, UiPageHeaderComponent, UiStatusBadgeComponent, UiTab, UiTabsComponent } from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { AuthService } from '../../../../core/services/auth.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  SUPPLIER_COMMISSION_BASIS_LABEL, SUPPLIER_RATE_TYPE_LABEL, SUPPLIER_TYPE_LABEL, TRAVEL_PERMISSIONS, TRAVEL_ROOM_TYPES, TRAVEL_SUPPLIER_COMMISSION_BASES,
  TRAVEL_SUPPLIER_COMMISSION_SERVICE_TYPES, TRAVEL_SUPPLIER_RATE_TYPES, TravelHotel, TravelRoomType, TravelSupplier, TravelSupplierBalance,
  TravelSupplierCommissionBasis, TravelSupplierCommissionRule, TravelSupplierCommissionServiceType, TravelSupplierRate, TravelSupplierRateType,
  TravelSupplierStatement
} from '../../models/travel.models';
import { TravelPackageService } from '../../services/travel-package.service';
import { TravelSupplierFinanceService } from '../../services/travel-supplier-finance.service';
import { TravelSupplierService } from '../../services/travel-supplier.service';
import { round } from '../finance/finance-documents';
import { PayablesBillsTabComponent, PayablesPaymentsTabComponent } from './payables-tabs.component';
import { SupplierPaymentDialogComponent } from './supplier-payment-dialog.component';

const today = (): string => new Date().toISOString().substring(0, 10);

@Component({
  selector: 'app-travel-supplier-detail',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, FormsModule, RouterLink, MatButtonModule, MatIconModule, UiPageHeaderComponent, UiTabsComponent, UiDrawerComponent,
    UiStatusBadgeComponent, HasPermissionDirective, PayablesBillsTabComponent, PayablesPaymentsTabComponent, SupplierPaymentDialogComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      @if (error()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div> }
      @if (supplier(); as s) {
        <ui-page-header [title]="s.name" eyebrow="TravelNexus · Suppliers" icon="storefront" [subtitle]="typeLabel[s.supplierType] + (s.currencyCode ? ' · bills in ' + s.currencyCode : '')"
          [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Suppliers', route: '/travel/suppliers' }, { label: s.name }]">
          <ui-status-badge [status]="s.isActive ? 'active' : 'inactive'"></ui-status-badge>
          <a mat-flat-button color="primary" *appHasPermission="perms.supplierBillManage" [routerLink]="['/travel/suppliers/bills/new']" [queryParams]="{ supplierId: s.id }">
            <mat-icon>add</mat-icon> New bill</a>
          <button mat-stroked-button *appHasPermission="perms.supplierPaymentManage" (click)="payOpen.set(true)"><mat-icon>payments</mat-icon> Pay supplier</button>
        </ui-page-header>

        <ui-tabs [tabs]="tabs()" [(activeId)]="active"></ui-tabs>
        @switch (active()) {
          @case ('profile') {
            <div class="grid gap-5 lg:grid-cols-3">
              <div class="tn-card tn-card-body">
                <dl class="tn-kv">
                  <div><dt>Type</dt><dd>{{ typeLabel[s.supplierType] }}</dd></div>
                  @if (s.iataCode) { <div><dt>IATA</dt><dd>{{ s.iataCode }}</dd></div> }
                  <div><dt>Contact</dt><dd>{{ s.contactName || '—' }}{{ s.phone ? ' · ' + s.phone : '' }}</dd></div>
                  <div><dt>Email</dt><dd>{{ s.email || '—' }}</dd></div>
                  <div><dt>Billing currency</dt><dd>{{ s.currencyCode || '—' }}</dd></div>
                </dl>
                @if (s.notes) { <p class="mt-3 whitespace-pre-line text-sm text-fleet-text-muted">{{ s.notes }}</p> }
              </div>
              @if (canFinance) {
                <div class="tn-card tn-card-body lg:col-span-2">
                  <h3 class="tn-section-title">Balance · all time</h3>
                  @if (!balances().length) { <p class="mt-2 text-sm text-fleet-text-muted">No posted bills or payments yet.</p> }
                  <div class="mt-2 grid gap-4 md:grid-cols-2">
                    @for (b of balances(); track b.currencyCode) {
                      <dl class="grid grid-cols-2 gap-2 text-sm">
                        <div><dt class="text-xs text-fleet-text-muted">{{ b.balance < 0 ? 'Owes us' : 'We owe' }}</dt>
                          <dd class="text-lg font-semibold" [class.text-emerald-700]="b.balance < 0">{{ abs(b.balance) | currency: b.currencyCode : 'code' : '1.2-2' }}</dd></div>
                        <div><dt class="text-xs text-fleet-text-muted">Overdue · as of today</dt><dd class="font-semibold" [class.text-red-600]="b.overdue > 0">{{ b.overdue | currency: b.currencyCode : 'code' : '1.2-2' }}</dd></div>
                        <div><dt class="text-xs text-fleet-text-muted">Billed</dt><dd>{{ b.billed | currency: b.currencyCode : 'code' : '1.2-2' }}</dd></div>
                        <div><dt class="text-xs text-fleet-text-muted">Advances</dt><dd>{{ b.unallocatedAdvances | currency: b.currencyCode : 'code' : '1.2-2' }}</dd></div>
                      </dl>
                    }
                  </div>
                </div>
              }
            </div>
          }
          @case ('rates') {
            <div class="tn-card">
              <div class="flex items-center justify-between border-b border-fleet-border px-5 py-4">
                <div><h3 class="tn-section-title">Rate cards</h3><p class="text-xs text-fleet-text-muted">Contracted costs. Hotel room-night cards fill "Use rate card" in the hotel editor.</p></div>
                <button mat-stroked-button *appHasPermission="perms.supplierManage" (click)="editRate(null)"><mat-icon>add</mat-icon> Add rate</button>
              </div>
              <div class="overflow-x-auto">
                <table class="w-full min-w-[760px] text-sm">
                  <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                    <th class="px-4 py-2 text-left">Type</th><th class="px-4 py-2 text-left">Hotel / description</th><th class="px-4 py-2 text-left">Room</th>
                    <th class="px-4 py-2 text-left">Valid</th><th class="px-4 py-2 text-right">Amount</th><th class="px-4 py-2"></th></tr></thead>
                  <tbody class="divide-y divide-fleet-border">
                    @if (!rates().length) { <tr><td colspan="6" class="px-4 py-6 text-center text-fleet-text-muted">No rate cards.</td></tr> }
                    @for (r of rates(); track r.id) {
                      <tr [class.opacity-60]="!r.isActive">
                        <td class="px-4 py-2">{{ rateTypeLabel[r.rateType] }}</td>
                        <td class="px-4 py-2">{{ r.hotelName || r.description || '—' }}</td>
                        <td class="px-4 py-2">{{ r.roomType || '—' }}</td>
                        <td class="px-4 py-2 whitespace-nowrap">{{ r.validFrom | date: 'mediumDate': 'UTC' }} – {{ r.validTo | date: 'mediumDate': 'UTC' }}</td>
                        <td class="px-4 py-2 text-right font-semibold">{{ r.amount | currency: r.currencyCode : 'code' : '1.2-2' }}{{ r.rateType === 'HotelRoomNight' ? ' / night' : '' }}</td>
                        <td class="px-4 py-2 text-right"><ng-container *appHasPermission="perms.supplierManage">
                          <button mat-icon-button aria-label="Edit rate" (click)="editRate(r)"><mat-icon>edit</mat-icon></button>
                          <button mat-icon-button aria-label="Delete rate" (click)="removeRate(r)"><mat-icon>delete_outline</mat-icon></button></ng-container></td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }
          @case ('commission') {
            <div class="tn-card">
              <div class="flex items-center justify-between border-b border-fleet-border px-5 py-4">
                <div><h3 class="tn-section-title">Commission rules</h3><p class="text-xs text-fleet-text-muted">What this supplier pays you back; pre-fills bill lines on the bill date. The latest rule in force wins.</p></div>
                <button mat-stroked-button *appHasPermission="perms.supplierManage" (click)="editRule(null)"><mat-icon>add</mat-icon> Add rule</button>
              </div>
              <div class="overflow-x-auto">
                <table class="w-full min-w-[680px] text-sm">
                  <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                    <th class="px-4 py-2 text-left">Service</th><th class="px-4 py-2 text-left">Basis</th><th class="px-4 py-2 text-right">Rate</th>
                    <th class="px-4 py-2 text-left">Valid</th><th class="px-4 py-2"></th></tr></thead>
                  <tbody class="divide-y divide-fleet-border">
                    @if (!rules().length) { <tr><td colspan="5" class="px-4 py-6 text-center text-fleet-text-muted">No commission rules — bills start with zero commission.</td></tr> }
                    @for (r of rules(); track r.id) {
                      <tr [class.opacity-60]="!r.isActive">
                        <td class="px-4 py-2">{{ r.serviceType }}</td>
                        <td class="px-4 py-2">{{ basisLabel[r.basis] }}</td>
                        <td class="px-4 py-2 text-right font-semibold">{{ r.basis === 'PercentOfCost' ? r.rate + '%' : (r.rate | currency: r.currencyCode ?? 'XXX' : 'code' : '1.2-2') }}</td>
                        <td class="px-4 py-2 whitespace-nowrap">from {{ r.validFrom | date: 'mediumDate': 'UTC' }}{{ r.validTo ? ' to ' + (r.validTo | date: 'mediumDate': 'UTC') : '' }}</td>
                        <td class="px-4 py-2 text-right"><ng-container *appHasPermission="perms.supplierManage">
                          <button mat-icon-button aria-label="Edit rule" (click)="editRule(r)"><mat-icon>edit</mat-icon></button>
                          <button mat-icon-button aria-label="Delete rule" (click)="removeRule(r)"><mat-icon>delete_outline</mat-icon></button></ng-container></td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }
          @case ('bills') { <app-payables-bills-tab [supplierId]="s.id" /> }
          @case ('payments') { <app-payables-payments-tab [supplierId]="s.id" /> }
          @case ('statement') {
            <div class="tn-card">
              <div class="flex flex-col gap-3 border-b border-fleet-border px-5 py-4 lg:flex-row lg:items-end">
                <label class="tn-field lg:w-32"><span class="text-xs">Currency</span>
                  <select class="tn-input" [ngModel]="statement()?.currencyCode" (ngModelChange)="statementCurrency = $event; loadStatement()">
                    @for (c of statement()?.currencies ?? []; track c) { <option [value]="c">{{ c }}</option> }
                  </select></label>
                <label class="tn-field lg:w-40"><span class="text-xs">From</span><input type="date" class="tn-input" [(ngModel)]="statementFrom" (ngModelChange)="loadStatement()" /></label>
                <label class="tn-field lg:w-40"><span class="text-xs">To</span><input type="date" class="tn-input" [(ngModel)]="statementTo" (ngModelChange)="loadStatement()" /></label>
                <p class="text-xs text-fleet-text-muted lg:ml-auto">Bills and refunds received increase what you owe; payments and supplier credits reduce it.</p>
              </div>
              @if (statement(); as st) {
                @if (!st.currencyCode) { <p class="px-5 py-8 text-center text-sm text-fleet-text-muted">No posted bills or payments for this supplier.</p> }
                @else {
                  <div class="overflow-x-auto">
                    <table class="w-full min-w-[760px] text-sm">
                      <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                        <th class="px-4 py-2 text-left">Date</th><th class="px-4 py-2 text-left">Reference</th><th class="px-4 py-2 text-left">Description</th>
                        <th class="px-4 py-2 text-right">Debit</th><th class="px-4 py-2 text-right">Credit</th><th class="px-4 py-2 text-right">Balance</th></tr></thead>
                      <tbody class="divide-y divide-fleet-border">
                        <tr class="bg-fleet-surface-alt/30"><td class="px-4 py-2" colspan="5">Opening balance</td>
                          <td class="px-4 py-2 text-right font-semibold">{{ st.openingBalance | currency: st.currencyCode : 'code' : '1.2-2' }}</td></tr>
                        @for (e of st.entries; track $index) {
                          <tr>
                            <td class="px-4 py-2 whitespace-nowrap">{{ e.date | date: 'mediumDate': 'UTC' }}</td>
                            <td class="px-4 py-2 font-mono text-xs">
                              @if (e.paymentId) { <a class="text-fleet-primary underline" [routerLink]="['/travel/suppliers/payments', e.paymentId]">{{ e.reference }}</a> }
                              @else if (e.invoiceId) { <a class="text-fleet-primary underline" [routerLink]="['/travel/suppliers/bills', e.invoiceId]">{{ e.reference }}</a> }
                              @else { {{ e.reference }} }</td>
                            <td class="px-4 py-2">{{ e.description }}</td>
                            <td class="px-4 py-2 text-right">{{ e.debit ? (e.debit | currency: st.currencyCode : 'code' : '1.2-2') : '' }}</td>
                            <td class="px-4 py-2 text-right">{{ e.credit ? (e.credit | currency: st.currencyCode : 'code' : '1.2-2') : '' }}</td>
                            <td class="px-4 py-2 text-right font-semibold">{{ e.balance | currency: st.currencyCode : 'code' : '1.2-2' }}</td>
                          </tr>
                        }
                        <tr class="bg-fleet-surface-alt/30"><td class="px-4 py-2 font-semibold" colspan="5">Closing balance{{ st.closingBalance < 0 ? ' (supplier owes you)' : '' }}</td>
                          <td class="px-4 py-2 text-right font-semibold">{{ st.closingBalance | currency: st.currencyCode : 'code' : '1.2-2' }}</td></tr>
                      </tbody>
                    </table>
                  </div>
                }
              } @else { <p class="px-5 py-6 text-sm text-fleet-text-muted">Loading statement…</p> }
            </div>
          }
        }

        <app-supplier-payment-dialog [(open)]="payOpen" [supplierId]="s.id" (saved)="paid()" />

        <ui-drawer [(open)]="rateOpen" [title]="rateId ? 'Edit rate' : 'Add rate'" width="480px">
          <div class="space-y-4">
            <div class="tn-grid">
              <div class="tn-field"><label for="rt-type" class="tn-required">Type</label>
                <select id="rt-type" class="tn-input" [(ngModel)]="rate.rateType">@for (t of rateTypes; track t) { <option [value]="t">{{ rateTypeLabel[t] }}</option> }</select></div>
              @if (rate.rateType === 'HotelRoomNight') {
                <div class="tn-field"><label for="rt-room" class="tn-required">Room type</label>
                  <select id="rt-room" class="tn-input" [(ngModel)]="rate.roomType">@for (r of roomTypes; track r) { <option [value]="r">{{ r }}</option> }</select></div>
              }
            </div>
            @if (rate.rateType === 'HotelRoomNight') {
              <div class="tn-field"><label for="rt-hotel" class="tn-required">Hotel</label>
                <select id="rt-hotel" class="tn-input" [(ngModel)]="rate.hotelId"><option [ngValue]="null">Choose…</option>
                  @for (h of hotels(); track h.id) { <option [ngValue]="h.id">{{ h.name }} · {{ h.city }}</option> }</select></div>
            }
            <div class="tn-field"><label for="rt-desc">Description</label><input id="rt-desc" class="tn-input" maxlength="200" [(ngModel)]="rate.description" placeholder="e.g. Jeddah airport → Makkah coach" /></div>
            <div class="tn-grid">
              <div class="tn-field"><label for="rt-from" class="tn-required">Valid from</label><input id="rt-from" type="date" class="tn-input" [(ngModel)]="rate.validFrom" /></div>
              <div class="tn-field"><label for="rt-to" class="tn-required">Valid to</label><input id="rt-to" type="date" class="tn-input" [(ngModel)]="rate.validTo" /></div>
              <div class="tn-field"><label for="rt-amount" class="tn-required">Amount{{ rate.rateType === 'HotelRoomNight' ? ' per night' : '' }}</label>
                <input id="rt-amount" type="number" min="0" step="0.01" class="tn-input" [(ngModel)]="rate.amount" /></div>
              <div class="tn-field"><label for="rt-cur" class="tn-required">Currency</label><input id="rt-cur" class="tn-input uppercase" maxlength="3" [(ngModel)]="rate.currencyCode" /></div>
            </div>
            <label class="flex items-center gap-2 text-sm"><input type="checkbox" [(ngModel)]="rate.isActive" /> Active</label>
            @if (drawerError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ drawerError() }}</div> }
            <div class="tn-actions">
              <button mat-stroked-button (click)="rateOpen.set(false)">Cancel</button>
              <button mat-flat-button color="primary" [disabled]="saving()" (click)="saveRate()">{{ saving() ? 'Saving…' : 'Save' }}</button>
            </div>
          </div>
        </ui-drawer>

        <ui-drawer [(open)]="ruleOpen" [title]="ruleId ? 'Edit commission rule' : 'Add commission rule'" width="440px">
          <div class="space-y-4">
            <div class="tn-grid">
              <div class="tn-field"><label for="cr-service" class="tn-required">Service</label>
                <select id="cr-service" class="tn-input" [(ngModel)]="rule.serviceType">@for (t of serviceTypes; track t) { <option [value]="t">{{ t }}</option> }</select></div>
              <div class="tn-field"><label for="cr-basis" class="tn-required">Basis</label>
                <select id="cr-basis" class="tn-input" [(ngModel)]="rule.basis">@for (b of bases; track b) { <option [value]="b">{{ basisLabel[b] }}</option> }</select></div>
              <div class="tn-field"><label for="cr-rate" class="tn-required">{{ rule.basis === 'PercentOfCost' ? 'Rate (%)' : 'Amount per unit' }}</label>
                <input id="cr-rate" type="number" min="0" step="0.01" class="tn-input" [(ngModel)]="rule.rate" /></div>
              @if (rule.basis === 'FixedPerUnit') {
                <div class="tn-field"><label for="cr-cur" class="tn-required">Currency</label><input id="cr-cur" class="tn-input uppercase" maxlength="3" [(ngModel)]="rule.currencyCode" />
                  <span class="tn-hint">Applies only to bills in this currency.</span></div>
              }
              <div class="tn-field"><label for="cr-from" class="tn-required">Valid from</label><input id="cr-from" type="date" class="tn-input" [(ngModel)]="rule.validFrom" /></div>
              <div class="tn-field"><label for="cr-to">Valid to</label><input id="cr-to" type="date" class="tn-input" [(ngModel)]="rule.validTo" /></div>
            </div>
            <label class="flex items-center gap-2 text-sm"><input type="checkbox" [(ngModel)]="rule.isActive" /> Active</label>
            @if (drawerError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ drawerError() }}</div> }
            <div class="tn-actions">
              <button mat-stroked-button (click)="ruleOpen.set(false)">Cancel</button>
              <button mat-flat-button color="primary" [disabled]="saving()" (click)="saveRule()">{{ saving() ? 'Saving…' : 'Save' }}</button>
            </div>
          </div>
        </ui-drawer>
      } @else if (!error()) {
        <p class="text-sm text-fleet-text-muted">Loading supplier…</p>
      }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelSupplierDetailComponent {
  private readonly suppliers = inject(TravelSupplierService);
  private readonly api = inject(TravelSupplierFinanceService);
  private readonly packages = inject(TravelPackageService);
  private readonly toast = inject(UiToastService);
  private readonly confirm = inject(UiConfirmService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly id = Number(inject(ActivatedRoute).snapshot.paramMap.get('id'));

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly canFinance = inject(AuthService).hasAnyPermission([TRAVEL_PERMISSIONS.financeView]);
  protected readonly typeLabel = SUPPLIER_TYPE_LABEL;
  protected readonly rateTypeLabel = SUPPLIER_RATE_TYPE_LABEL;
  protected readonly basisLabel = SUPPLIER_COMMISSION_BASIS_LABEL;
  protected readonly rateTypes = TRAVEL_SUPPLIER_RATE_TYPES;
  protected readonly roomTypes = TRAVEL_ROOM_TYPES;
  protected readonly serviceTypes = TRAVEL_SUPPLIER_COMMISSION_SERVICE_TYPES;
  protected readonly bases = TRAVEL_SUPPLIER_COMMISSION_BASES;

  protected readonly supplier = signal<TravelSupplier | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly balances = signal<TravelSupplierBalance[]>([]);
  protected readonly rates = signal<TravelSupplierRate[]>([]);
  protected readonly rules = signal<TravelSupplierCommissionRule[]>([]);
  protected readonly hotels = signal<TravelHotel[]>([]);
  protected readonly statement = signal<TravelSupplierStatement | null>(null);
  protected readonly active = signal('profile');
  protected readonly payOpen = signal(false);
  protected readonly rateOpen = signal(false);
  protected readonly ruleOpen = signal(false);
  protected readonly saving = signal(false);
  protected readonly drawerError = signal<string | null>(null);
  protected readonly tabs = computed<UiTab[]>(() => [
    { id: 'profile', label: 'Profile', icon: 'storefront' },
    { id: 'rates', label: 'Rate cards', icon: 'sell' },
    { id: 'commission', label: 'Commission rules', icon: 'percent' },
    ...(this.canFinance ? [
      { id: 'bills', label: 'Bills', icon: 'request_quote' },
      { id: 'payments', label: 'Payments', icon: 'payments' },
      { id: 'statement', label: 'Statement', icon: 'receipt' }
    ] : [])
  ]);

  protected rateId: number | null = null;
  protected rate = this.blankRate();
  protected ruleId: number | null = null;
  protected rule = this.blankRule();
  protected statementCurrency: string | null = null;
  protected statementFrom = '';
  protected statementTo = '';

  constructor() {
    this.suppliers.getSuppliers().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: list => {
        const s = list.find(x => x.id === this.id) ?? null;
        if (!s) { this.error.set('Supplier not found.'); return; }
        this.supplier.set(s);
        this.loadRates();
        this.loadRules();
        if (this.canFinance) { this.loadBalances(); this.loadStatement(); }
      },
      error: err => this.error.set(apiErrorMessage(err, 'Could not load the supplier.'))
    });
  }

  protected abs(n: number): number { return Math.abs(n); }
  protected paid(): void { this.loadBalances(); this.loadStatement(); }

  private loadBalances(): void {
    this.api.getBalances(this.id, false).pipe(catchError(() => of([] as TravelSupplierBalance[])), takeUntilDestroyed(this.destroyRef)).subscribe(b => this.balances.set(b));
  }
  private loadRates(): void {
    this.api.getRates(this.id).pipe(catchError(() => of([] as TravelSupplierRate[])), takeUntilDestroyed(this.destroyRef)).subscribe(r => this.rates.set(r));
  }
  private loadRules(): void {
    this.api.getCommissionRules(this.id).pipe(catchError(() => of([] as TravelSupplierCommissionRule[])), takeUntilDestroyed(this.destroyRef)).subscribe(r => this.rules.set(r));
  }
  protected loadStatement(): void {
    this.api.getStatement(this.id, this.statementCurrency, this.statementFrom || null, this.statementTo || null).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: s => this.statement.set(s),
      error: err => this.toast.error(apiErrorMessage(err, 'Could not load the statement.'))
    });
  }

  private blankRate(): { rateType: TravelSupplierRateType; hotelId: number | null; roomType: TravelRoomType; description: string; validFrom: string; validTo: string;
    amount: number | null; currencyCode: string; isActive: boolean } {
    return { rateType: 'HotelRoomNight', hotelId: null, roomType: 'Double', description: '', validFrom: today(), validTo: '', amount: null,
      currencyCode: this.supplier()?.currencyCode ?? '', isActive: true };
  }
  private blankRule(): { serviceType: TravelSupplierCommissionServiceType; basis: TravelSupplierCommissionBasis; rate: number | null; currencyCode: string;
    validFrom: string; validTo: string; isActive: boolean } {
    return { serviceType: 'Hotel', basis: 'PercentOfCost', rate: null, currencyCode: this.supplier()?.currencyCode ?? '', validFrom: today(), validTo: '', isActive: true };
  }

  protected editRate(r: TravelSupplierRate | null): void {
    this.rateId = r?.id ?? null;
    this.rate = r ? { rateType: r.rateType, hotelId: r.hotelId, roomType: r.roomType ?? 'Double', description: r.description ?? '', validFrom: r.validFrom.substring(0, 10),
      validTo: r.validTo.substring(0, 10), amount: r.amount, currencyCode: r.currencyCode, isActive: r.isActive } : this.blankRate();
    this.drawerError.set(null);
    if (!this.hotels().length)
      this.packages.getHotels(undefined, true).pipe(catchError(() => of([] as TravelHotel[])), takeUntilDestroyed(this.destroyRef)).subscribe(h => this.hotels.set(h));
    this.rateOpen.set(true);
  }

  protected saveRate(): void {
    const r = this.rate;
    const hotelRate = r.rateType === 'HotelRoomNight';
    if (!r.validFrom || !r.validTo || !(Number(r.amount) >= 0) || r.amount === null || !/^[A-Za-z]{3}$/.test(r.currencyCode) || (hotelRate && !r.hotelId)) {
      this.drawerError.set('Hotel (for room-night rates), dates, amount and a 3-letter currency are required.');
      return;
    }
    this.saving.set(true);
    this.api.saveRate(this.id, this.rateId, {
      rateType: r.rateType, hotelId: hotelRate ? r.hotelId : null, roomType: hotelRate ? r.roomType : null, description: r.description.trim() || null,
      validFrom: r.validFrom, validTo: r.validTo, amount: round(Number(r.amount)), currencyCode: r.currencyCode.toUpperCase(), isActive: r.isActive
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.saving.set(false); this.rateOpen.set(false); this.toast.success('Rate saved.'); this.loadRates(); },
      error: err => { this.saving.set(false); this.drawerError.set(apiErrorMessage(err, 'Could not save the rate.')); }
    });
  }

  protected async removeRate(r: TravelSupplierRate): Promise<void> {
    if (!(await this.confirm.confirmDelete('Delete this rate card?'))) return;
    this.api.deleteRate(this.id, r.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.toast.success('Rate deleted.'); this.loadRates(); },
      error: err => this.toast.error(apiErrorMessage(err, 'Could not delete the rate.'))
    });
  }

  protected editRule(r: TravelSupplierCommissionRule | null): void {
    this.ruleId = r?.id ?? null;
    this.rule = r ? { serviceType: r.serviceType, basis: r.basis, rate: r.rate, currencyCode: r.currencyCode ?? '', validFrom: r.validFrom.substring(0, 10),
      validTo: r.validTo?.substring(0, 10) ?? '', isActive: r.isActive } : this.blankRule();
    this.drawerError.set(null);
    this.ruleOpen.set(true);
  }

  protected saveRule(): void {
    const r = this.rule;
    const fixed = r.basis === 'FixedPerUnit';
    if (r.rate === null || !(Number(r.rate) >= 0) || (!fixed && Number(r.rate) > 100) || !r.validFrom || (fixed && !/^[A-Za-z]{3}$/.test(r.currencyCode))) {
      this.drawerError.set('A rate (at most 100% of cost), a start date and — for fixed amounts — a currency are required.');
      return;
    }
    this.saving.set(true);
    this.api.saveCommissionRule(this.id, this.ruleId, {
      serviceType: r.serviceType, basis: r.basis, rate: Number(r.rate), currencyCode: fixed ? r.currencyCode.toUpperCase() : null, validFrom: r.validFrom,
      validTo: r.validTo || null, isActive: r.isActive
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.saving.set(false); this.ruleOpen.set(false); this.toast.success('Commission rule saved.'); this.loadRules(); },
      error: err => { this.saving.set(false); this.drawerError.set(apiErrorMessage(err, 'Could not save the rule.')); }
    });
  }

  protected async removeRule(r: TravelSupplierCommissionRule): Promise<void> {
    if (!(await this.confirm.confirmDelete('Delete this commission rule? Existing bills keep their commission.'))) return;
    this.api.deleteCommissionRule(this.id, r.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.toast.success('Rule deleted.'); this.loadRules(); },
      error: err => this.toast.error(apiErrorMessage(err, 'Could not delete the rule.'))
    });
  }
}
