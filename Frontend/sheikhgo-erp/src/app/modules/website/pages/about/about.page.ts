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
          <h2>Who we are</h2>
          <p>
            {{ brand.companyName }} builds technology for businesses that run vehicles and operations.
            Our flagship product is SheikhGo Fleet — and we also deliver custom software, mobile apps, and AI
            integrations when a ready-made product is not enough.
          </p>
          <h2>SheikhGo Fleet</h2>
          <p>
            An intelligent fleet and travel management platform that brings vehicles, drivers, trips, GPS tracking,
            maintenance, fuel and operations together so teams can move with clearer control.
          </p>
          <h2>Mission</h2>
          <p>
            To simplify transportation and fleet operations through practical software, real-time data and
            automation — without locking operators into rigid, one-size workflows.
          </p>
          <h2>Vision</h2>
          <p>
            A single platform where fleet operators, travel businesses and enterprise teams run day-to-day operations
            with clarity, control and trustworthy location intelligence.
          </p>
          <h2>Technology</h2>
          <p>
            SheikhGo connects GPS devices through Traccar into a dedicated GPS engine, then surfaces live tracking,
            history, alerts and operational workflows in the web ERP and mobile apps — with role-based access and
            tenant isolation.
          </p>
        }
        <div class="about-actions">
          <a routerLink="/fleet-management" class="btn btn-primary">Explore SheikhGo Fleet</a>
          <a routerLink="/contact" class="btn btn-outline">Contact us</a>
        </div>
      </div>
    </section>
    <app-cta-band
      title="Ready to see the platform?"
      lead="Book a short fleet demo, or tell us about a custom software need."
      primaryText="Book a Fleet Demo"
      primaryUrl="/request-demo"
      secondaryText="Contact Us →"
      secondaryUrl="/contact"
    />
  `,
  styles: `
    .prose {
      max-width: 720px;
      width: 100%;
      overflow-wrap: break-word;
    }
    h2 {
      font-size: 1.25rem;
      margin: 1.75rem 0 0.6rem;
    }
    p {
      color: var(--sg-muted);
      overflow-wrap: break-word;
    }
    .about-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
      margin-top: 1.75rem;
    }
  `,
})
export class AboutPage implements OnInit {
  readonly brand = WEBSITE_BRAND;
  private readonly seo = inject(WebsiteSeoService);
  private readonly content = inject(WebsitePublicContentService);

  pageTitle = `About ${WEBSITE_BRAND.companyName}`;
  pageLead =
    'Fleet software for operators — and custom technology when your business needs a tailored build.';
  bodyHtml: string | null = null;

  ngOnInit(): void {
    this.seo.set(
      'About',
      `About ${WEBSITE_BRAND.companyName}: SheikhGo Fleet and custom software for transport and logistics.`,
      '/about',
    );

    this.content.getPage('about').subscribe(page => {
      if (!page?.page) return;
      this.pageTitle = page.page.title || this.pageTitle;
      this.pageLead = page.page.description || this.pageLead;
      if (page.page.metaTitle || page.page.metaDescription) {
        this.seo.set(
          page.page.metaTitle || this.pageTitle,
          page.page.metaDescription || this.pageLead,
          '/about',
        );
      }
      const hero = this.content.sectionByType(page.sections, 'Hero');
      if (hero?.content) this.bodyHtml = hero.content;
      if (hero?.title) this.pageTitle = hero.title;
      if (hero?.subtitle) this.pageLead = hero.subtitle;
    });
  }
}
