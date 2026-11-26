import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { TRAVEL_PERMISSIONS, TravelGroup, TravelGroupFinanceRow } from '../../models/travel.models';
import { TravelFinanceService } from '../../services/travel-finance.service';

/** Group detail → Finance: per booking party, pilgrims quoted vs invoiced, paid and balance. */
@Component({
  selector: 'app-group-finance-tab',
  standalone: true,
  imports: [CurrencyPipe, RouterLink, MatIconModule, HasPermissionDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-card overflow-x-auto">
      <div class="border-b border-fleet-border px-5 py-4"><h3 class="tn-section-title">Pilgrim payments</h3>
        <p class="text-xs text-fleet-text-muted">Each booking party is invoiced for its pilgrims' quoted package price; amounts per currency.</p></div>
      @if (error()) { <div class="tn-card-body"><div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div></div> }
      @if (!rows().length && !error()) { <p class="px-5 py-6 text-sm text-fleet-text-muted">No pilgrims registered.</p> }
      @else {
        <table class="w-full min-w-[820px] text-sm">
          <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
            <th class="px-4 py-2 text-left">Booking party</th><th class="px-4 py-2 text-right">Pilgrims</th><th class="px-4 py-2 text-right">Quoted</th>
            <th class="px-4 py-2 text-right">Invoiced</th><th class="px-4 py-2 text-right">Paid</th><th class="px-4 py-2 text-right">Balance</th><th class="px-4 py-2"></th></tr></thead>
          <tbody class="divide-y divide-fleet-border">
            @for (r of rows(); track r.customerId + r.currencyCode) {
              <tr>
                <td class="px-4 py-2"><a class="text-fleet-primary underline" [routerLink]="['/travel/customers', r.customerId]">{{ r.customerName }}</a></td>
                <td class="px-4 py-2 text-right">{{ r.pilgrims }}@if (r.uninvoicedPilgrims) { <span class="block text-xs text-amber-700">{{ r.uninvoicedPilgrims }} not invoiced</span> }</td>
                <td class="px-4 py-2 text-right">{{ r.quoted | currency: r.currencyCode : 'code' : '1.2-2' }}</td>
                <td class="px-4 py-2 text-right">{{ r.invoiced | currency: r.currencyCode : 'code' : '1.2-2' }}</td>
                <td class="px-4 py-2 text-right">{{ r.paid | currency: r.currencyCode : 'code' : '1.2-2' }}</td>
                <td class="px-4 py-2 text-right font-semibold" [class.text-red-600]="r.balance > 0">{{ r.balance | currency: r.currencyCode : 'code' : '1.2-2' }}</td>
                <td class="px-4 py-2 text-right">
                  @if (r.uninvoicedPilgrims) {
                    <a class="text-fleet-primary underline" *appHasPermission="perms.invoiceManage" [routerLink]="['/travel/finance/invoices/new']"
                      [queryParams]="{ customerId: r.customerId, currency: r.currencyCode }">Invoice pilgrims</a>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class GroupFinanceTabComponent {
  private readonly api = inject(TravelFinanceService);
  private readonly destroyRef = inject(DestroyRef);
  readonly group = input.required<TravelGroup>();
  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly rows = signal<TravelGroupFinanceRow[]>([]);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      const id = this.group().id;
      untracked(() => this.api.getGroupFinance(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: r => { this.rows.set(r); this.error.set(null); },
        error: err => this.error.set(apiErrorMessage(err, 'Could not load the group finance.'))
      }));
    });
  }
}
