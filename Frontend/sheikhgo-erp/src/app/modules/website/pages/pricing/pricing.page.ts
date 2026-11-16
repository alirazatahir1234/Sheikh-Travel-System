import { Component, OnInit, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { CtaBandComponent } from '../../shared/cta-band.component';
import { WebsiteSeoService } from '../../core/seo.service';
import { WebsiteI18nService } from '../../core/website-i18n.service';
import { WebsiteMarket, WebsiteMarketService } from '../../core/website-market.service';
import { WebsiteTranslatePipe } from '../../shared/website-translate.pipe';

@Component({
  selector: 'app-pricing-page',
  standalone: true,
  imports: [RouterLink, MatIconModule, CtaBandComponent, WebsiteTranslatePipe],
  templateUrl: './pricing.page.html',
  styleUrl: './pricing.page.scss',
})
export class PricingPage implements OnInit {
  private readonly seo = inject(WebsiteSeoService);
  readonly i18n = inject(WebsiteI18nService);
  readonly market = inject(WebsiteMarketService);

  readonly trackerAnnualTiers = computed(() => {
    this.i18n.lang();
    return [
      {
        id: 'essential',
        name: this.i18n.t('pages.pricing.trackerEssential.name'),
        subtitle: this.i18n.t('pages.pricing.trackerEssential.subtitle'),
        price: '8,000',
        featured: false,
        badge: undefined as string | undefined,
        features: [
          this.i18n.t('pages.pricing.trackerEssential.feature1'),
          this.i18n.t('pages.pricing.trackerEssential.feature2'),
          this.i18n.t('pages.pricing.trackerEssential.feature3'),
        ],
      },
      {
        id: 'plus',
        name: this.i18n.t('pages.pricing.trackerPlus.name'),
        subtitle: this.i18n.t('pages.pricing.trackerPlus.subtitle'),
        price: '9,000',
        featured: true,
        badge: this.i18n.t('pages.pricing.trackerPlus.badge'),
        features: [
          this.i18n.t('pages.pricing.trackerPlus.feature1'),
          this.i18n.t('pages.pricing.trackerPlus.feature2'),
          this.i18n.t('pages.pricing.trackerPlus.feature3'),
        ],
      },
      {
        id: 'premium',
        name: this.i18n.t('pages.pricing.trackerPremium.name'),
        subtitle: this.i18n.t('pages.pricing.trackerPremium.subtitle'),
        price: '10,000',
        featured: false,
        badge: undefined as string | undefined,
        features: [
          this.i18n.t('pages.pricing.trackerPremium.feature1'),
          this.i18n.t('pages.pricing.trackerPremium.feature2'),
          this.i18n.t('pages.pricing.trackerPremium.feature3'),
        ],
      },
    ] as const;
  });

  readonly tiers = computed(() => {
    this.i18n.lang();
    this.market.market();
    this.market.usdToPkrRate();
    this.market.vehicleCount();
    this.market.billingPeriod();
    const apiTiers = this.market.tiers();
    const byCode = (code: string) => apiTiers.find(t => t.code === code);
    const vehicles = this.market.vehicleCount();

    const build = (
      id: string,
      code: string,
      fallbackPerVehicle: number,
      featured: boolean,
      cta: 'demo' | 'trial' | 'sales',
      featureKeys: string[],
      highlightCount = 0,
    ) => {
      const tier = byCode(code) ?? {
        code,
        name: id,
        monthlyPerVehicleUsd: fallbackPerVehicle,
        yearlyPerVehicleUsd: fallbackPerVehicle * 12 * 0.85,
        platformFeeMonthlyUsd: code === 'PRO' ? 25 : 0,
      };
      const total = this.market.formatTierTotal(tier, vehicles);
      const perVehicle = this.market.formatMonthlyPerVehicle(tier.monthlyPerVehicleUsd);
      const feeFormatted = this.market.formatMoney(tier.platformFeeMonthlyUsd || 25);
      const feeNote =
        code === 'PRO' && !this.market.isAnnual()
          ? `${this.i18n.t('pages.pricing.pro.feeHintPrefix')} ${feeFormatted}`
          : code === 'PRO' && this.market.isAnnual()
            ? this.i18n.t('pages.pricing.pro.feeWaived')
            : null;

      return {
        id,
        code,
        name: this.i18n.t(`pages.pricing.${id}.name`),
        fleet: this.i18n.t(`pages.pricing.${id}.fleet`),
        price: total,
        perVehicleLabel: `${perVehicle} ${this.i18n.t('pages.pricing.perVehicleSuffix')}`,
        feeNote,
        featured,
        badge: featured ? this.i18n.t('pages.pricing.pro.badge') : undefined,
        features: featureKeys.map((key, i) => ({
          text: this.i18n.t(key),
          highlight: i < highlightCount,
        })),
        cta,
        ctaLabel: this.i18n.t(
          cta === 'sales'
            ? 'pages.pricing.cta.sales'
            : cta === 'trial'
              ? 'pages.pricing.cta.trial'
              : 'pages.pricing.cta.start',
        ),
        ctaLink: cta === 'sales' ? '/contact' : '/request-demo',
      };
    };

    return [
      build('starter', 'STARTER', 6, false, 'demo', [
        'pages.pricing.starter.feature1',
        'pages.pricing.starter.feature2',
        'pages.pricing.starter.feature3',
        'pages.pricing.starter.feature4',
        'pages.pricing.starter.feature5',
        'pages.pricing.starter.feature6',
      ]),
      build(
        'pro',
        'PRO',
        12,
        true,
        'trial',
        [
          'pages.pricing.pro.feature1',
          'pages.pricing.pro.feature2',
          'pages.pricing.pro.feature3',
          'pages.pricing.pro.feature4',
          'pages.pricing.pro.feature5',
          'pages.pricing.pro.feature6',
          'pages.pricing.pro.feature7',
        ],
        3,
      ),
      build('enterprise', 'ENTERPRISE', 20, false, 'sales', [
        'pages.pricing.enterprise.feature1',
        'pages.pricing.enterprise.feature2',
        'pages.pricing.enterprise.feature3',
        'pages.pricing.enterprise.feature4',
        'pages.pricing.enterprise.feature5',
        'pages.pricing.enterprise.feature6',
        'pages.pricing.enterprise.feature7',
      ]),
    ] as const;
  });

  readonly matrixRows = computed(() => {
    this.i18n.lang();
    return [
      { label: this.i18n.t('pages.pricing.matrix.liveMap'), starter: this.i18n.t('pages.pricing.matrix.liveMap.s'), pro: this.i18n.t('pages.pricing.matrix.liveMap.p'), enterprise: this.i18n.t('pages.pricing.matrix.liveMap.e') },
      { label: this.i18n.t('pages.pricing.matrix.hardware'), starter: this.i18n.t('pages.pricing.matrix.yes'), pro: this.i18n.t('pages.pricing.matrix.yes'), enterprise: this.i18n.t('pages.pricing.matrix.hardware.e') },
      { label: this.i18n.t('pages.pricing.matrix.history'), starter: this.i18n.t('pages.pricing.matrix.history.s'), pro: this.i18n.t('pages.pricing.matrix.history.p'), enterprise: this.i18n.t('pages.pricing.matrix.history.e') },
      { label: this.i18n.t('pages.pricing.matrix.geofence'), starter: this.i18n.t('pages.pricing.matrix.geofence.s'), pro: this.i18n.t('pages.pricing.matrix.geofence.p'), enterprise: this.i18n.t('pages.pricing.matrix.geofence.e') },
      { label: this.i18n.t('pages.pricing.matrix.immobilizer'), starter: this.i18n.t('pages.pricing.matrix.yes'), pro: this.i18n.t('pages.pricing.matrix.yes'), enterprise: this.i18n.t('pages.pricing.matrix.immobilizer.e') },
      { label: this.i18n.t('pages.pricing.matrix.alerts'), starter: this.i18n.t('pages.pricing.matrix.alerts.s'), pro: this.i18n.t('pages.pricing.matrix.alerts.p'), enterprise: this.i18n.t('pages.pricing.matrix.alerts.e') },
      { label: this.i18n.t('pages.pricing.matrix.sms'), starter: this.i18n.t('pages.pricing.matrix.sms.s'), pro: this.i18n.t('pages.pricing.matrix.sms.p'), enterprise: this.i18n.t('pages.pricing.matrix.sms.e') },
      { label: this.i18n.t('pages.pricing.matrix.harsh'), starter: '—', pro: this.i18n.t('pages.pricing.matrix.yes'), enterprise: this.i18n.t('pages.pricing.matrix.harsh.e') },
      { label: this.i18n.t('pages.pricing.matrix.docs'), starter: this.i18n.t('pages.pricing.matrix.docs.s'), pro: this.i18n.t('pages.pricing.matrix.docs.p'), enterprise: this.i18n.t('pages.pricing.matrix.docs.e') },
      { label: this.i18n.t('pages.pricing.matrix.maintenance'), starter: this.i18n.t('pages.pricing.matrix.maintenance.s'), pro: this.i18n.t('pages.pricing.matrix.maintenance.p'), enterprise: this.i18n.t('pages.pricing.matrix.maintenance.e') },
      { label: this.i18n.t('pages.pricing.matrix.users'), starter: this.i18n.t('pages.pricing.matrix.users.s'), pro: this.i18n.t('pages.pricing.matrix.users.p'), enterprise: this.i18n.t('pages.pricing.matrix.users.e') },
      { label: this.i18n.t('pages.pricing.matrix.api'), starter: '—', pro: this.i18n.t('pages.pricing.matrix.api.p'), enterprise: this.i18n.t('pages.pricing.matrix.api.e') },
      { label: this.i18n.t('pages.pricing.matrix.support'), starter: this.i18n.t('pages.pricing.matrix.support.s'), pro: this.i18n.t('pages.pricing.matrix.support.p'), enterprise: this.i18n.t('pages.pricing.matrix.support.e') },
    ];
  });

  ngOnInit(): void {
    this.seo.set(
      this.i18n.t('pages.pricing.title'),
      this.i18n.t('pages.pricing.metaDescription'),
      '/pricing',
    );
  }

  setMarket(market: WebsiteMarket): void {
    this.market.setMarket(market);
  }

  onVehicleInput(event: Event): void {
    const value = Number((event.target as HTMLInputElement).value);
    this.market.setVehicleCount(value);
  }
}
