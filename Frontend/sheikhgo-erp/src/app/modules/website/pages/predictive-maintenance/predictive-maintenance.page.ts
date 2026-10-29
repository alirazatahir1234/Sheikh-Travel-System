import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { CtaBandComponent } from '../../shared/cta-band.component';
import { WebsiteSeoService } from '../../core/seo.service';
import { WebsiteTranslatePipe } from '../../shared/website-translate.pipe';
import { websiteCssUrl } from '../../core/website-media';

@Component({
  selector: 'app-predictive-maintenance-page',
  standalone: true,
  imports: [RouterLink, MatIconModule, CtaBandComponent, WebsiteTranslatePipe],
  template: `
    <section class="page-hero page-hero-cinematic" [style.--hero-image]="heroImageCss">
      <div class="container hero-layout">
        <div class="hero-copy">
          <span class="section-kicker">{{ 'pages.predictiveMaintenance.kicker' | wsTranslate }}</span>
          <h1>{{ 'pages.predictiveMaintenance.title' | wsTranslate }}</h1>
          <p class="hero-tagline">{{ 'pages.predictiveMaintenance.tagline' | wsTranslate }}</p>
          <p class="hero-lead">{{ 'pages.predictiveMaintenance.lead' | wsTranslate }}</p>
          <div class="hero-actions">
            <a routerLink="/request-demo" class="btn btn-primary">{{ 'common.requestDemo' | wsTranslate }}</a>
          </div>
        </div>
      </div>
    </section>
    <section class="section">
      <div class="container feature-grid">
        <article><mat-icon aria-hidden="true">speed</mat-icon><h2>{{ 'pages.predictiveMaintenance.f1.title' | wsTranslate }}</h2><p>{{ 'pages.predictiveMaintenance.f1.text' | wsTranslate }}</p></article>
        <article><mat-icon aria-hidden="true">notifications_active</mat-icon><h2>{{ 'pages.predictiveMaintenance.f2.title' | wsTranslate }}</h2><p>{{ 'pages.predictiveMaintenance.f2.text' | wsTranslate }}</p></article>
        <article><mat-icon aria-hidden="true">event</mat-icon><h2>{{ 'pages.predictiveMaintenance.f3.title' | wsTranslate }}</h2><p>{{ 'pages.predictiveMaintenance.f3.text' | wsTranslate }}</p></article>
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
export class PredictiveMaintenancePage implements OnInit {
  readonly heroImageCss = websiteCssUrl('/website/nexus/modules/maintenance.png');

  private readonly seo = inject(WebsiteSeoService);
  ngOnInit(): void {
    this.seo.setPage({
      title: 'Predictive Maintenance',
      description: 'Schedule fleet service from mileage and alerts before breakdowns.',
      path: '/predictive-maintenance'
    });
  }
}
