import { DOCUMENT } from '@angular/common';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter, Subscription } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CookieConsentService } from './core/cookie-consent.service';
import { WebsiteGoogleAnalyticsService } from './core/website-google-analytics.service';
import { WebsitePublicContentService } from './core/website-public-content.service';
import { SiteHeaderComponent } from './layout/site-header/site-header.component';
import { SiteFooterComponent } from './layout/site-footer/site-footer.component';
import { CookieConsentComponent } from './shared/cookie-consent/cookie-consent.component';
import { WebsiteWhatsAppFabComponent } from './shared/website-whatsapp-fab/website-whatsapp-fab.component';

/** Document class for marketing scrollport reset (styles.scss). */
const WEBSITE_SCROLL_CLASS = 'sg-website-public';

@Component({
  standalone: true,
  selector: 'app-website-shell',
  imports: [
    RouterOutlet,
    SiteHeaderComponent,
    SiteFooterComponent,
    CookieConsentComponent,
    WebsiteWhatsAppFabComponent,
  ],
  template: `
    <div class="website-shell">
      <app-site-header />
      <main class="website-main">
        <router-outlet />
      </main>
      <app-site-footer />
      <app-cookie-consent />
      <app-website-whatsapp-fab />
    </div>
  `,
  styleUrl: './website-shell.component.scss',
})
export class WebsiteShellComponent implements OnInit, OnDestroy {
  private readonly router = inject(Router);
  private readonly cookies = inject(CookieConsentService);
  private readonly analytics = inject(WebsiteGoogleAnalyticsService);
  private readonly content = inject(WebsitePublicContentService);
  private readonly doc = inject(DOCUMENT);
  private navSub?: Subscription;

  ngOnInit(): void {
    // ERP uses html/body height:100% (body scrollport). That breaks sticky
    // marketing nav and causes deeper-page scroll flicker — restore window scroll.
    this.doc.documentElement.classList.add(WEBSITE_SCROLL_CLASS);
    this.doc.body.classList.add(WEBSITE_SCROLL_CLASS);

    this.analytics.configure(environment.googleAnalyticsMeasurementId);
    this.cookies.applyConsentScripts();

    this.content.getSettings().subscribe(settings => {
      if (settings?.analyticsId?.trim()) {
        this.analytics.configure(settings.analyticsId);
        this.cookies.applyConsentScripts();
      }
    });

    this.navSub = this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe(e => this.analytics.trackPageView(e.urlAfterRedirects));
  }

  ngOnDestroy(): void {
    this.navSub?.unsubscribe();
    this.doc.documentElement.classList.remove(WEBSITE_SCROLL_CLASS);
    this.doc.body.classList.remove(WEBSITE_SCROLL_CLASS);
  }
}
