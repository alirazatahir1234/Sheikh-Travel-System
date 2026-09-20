import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CtaBandComponent } from '../../shared/cta-band.component';
import { WebsiteSeoService } from '../../core/seo.service';
import { WebsitePublicContentService } from '../../core/website-public-content.service';

@Component({
  selector: 'app-fleet-page',
  standalone: true,
  imports: [RouterLink, CtaBandComponent],
  template: `
    <section class="page-hero">
      <div class="container">
        <span class="section-kicker" style="color:#99f6e4">Platform</span>
        <h1>{{ pageTitle }}</h1>
        <p>{{ pageLead }}</p>
        <div style="margin-top:1.4rem;display:flex;gap:.75rem;flex-wrap:wrap">
          <a [routerLink]="primaryUrl" class="btn btn-primary">{{ primaryText }}</a>
          <a [routerLink]="secondaryUrl" class="btn btn-ghost">{{ secondaryText }}</a>
        </div>
      </div>
    </section>

    <section class="section tour">
      <div class="container">
        <div class="tour-head">
          <span class="section-kicker">Inside the platform</span>
          <h2>What you actually get</h2>
          <p>Real screens from SheikhGo Fleet — not mockups.</p>
        </div>

        <figure class="shot shot-lead">
          <img [src]="shots[0].src" [alt]="shots[0].alt" width="1440" height="900" />
          <figcaption>
            <strong>{{ shots[0].title }}</strong>
            <span>{{ shots[0].text }}</span>
          </figcaption>
        </figure>

        <div class="shot-grid">
          @for (s of shots.slice(1); track s.src) {
            <figure class="shot">
              <img [src]="s.src" [alt]="s.alt" width="1440" height="900" loading="lazy" />
              <figcaption>
                <strong>{{ s.title }}</strong>
                <span>{{ s.text }}</span>
              </figcaption>
            </figure>
          }
        </div>
      </div>
    </section>

    <section class="section">
      <div class="container feature-list">
        @for (f of features; track f.title) {
          <article>
            <h2>{{ f.title }}</h2>
            <p>{{ f.text }}</p>
          </article>
        }
      </div>
    </section>
    <app-cta-band />
  `,
  styles: `
    .tour { background: var(--sg-page, #f5fbf9); }
    .tour-head { max-width: 640px; margin-bottom: 1.75rem; }
    .tour-head h2 { font-size: 1.9rem; margin: .5rem 0 .4rem; }
    .tour-head p { color: var(--sg-muted); }

    .shot {
      margin: 0;
      background: #fff;
      border: 1px solid var(--sg-line);
      border-radius: 16px;
      overflow: hidden;
      box-shadow: 0 14px 34px rgba(4, 31, 36, .08);
    }
    .shot img { display: block; width: 100%; height: auto; border-bottom: 1px solid var(--sg-line); }
    .shot figcaption { display: grid; gap: .2rem; padding: .95rem 1.1rem; }
    .shot figcaption strong { color: var(--sg-ink, #022f32); font-size: .975rem; }
    .shot figcaption span { color: var(--sg-muted); font-size: .875rem; }

    .shot-lead { margin-bottom: 1.25rem; }
    .shot-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1.25rem; }

    .feature-list {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 1rem;
    }
    article {
      background: #fff;
      border: 1px solid var(--sg-line);
      border-radius: 14px;
      padding: 1.35rem;
    }
    h2 { font-size: 1.15rem; margin-bottom: .4rem; }
    p { color: var(--sg-muted); }
    @media (max-width: 720px) {
      .feature-list { grid-template-columns: 1fr; }
      .shot-grid { grid-template-columns: 1fr; }
    }
  `,
})
export class FleetPage implements OnInit {
  private readonly seo = inject(WebsiteSeoService);
  private readonly content = inject(WebsitePublicContentService);

  pageTitle = 'Fleet Management';
  pageLead =
    'Operate vehicles, drivers, assignments, maintenance, fuel and live tracking from one control center.';
  primaryText = 'Request a Fleet Demo';
  primaryUrl = '/request-demo';
  secondaryText = 'GPS Tracking';
  secondaryUrl = '/gps-tracking';

  /** Captured from the running product via tools/capture-product-shots.mjs */
  readonly shots = [
    {
      src: '/website/product/live-map.png',
      alt: 'SheikhGo Fleet live GPS map showing tracked vehicles',
      title: 'Live GPS map',
      text: 'Every vehicle on one map with online state, speed and last update.',
    },
    {
      src: '/website/product/dashboard.png',
      alt: 'SheikhGo Fleet operations dashboard',
      title: 'Operations dashboard',
      text: 'Fleet health, active trips, alerts and utilisation at a glance.',
    },
    {
      src: '/website/product/vehicles.png',
      alt: 'SheikhGo Fleet vehicle register',
      title: 'Vehicle register',
      text: 'Plates, status, documents and the GPS device linked to each vehicle.',
    },
    {
      src: '/website/product/trips.png',
      alt: 'SheikhGo Fleet trip management screen',
      title: 'Trips',
      text: 'Plan, monitor and close trips with full GPS history behind each one.',
    },
    {
      src: '/website/product/drivers.png',
      alt: 'SheikhGo Fleet driver management screen',
      title: 'Drivers',
      text: 'Profiles, assignments, duty status and activity history.',
    },
    {
      src: '/website/product/reports.png',
      alt: 'SheikhGo Fleet reports and analytics',
      title: 'Reports',
      text: 'Utilisation, distance, fuel and cost reporting for managers.',
    },
  ];

  features = [
    { title: 'Fleet Dashboard', text: 'Operational overview of vehicles, drivers, online status, trips, alerts and maintenance.' },
    { title: 'Vehicles', text: 'Registration, plate, make/model, status, GPS device linkage and document records.' },
    { title: 'Drivers', text: 'Profiles, assignments, duty status, performance and activity history.' },
    { title: 'Assignments', text: 'Assign vehicles and drivers with clear operational ownership.' },
    { title: 'Live Tracking', text: 'Real-time location, online/offline state and speed on the fleet map.' },
    { title: 'Maintenance & Fuel', text: 'Service schedules, work orders, fuel entries and cost visibility.' },
    { title: 'Inspections & Compliance', text: 'Track inspections and compliance workflows across the fleet.' },
    { title: 'Alerts & Reports', text: 'Actionable alerts plus operational reports for managers.' },
  ];

  ngOnInit(): void {
    this.seo.set(
      'Fleet Management',
      'Manage vehicles, drivers, assignments, tracking, maintenance and fuel with SheikhGo.',
      '/fleet-management',
    );

    this.content.getPage('fleet-management').subscribe(page => {
      if (!page?.page) return;
      this.pageTitle = page.page.title || this.pageTitle;
      this.pageLead = page.page.description || this.pageLead;
      if (page.page.metaTitle || page.page.metaDescription) {
        this.seo.set(page.page.metaTitle || this.pageTitle, page.page.metaDescription || this.pageLead);
      }
      const hero = this.content.sectionByType(page.sections, 'Hero');
      if (hero) {
        if (hero.title) this.pageTitle = hero.title;
        if (hero.content || hero.subtitle) this.pageLead = hero.content || hero.subtitle || this.pageLead;
        if (hero.buttonText) this.primaryText = hero.buttonText;
        if (hero.buttonUrl) this.primaryUrl = hero.buttonUrl;
        if (hero.secondaryButtonText) this.secondaryText = hero.secondaryButtonText;
        if (hero.secondaryButtonUrl) this.secondaryUrl = hero.secondaryButtonUrl;
      }
    });
  }
}
