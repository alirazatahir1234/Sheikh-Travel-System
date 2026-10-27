import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { CtaBandComponent } from '../../shared/cta-band.component';
import { WebsiteSeoService } from '../../core/seo.service';
import { WebsiteTranslatePipe } from '../../shared/website-translate.pipe';

@Component({
  selector: 'app-anomaly-detection-page',
  standalone: true,
  imports: [RouterLink, MatIconModule, CtaBandComponent, WebsiteTranslatePipe],
  template: `
    <section class="page-hero page-hero-cinematic" style="--hero-image: url('/website/nexus/modules/alerts.png')">
      <div class="container hero-layout">
        <div class="hero-copy">
          <span class="section-kicker">{{ 'pages.anomalyDetection.kicker' | wsTranslate }}</span>
          <h1>{{ 'pages.anomalyDetection.title' | wsTranslate }}</h1>
          <p class="hero-tagline">{{ 'pages.anomalyDetection.tagline' | wsTranslate }}</p>
          <p class="hero-lead">{{ 'pages.anomalyDetection.lead' | wsTranslate }}</p>
          <div class="hero-actions">
            <a routerLink="/request-demo" class="btn btn-primary">{{ 'common.requestDemo' | wsTranslate }}</a>
          </div>
        </div>
      </div>
    </section>
    <section class="section">
      <div class="container feature-grid">
        <article><mat-icon>dark_mode</mat-icon><h2>{{ 'pages.anomalyDetection.f1.title' | wsTranslate }}</h2><p>{{ 'pages.anomalyDetection.f1.text' | wsTranslate }}</p></article>
        <article><mat-icon>speed</mat-icon><h2>{{ 'pages.anomalyDetection.f2.title' | wsTranslate }}</h2><p>{{ 'pages.anomalyDetection.f2.text' | wsTranslate }}</p></article>
        <article><mat-icon>alt_route</mat-icon><h2>{{ 'pages.anomalyDetection.f3.title' | wsTranslate }}</h2><p>{{ 'pages.anomalyDetection.f3.text' | wsTranslate }}</p></article>
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
export class AnomalyDetectionPage implements OnInit {
  private readonly seo = inject(WebsiteSeoService);
  ngOnInit(): void {
    this.seo.setPage({
      title: 'Anomaly detection',
      description: 'Flag unusual driving patterns and review alerts in ERP.',
      path: '/anomaly-detection',
    });
  }
}
