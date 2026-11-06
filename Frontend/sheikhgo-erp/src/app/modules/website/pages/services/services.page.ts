import { AfterViewInit, Component, OnInit, computed, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { CtaBandComponent } from '../../shared/cta-band.component';
import { SoftwareConsultancyComponent } from '../../shared/software-consultancy/software-consultancy.component';
import { WebsiteSeoService } from '../../core/seo.service';
import { WEBSITE_BRAND } from '../../core/brand';
import {
  NEXUS_DASHBOARD_IMAGE,
  NEXUS_MODULE_SHOTS,
  NEXUS_PORTFOLIO_ITEMS,
  NEXUS_VALUE_PILLARS,
} from '../../core/site-catalog';
import { WebsiteI18nService } from '../../core/website-i18n.service';
import { WebsiteTranslatePipe } from '../../shared/website-translate.pipe';

@Component({
  selector: 'app-services-page',
  standalone: true,
  imports: [
    RouterLink,
    MatIconModule,
    CtaBandComponent,
    SoftwareConsultancyComponent,
    WebsiteTranslatePipe,
  ],
  templateUrl: './services.page.html',
  styleUrl: './services.page.scss',
})
export class ServicesPage implements OnInit, AfterViewInit {
  private readonly seo = inject(WebsiteSeoService);
  private readonly route = inject(ActivatedRoute);
  readonly i18n = inject(WebsiteI18nService);

  readonly brand = WEBSITE_BRAND;
  readonly portfolio = NEXUS_PORTFOLIO_ITEMS;
  readonly valuePillars = NEXUS_VALUE_PILLARS;
  readonly moduleShots = NEXUS_MODULE_SHOTS;
  readonly dashboardImage = NEXUS_DASHBOARD_IMAGE;

  readonly pageTitle = computed(() => {
    this.i18n.lang();
    return this.i18n.t('pages.services.title');
  });
  readonly pageLead = computed(() => {
    this.i18n.lang();
    return this.i18n.t('pages.services.lead');
  });

  dashboardImageError = false;

  get moduleSection() {
    return this.portfolio.find(s => s.slug === 'platform-modules')!;
  }

  /** Alternate band backgrounds for readable rhythm. */
  detailAlt(slug: string): boolean {
    const order = ['fleet-intelligence', 'operations-modules'] as const;
    return (order as readonly string[]).includes(slug);
  }

  ngOnInit(): void {
    this.seo.set(
      this.pageTitle(),
      'SheikhGo Nexus portfolio plus software consultancy — websites, web apps, ERP systems, e-commerce stores, and mobile apps.',
      '/services',
    );
  }

  ngAfterViewInit(): void {
    this.route.fragment.subscribe(fragment => {
      if (!fragment) return;
      setTimeout(() => document.getElementById(fragment)?.scrollIntoView({ behavior: 'smooth', block: 'start' }));
    });
  }
}
