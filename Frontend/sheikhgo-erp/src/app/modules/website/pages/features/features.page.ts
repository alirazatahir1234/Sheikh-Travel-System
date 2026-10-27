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
        <span class="section-kicker" style="color:#99f6e4">Platform</span>
        <h1>{{ pageTitle }}</h1>
        <p>{{ pageLead }}</p>
        <div class="hero-actions">
          <a routerLink="/request-demo" class="btn btn-primary">Request a Demo</a>
          <a routerLink="/gps-tracking" class="btn btn-ghost">GPS Tracking →</a>
        </div>
      </div>
    </section>
    <section class="section section-band-white">
      <div class="container premium-card-grid">
        @for (f of features; track f.title) {
          <a class="premium-card" [routerLink]="f.link">
            <h2>{{ f.title }}</h2>
            <p>{{ f.text }}</p>
            <span style="font-size:.85rem;font-weight:700;color:var(--sg-teal);margin-top:.5rem;display:inline-block">Learn more →</span>
          </a>
        }
      </div>
    </section>
    <app-cta-band />
  `,
  styles: `
    h2 { font-size:1.1rem; margin-bottom:.4rem; }
    a.premium-card { text-decoration:none; color:inherit; display:grid; }
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
    { title: 'Live Tracking', text: 'Real-time fleet map, speed and status, geofence layers and history playback.', link: '/live-tracking' },
    { title: 'GPS Tracking', text: 'Overview of fleet GPS — addresses, capabilities and links to live map and alerts.', link: '/gps-tracking' },
    { title: 'Vehicle Management', text: 'Registration, status, documents and device linkage.', link: '/fleet-management' },
    { title: 'Driver Performance', text: 'Safety, fuel, on-time scoring, rewards and coaching flags.', link: '/driver-performance' },
    { title: 'Driver Management', text: 'Profiles, duty status, performance and history.', link: '/features' },
    { title: 'Trip Management', text: 'Create, monitor and complete trips with GPS context.', link: '/features' },
    { title: 'Predictive Maintenance', text: 'Mileage-based service before breakdowns; driver reminders.', link: '/predictive-maintenance' },
    { title: 'Accident risk scores', text: 'Driver and route risk from accidents, speed, fatigue, and route difficulty.', link: '/accident-risk' },
    { title: 'Client Tracking Links', text: 'Tokenized public shipment tracking — mobile-friendly.', link: '/client-tracking' },
    { title: 'Electric fleet', text: 'Battery %, charging sites, range alerts, EV-aware assignment.', link: '/electric-fleet' },
    { title: 'Document tracker', text: 'Insurance, registration, fitness expiry and renewal alerts.', link: '/document-tracker' },
    { title: 'Anomaly detection', text: 'Night driving, speed drops, off-route — admin review.', link: '/anomaly-detection' },
    { title: 'Maintenance', text: 'Schedules, work orders, vendors and history.', link: '/features' },
    { title: 'Fuel Management', text: 'Entries, cost, efficiency and reports.', link: '/features' },
    { title: 'Alerts & Notifications', text: 'Safety, tracking and vehicle health alerts — SOS, road speed, geofence, fuel and more.', link: '/gps-alerts' },
    { title: 'Reports & Analytics', text: 'Utilization, distance, speed, fuel and ops analytics.', link: '/features' },
    { title: 'SheikhGo AI Assistant', text: 'Ask operational questions across fleet data.', link: '/features' },
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
