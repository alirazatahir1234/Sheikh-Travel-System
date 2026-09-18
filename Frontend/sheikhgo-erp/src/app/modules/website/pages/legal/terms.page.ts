import { Component, OnInit, inject } from '@angular/core';
import { NgIf } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { WebsiteSeoService } from '../../core/seo.service';
import { WEBSITE_BRAND } from '../../core/brand';
import { WebsitePublicContentService } from '../../core/website-public-content.service';

@Component({
  selector: 'app-terms-page',
  standalone: true,
  imports: [NgIf],
  templateUrl: './terms.page.html',
  styleUrl: './terms.page.scss',
})
export class TermsPage implements OnInit {
  readonly brand = WEBSITE_BRAND;
  private readonly seo = inject(WebsiteSeoService);
  private readonly content = inject(WebsitePublicContentService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly version = '1.0';
  readonly lastUpdated = '10 September 2026';
  readonly websiteUrl = 'https://www.sheikhgo.com';

  cmsTitle: string | null = null;
  cmsHtml: SafeHtml | null = null;
  cmsVersion: string | null = null;

  ngOnInit(): void {
    this.seo.set(
      'Terms & Conditions',
      'Terms of use for SheikhGo Fleet and related services.',
      '/terms-and-conditions',
    );

    this.content.getLegal('Terms').subscribe(doc => {
      if (!doc?.content?.trim()) return;
      this.cmsTitle = doc.title || 'Terms & Conditions';
      this.cmsVersion = doc.version || null;
      this.cmsHtml = this.sanitizer.bypassSecurityTrustHtml(doc.content);
      this.seo.set(this.cmsTitle, 'Terms of use for SheikhGo Fleet and related services.', '/terms-and-conditions');
    });
  }
}
