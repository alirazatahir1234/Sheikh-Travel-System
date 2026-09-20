import { AfterViewInit, Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { CtaBandComponent } from '../../shared/cta-band.component';
import { WebsiteSeoService } from '../../core/seo.service';
import { SERVICE_ITEMS } from '../../core/site-catalog';

@Component({
  selector: 'app-services-page',
  standalone: true,
  imports: [RouterLink, MatIconModule, CtaBandComponent],
  template: `
    <section class="page-hero">
      <div class="container">
        <h1>{{ pageTitle }}</h1>
        <p>{{ pageLead }}</p>
        <nav class="jump" aria-label="Services">
          @for (s of services; track s.slug) {
            <a [routerLink]="[]" [fragment]="s.slug">{{ s.title }}</a>
          }
        </nav>
      </div>
    </section>

    @for (s of services; track s.slug) {
      <section class="service" [id]="s.slug">
        <div class="container service-grid">
          <div class="service-copy">
            <span class="service-icon" aria-hidden="true"><mat-icon>{{ s.icon }}</mat-icon></span>
            <h2>{{ s.title }}</h2>
            <p class="lead">{{ s.text }}</p>
            <p>{{ s.detail }}</p>
            <a class="btn btn-primary" routerLink="/contact">Talk about {{ s.title }} →</a>
          </div>
          <ul class="service-points">
            @for (h of s.highlights; track h) {
              <li>{{ h }}</li>
            }
          </ul>
        </div>
      </section>
    }

    <app-cta-band />
  `,
  styles: `
    .jump { display:flex; flex-wrap:wrap; gap:.5rem; margin-top:1.25rem; }
    .jump a {
      padding:.45rem .85rem; border-radius:999px; font-size:.85rem; font-weight:600;
      background:rgba(15,118,110,.08); color:var(--sg-teal,#0f766e);
    }
    .jump a:hover { background:var(--sg-mint,#e9faf4); }

    .service { padding:3.5rem 0; border-bottom:1px solid var(--sg-line,rgba(2,47,50,.1)); scroll-margin-top:96px; }
    .service:nth-child(even) { background:var(--sg-mint,#e9faf4); }
    .service-grid { display:grid; grid-template-columns:1.6fr 1fr; gap:2.5rem; align-items:start; }

    .service-icon {
      display:inline-flex; align-items:center; justify-content:center;
      width:48px; height:48px; border-radius:12px;
      background:var(--sg-teal,#0f766e); color:#fff;
    }
    .service-icon mat-icon { font-size:1.5rem; width:1.5rem; height:1.5rem; }
    h2 { font-size:1.6rem; margin:.9rem 0 .4rem; }
    .lead { font-weight:600; color:var(--sg-ink,#022f32); margin-bottom:.6rem; }
    p { color:var(--sg-muted); }
    .btn { margin-top:1.25rem; }

    .service-points { display:grid; gap:.6rem; margin:0; padding:0; list-style:none; }
    .service-points li {
      background:#fff; border:1px solid var(--sg-line,rgba(2,47,50,.1)); border-radius:12px;
      padding:.85rem 1rem; font-weight:600; font-size:.95rem; color:var(--sg-ink,#022f32);
    }

    @media (max-width:900px) {
      .service-grid { grid-template-columns:1fr; gap:1.5rem; }
      .service { padding:2.5rem 0; }
    }
  `,
})
export class ServicesPage implements OnInit, AfterViewInit {
  private readonly seo = inject(WebsiteSeoService);
  private readonly route = inject(ActivatedRoute);

  readonly services = SERVICE_ITEMS;
  readonly pageTitle = 'Services';
  readonly pageLead =
    'End-to-end technology services — web, mobile, design, cloud, consulting and custom software — delivered by one team.';

  ngOnInit(): void {
    this.seo.set(
      this.pageTitle,
      'Web development, mobile apps, UI/UX design, cloud solutions, IT consulting and custom software from SheikhGo Technologies.',
      '/services',
    );
  }

  /** Router anchor scrolling runs before this lazy route paints, so jump here once rendered. */
  ngAfterViewInit(): void {
    this.route.fragment.subscribe(fragment => {
      if (!fragment) return;
      setTimeout(() => document.getElementById(fragment)?.scrollIntoView({ behavior: 'smooth', block: 'start' }));
    });
  }
}
