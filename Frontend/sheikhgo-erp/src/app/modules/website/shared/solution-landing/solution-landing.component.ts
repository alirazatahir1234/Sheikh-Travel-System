import { Component, Input, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { CtaBandComponent } from '../cta-band.component';
import {
  CHANNEL_C_CTAS,
  ChannelCSolutionPage,
} from '../../core/channel-c-solutions';
import { WebsiteGoogleAnalyticsService } from '../../core/website-google-analytics.service';
import { WebsitePlausibleAnalyticsService } from '../../core/website-plausible-analytics.service';

@Component({
  selector: 'app-solution-landing',
  standalone: true,
  imports: [RouterLink, MatIconModule, CtaBandComponent],
  template: `
    <section class="page-hero page-hero-cinematic" [style.--hero-image]="'url(' + page.heroImage + ')'">
      <div class="container">
        <span class="section-kicker" style="color:#99f6e4">{{ page.kicker }}</span>
        <h1>{{ page.title }}</h1>
        <p class="hero-lead">{{ page.lead }}</p>
        <div class="hero-actions">
          <a
            [routerLink]="ctas.primary.url"
            class="btn btn-primary"
            (click)="trackCta('book_demo', 'hero')"
            >{{ ctas.primary.text }}</a
          >
          <a
            [routerLink]="ctas.secondary.url"
            class="btn btn-ghost"
            (click)="trackCta('fleet_assessment', 'hero')"
            >{{ ctas.secondary.text }}</a
          >
        </div>
      </div>
    </section>

    @if (page.honestyNote) {
      <section class="section honesty">
        <div class="container">
          <aside class="honesty-note" role="note">
            <mat-icon aria-hidden="true">info</mat-icon>
            <p>{{ page.honestyNote }}</p>
          </aside>
        </div>
      </section>
    }

    <section class="section section-band-white">
      <div class="container split">
        <article>
          <span class="section-kicker">Target customer</span>
          <h2 class="section-title">{{ page.targetCustomer.title }}</h2>
          <p class="section-lead">{{ page.targetCustomer.lead }}</p>
          <ul class="bullets">
            @for (b of page.targetCustomer.bullets; track b) {
              <li>{{ b }}</li>
            }
          </ul>
        </article>
        <article>
          <span class="section-kicker">Problem solved</span>
          <h2 class="section-title">{{ page.problem.title }}</h2>
          <p class="section-lead">{{ page.problem.lead }}</p>
          <ul class="bullets">
            @for (b of page.problem.bullets; track b) {
              <li>{{ b }}</li>
            }
          </ul>
        </article>
      </div>
    </section>

    <section class="section">
      <div class="container">
        <div class="section-head">
          <span class="section-kicker">Features</span>
          <h2 class="section-title">{{ page.featuresTitle }}</h2>
          <p class="section-lead">{{ page.featuresLead }}</p>
        </div>
        <div class="feature-grid">
          @for (f of page.features; track f.title) {
            <article class="feature-card">
              @if (f.icon) {
                <mat-icon aria-hidden="true">{{ f.icon }}</mat-icon>
              }
              <h3>{{ f.title }}</h3>
              <p>{{ f.text }}</p>
            </article>
          }
        </div>
        @if (relatedLinks.length) {
          <div class="related">
            @for (link of relatedLinks; track link.url) {
              <a [routerLink]="link.url" class="btn btn-outline">{{ link.label }}</a>
            }
          </div>
        }
      </div>
    </section>

    @if (page.useCases?.length) {
      <section class="section section-band-alt">
        <div class="container">
          <div class="section-head">
            <span class="section-kicker">Use cases</span>
            <h2 class="section-title">{{ page.useCasesTitle || 'Use cases' }}</h2>
            @if (page.useCasesLead) {
              <p class="section-lead">{{ page.useCasesLead }}</p>
            }
          </div>
          <div class="feature-grid">
            @for (u of page.useCases!; track u.title) {
              <article class="feature-card">
                @if (u.icon) {
                  <mat-icon aria-hidden="true">{{ u.icon }}</mat-icon>
                }
                <h3>{{ u.title }}</h3>
                <p>{{ u.text }}</p>
              </article>
            }
          </div>
        </div>
      </section>
    }

    @if (page.benefits?.length) {
      <section class="section section-band-white">
        <div class="container">
          <div class="section-head">
            <span class="section-kicker">Benefits</span>
            <h2 class="section-title">{{ page.benefitsTitle || 'Benefits' }}</h2>
            @if (page.benefitsLead) {
              <p class="section-lead">{{ page.benefitsLead }}</p>
            }
          </div>
          <div class="feature-grid">
            @for (b of page.benefits!; track b.title) {
              <article class="feature-card">
                @if (b.icon) {
                  <mat-icon aria-hidden="true">{{ b.icon }}</mat-icon>
                }
                <h3>{{ b.title }}</h3>
                <p>{{ b.text }}</p>
              </article>
            }
          </div>
        </div>
      </section>
    }

    @if (page.shots?.length) {
      <section class="section tour">
        <div class="container">
          <div class="section-head">
            <span class="section-kicker">Product</span>
            <h2 class="section-title">{{ page.shotsTitle || 'Inside the platform' }}</h2>
            @if (page.shotsLead) {
              <p class="section-lead">{{ page.shotsLead }}</p>
            }
          </div>
          <figure class="shot shot-lead">
            <img [src]="page.shots![0].src" [alt]="page.shots![0].alt" width="1440" height="900" />
            <figcaption>
              <strong>{{ page.shots![0].title }}</strong>
              <span>{{ page.shots![0].text }}</span>
            </figcaption>
          </figure>
          <div class="shot-grid">
            @for (s of page.shots!.slice(1); track s.src) {
              <figure class="shot">
                <img [src]="s.src" [alt]="s.alt" width="1440" height="900" loading="lazy" />
                <figcaption>
                  <strong>{{ s.title }}</strong>
                  <span>{{ s.text }}</span>
                </figcaption>
              </figure>
            }
          </div>
        </div>
      </section>
    }

    <section class="section section-band-alt next-step">
      <div class="container next-inner">
        <div>
          <span class="section-kicker">Next step</span>
          <h2 class="section-title">{{ page.nextStep.title }}</h2>
          <p class="section-lead">{{ page.nextStep.lead }}</p>
        </div>
        <div class="hero-actions">
          <a
            [routerLink]="ctas.primary.url"
            class="btn btn-primary"
            (click)="trackCta('book_demo', 'next_step')"
            >{{ ctas.primary.text }}</a
          >
          <a
            [routerLink]="ctas.secondary.url"
            class="btn btn-outline"
            (click)="trackCta('fleet_assessment', 'next_step')"
            >{{ ctas.secondary.text }}</a
          >
        </div>
      </div>
    </section>

    <app-cta-band
      [title]="ctaTitle"
      [lead]="ctaLead"
      [primaryText]="ctas.primary.text"
      [primaryUrl]="ctas.primary.url"
      [secondaryText]="ctas.secondary.text"
      [secondaryUrl]="ctas.secondary.url"
    />
  `,
  styles: `
    .honesty { padding-top: 1.5rem; padding-bottom: 0; }
    .honesty-note {
      display: flex;
      gap: 0.85rem;
      align-items: flex-start;
      background: #f0fdfa;
      border: 1px solid #99f6e4;
      border-radius: 14px;
      padding: 1rem 1.15rem;
      color: var(--sg-ink, #022f32);
    }
    .honesty-note mat-icon { color: #0f766e; flex-shrink: 0; margin-top: 0.1rem; }
    .honesty-note p { margin: 0; font-size: 0.95rem; line-height: 1.55; color: var(--sg-muted, #475569); }

    .split {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 2rem;
      align-items: start;
    }
    .bullets {
      margin: 1rem 0 0;
      padding-left: 1.15rem;
      color: var(--sg-muted);
    }
    .bullets li + li { margin-top: 0.45rem; }

    .section-head { max-width: 40rem; margin-bottom: 1.5rem; }
    .feature-grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 1rem;
    }
    .feature-card {
      background: #fff;
      border: 1px solid var(--sg-line, #e2e8f0);
      border-radius: 14px;
      padding: 1.25rem;
    }
    .feature-card mat-icon { color: #0f766e; margin-bottom: 0.45rem; }
    .feature-card h3 { font-size: 1.05rem; margin: 0 0 0.35rem; }
    .feature-card p { margin: 0; color: var(--sg-muted); font-size: 0.925rem; line-height: 1.5; }

    .related { display: flex; flex-wrap: wrap; gap: 0.65rem; margin-top: 1.5rem; }

    .tour { background: var(--sg-page, #f5fbf9); }
    .shot {
      margin: 0;
      background: #fff;
      border: 1px solid var(--sg-line, #e2e8f0);
      border-radius: 16px;
      overflow: hidden;
      box-shadow: 0 14px 34px rgba(4, 31, 36, 0.08);
    }
    .shot img { display: block; width: 100%; height: auto; border-bottom: 1px solid var(--sg-line, #e2e8f0); }
    .shot figcaption { display: grid; gap: 0.2rem; padding: 0.95rem 1.1rem; }
    .shot figcaption strong { color: var(--sg-ink, #022f32); font-size: 0.975rem; }
    .shot figcaption span { color: var(--sg-muted); font-size: 0.875rem; }
    .shot-lead { margin-bottom: 1.25rem; }
    .shot-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1.25rem; }

    .next-inner {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 1.5rem;
      flex-wrap: wrap;
    }
    .next-inner .section-title { max-width: 18ch; }

    @media (max-width: 900px) {
      .feature-grid { grid-template-columns: 1fr 1fr; }
    }
    @media (max-width: 720px) {
      .split, .shot-grid, .feature-grid { grid-template-columns: 1fr; }
    }
  `,
})
export class SolutionLandingComponent {
  private readonly ga = inject(WebsiteGoogleAnalyticsService);
  private readonly plausible = inject(WebsitePlausibleAnalyticsService);

  @Input({ required: true }) page!: ChannelCSolutionPage;
  @Input() relatedLinks: readonly { label: string; url: string }[] = [];
  @Input() ctaTitle = 'Ready to see SheikhGo Nexus?';
  @Input() ctaLead =
    'Book a product demo for your fleet workflows, or request a structured fleet assessment before you buy ads or commit to a pilot.';

  readonly ctas = CHANNEL_C_CTAS;

  trackCta(kind: 'book_demo' | 'fleet_assessment', placement: string): void {
    const event = kind === 'book_demo' ? 'cta_book_demo_click' : 'cta_fleet_assessment_click';
    const params = {
      page_path: this.page.path,
      placement,
    };
    this.ga.trackEvent(event, params);
    this.plausible.trackEvent(event, params);
  }
}
