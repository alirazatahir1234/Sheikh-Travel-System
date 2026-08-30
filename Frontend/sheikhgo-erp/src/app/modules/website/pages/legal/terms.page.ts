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
            <h1>Terms &amp; Conditions</h1>
            <p class="meta">Effective date: 29 August 2026 · {{ brand.companyName }}</p>
            <p>By accessing or using SheikhGo you agree to these Terms. GPS accuracy depends on device and network conditions.</p>
            <p><a [href]="'mailto:' + brand.supportEmail">{{ brand.supportEmail }}</a></p>
          </ng-template>
        </article>
      </div>
    </section>
  `,
})
export class TermsPage implements OnInit {
  readonly brand = WEBSITE_BRAND;
  private readonly seo = inject(WebsiteSeoService);
  private readonly content = inject(WebsitePublicContentService);
  private readonly sanitizer = inject(DomSanitizer);

  title = 'Terms & Conditions';
  version: string | null = null;
  htmlContent: SafeHtml | null = null;

  ngOnInit(): void {
    this.seo.set('Terms & Conditions', 'Terms of use for SheikhGo.', '/terms-and-conditions');
    this.content.getLegal('Terms').subscribe(doc => {
      if (!doc?.content) return;
      this.title = doc.title;
      this.version = doc.version ?? null;
      this.htmlContent = this.sanitizer.bypassSecurityTrustHtml(doc.content);
    });
  }
}
