import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { UiPageHeaderComponent } from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { TravelStatement } from '../../models/travel.models';
import { TravelFinanceService } from '../../services/travel-finance.service';
import { buildStatementPdf } from './finance-documents';

/** Customer statement of account in one currency, with opening balance and running balance. */
@Component({
  selector: 'app-travel-statement',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, FormsModule, RouterLink, MatButtonModule, MatIconModule, UiPageHeaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      <ui-page-header [title]="'Statement · ' + (statement()?.customerName ?? '')" eyebrow="TravelNexus · Invoices & Payments" icon="request_quote"
        subtitle="Invoices and refunds increase the balance; payments and credit notes reduce it."
        [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Invoices & Payments', route: '/travel/finance' }, { label: 'Statement' }]">
        <button mat-stroked-button [disabled]="!statement()?.currencyCode" (click)="pdf()"><mat-icon>picture_as_pdf</mat-icon> PDF</button>
      </ui-page-header>
      @if (error()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div> }
      @if (statement(); as s) {
        <div class="tn-card">
          <div class="flex flex-col gap-3 border-b border-fleet-border px-5 py-4 lg:flex-row lg:items-end">
            <label class="tn-field lg:w-32"><span class="text-xs">Currency</span>
              <select class="tn-input" [ngModel]="s.currencyCode" (ngModelChange)="currency = $event; load()">
                @for (c of s.currencies; track c) { <option [value]="c">{{ c }}</option> }
              </select></label>
            <label class="tn-field lg:w-40"><span class="text-xs">From</span><input type="date" class="tn-input" [(ngModel)]="from" (ngModelChange)="load()" /></label>
            <label class="tn-field lg:w-40"><span class="text-xs">To</span><input type="date" class="tn-input" [(ngModel)]="to" (ngModelChange)="load()" /></label>
            <a class="text-sm text-fleet-primary underline lg:ml-auto" [routerLink]="['/travel/customers', s.customerId]">Customer profile</a>
          </div>
          @if (!s.currencyCode) { <p class="px-5 py-8 text-center text-sm text-fleet-text-muted">No issued invoices or payments for this customer.</p> }
          @else {
            <div class="overflow-x-auto">
              <table class="w-full min-w-[760px] text-sm">
                <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                  <th class="px-4 py-2 text-left">Date</th><th class="px-4 py-2 text-left">Reference</th><th class="px-4 py-2 text-left">Description</th>
                  <th class="px-4 py-2 text-right">Debit</th><th class="px-4 py-2 text-right">Credit</th><th class="px-4 py-2 text-right">Balance</th></tr></thead>
                <tbody class="divide-y divide-fleet-border">
                  <tr class="bg-fleet-surface-alt/30"><td class="px-4 py-2" colspan="5">Opening balance</td>
                    <td class="px-4 py-2 text-right font-semibold">{{ s.openingBalance | currency: s.currencyCode : 'code' : '1.2-2' }}</td></tr>
                  @for (e of s.entries; track $index) {
                    <tr>
                      <td class="px-4 py-2 whitespace-nowrap">{{ e.date | date: 'mediumDate': 'UTC' }}</td>
                      <td class="px-4 py-2 font-mono text-xs">
                        @if (e.invoiceId && e.entryType !== 'Payment' && e.entryType !== 'Advance') { <a class="text-fleet-primary underline" [routerLink]="['/travel/finance/invoices', e.invoiceId]">{{ e.reference }}</a> }
                        @else if (e.paymentId) { <a class="text-fleet-primary underline" [routerLink]="['/travel/finance/payments', e.paymentId]">{{ e.reference }}</a> }
                        @else { {{ e.reference }} }</td>
                      <td class="px-4 py-2">{{ e.description }}</td>
                      <td class="px-4 py-2 text-right">{{ e.debit ? (e.debit | currency: s.currencyCode : 'code' : '1.2-2') : '' }}</td>
                      <td class="px-4 py-2 text-right">{{ e.credit ? (e.credit | currency: s.currencyCode : 'code' : '1.2-2') : '' }}</td>
                      <td class="px-4 py-2 text-right font-semibold">{{ e.balance | currency: s.currencyCode : 'code' : '1.2-2' }}</td>
                    </tr>
                  }
                  @if (!s.entries.length) { <tr><td colspan="6" class="px-4 py-6 text-center text-fleet-text-muted">No movements in this period.</td></tr> }
                </tbody>
                <tfoot><tr><td colspan="5" class="px-4 py-2 text-right font-semibold">Closing balance{{ s.closingBalance < 0 ? ' (in the customer\\'s favour)' : '' }}</td>
                  <td class="px-4 py-2 text-right font-semibold">{{ s.closingBalance | currency: s.currencyCode : 'code' : '1.2-2' }}</td></tr></tfoot>
              </table>
            </div>
          }
        </div>
      }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelStatementComponent {
  private readonly api = inject(TravelFinanceService);
  private readonly toast = inject(UiToastService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly customerId = Number(this.route.snapshot.paramMap.get('customerId'));

  protected readonly statement = signal<TravelStatement | null>(null);
  protected readonly error = signal<string | null>(null);
  protected currency = this.route.snapshot.queryParamMap.get('currency') ?? '';
  protected from = '';
  protected to = '';

  constructor() { this.load(); }

  protected load(): void {
    this.api.getStatement(this.customerId, this.currency || null, this.from || null, this.to || null).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: s => { this.statement.set(s); this.currency = s.currencyCode; this.error.set(null); },
      error: err => this.error.set(apiErrorMessage(err, 'Could not load the statement.'))
    });
  }

  protected pdf(): void {
    const s = this.statement()!;
    try { buildStatementPdf(s).save(`statement-${s.customerName.replace(/\s+/g, '-')}-${s.currencyCode}.pdf`); } catch { this.toast.error('Could not build the PDF.'); }
  }
}
