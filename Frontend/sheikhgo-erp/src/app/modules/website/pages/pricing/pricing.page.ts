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

  readonly tiers = computed(() => {
    this.i18n.lang();
    return [
      {
        id: 'starter',
        name: this.i18n.t('pages.pricing.starter.name'),
        fleet: this.i18n.t('pages.pricing.starter.fleet'),
        price: '79,000',
        priceNote: null as string | null,
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
        price: '119,000',
        priceNote: null as string | null,
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
        price: '165,000',
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
