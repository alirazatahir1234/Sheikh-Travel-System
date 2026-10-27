import { Component, OnInit, inject } from '@angular/core';
import { DecimalPipe, NgFor, NgIf } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { CtaBandComponent } from '../../shared/cta-band.component';
import { WebsiteSeoService } from '../../core/seo.service';
import { WebsiteTranslatePipe } from '../../shared/website-translate.pipe';
import { PublicDriverRating, TripFeedbackService } from '../../../../core/services/trip-feedback.service';

@Component({
  selector: 'app-driver-ratings-page',
  standalone: true,
  imports: [NgIf, NgFor, DecimalPipe, RouterLink, MatIconModule, CtaBandComponent, WebsiteTranslatePipe],
  template: `
    <section class="page-hero page-hero-cinematic" style="--hero-image: url('/website/nexus/modules/drivers.png')">
      <div class="container hero-layout">
        <div class="hero-copy">
          <span class="section-kicker">{{ 'pages.driverRatings.kicker' | wsTranslate }}</span>
          <h1>{{ 'pages.driverRatings.title' | wsTranslate }}</h1>
          <p class="hero-tagline">{{ 'pages.driverRatings.tagline' | wsTranslate }}</p>
          <p class="hero-lead">{{ 'pages.driverRatings.lead' | wsTranslate }}</p>
          <div class="hero-actions">
            <a routerLink="/request-demo" class="btn btn-primary">{{ 'common.requestDemo' | wsTranslate }}</a>
            <a routerLink="/driver-performance" class="btn btn-ghost">{{ 'pages.driverRatings.linkPerformance' | wsTranslate }}</a>
          </div>
        </div>
      </div>
    </section>

    <section class="section">
      <div class="container">
        <p *ngIf="loading" class="muted">{{ 'pages.driverRatings.loading' | wsTranslate }}</p>
        <p *ngIf="!loading && !drivers.length" class="muted">{{ 'pages.driverRatings.empty' | wsTranslate }}</p>
        <ul class="leaderboard" *ngIf="!loading && drivers.length">
          <li *ngFor="let d of drivers; let i = index">
            <span class="rank">{{ i + 1 }}</span>
            <div class="meta">
              <strong>{{ d.displayName }}</strong>
              <span class="muted">{{ 'pages.driverRatings.reviews' | wsTranslate }}: {{ d.reviewCount }}</span>
            </div>
            <div class="stars" aria-hidden="true">
              <mat-icon *ngFor="let s of starFill(d.averageRating)">{{ s ? 'star' : 'star_border' }}</mat-icon>
              <span class="avg">{{ d.averageRating | number:'1.1-1' }}</span>
            </div>
          </li>
        </ul>
        <p class="privacy-note">{{ 'pages.driverRatings.privacy' | wsTranslate }}</p>
      </div>
    </section>
    <app-cta-band />
  `,
  styles: [`
    .leaderboard { list-style: none; padding: 0; margin: 0; display: grid; gap: .75rem; }
    li { display: grid; grid-template-columns: 2rem 1fr auto; gap: 1rem; align-items: center; padding: 1rem; border-radius: 12px; background: #f8fafc; }
    .rank { font-weight: 700; color: #0f766e; }
    .meta { display: flex; flex-direction: column; gap: .15rem; }
    .stars { display: flex; align-items: center; gap: .1rem; color: #f59e0b; }
    .avg { margin-left: .35rem; font-weight: 600; color: #0f172a; }
    .muted { color: #64748b; }
    .privacy-note { margin-top: 1.5rem; font-size: .9rem; color: #64748b; max-width: 40rem; }
  `],
})
export class DriverRatingsPage implements OnInit {
  private readonly seo = inject(WebsiteSeoService);
  private readonly feedback = inject(TripFeedbackService);

  loading = true;
  drivers: PublicDriverRating[] = [];

  ngOnInit(): void {
    this.seo.setPage({
      title: 'Driver ratings',
      description: 'Public average client ratings for SheikhGo drivers — names only, no photos.',
      path: '/driver-ratings',
    });
    this.feedback.getPublicDriverRatings(15).subscribe({
      next: rows => {
        this.drivers = rows;
        this.loading = false;
      },
      error: () => {
        this.drivers = [];
        this.loading = false;
      },
    });
  }

  starFill(avg: number): boolean[] {
    const full = Math.round(avg);
    return [1, 2, 3, 4, 5].map(i => i <= full);
  }
}
