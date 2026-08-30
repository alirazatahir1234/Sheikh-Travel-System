import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CtaBandComponent } from '../../shared/cta-band.component';
import { WebsiteSeoService } from '../../core/seo.service';
import { WebsitePublicContentService } from '../../core/website-public-content.service';

@Component({
  selector: 'app-gps-page',
  standalone: true,
  imports: [RouterLink, CtaBandComponent],
  template: `
    <section class="page-hero">
      <div class="container">
        <span class="section-kicker" style="color:#99f6e4">Platform</span>
        <h1>{{ pageTitle }}</h1>
        <p>{{ pageLead }}</p>
        <div style="margin-top:1.4rem">
          <a [routerLink]="primaryUrl" class="btn btn-primary">{{ primaryText }}</a>
        </div>
      </div>
    </section>

    <section class="section">
      <div class="container split">
        <div>
          <span class="section-kicker">Exact location</span>
          <h2 class="section-title">Addresses, not just coordinates</h2>
          <p class="section-lead">SheikhGo reverse-geocodes GPS points so operators see road and area context, with nearby places as secondary context.</p>
          <div class="sample">
            <strong>Toyota Corolla · LEC-8825</strong>
            <span>📍 Sialkot-Pasrur Road, Sialkot, Punjab, Pakistan</span>
            <em>Near Peak Performance Partners</em>
            <small>Speed 22 km/h · Ignition Off · GPS Active · Updated seconds ago</small>
          </div>
        </div>
        <div>
          <span class="section-kicker">Capabilities</span>
          <ul class="caps">
            @for (c of caps; track c) {
              <li>{{ c }}</li>
            }
          </ul>
        </div>
      </div>
    </section>

    <section class="section" style="background:#fff">
      <div class="container">
        <span class="section-kicker">Architecture</span>
        <h2 class="section-title">From device to dashboard</h2>
        <div class="arch">
          <span>Jimi GPS devices</span><span>→</span>
          <span>Traccar</span><span>→</span>
          <span>SheikhGo GPS Engine</span><span>→</span>
          <span>ERP &amp; Mobile</span>
        </div>
      </div>
    </section>
    <app-cta-band />
  `,
  styles: `
    .split { display:grid; grid-template-columns:1fr 1fr; gap:2rem; align-items:start; }
    .sample {
      margin-top:1.25rem; background:#fff; border:1px solid var(--sg-line); border-radius:14px; padding:1.2rem;
      display:grid; gap:.35rem;
    }
    .sample span { color:var(--sg-text); font-weight:600; }
    .sample em { color:var(--sg-teal); font-style:italic; }
    .sample small { color:var(--sg-muted); }
    .caps { margin:1rem 0 0; padding-left:1.1rem; color:var(--sg-muted); columns:1; }
    .caps li + li { margin-top:.4rem; }
    .arch {
      margin-top:1.25rem; display:flex; flex-wrap:wrap; gap:.5rem; align-items:center; font-weight:700;
    }
    .arch span:nth-child(odd) {
      background:var(--sg-mist); color:var(--sg-fleet); padding:.65rem .9rem; border-radius:999px;
    }
    @media (max-width:800px){ .split{grid-template-columns:1fr;} }
  `,
})
export class GpsPage implements OnInit {
  private readonly seo = inject(WebsiteSeoService);
  private readonly content = inject(WebsitePublicContentService);

  pageTitle = 'GPS Tracking';
  pageLead =
    'Know exactly where your vehicles are — with human-readable addresses, live status and journey playback.';
  primaryText = 'Request a Demo';
  primaryUrl = '/request-demo';

  readonly caps = [
    'Live vehicle location',
    'Online / offline / moving / idle / parked',
    'Speed, ignition, battery, last update',
    'Human-readable address + coordinates',
    'History, playback, stops & parking',
    'Geofences, alerts and device commands',
  ];

  ngOnInit(): void {
    this.seo.set(
      'GPS Tracking',
      'Real-time fleet GPS tracking with human-readable addresses, history playback, geofences and alerts.',
      '/gps-tracking',
    );

    this.content.getPage('gps-tracking').subscribe(page => {
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
      }
    });
  }
}
