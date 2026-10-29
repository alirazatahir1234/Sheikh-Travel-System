import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { CtaBandComponent } from '../../shared/cta-band.component';
import { WebsiteSeoService } from '../../core/seo.service';
import { WebsiteTranslatePipe } from '../../shared/website-translate.pipe';
import { websiteCssUrl } from '../../core/website-media';

@Component({
  selector: 'app-driver-performance-page',
  standalone: true,
  imports: [RouterLink, MatIconModule, CtaBandComponent, WebsiteTranslatePipe],
  template: `
    <section class="page-hero page-hero-cinematic" [style.--hero-image]="heroImageCss">
      <div class="container hero-layout">
        <div class="hero-copy">
          <span class="section-kicker">{{ 'pages.driverPerformance.kicker' | wsTranslate }}</span>
          <h1>{{ 'pages.driverPerformance.title' | wsTranslate }}</h1>
          <p class="hero-tagline">{{ 'pages.driverPerformance.tagline' | wsTranslate }}</p>
          <p class="hero-lead">{{ 'pages.driverPerformance.lead' | wsTranslate }}</p>
          <div class="hero-actions">
            <a routerLink="/request-demo" class="btn btn-primary">{{ 'common.requestDemo' | wsTranslate }}</a>
            <a routerLink="/live-tracking" class="btn btn-ghost">{{ 'common.liveTrackingArrow' | wsTranslate }}</a>
            <a routerLink="/driver-ratings" class="btn btn-ghost">{{ 'pages.driverPerformance.clientRatingsLink' | wsTranslate }}</a>
          </div>
        </div>
      </div>
    </section>
    <section class="section">
      <div class="container feature-grid">
        <article><mat-icon aria-hidden="true">shield</mat-icon><h2>{{ 'pages.driverPerformance.f1.title' | wsTranslate }}</h2><p>{{ 'pages.driverPerformance.f1.text' | wsTranslate }}</p></article>
        <article><mat-icon aria-hidden="true">local_gas_station</mat-icon><h2>{{ 'pages.driverPerformance.f2.title' | wsTranslate }}</h2><p>{{ 'pages.driverPerformance.f2.text' | wsTranslate }}</p></article>
        <article><mat-icon aria-hidden="true">schedule</mat-icon><h2>{{ 'pages.driverPerformance.f3.title' | wsTranslate }}</h2><p>{{ 'pages.driverPerformance.f3.text' | wsTranslate }}</p></article>
      </div>
    </section>
    <app-cta-band />
  `,
  styles: [`
    .feature-grid { display: grid; gap: 1.5rem; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); }
    article { padding: 1.25rem; border-radius: 12px; background: #f8fafc; }
    mat-icon { color: #0f766e; margin-bottom: 0.5rem; }
  `]
})
export class DriverPerformancePage implements OnInit {
  readonly heroImageCss = websiteCssUrl('/website/nexus/modules/drivers.png');

  private readonly seo = inject(WebsiteSeoService);
  ngOnInit(): void {
    this.seo.setPage({
      title: 'Driver Performance Scoring',
      description: 'Rank drivers by safety, fuel efficiency, and on-time delivery in SheikhGo Nexus.',
      path: '/driver-performance'
    });
  }
}
