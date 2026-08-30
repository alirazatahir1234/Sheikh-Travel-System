import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CtaBandComponent } from '../../shared/cta-band.component';
import { WebsiteSeoService } from '../../core/seo.service';
import { WEBSITE_BRAND } from '../../core/brand';
import { WebsitePublicContentService } from '../../core/website-public-content.service';

@Component({
  selector: 'app-about-page',
  standalone: true,
  imports: [RouterLink, CtaBandComponent],
  template: `
    <section class="page-hero">
      <div class="container">
        <h1>{{ pageTitle }}</h1>
        <p>{{ pageLead }}</p>
      </div>
    </section>
    <section class="section">
      <div class="container prose">
        @if (bodyHtml) {
          <div [innerHTML]="bodyHtml"></div>
        } @else {
          <h2>About SheikhGo</h2>
          <p>
            SheikhGo is an intelligent fleet and travel management platform from {{ brand.companyName }}.
            It brings vehicles, drivers, trips, GPS tracking, maintenance, fuel and operations together
            so teams can move smarter and travel further.
          </p>
          <h2>Mission</h2>
          <p>
            To simplify transportation and fleet operations through intelligent technology, real-time data and automation.
          </p>
          <h2>Vision</h2>
          <p>
            A single platform where fleet operators, travel businesses and enterprise teams run day-to-day operations
            with clarity, control and trustworthy location intelligence.
          </p>
          <h2>Technology</h2>
          <p>
            SheikhGo connects GPS devices through Traccar into a dedicated GPS engine, then surfaces live tracking,
            history, alerts and operational workflows in the web ERP and mobile apps — with role-based access and tenant isolation.
          </p>
        }
        <a routerLink="/contact" class="btn btn-primary" style="margin-top:1.5rem">Contact us</a>
      </div>
    </section>
    <app-cta-band />
  `,
  styles: `
    .prose { max-width: 720px; }
    h2 { font-size: 1.25rem; margin: 1.75rem 0 .6rem; }
    p { color: var(--sg-muted); }
  `,
})
export class AboutPage implements OnInit {
  readonly brand = WEBSITE_BRAND;
  private readonly seo = inject(WebsiteSeoService);
  private readonly content = inject(WebsitePublicContentService);

  pageTitle = `About ${WEBSITE_BRAND.companyName}`;
  pageLead = 'Building intelligent technology for transportation and fleet operations.';
  bodyHtml: string | null = null;

  ngOnInit(): void {
    this.seo.set('About', `About ${WEBSITE_BRAND.companyName} and the SheikhGo fleet & travel management platform.`, '/about');

    this.content.getPage('about').subscribe(page => {
      if (!page?.page) return;
      this.pageTitle = page.page.title || this.pageTitle;
      this.pageLead = page.page.description || this.pageLead;
      if (page.page.metaTitle || page.page.metaDescription) {
        this.seo.set(page.page.metaTitle || this.pageTitle, page.page.metaDescription || this.pageLead);
      }
      const hero = this.content.sectionByType(page.sections, 'Hero');
      if (hero?.content) this.bodyHtml = hero.content;
      if (hero?.title) this.pageTitle = hero.title;
      if (hero?.subtitle) this.pageLead = hero.subtitle;
    });
  }
}
