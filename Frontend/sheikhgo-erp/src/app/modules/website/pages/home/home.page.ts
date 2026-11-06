import { AfterViewInit, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { MatIconModule } from '@angular/material/icon';
import { WebsiteSeoService } from '../../core/seo.service';
import { WEBSITE_BRAND } from '../../core/brand';
import { WebsitePublicContentService } from '../../core/website-public-content.service';
import { WebsiteI18nService } from '../../core/website-i18n.service';
import {
  NEXUS_DASHBOARD_IMAGE,
  PLATFORM_OVERVIEW_CARDS,
  PRODUCT_SHOWCASE_TABS,
} from '../../core/site-catalog';
import { WebsiteTranslatePipe } from '../../shared/website-translate.pipe';
import { NexusDemoModalComponent } from '../../shared/nexus-demo-modal/nexus-demo-modal.component';
import { MobileAppPromoComponent } from '../../shared/mobile-app-promo/mobile-app-promo.component';
import { NEXUS_MOBILE_PROMO_POSTER, NexusDemoModalMode } from '../../core/demo-media';
import { WebsiteGoogleAnalyticsService } from '../../core/website-google-analytics.service';

@Component({
  selector: 'app-home-page',
  standalone: true,
  imports: [
    RouterLink,
    MatIconModule,
    NexusDemoModalComponent,
    MobileAppPromoComponent,
    WebsiteTranslatePipe,
  ],
  templateUrl: './home.page.html',
  styleUrl: './home.page.scss',
})
export class HomePage implements OnInit, AfterViewInit, OnDestroy {
  readonly brand = WEBSITE_BRAND;
  readonly i18n = inject(WebsiteI18nService);
  private readonly seo = inject(WebsiteSeoService);
  private readonly content = inject(WebsitePublicContentService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly analytics = inject(WebsiteGoogleAnalyticsService);
  private fragmentSub?: Subscription;

  nexusImageError = false;
  readonly nexusDashboardSrc = NEXUS_DASHBOARD_IMAGE.src;
  readonly nexusDashboardAlt = NEXUS_DASHBOARD_IMAGE.alt;
  readonly mobilePromoPosterSrc = NEXUS_MOBILE_PROMO_POSTER;
  readonly demoModalOpen = signal(false);
  readonly demoModalMode = signal<NexusDemoModalMode>('erp');

  readonly platformOverviewCards = PLATFORM_OVERVIEW_CARDS;
  readonly productShowcaseTabs = PRODUCT_SHOWCASE_TABS;
  readonly showcaseTabIndex = signal(0);

  readonly activeShowcaseTab = computed(() => {
    const tabs = this.productShowcaseTabs;
    const i = this.showcaseTabIndex();
    return tabs[Math.min(Math.max(i, 0), tabs.length - 1)]!;
  });

  readonly heroPerks = computed(() => {
    this.i18n.lang();
    return [
      { icon: 'my_location', text: this.i18n.t('home.perk.visibility') },
      { icon: 'hub', text: this.i18n.t('home.perk.operations') },
      { icon: 'insights', text: this.i18n.t('home.perk.analytics') },
    ] as const;
  });

  selectShowcaseTab(index: number): void {
    const max = this.productShowcaseTabs.length - 1;
    this.showcaseTabIndex.set(Math.min(Math.max(index, 0), max));
  }

  onShowcaseTabKeydown(event: KeyboardEvent, index: number): void {
    const count = this.productShowcaseTabs.length;
    let next = index;
    switch (event.key) {
      case 'ArrowRight':
      case 'ArrowDown':
        next = (index + 1) % count;
        break;
      case 'ArrowLeft':
      case 'ArrowUp':
        next = (index - 1 + count) % count;
        break;
      case 'Home':
        next = 0;
        break;
      case 'End':
        next = count - 1;
        break;
      default:
        return;
    }
    event.preventDefault();
    this.selectShowcaseTab(next);
    requestAnimationFrame(() => {
      (
        document.getElementById(`showcase-tab-${this.productShowcaseTabs[next]!.id}`) as HTMLButtonElement | null
      )?.focus();
    });
  }

  readonly subscriptionTiers = computed(() => {
    this.i18n.lang();
    return [
      {
        id: 'starter',
        name: this.i18n.t('pages.pricing.starter.name'),
        fleet: this.i18n.t('pages.pricing.starter.fleet'),
        price: '79,000',
        featured: false,
      },
      {
        id: 'pro',
        name: this.i18n.t('pages.pricing.pro.name'),
        fleet: this.i18n.t('pages.pricing.pro.fleet'),
        price: '119,000',
        featured: true,
        badge: this.i18n.t('pages.pricing.pro.badge'),
      },
      {
        id: 'enterprise',
        name: this.i18n.t('pages.pricing.enterprise.name'),
        fleet: this.i18n.t('pages.pricing.enterprise.fleet'),
        price: '165,000',
        featured: false,
      },
    ];
  });

  /** Concise 6-card platform highlights replacing repetitive 3-card feature dumps. */
  readonly platformHighlights = computed(() => {
    this.i18n.lang();
    return [
      {
        title: this.i18n.t('home.highlights.gps.title'),
        text: this.i18n.t('home.highlights.gps.text'),
        icon: 'my_location',
        link: '/live-tracking',
        linkLabel: this.i18n.t('home.highlights.gps.linkLabel'),
      },
      {
        title: this.i18n.t('home.highlights.alerts.title'),
        text: this.i18n.t('home.highlights.alerts.text'),
        icon: 'notifications_active',
        link: '/gps-alerts',
        linkLabel: this.i18n.t('home.highlights.alerts.linkLabel'),
      },
      {
        title: this.i18n.t('home.highlights.dispatch.title'),
        text: this.i18n.t('home.highlights.dispatch.text'),
        icon: 'route',
        link: '/fleet-management',
        linkLabel: this.i18n.t('home.highlights.dispatch.linkLabel'),
      },
      {
        title: this.i18n.t('home.highlights.fuel.title'),
        text: this.i18n.t('home.highlights.fuel.text'),
        icon: 'local_gas_station',
        link: '/fuel-management',
        linkLabel: this.i18n.t('home.highlights.fuel.linkLabel'),
      },
      {
        title: this.i18n.t('home.highlights.drivers.title'),
        text: this.i18n.t('home.highlights.drivers.text'),
        icon: 'verified_user',
        link: '/driver-performance',
        linkLabel: this.i18n.t('home.highlights.drivers.linkLabel'),
      },
      {
        title: this.i18n.t('home.highlights.maintenance.title'),
        text: this.i18n.t('home.highlights.maintenance.text'),
        icon: 'build_circle',
        link: '/predictive-maintenance',
        linkLabel: this.i18n.t('home.highlights.maintenance.linkLabel'),
      },
    ] as const;
  });

  readonly aiFeatures = [
    'AI Assistant & chat gateway',
    'Nexus AI Management Center',
    'Fleet health & morning digest',
    'Heuristic recommendations',
    'WhatsApp AI Assist (human-approved)',
    'Driver & fleet mobile copilot',
  ] as const;

  readonly aiSpotlightCards = [
    {
      title: 'Tool-first assistant',
      text: 'Structured answers from trips, fuel, alerts, and live map data.',
      icon: 'support_agent',
    },
    {
      title: 'Configurable LLM providers',
      text: 'Optional natural-language layer using Google Gemini, Ollama, or DeepInfra when enabled.',
      icon: 'hub',
    },
  ] as const;

  private static readonly HOME_SEO_TITLE =
    'SheikhGo | Fleet Management, GPS Tracking & Mobility Solutions';
  private static readonly HOME_SEO_DESCRIPTION =
    "Manage fleet operations, GPS tracking, vehicle rentals, and travel workflows with SheikhGo's connected mobility platform.";

  private static readonly LEGACY_FRAGMENT_MAP: Readonly<Record<string, string>> = {
    'see-the-platform': 'product-showcase',
    'platform-modules': 'product-showcase',
    'business-benefits': 'platform-features',
    'industries-serve': 'platform-features',
    'client-billing': 'subscription-plans',
    'driver-payments': 'platform-features',
    'smart-assignment': 'platform-features',
    'live-traffic': 'platform-features',
    'driver-verification': 'platform-features',
    'driver-voice': 'platform-features',
    'fatigue-detection': 'platform-features',
    'driver-offline-sync': 'platform-features',
    'client-testimonials': 'platform-features',
    news: 'platform-features',
  };

  ngOnInit(): void {
    this.seo.set(HomePage.HOME_SEO_TITLE, HomePage.HOME_SEO_DESCRIPTION, '/');

    this.content.getHome().subscribe(home => {
      if (!home?.settings) return;
      if (home.settings.defaultMetaTitle || home.settings.defaultMetaDescription) {
        this.seo.set(
          home.settings.defaultMetaTitle || HomePage.HOME_SEO_TITLE,
          home.settings.defaultMetaDescription || HomePage.HOME_SEO_DESCRIPTION,
        );
      }
    });
  }

  openDemoModal(): void {
    this.demoModalMode.set('erp');
    this.demoModalOpen.set(true);
  }

  openPromoModal(): void {
    this.demoModalMode.set('promo');
    this.demoModalOpen.set(true);
  }

  openMobilePromoModal(): void {
    this.analytics.trackEvent('mobile_promo_video_open', {
      source: 'home',
      video: 'sheikhgo-nexus-mobile-promo',
    });
    this.demoModalMode.set('mobile');
    this.demoModalOpen.set(true);
  }

  closeDemoModal(): void {
    this.demoModalOpen.set(false);
    const fragment = this.route.snapshot.fragment;
    if (fragment === 'watch-demo' || fragment === 'watch-promo' || fragment === 'watch-mobile') {
      void this.router.navigate([], { fragment: undefined, replaceUrl: true });
    }
  }

  scrollToHomeSection(event: Event, fragment: string): void {
    event.preventDefault();
    void this.router.navigate(['/'], { fragment }).then(() => this.scrollToFragmentId(fragment));
  }

  private scrollToFragmentId(fragment: string): void {
    const targetId = HomePage.LEGACY_FRAGMENT_MAP[fragment] ?? fragment;
    requestAnimationFrame(() => {
      document.getElementById(targetId)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
  }

  ngAfterViewInit(): void {
    this.fragmentSub = this.route.fragment.subscribe(fragment => {
      if (!fragment) return;
      if (fragment === 'ai-solutions' || fragment === 'home-ai') {
        void this.router.navigate(['/ai-solutions'], { replaceUrl: true });
        return;
      }
      if (fragment === 'watch-demo') {
        this.demoModalMode.set('erp');
        this.demoModalOpen.set(true);
        return;
      }
      if (fragment === 'watch-promo') {
        this.demoModalMode.set('promo');
        this.demoModalOpen.set(true);
        return;
      }
      if (fragment === 'watch-mobile') {
        this.demoModalMode.set('mobile');
        this.demoModalOpen.set(true);
        return;
      }
      setTimeout(() => this.scrollToFragmentId(fragment), 0);
    });
  }

  ngOnDestroy(): void {
    this.fragmentSub?.unsubscribe();
  }
}
