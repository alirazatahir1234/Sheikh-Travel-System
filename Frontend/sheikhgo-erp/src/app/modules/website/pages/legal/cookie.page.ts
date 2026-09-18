import { Component, OnInit, inject } from '@angular/core';
import { NgIf } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { WebsiteSeoService } from '../../core/seo.service';
import { WEBSITE_BRAND } from '../../core/brand';
import { CookieConsentService } from '../../core/cookie-consent.service';
import { WebsitePublicContentService } from '../../core/website-public-content.service';

@Component({
  selector: 'app-cookie-page',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, NgIf],
  templateUrl: './cookie.page.html',
  styleUrl: './cookie.page.scss',
})
export class CookiePage implements OnInit {
  readonly brand = WEBSITE_BRAND;
  private readonly seo = inject(WebsiteSeoService);
  private readonly cookies = inject(CookieConsentService);
  private readonly content = inject(WebsitePublicContentService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly lastUpdated = '10 September 2026';

  cmsTitle: string | null = null;
  cmsHtml: SafeHtml | null = null;
  cmsVersion: string | null = null;

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

    this.content.getLegal('Cookie').subscribe(doc => {
      if (!doc?.content?.trim()) return;
      this.cmsTitle = doc.title || 'Cookie Policy';
      this.cmsVersion = doc.version || null;
      this.cmsHtml = this.sanitizer.bypassSecurityTrustHtml(doc.content);
      this.seo.set(this.cmsTitle, 'How SheikhGo uses cookies and how you can manage your preferences.', '/cookie-policy');
    });
  }

  openCookieSettings(): void {
    this.cookies.openCookieSettings();
  }
}
