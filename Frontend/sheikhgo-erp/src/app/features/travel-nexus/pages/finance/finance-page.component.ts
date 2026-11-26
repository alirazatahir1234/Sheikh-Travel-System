import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { UiPageHeaderComponent, UiTab, UiTabsComponent } from '../../../../shared/components/ui';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { TRAVEL_PERMISSIONS, TravelFinanceSummary } from '../../models/travel.models';
import { TravelFinanceService } from '../../services/travel-finance.service';
import {
  FinanceAdjustmentsTabComponent, FinanceBalancesTabComponent, FinanceInvoicesTabComponent, FinancePaymentsTabComponent, FinanceReconciliationTabComponent
} from './finance-tabs.component';
import { RecordPaymentDialogComponent } from './record-payment-dialog.component';

@Component({
  selector: 'app-travel-finance-page',
  standalone: true,
  imports: [CurrencyPipe, RouterLink, MatButtonModule, MatIconModule, UiPageHeaderComponent, UiTabsComponent, HasPermissionDirective,
    FinanceInvoicesTabComponent, FinancePaymentsTabComponent, FinanceAdjustmentsTabComponent, FinanceBalancesTabComponent,
    FinanceReconciliationTabComponent, RecordPaymentDialogComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      <ui-page-header title="Invoices & Payments" eyebrow="TravelNexus" icon="receipt_long"
        subtitle="Customer invoices, receipts, credit notes, refunds and balances — per currency, no conversion."
        [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Invoices & Payments' }]">
        <a mat-flat-button color="primary" routerLink="/travel/finance/invoices/new" *appHasPermission="perms.invoiceManage"><mat-icon>add</mat-icon> New invoice</a>
        <button mat-stroked-button *appHasPermission="perms.paymentManage" (click)="paymentOpen.set(true)"><mat-icon>payments</mat-icon> Record payment</button>
      </ui-page-header>

      <section aria-label="Finance summary per currency">
        @if (!summary()?.currencies?.length) {
          <p class="text-sm text-fleet-text-muted">No issued invoices or payments yet.</p>
        }
        <div class="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          @for (c of summary()?.currencies ?? []; track c.currencyCode) {
            <div class="tn-card tn-card-body">
              <p class="text-xs font-bold uppercase tracking-wider text-fleet-text-muted">{{ c.currencyCode }}</p>
              <dl class="mt-2 grid grid-cols-2 gap-x-4 gap-y-2 text-sm">
                <div><dt class="text-xs text-fleet-text-muted">Outstanding · all time</dt><dd class="font-semibold">{{ c.outstanding | currency: c.currencyCode : 'code' : '1.0-0' }}</dd></div>
                <div><dt class="text-xs text-fleet-text-muted">Overdue · as of today</dt><dd class="font-semibold" [class.text-red-600]="c.overdue > 0">{{ c.overdue | currency: c.currencyCode : 'code' : '1.0-0' }}</dd></div>
                <div><dt class="text-xs text-fleet-text-muted">Collected · this month</dt><dd class="font-semibold">{{ c.collectedThisMonth | currency: c.currencyCode : 'code' : '1.0-0' }}</dd></div>
                <div><dt class="text-xs text-fleet-text-muted">On account · all time</dt><dd class="font-semibold">{{ c.unallocatedAdvances | currency: c.currencyCode : 'code' : '1.0-0' }}</dd></div>
                @if (c.creditDue > 0) { <div class="col-span-2"><dt class="text-xs text-fleet-text-muted">Credit owed back to customers · all time</dt><dd class="font-semibold text-emerald-700">{{ c.creditDue | currency: c.currencyCode : 'code' : '1.0-0' }}</dd></div> }
              </dl>
            </div>
          }
        </div>
        @if (summary(); as s) {
          <p class="mt-2 text-xs text-fleet-text-soft">Invoices (all time): {{ s.invoiceCounts.Draft }} draft · {{ s.invoiceCounts.Issued }} issued · {{ s.invoiceCounts.Void }} void · total {{ s.invoiceTotal }}</p>
        }
      </section>

      <ui-tabs [tabs]="tabs" [(activeId)]="active"></ui-tabs>
      @switch (active()) {
        @case ('invoices') { <app-finance-invoices-tab /> }
        @case ('payments') { <app-finance-payments-tab /> }
        @case ('adjustments') { <app-finance-adjustments-tab /> }
        @case ('balances') { <app-finance-balances-tab /> }
        @case ('reconciliation') { <app-finance-reconciliation-tab /> }
      }
      <app-record-payment-dialog [(open)]="paymentOpen" (saved)="paid($event)" />
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelFinancePageComponent {
  private readonly api = inject(TravelFinanceService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly tabs: UiTab[] = [
    { id: 'invoices', label: 'Invoices', icon: 'receipt_long' },
    { id: 'payments', label: 'Payments', icon: 'payments' },
    { id: 'adjustments', label: 'Credit notes & refunds', icon: 'undo' },
    { id: 'balances', label: 'Balances', icon: 'account_balance_wallet' },
    { id: 'reconciliation', label: 'Reconciliation', icon: 'fact_check' }
  ];
  protected readonly active = signal('invoices');
  protected readonly paymentOpen = signal(false);
  protected readonly summary = signal<TravelFinanceSummary | null>(null);

  constructor() { this.loadSummary(); }

  private loadSummary(): void {
    this.api.getSummary().pipe(catchError(() => of(null)), takeUntilDestroyed(this.destroyRef)).subscribe(s => this.summary.set(s));
  }

  protected paid(id: number): void { this.router.navigate(['/travel/finance/payments', id]); }
}
