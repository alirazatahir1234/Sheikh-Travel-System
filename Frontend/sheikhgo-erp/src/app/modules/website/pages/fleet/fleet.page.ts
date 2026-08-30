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
