import { Component, OnInit, inject } from '@angular/core';
import { NgIf } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { WebsiteSeoService } from '../../core/seo.service';
import { WEBSITE_BRAND } from '../../core/brand';
import { WebsitePublicContentService } from '../../core/website-public-content.service';

@Component({
  selector: 'app-privacy-page',
  standalone: true,
  imports: [NgIf],
  template: `
    <section class="legal-doc">
      <div class="container">
        <article>
          <ng-container *ngIf="htmlContent; else fallback">
            <h1>{{ title }}</h1>
            <p class="meta" *ngIf="version">Version {{ version }}</p>
            <div [innerHTML]="htmlContent"></div>
          </ng-container>
          <ng-template #fallback>
            <h1>Privacy Policy</h1>
            <p class="meta">Effective date: 29 August 2026 · Operator: {{ brand.companyName }} / SheikhGo</p>
            <p>
              This Privacy Policy explains how SheikhGo collects and uses information when you use the SheikhGo website,
              ERP and related mobile applications. Prefer the published CMS version once the API is available.
            </p>
            <h2>Contact</h2>
            <p>
              Privacy: <a [href]="'mailto:' + brand.privacyEmail">{{ brand.privacyEmail }}</a><br />
              Support: <a [href]="'mailto:' + brand.supportEmail">{{ brand.supportEmail }}</a>
            </p>
          </ng-template>
        </article>
      </div>
    </section>
  `,
})
export class PrivacyPage implements OnInit {
  readonly brand = WEBSITE_BRAND;
  private readonly seo = inject(WebsiteSeoService);
  private readonly content = inject(WebsitePublicContentService);
  private readonly sanitizer = inject(DomSanitizer);

  title = 'Privacy Policy';
  version: string | null = null;
  htmlContent: SafeHtml | null = null;

  ngOnInit(): void {
    this.seo.set('Privacy Policy', 'How SheikhGo collects and protects data.', '/privacy-policy');
    this.content.getLegal('Privacy').subscribe(doc => {
      if (!doc?.content) return;
      this.title = doc.title;
      this.version = doc.version ?? null;
      this.htmlContent = this.sanitizer.bypassSecurityTrustHtml(doc.content);
    });
  }
}
