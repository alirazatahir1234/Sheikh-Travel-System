import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import {
  UiConfirmService, UiDrawerComponent, UiEmptyStateComponent, UiModalComponent, UiPageHeaderComponent, UiStatusBadgeComponent, UiTab, UiTabsComponent
} from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  BENEFICIARY_TYPE_LABEL, COMMISSION_BASIS_LABEL, COMMISSION_STATUS_VARIANT, PAYMENT_METHOD_LABEL, TRAVEL_BENEFICIARY_TYPES, TRAVEL_COMMISSION_BASES,
  TRAVEL_COMMISSION_SCOPES, TRAVEL_PAYMENT_METHODS, TRAVEL_PERMISSIONS, TravelBeneficiaryType, TravelCommissionBalance, TravelCommissionBasis,
  TravelCommissionLine, TravelCommissionPayout, TravelCommissionRule, TravelCommissionScope, TravelCommissionStatus, TravelLookupItem, TravelPaymentMethod,
  TravelSubAgent
} from '../../models/travel.models';
import { TravelCommissionService } from '../../services/travel-commission.service';
import { TravelConfigService } from '../../services/travel-config.service';
import { newRequestKey } from '../../services/travel-finance.service';
import { round } from '../finance/finance-documents';

const today = (): string => new Date().toISOString().substring(0, 10);

@Component({
  selector: 'app-travel-commissions-page',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, FormsModule, RouterLink, MatButtonModule, MatIconModule, UiPageHeaderComponent, UiTabsComponent, UiDrawerComponent,
    UiModalComponent, UiEmptyStateComponent, UiStatusBadgeComponent, HasPermissionDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      <ui-page-header title="Commissions" eyebrow="TravelNexus" icon="handshake"
        subtitle="Agent commission for staff and sub-agents: accrued when an invoice is issued, payable once the customer has paid it in full."
        [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Commissions' }]">
        <button mat-flat-button color="primary" *appHasPermission="perms.commissionManage" (click)="openPayout(null)"><mat-icon>payments</mat-icon> Record payout</button>
      </ui-page-header>
      @if (error()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div> }

      <ui-tabs [tabs]="tabs" [(activeId)]="active"></ui-tabs>
      @switch (active()) {
        @case ('balances') {
          <div class="tn-card overflow-x-auto">
            <table class="w-full min-w-[760px] text-left text-sm">
              <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                <th class="px-4 py-3">Beneficiary</th><th class="px-4 py-3 text-right">Accrued · customer still owes</th><th class="px-4 py-3 text-right">Payable</th>
                <th class="px-4 py-3 text-right">Paid out</th><th class="px-4 py-3 text-right">Balance to pay</th><th class="px-4 py-3"></th></tr></thead>
              <tbody class="divide-y divide-fleet-border">
                @if (!balances().length) { <tr><td colspan="6"><ui-empty-state icon="handshake" title="No commission yet" description="Add commission rules and attribute invoices to agents or sub-agents."></ui-empty-state></td></tr> }
                @for (b of balances(); track b.beneficiaryType + b.beneficiaryId + b.currencyCode) {
                  <tr>
                    <td class="px-4 py-3"><span class="font-semibold">{{ b.beneficiaryName }}</span>
                      <span class="block text-xs text-fleet-text-muted">{{ typeLabel[b.beneficiaryType] }} · {{ b.currencyCode }}</span></td>
                    <td class="px-4 py-3 text-right">{{ b.accrued | currency: b.currencyCode : 'code' : '1.2-2' }}</td>
                    <td class="px-4 py-3 text-right">{{ b.payable | currency: b.currencyCode : 'code' : '1.2-2' }}</td>
                    <td class="px-4 py-3 text-right">{{ b.paid | currency: b.currencyCode : 'code' : '1.2-2' }}</td>
                    <td class="px-4 py-3 text-right font-semibold" [class.text-red-600]="b.balance < 0">{{ b.balance | currency: b.currencyCode : 'code' : '1.2-2' }}
                      @if (b.balance < 0) { <span class="block text-xs font-normal">overpaid after a credit note</span> }</td>
                    <td class="px-4 py-3 text-right">@if (b.balance > 0) {
                      <button mat-stroked-button *appHasPermission="perms.commissionManage" (click)="openPayout(b)">Pay</button> }</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
        @case ('lines') {
          <div class="tn-card">
            <div class="flex flex-col gap-3 border-b border-fleet-border px-5 py-4 lg:flex-row lg:flex-wrap lg:items-end">
              <select class="tn-input lg:w-40" aria-label="Status" [(ngModel)]="lineStatus" (ngModelChange)="loadLines()">
                <option value="">Accrued and payable</option><option value="Accrued">Accrued</option><option value="Payable">Payable</option>
              </select>
              <label class="tn-field lg:w-40"><span class="text-xs">Issued from</span><input type="date" class="tn-input" [(ngModel)]="lineFrom" (ngModelChange)="loadLines()" /></label>
              <label class="tn-field lg:w-40"><span class="text-xs">Issued to</span><input type="date" class="tn-input" [(ngModel)]="lineTo" (ngModelChange)="loadLines()" /></label>
            </div>
            <div class="overflow-x-auto">
              <table class="w-full min-w-[900px] text-left text-sm">
                <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                  <th class="px-4 py-3">Invoice</th><th class="px-4 py-3">Customer</th><th class="px-4 py-3">Beneficiary</th><th class="px-4 py-3">Rule</th>
                  <th class="px-4 py-3 text-right">On</th><th class="px-4 py-3 text-right">Commission</th><th class="px-4 py-3">Status</th></tr></thead>
                <tbody class="divide-y divide-fleet-border">
                  @if (!lines().length) { <tr><td colspan="7" class="px-4 py-6 text-center text-fleet-text-muted">No commission for these filters.</td></tr> }
                  @for (l of lines(); track l.invoiceId + l.beneficiaryType + l.beneficiaryId + l.basis + l.rate) {
                    <tr>
                      <td class="px-4 py-3"><a class="font-mono text-xs text-fleet-primary underline" [routerLink]="['/travel/finance/invoices', l.invoiceId]">{{ l.invoiceNumber }}</a>
                        <span class="block text-xs text-fleet-text-muted">{{ l.issueDate | date: 'mediumDate': 'UTC' }}</span></td>
                      <td class="px-4 py-3">{{ l.customerName }}</td>
                      <td class="px-4 py-3">{{ l.beneficiaryName }}<span class="block text-xs text-fleet-text-muted">{{ typeLabel[l.beneficiaryType] }}</span></td>
                      <td class="px-4 py-3">{{ l.rate }}% {{ basisLabel[l.basis] }}</td>
                      <td class="px-4 py-3 text-right">{{ l.basisAmount | currency: l.currencyCode : 'code' : '1.2-2' }}</td>
                      <td class="px-4 py-3 text-right font-semibold">
                        @if (l.missingRate) { <span class="text-xs text-amber-700">exchange rate missing</span> }
                        @else { {{ l.amount | currency: l.currencyCode : 'code' : '1.2-2' }} }</td>
                      <td class="px-4 py-3"><ui-status-badge [status]="l.status" [variant]="statusVariant[l.status]"></ui-status-badge></td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }
        @case ('rules') {
          <div class="tn-card">
            <div class="flex items-center justify-between border-b border-fleet-border px-5 py-4">
              <p class="text-xs text-fleet-text-muted">Per invoice line, the person's own rule beats the default for everyone; a service rule beats "All".</p>
              <button mat-stroked-button *appHasPermission="perms.commissionManage" (click)="editRule(null)"><mat-icon>add</mat-icon> Add rule</button>
            </div>
            <div class="overflow-x-auto">
              <table class="w-full min-w-[760px] text-left text-sm">
                <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                  <th class="px-4 py-3">For</th><th class="px-4 py-3">Services</th><th class="px-4 py-3">Rate</th><th class="px-4 py-3">Valid</th><th class="px-4 py-3"></th></tr></thead>
                <tbody class="divide-y divide-fleet-border">
                  @if (!rules().length) { <tr><td colspan="5" class="px-4 py-6 text-center text-fleet-text-muted">No commission rules.</td></tr> }
                  @for (r of rules(); track r.id) {
                    <tr [class.opacity-60]="!r.isActive">
                      <td class="px-4 py-3">{{ r.beneficiaryName ?? 'Every ' + (r.beneficiaryType === 'Staff' ? 'staff agent' : 'sub-agent') }}
                        <span class="block text-xs text-fleet-text-muted">{{ typeLabel[r.beneficiaryType] }}</span></td>
                      <td class="px-4 py-3">{{ r.scope }}</td>
                      <td class="px-4 py-3 font-semibold">{{ r.rate }}% {{ basisLabel[r.basis] }}</td>
                      <td class="px-4 py-3 whitespace-nowrap">from {{ r.validFrom | date: 'mediumDate': 'UTC' }}{{ r.validTo ? ' to ' + (r.validTo | date: 'mediumDate': 'UTC') : '' }}</td>
                      <td class="px-4 py-3 text-right"><ng-container *appHasPermission="perms.commissionManage">
                        <button mat-icon-button aria-label="Edit rule" (click)="editRule(r)"><mat-icon>edit</mat-icon></button>
                        <button mat-icon-button aria-label="Delete rule" (click)="removeRule(r)"><mat-icon>delete_outline</mat-icon></button></ng-container></td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }
        @case ('subAgents') {
          <div class="tn-card">
            <div class="flex items-center justify-between border-b border-fleet-border px-5 py-4">
              <p class="text-xs text-fleet-text-muted">External agents who refer customers. A customer's "referred by" becomes the default sub-agent on their invoices.</p>
              <button mat-stroked-button *appHasPermission="perms.commissionManage" (click)="editSubAgent(null)"><mat-icon>add</mat-icon> Add sub-agent</button>
            </div>
            <div class="overflow-x-auto">
              <table class="w-full min-w-[680px] text-left text-sm">
                <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                  <th class="px-4 py-3">Name</th><th class="px-4 py-3">Contact</th><th class="px-4 py-3 text-right">Referred customers</th><th class="px-4 py-3">Status</th><th class="px-4 py-3"></th></tr></thead>
                <tbody class="divide-y divide-fleet-border">
                  @if (!subAgents().length) { <tr><td colspan="5" class="px-4 py-6 text-center text-fleet-text-muted">No sub-agents.</td></tr> }
                  @for (a of subAgents(); track a.id) {
                    <tr>
                      <td class="px-4 py-3 font-semibold">{{ a.name }}</td>
                      <td class="px-4 py-3">{{ a.phone || '—' }}{{ a.email ? ' · ' + a.email : '' }}</td>
                      <td class="px-4 py-3 text-right">{{ a.referredCustomers }}</td>
                      <td class="px-4 py-3"><ui-status-badge [status]="a.isActive ? 'active' : 'inactive'"></ui-status-badge></td>
                      <td class="px-4 py-3 text-right"><ng-container *appHasPermission="perms.commissionManage">
                        <button mat-icon-button aria-label="Edit sub-agent" (click)="editSubAgent(a)"><mat-icon>edit</mat-icon></button>
                        <button mat-icon-button aria-label="Remove sub-agent" (click)="removeSubAgent(a)"><mat-icon>delete_outline</mat-icon></button></ng-container></td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }
        @case ('payouts') {
          <div class="tn-card overflow-x-auto">
            <table class="w-full min-w-[760px] text-left text-sm">
              <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                <th class="px-4 py-3">Payout</th><th class="px-4 py-3">Paid to</th><th class="px-4 py-3">Paid on</th><th class="px-4 py-3">Method</th><th class="px-4 py-3 text-right">Amount</th></tr></thead>
              <tbody class="divide-y divide-fleet-border">
                @if (!payouts().length) { <tr><td colspan="5" class="px-4 py-6 text-center text-fleet-text-muted">No payouts recorded.</td></tr> }
                @for (p of payouts(); track p.id) {
                  <tr>
                    <td class="px-4 py-3 font-mono text-xs">{{ p.payoutNumber }}</td>
                    <td class="px-4 py-3">{{ p.beneficiaryName }}<span class="block text-xs text-fleet-text-muted">{{ typeLabel[p.beneficiaryType] }}</span></td>
                    <td class="px-4 py-3">{{ p.paidOn | date: 'mediumDate': 'UTC' }}</td>
                    <td class="px-4 py-3">{{ methodLabel[p.method] }}{{ p.reference ? ' · ' + p.reference : '' }}</td>
                    <td class="px-4 py-3 text-right font-semibold">{{ p.amount | currency: p.currencyCode : 'code' : '1.2-2' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      }

      <ui-drawer [(open)]="ruleOpen" [title]="ruleId ? 'Edit commission rule' : 'Add commission rule'" width="460px">
        <div class="space-y-4">
          <div class="tn-field"><label for="ar-type" class="tn-required">For</label>
            <select id="ar-type" class="tn-input" [(ngModel)]="rule.beneficiaryType">@for (t of beneficiaryTypes; track t) { <option [value]="t">{{ typeLabel[t] }}</option> }</select></div>
          @if (rule.beneficiaryType === 'Staff') {
            <div class="tn-field"><label for="ar-user">Agent</label>
              <select id="ar-user" class="tn-input" [(ngModel)]="rule.userId"><option [ngValue]="null">Every staff agent (default)</option>
                @for (a of agents(); track a.id) { <option [ngValue]="a.id">{{ a.name }}</option> }</select></div>
          } @else {
            <div class="tn-field"><label for="ar-sub">Sub-agent</label>
              <select id="ar-sub" class="tn-input" [(ngModel)]="rule.subAgentId"><option [ngValue]="null">Every sub-agent (default)</option>
                @for (a of subAgents(); track a.id) { <option [ngValue]="a.id">{{ a.name }}</option> }</select></div>
          }
          <div class="tn-grid">
            <div class="tn-field"><label for="ar-scope" class="tn-required">Services</label>
              <select id="ar-scope" class="tn-input" [(ngModel)]="rule.scope">@for (s of scopes; track s) { <option [value]="s">{{ s }}</option> }</select></div>
            <div class="tn-field"><label for="ar-basis" class="tn-required">Basis</label>
              <select id="ar-basis" class="tn-input" [(ngModel)]="rule.basis">@for (b of bases; track b) { <option [value]="b">{{ basisLabel[b] }}</option> }</select></div>
            <div class="tn-field"><label for="ar-rate" class="tn-required">Rate (%)</label><input id="ar-rate" type="number" min="0" max="100" step="0.01" class="tn-input" [(ngModel)]="rule.rate" /></div>
            <div class="tn-field"><label for="ar-from" class="tn-required">Valid from</label><input id="ar-from" type="date" class="tn-input" [(ngModel)]="rule.validFrom" /></div>
            <div class="tn-field"><label for="ar-to">Valid to</label><input id="ar-to" type="date" class="tn-input" [(ngModel)]="rule.validTo" /></div>
          </div>
          <p class="text-xs text-fleet-text-muted">% of margin uses the invoice's margin in the reporting currency (never below zero); % of sell uses net revenue in the invoice currency.</p>
          <label class="flex items-center gap-2 text-sm"><input type="checkbox" [(ngModel)]="rule.isActive" /> Active</label>
          @if (formError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ formError() }}</div> }
          <div class="tn-actions">
            <button mat-stroked-button (click)="ruleOpen.set(false)">Cancel</button>
            <button mat-flat-button color="primary" [disabled]="busy()" (click)="saveRule()">{{ busy() ? 'Saving…' : 'Save' }}</button>
          </div>
        </div>
      </ui-drawer>

      <ui-drawer [(open)]="subAgentOpen" [title]="subAgentId ? 'Edit sub-agent' : 'Add sub-agent'" width="420px">
        <div class="space-y-4">
          <div class="tn-field"><label for="sa-name" class="tn-required">Name</label><input id="sa-name" class="tn-input" maxlength="150" [(ngModel)]="subAgent.name" /></div>
          <div class="tn-grid">
            <div class="tn-field"><label for="sa-phone">Phone</label><input id="sa-phone" class="tn-input" maxlength="30" [(ngModel)]="subAgent.phone" /></div>
            <div class="tn-field"><label for="sa-email">Email</label><input id="sa-email" type="email" class="tn-input" maxlength="150" [(ngModel)]="subAgent.email" /></div>
          </div>
          <div class="tn-field"><label for="sa-notes">Notes</label><textarea id="sa-notes" class="tn-input" rows="3" maxlength="1000" [(ngModel)]="subAgent.notes"></textarea></div>
          <label class="flex items-center gap-2 text-sm"><input type="checkbox" [(ngModel)]="subAgent.isActive" /> Active</label>
          @if (formError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ formError() }}</div> }
          <div class="tn-actions">
            <button mat-stroked-button (click)="subAgentOpen.set(false)">Cancel</button>
            <button mat-flat-button color="primary" [disabled]="busy() || !subAgent.name.trim()" (click)="saveSubAgent()">{{ busy() ? 'Saving…' : 'Save' }}</button>
          </div>
        </div>
      </ui-drawer>

      <ui-modal [(open)]="payoutOpen" title="Record commission payout" size="sm">
        <div class="space-y-3">
          <div class="tn-grid">
            <div class="tn-field"><label for="po-type" class="tn-required">Paid to</label>
              <select id="po-type" class="tn-input" [(ngModel)]="payout.beneficiaryType" (ngModelChange)="payout.beneficiaryId = null">
                @for (t of beneficiaryTypes; track t) { <option [value]="t">{{ typeLabel[t] }}</option> }</select></div>
            <div class="tn-field"><label for="po-who" class="tn-required">{{ payout.beneficiaryType === 'Staff' ? 'Agent' : 'Sub-agent' }}</label>
              <select id="po-who" class="tn-input" [(ngModel)]="payout.beneficiaryId"><option [ngValue]="null">Choose…</option>
                @for (a of payout.beneficiaryType === 'Staff' ? agents() : subAgentItems(); track a.id) { <option [ngValue]="a.id">{{ a.name }}</option> }</select></div>
            <div class="tn-field"><label for="po-amount" class="tn-required">Amount</label><input id="po-amount" type="number" min="0" step="0.01" class="tn-input" [(ngModel)]="payout.amount" /></div>
            <div class="tn-field"><label for="po-cur" class="tn-required">Currency</label><input id="po-cur" class="tn-input uppercase" maxlength="3" [(ngModel)]="payout.currencyCode" /></div>
            <div class="tn-field"><label for="po-date" class="tn-required">Paid on</label><input id="po-date" type="date" class="tn-input" [(ngModel)]="payout.paidOn" /></div>
            <div class="tn-field"><label for="po-method">Method</label>
              <select id="po-method" class="tn-input" [(ngModel)]="payout.method">@for (m of methods; track m) { <option [value]="m">{{ methodLabel[m] }}</option> }</select></div>
          </div>
          <div class="tn-field"><label for="po-ref">Reference</label><input id="po-ref" class="tn-input" maxlength="100" [(ngModel)]="payout.reference" /></div>
          <p class="text-xs text-fleet-text-muted">At most the beneficiary's payable balance in that currency can be paid out.</p>
          @if (formError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ formError() }}</div> }
        </div>
        <div modal-footer class="tn-actions">
          <button mat-stroked-button (click)="payoutOpen.set(false)">Cancel</button>
          <button mat-flat-button color="primary" [disabled]="busy() || !payout.beneficiaryId || !(payout.amount > 0) || payout.currencyCode.length !== 3 || !payout.paidOn" (click)="savePayout()">Record payout</button>
        </div>
      </ui-modal>
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelCommissionsPageComponent {
  private readonly api = inject(TravelCommissionService);
  private readonly config = inject(TravelConfigService);
  private readonly toast = inject(UiToastService);
  private readonly confirm = inject(UiConfirmService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly typeLabel = BENEFICIARY_TYPE_LABEL;
  protected readonly basisLabel = COMMISSION_BASIS_LABEL;
  protected readonly statusVariant = COMMISSION_STATUS_VARIANT;
  protected readonly methodLabel = PAYMENT_METHOD_LABEL;
  protected readonly methods = TRAVEL_PAYMENT_METHODS;
  protected readonly beneficiaryTypes = TRAVEL_BENEFICIARY_TYPES;
  protected readonly scopes = TRAVEL_COMMISSION_SCOPES;
  protected readonly bases = TRAVEL_COMMISSION_BASES;
  protected readonly tabs: UiTab[] = [
    { id: 'balances', label: 'Balances', icon: 'account_balance_wallet' },
    { id: 'lines', label: 'Per invoice', icon: 'receipt_long' },
    { id: 'rules', label: 'Rules', icon: 'rule' },
    { id: 'subAgents', label: 'Sub-agents', icon: 'groups' },
    { id: 'payouts', label: 'Payouts', icon: 'payments' }
  ];
  protected readonly active = signal('balances');
  protected readonly error = signal<string | null>(null);
  protected readonly formError = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly balances = signal<TravelCommissionBalance[]>([]);
  protected readonly lines = signal<TravelCommissionLine[]>([]);
  protected readonly rules = signal<TravelCommissionRule[]>([]);
  protected readonly subAgents = signal<TravelSubAgent[]>([]);
  protected readonly payouts = signal<TravelCommissionPayout[]>([]);
  protected readonly agents = signal<TravelLookupItem[]>([]);
  protected readonly subAgentItems = computed<TravelLookupItem[]>(() => this.subAgents().filter(a => a.isActive).map(a => ({ id: a.id, name: a.name })));
  protected readonly ruleOpen = signal(false);
  protected readonly subAgentOpen = signal(false);
  protected readonly payoutOpen = signal(false);

  protected lineStatus: TravelCommissionStatus | '' = '';
  protected lineFrom = '';
  protected lineTo = '';
  protected ruleId: number | null = null;
  protected rule = this.blankRule();
  protected subAgentId: number | null = null;
  protected subAgent = { name: '', phone: '', email: '', notes: '', isActive: true };
  private payoutKey = newRequestKey();
  protected payout = this.blankPayout();

  constructor() {
    this.config.getLookups().pipe(catchError(() => of({ branches: [], agents: [] })), takeUntilDestroyed(this.destroyRef)).subscribe(l => this.agents.set(l.agents));
    this.loadSubAgents();
    effect(() => { this.active(); untracked(() => this.loadTab()); });
  }

  protected loadTab(): void {
    this.error.set(null);
    switch (this.active()) {
      case 'balances': this.loadBalances(); break;
      case 'lines': this.loadLines(); break;
      case 'rules': this.loadRules(); break;
      case 'subAgents': this.loadSubAgents(); break;
      case 'payouts': this.loadPayouts(); break;
    }
  }

  private fail(message: string) { return (err: unknown) => { this.error.set(apiErrorMessage(err, message)); return of(null); }; }

  private loadBalances(): void {
    this.api.getBalances().pipe(catchError(this.fail('Could not load balances.')), takeUntilDestroyed(this.destroyRef)).subscribe(b => b && this.balances.set(b));
  }
  protected loadLines(): void {
    this.api.getLines({ status: this.lineStatus, issuedFrom: this.lineFrom || undefined, issuedTo: this.lineTo || undefined })
      .pipe(catchError(this.fail('Could not load commission.')), takeUntilDestroyed(this.destroyRef)).subscribe(l => l && this.lines.set(l));
  }
  private loadRules(): void {
    this.api.getRules().pipe(catchError(this.fail('Could not load rules.')), takeUntilDestroyed(this.destroyRef)).subscribe(r => r && this.rules.set(r));
  }
  private loadSubAgents(): void {
    this.api.getSubAgents().pipe(catchError(this.fail('Could not load sub-agents.')), takeUntilDestroyed(this.destroyRef)).subscribe(a => a && this.subAgents.set(a));
  }
  private loadPayouts(): void {
    this.api.getPayouts().pipe(catchError(this.fail('Could not load payouts.')), takeUntilDestroyed(this.destroyRef)).subscribe(p => p && this.payouts.set(p));
  }

  private blankRule(): { beneficiaryType: TravelBeneficiaryType; userId: number | null; subAgentId: number | null; scope: TravelCommissionScope;
    basis: TravelCommissionBasis; rate: number | null; validFrom: string; validTo: string; isActive: boolean } {
    return { beneficiaryType: 'Staff', userId: null, subAgentId: null, scope: 'All', basis: 'PercentOfMargin', rate: null, validFrom: today(), validTo: '', isActive: true };
  }
  private blankPayout(): { beneficiaryType: TravelBeneficiaryType; beneficiaryId: number | null; amount: number; currencyCode: string; paidOn: string;
    method: TravelPaymentMethod; reference: string } {
    return { beneficiaryType: 'Staff', beneficiaryId: null, amount: 0, currencyCode: '', paidOn: today(), method: 'BankTransfer', reference: '' };
  }

  private save(obs: ReturnType<TravelCommissionService['saveRule']>, ok: string, close: () => void, reload: () => void): void {
    this.busy.set(true);
    this.formError.set(null);
    obs.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); close(); this.toast.success(ok); reload(); },
      error: err => { this.busy.set(false); this.formError.set(apiErrorMessage(err, 'The change was refused.')); }
    });
  }

  protected editRule(r: TravelCommissionRule | null): void {
    this.ruleId = r?.id ?? null;
    this.rule = r ? { beneficiaryType: r.beneficiaryType, userId: r.userId, subAgentId: r.subAgentId, scope: r.scope, basis: r.basis, rate: r.rate,
      validFrom: r.validFrom.substring(0, 10), validTo: r.validTo?.substring(0, 10) ?? '', isActive: r.isActive } : this.blankRule();
    this.formError.set(null);
    this.ruleOpen.set(true);
  }
  protected saveRule(): void {
    const r = this.rule;
    if (r.rate === null || !(Number(r.rate) > 0) || Number(r.rate) > 100 || !r.validFrom) { this.formError.set('A rate between 0 and 100% and a start date are required.'); return; }
    const staff = r.beneficiaryType === 'Staff';
    this.save(this.api.saveRule(this.ruleId, { beneficiaryType: r.beneficiaryType, userId: staff ? r.userId : null, subAgentId: staff ? null : r.subAgentId,
      scope: r.scope, basis: r.basis, rate: Number(r.rate), validFrom: r.validFrom, validTo: r.validTo || null, isActive: r.isActive }),
      'Commission rule saved.', () => this.ruleOpen.set(false), () => this.loadRules());
  }
  protected async removeRule(r: TravelCommissionRule): Promise<void> {
    if (!(await this.confirm.confirmDelete('Delete this rule? Commission is recalculated without it.'))) return;
    this.api.deleteRule(r.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.toast.success('Rule deleted.'); this.loadRules(); },
      error: err => this.toast.error(apiErrorMessage(err, 'Could not delete the rule.'))
    });
  }

  protected editSubAgent(a: TravelSubAgent | null): void {
    this.subAgentId = a?.id ?? null;
    this.subAgent = { name: a?.name ?? '', phone: a?.phone ?? '', email: a?.email ?? '', notes: a?.notes ?? '', isActive: a?.isActive ?? true };
    this.formError.set(null);
    this.subAgentOpen.set(true);
  }
  protected saveSubAgent(): void {
    const a = this.subAgent;
    this.save(this.api.saveSubAgent(this.subAgentId, { name: a.name.trim(), phone: a.phone.trim() || null, email: a.email.trim() || null, notes: a.notes.trim() || null,
      isActive: a.isActive }), 'Sub-agent saved.', () => this.subAgentOpen.set(false), () => this.loadSubAgents());
  }
  protected async removeSubAgent(a: TravelSubAgent): Promise<void> {
    if (!(await this.confirm.confirmDelete(`Remove ${a.name}? Invoices already attributed keep the name.`))) return;
    this.api.deleteSubAgent(a.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.toast.success('Sub-agent removed.'); this.loadSubAgents(); },
      error: err => this.toast.error(apiErrorMessage(err, 'Could not remove the sub-agent.'))
    });
  }

  protected openPayout(b: TravelCommissionBalance | null): void {
    this.payoutKey = newRequestKey();
    this.payout = b ? { ...this.blankPayout(), beneficiaryType: b.beneficiaryType, beneficiaryId: b.beneficiaryId, amount: round(b.balance), currencyCode: b.currencyCode }
      : this.blankPayout();
    this.formError.set(null);
    this.payoutOpen.set(true);
  }
  protected savePayout(): void {
    const p = this.payout;
    this.save(this.api.recordPayout({ idempotencyKey: this.payoutKey, beneficiaryType: p.beneficiaryType, beneficiaryId: p.beneficiaryId!, paidOn: p.paidOn,
      method: p.method, reference: p.reference.trim() || null, amount: round(Number(p.amount)), currencyCode: p.currencyCode.trim().toUpperCase(), notes: null }),
      'Payout recorded.', () => this.payoutOpen.set(false), () => { this.loadBalances(); this.loadPayouts(); });
  }
}
