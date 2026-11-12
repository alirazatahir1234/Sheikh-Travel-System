import { Component, OnInit, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { CtaBandComponent } from '../../shared/cta-band.component';
import { WebsiteSeoService } from '../../core/seo.service';
import { WebsiteI18nService } from '../../core/website-i18n.service';
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
    return [
      {
        id: 'starter',
        name: this.i18n.t('pages.pricing.starter.name'),
        fleet: this.i18n.t('pages.pricing.starter.fleet'),
        price: '20,000',
        priceNote: this.i18n.t('pages.pricing.starter.priceNote') as string | null,
        featured: false,
        badge: undefined as string | undefined,
        features: [
          this.i18n.t('pages.pricing.starter.feature1'),
          this.i18n.t('pages.pricing.starter.feature2'),
          this.i18n.t('pages.pricing.starter.feature3'),
          this.i18n.t('pages.pricing.starter.feature4'),
        ],
      },
      {
        id: 'pro',
        name: this.i18n.t('pages.pricing.pro.name'),
        fleet: this.i18n.t('pages.pricing.pro.fleet'),
        price: '45,000',
        priceNote: this.i18n.t('pages.pricing.pro.priceNote') as string | null,
        featured: true,
        badge: this.i18n.t('pages.pricing.pro.badge'),
        features: [
          this.i18n.t('pages.pricing.pro.feature1'),
          this.i18n.t('pages.pricing.pro.feature2'),
          this.i18n.t('pages.pricing.pro.feature3'),
          this.i18n.t('pages.pricing.pro.feature4'),
        ],
      },
      {
        id: 'enterprise',
        name: this.i18n.t('pages.pricing.enterprise.name'),
        fleet: this.i18n.t('pages.pricing.enterprise.fleet'),
        price: '70,000',
        priceNote: this.i18n.t('pages.pricing.enterprise.priceNote'),
        featured: false,
        badge: undefined as string | undefined,
        features: [
          this.i18n.t('pages.pricing.enterprise.feature1'),
          this.i18n.t('pages.pricing.enterprise.feature2'),
          this.i18n.t('pages.pricing.enterprise.feature3'),
          this.i18n.t('pages.pricing.enterprise.feature4'),
        ],
      },
    ] as const;
  });

  ngOnInit(): void {
    this.seo.set(
      this.i18n.t('pages.pricing.title'),
      this.i18n.t('pages.pricing.metaDescription'),
      '/pricing',
    );
  }
}
