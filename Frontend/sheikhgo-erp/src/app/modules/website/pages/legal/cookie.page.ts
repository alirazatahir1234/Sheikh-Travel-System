import { Component, OnInit, inject } from '@angular/core';
import { NgIf } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { WebsiteSeoService } from '../../core/seo.service';
import { WEBSITE_BRAND } from '../../core/brand';
import { WebsitePublicContentService } from '../../core/website-public-content.service';

@Component({
  selector: 'app-cookie-page',
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
            <h1>Cookie Policy</h1>
            <p class="meta">Effective date: 29 August 2026 · {{ brand.productName }}</p>
            <p>SheikhGo uses essential and session cookies to operate the website and authenticated applications.</p>
            <p><a [href]="'mailto:' + brand.privacyEmail">{{ brand.privacyEmail }}</a></p>
          </ng-template>
        </article>
      </div>
    </section>
  `,
})
export class CookiePage implements OnInit {
  readonly brand = WEBSITE_BRAND;
  private readonly seo = inject(WebsiteSeoService);
  private readonly content = inject(WebsitePublicContentService);
  private readonly sanitizer = inject(DomSanitizer);

  title = 'Cookie Policy';
  version: string | null = null;
  htmlContent: SafeHtml | null = null;

  ngOnInit(): void {
    this.seo.set('Cookie Policy', 'How SheikhGo uses cookies.', '/cookie-policy');
    this.content.getLegal('Cookie').subscribe(doc => {
      if (!doc?.content) return;
      this.title = doc.title;
      this.version = doc.version ?? null;
      this.htmlContent = this.sanitizer.bypassSecurityTrustHtml(doc.content);
    });
  }
}
