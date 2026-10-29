import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { CtaBandComponent } from '../../shared/cta-band.component';
import { WebsiteSeoService } from '../../core/seo.service';
import { WebsiteTranslatePipe } from '../../shared/website-translate.pipe';
import { websiteCssUrl } from '../../core/website-media';

@Component({
  selector: 'app-accident-risk-page',
  standalone: true,
  imports: [RouterLink, MatIconModule, CtaBandComponent, WebsiteTranslatePipe],
  template: `
    <section class="page-hero page-hero-cinematic" [style.--hero-image]="heroImageCss">
      <div class="container hero-layout">
        <div class="hero-copy">
          <span class="section-kicker">{{ 'pages.accidentRisk.kicker' | wsTranslate }}</span>
          <h1>{{ 'pages.accidentRisk.title' | wsTranslate }}</h1>
          <p class="hero-tagline">{{ 'pages.accidentRisk.tagline' | wsTranslate }}</p>
          <p class="hero-lead">{{ 'pages.accidentRisk.lead' | wsTranslate }}</p>
          <div class="hero-actions">
            <a routerLink="/request-demo" class="btn btn-primary">{{ 'common.requestDemo' | wsTranslate }}</a>
          </div>
        </div>
      </div>
    </section>
    <section class="section">
      <div class="container feature-grid">
        <article><mat-icon aria-hidden="true">history</mat-icon><h2>{{ 'pages.accidentRisk.f1.title' | wsTranslate }}</h2><p>{{ 'pages.accidentRisk.f1.text' | wsTranslate }}</p></article>
        <article><mat-icon aria-hidden="true">speed</mat-icon><h2>{{ 'pages.accidentRisk.f2.title' | wsTranslate }}</h2><p>{{ 'pages.accidentRisk.f2.text' | wsTranslate }}</p></article>
        <article><mat-icon aria-hidden="true">bedtime</mat-icon><h2>{{ 'pages.accidentRisk.f3.title' | wsTranslate }}</h2><p>{{ 'pages.accidentRisk.f3.text' | wsTranslate }}</p></article>
        <article><mat-icon aria-hidden="true">alt_route</mat-icon><h2>{{ 'pages.accidentRisk.f4.title' | wsTranslate }}</h2><p>{{ 'pages.accidentRisk.f4.text' | wsTranslate }}</p></article>
      </div>
    </section>
    <app-cta-band />
  `,
  styles: [`
    .feature-grid { display: grid; gap: 1.5rem; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); }
    article { padding: 1.25rem; border-radius: 12px; background: #f8fafc; }
    mat-icon { color: #0f766e; margin-bottom: 0.5rem; }
  `]
})
export class AccidentRiskPage implements OnInit {
  readonly heroImageCss = websiteCssUrl('/website/gps-alerts/scene-safety.jpg');

  private readonly seo = inject(WebsiteSeoService);
  ngOnInit(): void {
    this.seo.setPage({
      title: 'Predictive accident risk',
      description: 'Driver and route risk scores from accidents, speed, fatigue, and route difficulty.',
      path: '/accident-risk'
    });
  }
}
