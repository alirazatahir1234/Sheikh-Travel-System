import { Component, OnInit, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { WebsiteSeoService } from '../../core/seo.service';
import { WEBSITE_BRAND } from '../../core/brand';
import { CookieConsentService } from '../../core/cookie-consent.service';

@Component({
  selector: 'app-cookie-page',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './cookie.page.html',
  styleUrl: './cookie.page.scss',
})
export class CookiePage implements OnInit {
  readonly brand = WEBSITE_BRAND;
  private readonly seo = inject(WebsiteSeoService);
  private readonly cookies = inject(CookieConsentService);

  readonly lastUpdated = '10 September 2026';

  readonly legalLinks = [
    { path: '/privacy-policy', label: 'Privacy Policy' },
    { path: '/cookie-policy', label: 'Cookie Policy' },
    { path: '/terms-and-conditions', label: 'Terms & Conditions' },
    { path: '/delete-account', label: 'Delete Account' },
  ] as const;

  ngOnInit(): void {
    this.seo.set(
      'Cookie Policy',
      'How SheikhGo uses cookies and how you can manage your preferences.',
      '/cookie-policy',
    );
  }

  openCookieSettings(): void {
    this.cookies.openCookieSettings();
  }
}
