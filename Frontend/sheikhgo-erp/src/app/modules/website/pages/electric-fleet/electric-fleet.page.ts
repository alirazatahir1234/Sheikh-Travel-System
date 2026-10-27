import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { CtaBandComponent } from '../../shared/cta-band.component';
import { WebsiteSeoService } from '../../core/seo.service';
import { WebsiteTranslatePipe } from '../../shared/website-translate.pipe';

@Component({
  selector: 'app-electric-fleet-page',
  standalone: true,
  imports: [RouterLink, MatIconModule, CtaBandComponent, WebsiteTranslatePipe],
  template: `
    <section class="page-hero page-hero-cinematic" style="--hero-image: url('/website/nexus/modules/fuel.png')">
      <div class="container hero-layout">
        <div class="hero-copy">
          <span class="section-kicker">{{ 'pages.electricFleet.kicker' | wsTranslate }}</span>
          <h1>{{ 'pages.electricFleet.title' | wsTranslate }}</h1>
          <p class="hero-tagline">{{ 'pages.electricFleet.tagline' | wsTranslate }}</p>
          <p class="hero-lead">{{ 'pages.electricFleet.lead' | wsTranslate }}</p>
          <div class="hero-actions">
            <a routerLink="/request-demo" class="btn btn-primary">{{ 'common.requestDemo' | wsTranslate }}</a>
          </div>
        </div>
      </div>
    </section>
    <section class="section">
      <div class="container feature-grid">
        <article><mat-icon>battery_charging_full</mat-icon><h2>{{ 'pages.electricFleet.f1.title' | wsTranslate }}</h2><p>{{ 'pages.electricFleet.f1.text' | wsTranslate }}</p></article>
        <article><mat-icon>ev_station</mat-icon><h2>{{ 'pages.electricFleet.f2.title' | wsTranslate }}</h2><p>{{ 'pages.electricFleet.f2.text' | wsTranslate }}</p></article>
        <article><mat-icon>route</mat-icon><h2>{{ 'pages.electricFleet.f3.title' | wsTranslate }}</h2><p>{{ 'pages.electricFleet.f3.text' | wsTranslate }}</p></article>
      </div>
    </section>
    <app-cta-band />
  `,
  styles: [`
    .feature-grid { display: grid; gap: 1.5rem; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); }
    article { padding: 1.25rem; border-radius: 12px; background: #f8fafc; }
    mat-icon { color: #0f766e; margin-bottom: 0.5rem; }
  `],
})
export class ElectricFleetPage implements OnInit {
  private readonly seo = inject(WebsiteSeoService);
  ngOnInit(): void {
    this.seo.setPage({
      title: 'Electric fleet',
      description: 'EV battery, charging stations, range alerts, and range-aware trip assignment.',
      path: '/electric-fleet',
    });
  }
}
