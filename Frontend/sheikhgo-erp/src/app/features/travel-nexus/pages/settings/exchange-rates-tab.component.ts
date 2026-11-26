import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { catchError, forkJoin, of } from 'rxjs';
import { UiConfirmService } from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { TravelExchangeRate, TravelSettings } from '../../models/travel.models';
import { TravelConfigService } from '../../services/travel-config.service';
import { TravelSupplierFinanceService } from '../../services/travel-supplier-finance.service';

/**
 * Rates to the reporting currency, used by profitability and margin-based commission. Each amount converts at the latest rate
 * that started on or before its date; amounts with no such rate are reported as missing, never guessed.
 */
@Component({
  selector: 'app-exchange-rates-tab',
  standalone: true,
  imports: [DatePipe, FormsModule, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (error()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}<button mat-stroked-button class="ml-auto" (click)="load()">Retry</button></div> }
    <div class="tn-card tn-card-body space-y-4">
      @if (!reporting()) {
        <div class="tn-banner warn"><mat-icon>currency_exchange</mat-icon>Set a reporting currency (or a default currency) under General first.</div>
      } @else {
        <p class="text-sm">Reporting currency: <strong class="font-mono">{{ reporting() }}</strong>
          <span class="text-fleet-text-muted"> — rates say how many {{ reporting() }} one unit of the other currency is worth.</span></p>
      }
      <div class="flex flex-wrap items-end gap-3">
        <div class="tn-field w-28"><label for="fx-cur" class="tn-required">Currency</label><input id="fx-cur" class="tn-input uppercase" maxlength="3" [(ngModel)]="currency" /></div>
        <div class="tn-field w-40"><label for="fx-rate" class="tn-required">1 unit = {{ reporting() || '…' }}</label>
          <input id="fx-rate" type="number" min="0" step="0.000001" class="tn-input" [(ngModel)]="rate" /></div>
        <div class="tn-field w-44"><label for="fx-from" class="tn-required">In force from</label><input id="fx-from" type="date" class="tn-input" [(ngModel)]="effectiveFrom" /></div>
        <button mat-flat-button color="primary" [disabled]="saving() || !reporting() || !valid()" (click)="save()">{{ editingId ? 'Update rate' : 'Add rate' }}</button>
        @if (editingId) { <button mat-button (click)="cancel()">Cancel</button> }
      </div>
      @if (saveError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ saveError() }}</div> }
    </div>

    <div class="tn-card mt-4 overflow-x-auto">
      <table class="w-full min-w-[520px] text-left text-sm">
        <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
          <th class="px-4 py-3">Currency</th><th class="px-4 py-3 text-right">Rate to {{ reporting() || 'reporting' }}</th><th class="px-4 py-3">In force from</th><th class="px-4 py-3"></th></tr></thead>
        <tbody class="divide-y divide-fleet-border">
          @if (!loading() && !rates().length) { <tr><td colspan="4" class="px-4 py-6 text-center text-fleet-text-muted">No rates yet — amounts in other currencies are left out of profitability.</td></tr> }
          @for (r of rates(); track r.id) {
            <tr>
              <td class="px-4 py-3 font-mono">{{ r.currencyCode }}</td>
              <td class="px-4 py-3 text-right">{{ r.rateToReporting }}</td>
              <td class="px-4 py-3">{{ r.effectiveFrom | date: 'mediumDate': 'UTC' }}</td>
              <td class="px-4 py-3 text-right">
                <button mat-icon-button aria-label="Edit rate" (click)="edit(r)"><mat-icon>edit</mat-icon></button>
                <button mat-icon-button aria-label="Delete rate" (click)="remove(r)"><mat-icon>delete_outline</mat-icon></button>
              </td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class ExchangeRatesTabComponent {
  private readonly api = inject(TravelSupplierFinanceService);
  private readonly config = inject(TravelConfigService);
  private readonly toast = inject(UiToastService);
  private readonly confirm = inject(UiConfirmService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly settings = signal<TravelSettings | null>(null);
  protected readonly rates = signal<TravelExchangeRate[]>([]);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly saveError = signal<string | null>(null);
  protected readonly reporting = computed(() => this.settings()?.reportingCurrencyCode ?? this.settings()?.defaultCurrencyCode ?? null);

  protected editingId: number | null = null;
  protected currency = '';
  protected rate: number | null = null;
  protected effectiveFrom = new Date().toISOString().substring(0, 10);

  constructor() { this.load(); }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    forkJoin({ settings: this.config.getSettings(), rates: this.api.getExchangeRates().pipe(catchError(() => of([] as TravelExchangeRate[]))) })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: r => { this.settings.set(r.settings); this.rates.set(r.rates); this.loading.set(false); },
        error: err => { this.error.set(apiErrorMessage(err, 'Could not load exchange rates.')); this.loading.set(false); }
      });
  }

  protected valid(): boolean { return /^[A-Za-z]{3}$/.test(this.currency.trim()) && Number(this.rate) > 0 && !!this.effectiveFrom; }

  protected edit(r: TravelExchangeRate): void {
    this.editingId = r.id;
    this.currency = r.currencyCode;
    this.rate = r.rateToReporting;
    this.effectiveFrom = r.effectiveFrom.substring(0, 10);
    this.saveError.set(null);
  }

  protected cancel(): void {
    this.editingId = null;
    this.currency = '';
    this.rate = null;
    this.saveError.set(null);
  }

  protected save(): void {
    this.saving.set(true);
    this.saveError.set(null);
    this.api.saveExchangeRate(this.editingId, { currencyCode: this.currency.trim().toUpperCase(), rateToReporting: Number(this.rate), effectiveFrom: this.effectiveFrom })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => { this.saving.set(false); this.toast.success('Exchange rate saved.'); this.cancel(); this.load(); },
        error: err => { this.saving.set(false); this.saveError.set(apiErrorMessage(err, 'Could not save the rate.')); }
      });
  }

  protected async remove(r: TravelExchangeRate): Promise<void> {
    if (!(await this.confirm.confirmDelete(`Delete the ${r.currencyCode} rate from ${r.effectiveFrom.substring(0, 10)}? Profitability recalculates without it.`))) return;
    this.api.deleteExchangeRate(r.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.toast.success('Rate deleted.'); this.load(); },
      error: err => this.toast.error(apiErrorMessage(err, 'Could not delete the rate.'))
    });
  }
}
