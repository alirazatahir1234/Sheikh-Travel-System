import { Component, OnInit, inject } from '@angular/core';
import { NgIf } from '@angular/common';
import { RouterLink } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { WebsiteSeoService } from '../../core/seo.service';
import { WEBSITE_BRAND } from '../../core/brand';
import { WebsitePublicContentService } from '../../core/website-public-content.service';

@Component({
  selector: 'app-privacy-page',
  standalone: true,
  imports: [RouterLink, NgIf],
  templateUrl: './privacy.page.html',
  styleUrl: './privacy.page.scss',
})
export class PrivacyPage implements OnInit {
  readonly brand = WEBSITE_BRAND;
  private readonly seo = inject(WebsiteSeoService);
  private readonly content = inject(WebsitePublicContentService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly version = '1.0';
  readonly effectiveDate = '29 August 2026';
  readonly lastUpdated = '10 September 2026';
  readonly websiteUrl = 'https://www.sheikhgo.com';

  cmsTitle: string | null = null;
  cmsHtml: SafeHtml | null = null;
  cmsVersion: string | null = null;

  ngOnInit(): void {
    this.seo.set(
      'Privacy Policy',
      'How SheikhGo Fleet collects, uses, protects, and shares information — including GPS location data.',
      '/privacy-policy',
    );

    this.content.getLegal('Privacy').subscribe(doc => {
      if (!doc?.content?.trim()) return;
      this.cmsTitle = doc.title || 'Privacy Policy';
      this.cmsVersion = doc.version || null;
      this.cmsHtml = this.sanitizer.bypassSecurityTrustHtml(doc.content);
      this.seo.set(this.cmsTitle, 'Privacy Policy for SheikhGo Fleet and related services.', '/privacy-policy');
    });
  }
}
