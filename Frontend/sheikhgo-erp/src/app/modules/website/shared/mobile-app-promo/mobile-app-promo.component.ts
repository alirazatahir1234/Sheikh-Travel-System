import { Component, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import {
  NEXUS_MOBILE_PROMO_POSTER,
  NEXUS_MOBILE_PROMO_TITLE,
} from '../../core/demo-media';
import {
  MOBILE_APP_BENEFIT,
  MOBILE_APP_NAME,
  MOBILE_APP_STORE_LINKS,
  MobileAppStoreId,
  MobileAppStoreLink,
} from '../../core/mobile-app';
import { WebsiteGoogleAnalyticsService } from '../../core/website-google-analytics.service';
import { WebsiteTranslatePipe } from '../website-translate.pipe';
import { NexusDemoModalComponent } from '../nexus-demo-modal/nexus-demo-modal.component';

/** Cohesive mobile-app promo: portrait thumbnail → modal, store links, Book a Demo. */
@Component({
  selector: 'app-mobile-app-promo',
  standalone: true,
  imports: [RouterLink, MatIconModule, WebsiteTranslatePipe, NexusDemoModalComponent],
  templateUrl: './mobile-app-promo.component.html',
  styleUrl: './mobile-app-promo.component.scss',
})
export class MobileAppPromoComponent {
  /** Full homepage section vs compact tracking-page banner. */
  readonly variant = input<'section' | 'banner'>('section');

  /** Analytics `source` param (home | live-tracking | gps-tracking). */
  readonly analyticsSource = input<string>('home');

  readonly appName = MOBILE_APP_NAME;
  readonly appBenefit = MOBILE_APP_BENEFIT;
  readonly storeLinks = MOBILE_APP_STORE_LINKS;
  readonly posterSrc = NEXUS_MOBILE_PROMO_POSTER;
  readonly videoTitle = NEXUS_MOBILE_PROMO_TITLE;

  readonly modalOpen = signal(false);

  private readonly analytics = inject(WebsiteGoogleAnalyticsService);

  openVideo(): void {
    this.analytics.trackEvent('mobile_promo_video_open', {
      source: this.analyticsSource(),
      video: 'sheikhgo-nexus-mobile-promo',
    });
    this.modalOpen.set(true);
  }

  closeModal(): void {
    this.modalOpen.set(false);
  }

  onStoreActivate(link: MobileAppStoreLink, event: Event): void {
    const status = link.url ? 'live' : 'placeholder';
    this.analytics.trackEvent('mobile_app_store_click', {
      store: link.id,
      status,
      source: this.analyticsSource(),
    });
    if (!link.url) {
      event.preventDefault();
    }
  }

  storeHref(link: MobileAppStoreLink): string {
    return link.url ?? `#mobile-app-store-${link.id}-coming-soon`;
  }

  storeLabelKey(id: MobileAppStoreId): string {
    return id === 'ios' ? 'mobileApp.store.ios' : 'mobileApp.store.android';
  }

  storeStatusKey(link: MobileAppStoreLink): string {
    return link.url ? 'mobileApp.store.open' : 'mobileApp.store.comingSoon';
  }
}
