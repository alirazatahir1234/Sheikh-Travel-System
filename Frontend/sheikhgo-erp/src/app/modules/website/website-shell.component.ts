import { Component } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { SiteHeaderComponent } from './layout/site-header/site-header.component';
import { SiteFooterComponent } from './layout/site-footer/site-footer.component';
import { CookieConsentComponent } from './shared/cookie-consent/cookie-consent.component';

@Component({
  standalone: true,
  selector: 'app-website-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    SiteHeaderComponent,
    SiteFooterComponent,
    CookieConsentComponent,
  ],
  template: `
    <div class="website-shell">
      <app-site-header />
      <main class="website-main">
        <router-outlet />
      </main>
      <app-site-footer />
      <app-cookie-consent />
      <div class="mobile-cta">
        <a routerLink="/request-demo" class="btn btn-primary">Request a Quote</a>
      </div>
    </div>
  `,
  styleUrl: './website-shell.component.scss',
})
export class WebsiteShellComponent {}
