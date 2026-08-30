import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CtaBandComponent } from '../../shared/cta-band.component';
import { WebsiteSeoService } from '../../core/seo.service';
import { PublicWebsiteFeature, WebsitePublicContentService } from '../../core/website-public-content.service';

@Component({
  selector: 'app-features-page',
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
      <div class="container grid">
        @for (f of features; track f.title) {
          <a class="card" [routerLink]="f.link">
            <h2>{{ f.title }}</h2>
            <p>{{ f.text }}</p>
          </a>
        }
      </div>
    </section>
    <app-cta-band />
  `,
  styles: `
    .grid { display:grid; grid-template-columns:repeat(3,1fr); gap:1rem; }
    .card {
      background:#fff; border:1px solid var(--sg-line); border-radius:14px; padding:1.35rem;
      transition: transform .18s ease, box-shadow .18s ease;
    }
    .card:hover { transform:translateY(-3px); box-shadow:0 14px 34px rgba(15,118,110,.12); }
    h2 { font-size:1.1rem; margin-bottom:.4rem; }
    p { color:var(--sg-muted); font-size:.95rem; }
    @media (max-width:900px){ .grid{grid-template-columns:1fr 1fr;} }
    @media (max-width:560px){ .grid{grid-template-columns:1fr;} }
  `,
})
export class FeaturesPage implements OnInit {
  private readonly seo = inject(WebsiteSeoService);
  private readonly content = inject(WebsitePublicContentService);

  pageTitle = 'Features';
  pageLead =
    'SheikhGo brings fleet management, GPS tracking, travel operations and AI-assisted insights into one platform.';

  features: { title: string; text: string; link: string }[] = [
    { title: 'Fleet Management', text: 'Vehicles, drivers, assignments and operational control.', link: '/fleet-management' },
    { title: 'GPS Tracking', text: 'Live map, history, playback, geofences and alerts.', link: '/gps-tracking' },
    { title: 'Vehicle Management', text: 'Registration, status, documents and device linkage.', link: '/fleet-management' },
    { title: 'Driver Management', text: 'Profiles, duty status, performance and history.', link: '/features' },
    { title: 'Trip Management', text: 'Create, monitor and complete trips with GPS context.', link: '/features' },
    { title: 'Maintenance', text: 'Schedules, work orders, vendors and history.', link: '/features' },
    { title: 'Fuel Management', text: 'Entries, cost, efficiency and reports.', link: '/features' },
    { title: 'Alerts & Notifications', text: 'Offline, overspeed, geofence and maintenance alerts.', link: '/gps-tracking' },
    { title: 'Reports & Analytics', text: 'Utilization, distance, speed, fuel and ops analytics.', link: '/features' },
    { title: 'SheikhGo AI', text: 'Ask operational questions across fleet data.', link: '/features' },
    { title: 'Security & Access', text: 'RBAC, tenant isolation, JWT and audit logging.', link: '/about' },
    { title: 'Mobile Apps', text: 'Driver and fleet apps connected to the same platform.', link: '/contact' },
  ];

  ngOnInit(): void {
    this.seo.set('Features', 'Explore SheikhGo fleet, GPS, trips, maintenance, fuel, alerts, analytics and AI capabilities.', '/features');

    this.content.getPage('features').subscribe(page => {
      if (!page?.page) return;
      this.pageTitle = page.page.title || this.pageTitle;
      this.pageLead = page.page.description || this.pageLead;
      if (page.page.metaTitle || page.page.metaDescription) {
        this.seo.set(page.page.metaTitle || this.pageTitle, page.page.metaDescription || this.pageLead);
      }
    });

    this.content.getFeatures().subscribe((list: PublicWebsiteFeature[]) => {
      if (!list.length) return;
      this.features = list.map(f => ({
        title: f.title,
        text: f.description || '',
        link: f.linkUrl || '/features',
      }));
    });
  }
}
