import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { CtaBandComponent } from '../../shared/cta-band.component';
import { WebsiteSeoService } from '../../core/seo.service';
import { WebsiteTranslatePipe } from '../../shared/website-translate.pipe';

@Component({
  selector: 'app-fuel-page',
  standalone: true,
  imports: [RouterLink, MatIconModule, CtaBandComponent, WebsiteTranslatePipe],
  template: `
    <section class="page-hero page-hero-cinematic" style="--hero-image: url('/website/nexus/fuel.png')">
      <div class="container">
        <span class="section-kicker" style="color:#99f6e4">{{ 'pages.fuel.kicker' | wsTranslate }}</span>
        <h1>{{ 'pages.fuel.title' | wsTranslate }}</h1>
        <p class="hero-lead">{{ 'pages.fuel.lead' | wsTranslate }}</p>
        <div class="hero-actions">
          <a routerLink="/request-demo" class="btn btn-primary">{{ 'common.requestDemo' | wsTranslate }}</a>
          <a routerLink="/gps-alerts" class="btn btn-ghost">{{ 'common.gpsAlertsArrow' | wsTranslate }}</a>
        </div>
      </div>
    </section>

    <section class="section section-band-white">
      <div class="container feature-grid">
        @for (f of features; track f.icon) {
          <article class="feature-card">
            <mat-icon>{{ f.icon }}</mat-icon>
            <h2>{{ f.titleKey | wsTranslate }}</h2>
            <p>{{ f.textKey | wsTranslate }}</p>
          </article>
        }
      </div>
    </section>

    <section class="section section-band-alt">
      <div class="container integration">
        <span class="section-kicker">{{ 'pages.fuel.integrationKicker' | wsTranslate }}</span>
        <h2 class="section-title">{{ 'pages.fuel.integrationTitle' | wsTranslate }}</h2>
        <p class="section-lead">{{ 'pages.fuel.integrationLead' | wsTranslate }}</p>
        <ul>
          <li>{{ 'pages.fuel.integration.obd' | wsTranslate }}</li>
          <li>{{ 'pages.fuel.integration.station' | wsTranslate }}</li>
          <li>{{ 'pages.fuel.integration.receipts' | wsTranslate }}</li>
          <li>{{ 'pages.fuel.integration.fraud' | wsTranslate }}</li>
        </ul>
      </div>
    </section>

    <app-cta-band />
  `,
  styles: `
    .feature-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
      gap: 1.25rem;
    }
    .feature-card {
      background: #fff;
      border: 1px solid var(--sg-line);
      border-radius: 16px;
      padding: 1.25rem;
      box-shadow: 0 12px 32px rgba(4, 31, 36, 0.06);
    }
    .feature-card mat-icon {
      color: var(--sg-teal, #0f766e);
      font-size: 28px;
      width: 28px;
      height: 28px;
    }
    .feature-card h2 {
      font-size: 1.1rem;
      margin: 0.75rem 0 0.35rem;
    }
    .feature-card p {
      margin: 0;
      color: var(--sg-muted);
      font-size: 0.95rem;
    }
    .integration {
      max-width: 42rem;
    }
    .integration ul {
      margin: 1rem 0 0;
      padding-left: 1.1rem;
      color: var(--sg-muted);
    }
    .integration li + li {
      margin-top: 0.35rem;
    }
  `
})
export class FuelPage implements OnInit {
  private readonly seo = inject(WebsiteSeoService);

  readonly features = [
    {
      icon: 'local_gas_station',
      titleKey: 'pages.fuel.feature.obd.title',
      textKey: 'pages.fuel.feature.obd.text'
    },
    {
      icon: 'place',
      titleKey: 'pages.fuel.feature.station.title',
      textKey: 'pages.fuel.feature.station.text'
    },
    {
      icon: 'speed',
      titleKey: 'pages.fuel.feature.efficiency.title',
      textKey: 'pages.fuel.feature.efficiency.text'
    },
    {
      icon: 'receipt_long',
      titleKey: 'pages.fuel.feature.logs.title',
      textKey: 'pages.fuel.feature.logs.text'
    },
    {
      icon: 'gpp_bad',
      titleKey: 'pages.fuel.feature.fraud.title',
      textKey: 'pages.fuel.feature.fraud.text'
    }
  ] as const;

  ngOnInit(): void {
    this.seo.set(
      'Fuel Management — SheikhGo Nexus',
      'OBD fuel telemetry, petrol-station stop alerts, fuel receipts, and km/L reports by vehicle, driver, and route in SheikhGo Nexus.',
      '/fuel-management'
    );
  }
}
