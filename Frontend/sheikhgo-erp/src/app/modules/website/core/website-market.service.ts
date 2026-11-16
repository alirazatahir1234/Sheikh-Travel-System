import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { catchError, of } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  ANNUAL_DISCOUNT_PERCENT,
  PRO_PLATFORM_FEE_MONTHLY_USD,
  USD_TO_PKR_DEFAULT,
  usdToPkr,
} from '../../../core/models/platform.model';

/** Marketing display markets. AED is display-only (not a billing currency). */
export type WebsiteMarket = 'intl' | 'aed' | 'pk';
export type WebsiteBillingPeriod = 'monthly' | 'annual';

const STORAGE_KEY = 'sheikhgo.website.market';
const VEHICLES_KEY = 'sheikhgo.website.vehicles';
const PERIOD_KEY = 'sheikhgo.website.billingPeriod';

/** Marketing AED FX vs USD (not used for invoices). */
export const USD_TO_AED_DISPLAY = 3.67;

export interface PublicPricingTier {
  code: string;
  name: string;
  monthlyPerVehicleUsd: number;
  yearlyPerVehicleUsd: number;
  platformFeeMonthlyUsd: number;
}

@Injectable({ providedIn: 'root' })
export class WebsiteMarketService {
  private readonly http = inject(HttpClient);

  private readonly marketSignal = signal<WebsiteMarket>(this.readStoredMarket());
  private readonly usdToPkrSignal = signal(USD_TO_PKR_DEFAULT);
  private readonly vehicleCountSignal = signal(this.readStoredVehicles());
  private readonly billingPeriodSignal = signal<WebsiteBillingPeriod>(this.readStoredPeriod());
  private readonly tiersSignal = signal<PublicPricingTier[]>([
    { code: 'STARTER', name: 'Starter', monthlyPerVehicleUsd: 6, yearlyPerVehicleUsd: 61.2, platformFeeMonthlyUsd: 0 },
    { code: 'PRO', name: 'Pro', monthlyPerVehicleUsd: 12, yearlyPerVehicleUsd: 122.4, platformFeeMonthlyUsd: PRO_PLATFORM_FEE_MONTHLY_USD },
    {
      code: 'ENTERPRISE',
      name: 'Enterprise',
      monthlyPerVehicleUsd: 20,
      yearlyPerVehicleUsd: 204,
      platformFeeMonthlyUsd: 0,
    },
  ]);

  readonly market = this.marketSignal.asReadonly();
  readonly isPakistan = computed(() => this.marketSignal() === 'pk');
  readonly isAed = computed(() => this.marketSignal() === 'aed');
  readonly currencyCode = computed(() => {
    const m = this.marketSignal();
    if (m === 'pk') return 'PKR';
    if (m === 'aed') return 'AED';
    return 'USD';
  });
  readonly usdToPkrRate = this.usdToPkrSignal.asReadonly();
  readonly annualDiscountPercent = ANNUAL_DISCOUNT_PERCENT;
  readonly tiers = this.tiersSignal.asReadonly();
  readonly vehicleCount = this.vehicleCountSignal.asReadonly();
  readonly billingPeriod = this.billingPeriodSignal.asReadonly();
  readonly isAnnual = computed(() => this.billingPeriodSignal() === 'annual');

  constructor() {
    this.loadRemotePricing();
  }

  setMarket(market: WebsiteMarket): void {
    this.marketSignal.set(market);
    try {
      localStorage.setItem(STORAGE_KEY, market);
    } catch {
      /* ignore */
    }
  }

  setVehicleCount(count: number): void {
    const clamped = Math.min(150, Math.max(1, Math.round(count) || 1));
    this.vehicleCountSignal.set(clamped);
    try {
      localStorage.setItem(VEHICLES_KEY, String(clamped));
    } catch {
      /* ignore */
    }
  }

  setBillingPeriod(period: WebsiteBillingPeriod): void {
    this.billingPeriodSignal.set(period);
    try {
      localStorage.setItem(PERIOD_KEY, period);
    } catch {
      /* ignore */
    }
  }

  toggleBillingPeriod(): void {
    this.setBillingPeriod(this.isAnnual() ? 'monthly' : 'annual');
  }

  /**
   * USD list total for a tier before FX:
   * per-vehicle × seats + platform fee (monthly cycle only; waived on annual prepaid).
   */
  tierMonthlyListUsd(tier: PublicPricingTier, vehicles = this.vehicleCountSignal()): number {
    const seats = Math.max(1, vehicles);
    const fee = this.isAnnual() ? 0 : (tier.platformFeeMonthlyUsd || 0);
    return tier.monthlyPerVehicleUsd * seats + fee;
  }

  /**
   * Display monthly equivalent in USD: annual shows prepaid monthly-equivalent after 15% off.
   */
  tierDisplayMonthlyUsd(tier: PublicPricingTier, vehicles = this.vehicleCountSignal()): number {
    const monthly = this.tierMonthlyListUsd(tier, vehicles);
    if (!this.isAnnual()) return monthly;
    // Annual prepaid: 15% off the monthly list (platform fee already waived above).
    return Math.round(monthly * (1 - ANNUAL_DISCOUNT_PERCENT / 100) * 100) / 100;
  }

  formatMoney(usd: number): string {
    const amount = this.convertFromUsd(usd);
    const code = this.currencyCode();
    if (code === 'PKR') {
      return `PKR ${amount.toLocaleString('en-PK', { maximumFractionDigits: 0 })}`;
    }
    if (code === 'AED') {
      return `AED ${amount.toLocaleString('en-AE', { maximumFractionDigits: 0 })}`;
    }
    return `$${amount.toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 2 })}`;
  }

  /** Compact price for tier cards (monthly per vehicle list). */
  formatMonthlyPerVehicle(usd: number): string {
    return this.formatMoney(usd);
  }

  /** Formatted fleet total for calculator cards. */
  formatTierTotal(tier: PublicPricingTier, vehicles = this.vehicleCountSignal()): string {
    return this.formatMoney(this.tierDisplayMonthlyUsd(tier, vehicles));
  }

  convertFromUsd(usd: number): number {
    const m = this.marketSignal();
    if (m === 'pk') return usdToPkr(usd, this.usdToPkrSignal());
    if (m === 'aed') return Math.round(usd * USD_TO_AED_DISPLAY * 100) / 100;
    return usd;
  }

  private readStoredMarket(): WebsiteMarket {
    try {
      const v = localStorage.getItem(STORAGE_KEY);
      if (v === 'pk' || v === 'intl' || v === 'aed') return v;
    } catch {
      /* ignore */
    }
    return 'intl';
  }

  private readStoredVehicles(): number {
    try {
      const n = Number(localStorage.getItem(VEHICLES_KEY));
      if (Number.isFinite(n) && n >= 1 && n <= 150) return Math.round(n);
    } catch {
      /* ignore */
    }
    return 15;
  }

  private readStoredPeriod(): WebsiteBillingPeriod {
    try {
      const v = localStorage.getItem(PERIOD_KEY);
      if (v === 'monthly' || v === 'annual') return v;
    } catch {
      /* ignore */
    }
    return 'annual';
  }

  private loadRemotePricing(): void {
    const url = `${environment.apiUrl}/website/public/pricing`;
    this.http
      .get<{
        usdToPkr?: number;
        tiers?: PublicPricingTier[];
      }>(url)
      .pipe(catchError(() => of(null)))
      .subscribe(res => {
        if (!res) return;
        if (res.usdToPkr && res.usdToPkr > 0) this.usdToPkrSignal.set(res.usdToPkr);
        if (res.tiers?.length) this.tiersSignal.set(res.tiers);
      });
  }
}
