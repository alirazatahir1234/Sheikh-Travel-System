import { AfterViewInit, Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { CtaBandComponent } from '../../shared/cta-band.component';
import { MobileAppPromoComponent } from '../../shared/mobile-app-promo/mobile-app-promo.component';
import { WEBSITE_DEMO_MAP_ADDRESS_SHORT, WEBSITE_DEMO_MAP_COORDINATES } from '../../core/brand';
import { WebsiteSeoService } from '../../core/seo.service';
import { WebsiteTranslatePipe } from '../../shared/website-translate.pipe';
import { websiteCssUrl } from '../../core/website-media';

interface FeatureItem {
  title: string;
  icon: string;
  what: string;
  why: string;
}

interface FeatureGroup {
  id: string;
  title: string;
  kicker: string;
  lead: string;
  icon: string;
  accent: 'map' | 'status' | 'review';
  count: number;
  items: FeatureItem[];
}

@Component({
  selector: 'app-live-tracking-page',
  standalone: true,
  imports: [RouterLink, MatIconModule, CtaBandComponent, MobileAppPromoComponent, WebsiteTranslatePipe],
  template: `
    <section class="page-hero page-hero-cinematic" [style.--hero-image]="heroImageCss">
      <div class="container hero-layout">
        <div class="hero-copy">
          <span class="section-kicker" style="color:#99f6e4">{{ 'pages.liveTracking.kicker' | wsTranslate }}</span>
          <h1>{{ 'pages.liveTracking.title' | wsTranslate }}</h1>
          <p class="hero-tagline">{{ 'pages.liveTracking.tagline' | wsTranslate }}</p>
          <p class="hero-lead">{{ 'pages.liveTracking.lead' | wsTranslate }}</p>
          <div class="hero-actions">
            <a routerLink="/request-demo" class="btn btn-primary">{{ 'common.requestDemo' | wsTranslate }}</a>
            <a routerLink="/gps-alerts" class="btn btn-ghost">{{ 'common.gpsAlertsArrow' | wsTranslate }}</a>
          </div>
        </div>
        <aside class="hero-panel" aria-label="Live tracking at a glance">
          <div class="hero-panel-glow" aria-hidden="true"></div>
          <div class="stat-chip">
            <span class="stat-num">Live</span>
            <span class="stat-label">Fleet map</span>
          </div>
          <div class="stat-chip">
            <span class="stat-num">Map</span>
            <span class="stat-label">Geofence layers</span>
          </div>
          <div class="stat-chip wide">
            <mat-icon aria-hidden="true">history</mat-icon>
            <span class="stat-label">History · Playback · Fleet Health</span>
          </div>
          <ul class="hero-pulse" aria-hidden="true">
            <li><span class="dot ok"></span> Moving · 62 km/h</li>
            <li><span class="dot warn"></span> Idle · Ignition on</li>
            <li><span class="dot danger"></span> Offline · Stale fix</li>
          </ul>
        </aside>
      </div>
    </section>

    <app-mobile-app-promo variant="banner" analyticsSource="live-tracking" />

    <section class="section categories">
      <div class="container">
        <header class="section-head">
          <span class="section-kicker">{{ 'pages.liveTracking.overviewKicker' | wsTranslate }}</span>
          <h2 class="section-title">{{ 'pages.liveTracking.overviewTitle' | wsTranslate }}</h2>
          <p class="section-lead">
            Alerts tell you when something breaks the rules. Live Tracking shows you what is happening
            right now — and gives you the context to investigate with history and geofences.
          </p>
        </header>
        <div class="cat-grid">
          @for (g of groups; track g.id) {
            <a class="cat-card" [attr.data-accent]="g.accent" [routerLink]="[]" [fragment]="g.id">
              <span class="cat-icon" aria-hidden="true"><mat-icon>{{ g.icon }}</mat-icon></span>
              <div class="cat-meta">
                <span class="cat-count">{{ g.count }} capabilities</span>
                <h3>{{ g.title }}</h3>
                <p>{{ g.lead }}</p>
              </div>
              <span class="cat-arrow" aria-hidden="true">→</span>
            </a>
          }
        </div>
        <ul class="intro-points">
          <li>
            <mat-icon aria-hidden="true">check_circle</mat-icon>
            <span>See every vehicle on one map — search by plate, driver or device</span>
          </li>
          <li>
            <mat-icon aria-hidden="true">check_circle</mat-icon>
            <span>Human-readable addresses alongside coordinates, not pins alone</span>
          </li>
          <li>
            <mat-icon aria-hidden="true">check_circle</mat-icon>
            <span>Moving, idle, parked and offline states at a glance</span>
          </li>
          <li>
            <mat-icon aria-hidden="true">check_circle</mat-icon>
            <span>Open GPS History for route playback when you need the full picture</span>
          </li>
        </ul>
      </div>
    </section>

    <nav class="jump-wrap" aria-label="Live tracking groups">
      <div class="container jump">
        @for (g of groups; track g.id) {
          <a [routerLink]="[]" [fragment]="g.id">
            <mat-icon aria-hidden="true">{{ g.icon }}</mat-icon>
            {{ g.title }}
          </a>
        }
      </div>
    </nav>

    @for (g of groups; track g.id) {
      <section class="section group" [id]="g.id" [attr.data-accent]="g.accent">
        <div class="container">
          <header class="group-head">
            <span class="group-badge" aria-hidden="true"><mat-icon>{{ g.icon }}</mat-icon></span>
            <div>
              <span class="section-kicker">{{ g.kicker }}</span>
              <h2 class="section-title">{{ g.title }}</h2>
              <p class="section-lead">{{ g.lead }}</p>
            </div>
          </header>
          <div class="alert-grid">
            @for (a of g.items; track a.title) {
              <article class="alert-card">
                <div class="alert-top">
                  <span class="alert-icon" aria-hidden="true"><mat-icon>{{ a.icon }}</mat-icon></span>
                  <h3>{{ a.title }}</h3>
                </div>
                <div class="alert-body">
                  <p class="what">
                    <span class="label">What you get</span>
                    {{ a.what }}
                  </p>
                  <p class="why">
                    <span class="label">Why it matters</span>
                    {{ a.why }}
                  </p>
                </div>
              </article>
            }
          </div>
        </div>
      </section>
    }

    <section class="section highlight">
      <div class="container highlight-grid">
        <div class="highlight-visual" aria-hidden="true">
          <div class="road-card">
            <mat-icon>place</mat-icon>
            <div class="road-meta">
              <strong>Toyota Corolla · LEC-8825</strong>
              <span>{{ demoMapCoordinates }} · {{ demoMapAddressLine }}</span>
            </div>
            <span class="road-badge">Live</span>
          </div>
          <div class="road-card dim">
            <mat-icon>speed</mat-icon>
            <div class="road-meta">
              <strong>22 km/h · Ignition off</strong>
              <span>GPS active · Updated moments ago</span>
            </div>
          </div>
        </div>
        <div class="highlight-copy">
          <span class="section-kicker">Plain-language location</span>
          <h2 class="section-title">Addresses, not just coordinates</h2>
          <p>
            Operators need to know where a vehicle is in words they can act on. SheikhGo resolves
            road and area context on the map and in vehicle detail — speed, ignition, battery and last
            update sit beside the address so dispatch and owners share the same picture.
          </p>
        </div>
      </div>
    </section>

    <app-cta-band
      title="Ready to see Live Tracking on your fleet?"
      lead="Book a short demo. We will walk through the fleet map, status filters, geofences and history playback for your vehicles."
      primaryText="Request a Demo"
      primaryUrl="/request-demo"
      secondaryText="Explore GPS Alerts →"
      secondaryUrl="/gps-alerts"
    />
  `,
  styles: `
    .hero-layout {
      display: grid;
      grid-template-columns: minmax(0, 1.25fr) minmax(0, 0.85fr);
      gap: 2.5rem;
      align-items: center;
    }
    .hero-tagline {
      font-size: 1.15rem;
      color: #99f6e4;
      margin: 0.75rem 0 1rem;
      font-weight: 600;
    }
    .hero-lead {
      color: rgba(255, 255, 255, 0.82);
      max-width: 52ch;
    }
    .hero-actions {
      margin-top: 1.4rem;
      display: flex;
      gap: 0.75rem;
      flex-wrap: wrap;
    }

    .hero-panel {
      position: relative;
      display: grid;
      gap: 0.75rem;
      padding: 1.35rem;
      border-radius: 20px;
      background: rgba(255, 255, 255, 0.06);
      border: 1px solid rgba(255, 255, 255, 0.14);
      backdrop-filter: blur(10px);
      animation: fadeUp 0.7s ease both;
    }
    .hero-panel-glow {
      position: absolute;
      inset: -20% -10% auto auto;
      width: 160px;
      height: 160px;
      background: radial-gradient(circle, rgba(24, 201, 135, 0.35), transparent 70%);
      pointer-events: none;
    }
    .stat-chip {
      position: relative;
      display: flex;
      flex-direction: column;
      gap: 0.15rem;
      padding: 0.95rem 1.1rem;
      border-radius: 14px;
      background: rgba(2, 47, 50, 0.35);
      border: 1px solid rgba(153, 246, 228, 0.18);
    }
    .stat-chip.wide {
      flex-direction: row;
      align-items: center;
      gap: 0.65rem;
    }
    .stat-chip.wide mat-icon {
      color: var(--sg-primary-strong, #00d97e);
      font-size: 1.35rem;
      width: 1.35rem;
      height: 1.35rem;
    }
    .stat-num {
      font-family: var(--font-display);
      font-size: 1.85rem;
      font-weight: 800;
      color: var(--sg-primary-strong, #00d97e);
      line-height: 1;
    }
    .stat-label {
      color: rgba(255, 255, 255, 0.78);
      font-size: 0.875rem;
      font-weight: 600;
    }
    .hero-pulse {
      list-style: none;
      margin: 0.25rem 0 0;
      padding: 0;
      display: grid;
      gap: 0.45rem;
    }
    .hero-pulse li {
      display: flex;
      align-items: center;
      gap: 0.55rem;
      font-size: 0.85rem;
      font-weight: 600;
      color: rgba(255, 255, 255, 0.88);
      padding: 0.45rem 0.65rem;
      border-radius: 10px;
      background: rgba(255, 255, 255, 0.05);
      animation: pulseSoft 3.6s ease-in-out infinite;
    }
    .hero-pulse li:nth-child(2) { animation-delay: 0.4s; }
    .hero-pulse li:nth-child(3) { animation-delay: 0.8s; }
    .dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      flex-shrink: 0;
    }
    .dot.danger { background: #fb7185; box-shadow: 0 0 0 4px rgba(251, 113, 133, 0.2); }
    .dot.warn { background: #fbbf24; box-shadow: 0 0 0 4px rgba(251, 191, 36, 0.2); }
    .dot.ok { background: var(--sg-primary-strong, #00d97e); box-shadow: 0 0 0 4px rgba(0, 217, 126, 0.2); }

    .categories { background: #fff; }
    .section-head { margin-bottom: 2rem; max-width: 40rem; }

    .cat-grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 1rem;
      margin-bottom: 1.75rem;
    }
    .cat-card {
      position: relative;
      display: grid;
      grid-template-columns: auto 1fr auto;
      gap: 1rem;
      align-items: start;
      padding: 1.35rem 1.25rem;
      border-radius: var(--sg-radius, 16px);
      border: 1px solid var(--sg-line);
      background: var(--sg-page, #f5fbf9);
      transition: transform 0.2s ease, box-shadow 0.2s ease, border-color 0.2s ease;
      min-height: 100%;
    }
    .cat-card:hover {
      transform: translateY(-4px);
      box-shadow: 0 16px 40px rgba(15, 118, 110, 0.12);
      border-color: rgba(24, 201, 135, 0.4);
    }
    .cat-icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 3rem;
      height: 3rem;
      border-radius: 14px;
      background: var(--sg-mist, #e9faf4);
      color: var(--sg-teal, #0f766e);
    }
    .cat-card[data-accent='map'] .cat-icon {
      background: rgba(15, 118, 110, 0.12);
      color: var(--sg-teal, #0f766e);
    }
    .cat-card[data-accent='status'] .cat-icon {
      background: rgba(24, 201, 135, 0.15);
      color: #0b8f6a;
    }
    .cat-card[data-accent='review'] .cat-icon {
      background: rgba(6, 37, 45, 0.08);
      color: var(--sg-navy, #06252d);
    }
    .cat-icon mat-icon { font-size: 1.5rem; width: 1.5rem; height: 1.5rem; }
    .cat-count {
      display: inline-block;
      font-size: 0.72rem;
      font-weight: 700;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: var(--sg-teal, #0f766e);
      margin-bottom: 0.35rem;
    }
    .cat-meta h3 {
      font-size: 1.15rem;
      margin-bottom: 0.4rem;
      color: var(--sg-ink, #022f32);
    }
    .cat-meta p {
      color: var(--sg-muted);
      font-size: 0.92rem;
      line-height: 1.5;
    }
    .cat-arrow {
      font-size: 1.15rem;
      font-weight: 700;
      color: var(--sg-primary, #18c987);
      margin-top: 0.2rem;
      transition: transform 0.2s ease;
    }
    .cat-card:hover .cat-arrow { transform: translateX(3px); }

    .intro-points {
      margin: 0;
      padding: 0;
      list-style: none;
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.75rem;
    }
    .intro-points li {
      display: flex;
      align-items: flex-start;
      gap: 0.65rem;
      background: var(--sg-mist, #e9faf4);
      border-radius: 12px;
      padding: 0.9rem 1rem;
      font-weight: 600;
      color: var(--sg-ink, #022f32);
      font-size: 0.95rem;
    }
    .intro-points mat-icon {
      color: var(--sg-teal, #0f766e);
      font-size: 1.2rem;
      width: 1.2rem;
      height: 1.2rem;
      flex-shrink: 0;
      margin-top: 0.1rem;
    }

    .jump-wrap {
      padding: 1.1rem 0;
      background: var(--sg-page, #f5fbf9);
      border-block: 1px solid var(--sg-line);
      position: sticky;
      top: var(--header-h, 72px);
      z-index: 5;
      backdrop-filter: blur(8px);
    }
    .jump {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
    }
    .jump a {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      padding: 0.5rem 0.95rem;
      border-radius: 999px;
      font-size: 0.85rem;
      font-weight: 600;
      background: rgba(15, 118, 110, 0.08);
      color: var(--sg-teal, #0f766e);
      transition: background 0.18s ease, transform 0.18s ease;
    }
    .jump a mat-icon {
      font-size: 1.05rem;
      width: 1.05rem;
      height: 1.05rem;
    }
    .jump a:hover {
      background: var(--sg-mist, #e9faf4);
      transform: translateY(-1px);
    }

    .group {
      scroll-margin-top: calc(var(--header-h, 72px) + 64px);
      border-bottom: 1px solid var(--sg-line);
      background: var(--sg-page, #f5fbf9);
    }
    .group:nth-of-type(even) { background: #fff; }
    .group-head {
      display: flex;
      gap: 1.1rem;
      align-items: flex-start;
      max-width: 44rem;
      margin-bottom: 1.75rem;
    }
    .group-badge {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 3.25rem;
      height: 3.25rem;
      border-radius: 16px;
      background: linear-gradient(145deg, var(--sg-primary, #18c987), var(--sg-teal, #0f766e));
      color: #fff;
      flex-shrink: 0;
      box-shadow: 0 10px 24px rgba(15, 118, 110, 0.25);
    }
    .group-badge mat-icon { font-size: 1.55rem; width: 1.55rem; height: 1.55rem; }

    .alert-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 1rem;
    }
    .alert-card {
      background: #fff;
      border: 1px solid var(--sg-line);
      border-radius: 16px;
      padding: 1.25rem 1.3rem;
      transition: transform 0.2s ease, box-shadow 0.2s ease, border-color 0.2s ease;
    }
    .group:nth-of-type(even) .alert-card {
      background: var(--sg-page, #f5fbf9);
    }
    .alert-card:hover {
      transform: translateY(-3px);
      box-shadow: 0 16px 36px rgba(15, 118, 110, 0.1);
      border-color: rgba(24, 201, 135, 0.4);
    }
    .alert-top {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      margin-bottom: 0.9rem;
    }
    .alert-icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 2.5rem;
      height: 2.5rem;
      border-radius: 12px;
      background: var(--sg-mist, #e9faf4);
      color: var(--sg-teal, #0f766e);
      flex-shrink: 0;
    }
    .group[data-accent='status'] .alert-icon {
      background: rgba(24, 201, 135, 0.14);
      color: #0b8f6a;
    }
    .group[data-accent='review'] .alert-icon {
      background: rgba(6, 37, 45, 0.08);
      color: var(--sg-navy, #06252d);
    }
    .alert-icon mat-icon { font-size: 1.25rem; width: 1.25rem; height: 1.25rem; }
    .alert-card h3 {
      font-size: 1.08rem;
      color: var(--sg-ink, #022f32);
    }
    .alert-body { display: grid; gap: 0.7rem; }
    .what, .why {
      color: var(--sg-muted);
      font-size: 0.94rem;
      margin: 0;
      line-height: 1.55;
    }
    .label {
      display: block;
      font-size: 0.7rem;
      font-weight: 700;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: var(--sg-teal, #0f766e);
      margin-bottom: 0.25rem;
    }

    .highlight {
      background:
        radial-gradient(ellipse 50% 60% at 10% 20%, rgba(24, 201, 135, 0.12), transparent 55%),
        linear-gradient(135deg, #e9faf4 0%, #f5fbf9 55%, #fff 100%);
    }
    .highlight-grid {
      display: grid;
      grid-template-columns: 0.9fr 1.1fr;
      gap: 2.5rem;
      align-items: center;
    }
    .highlight-visual {
      display: grid;
      gap: 0.85rem;
    }
    .road-card {
      display: flex;
      align-items: center;
      gap: 0.85rem;
      padding: 1.1rem 1.2rem;
      border-radius: 16px;
      background: #fff;
      border: 1px solid var(--sg-line);
      box-shadow: 0 12px 28px rgba(2, 47, 50, 0.06);
      animation: fadeUp 0.8s ease both;
    }
    .road-card.dim {
      opacity: 0.85;
      animation-delay: 0.12s;
    }
    .road-card mat-icon {
      color: var(--sg-teal, #0f766e);
      font-size: 1.6rem;
      width: 1.6rem;
      height: 1.6rem;
    }
    .road-meta {
      display: grid;
      gap: 0.15rem;
      flex: 1;
    }
    .road-meta strong {
      color: var(--sg-ink, #022f32);
      font-size: 0.98rem;
    }
    .road-meta span {
      color: var(--sg-muted);
      font-size: 0.85rem;
    }
    .road-badge {
      font-size: 0.72rem;
      font-weight: 700;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      color: var(--sg-ink, #022f32);
      background: linear-gradient(135deg, var(--sg-primary, #18c987), var(--sg-primary-strong, #00d97e));
      padding: 0.35rem 0.65rem;
      border-radius: 999px;
    }
    .highlight-copy p {
      color: var(--sg-muted);
      margin-top: 0.75rem;
      max-width: 46rem;
    }
    .highlight-copy strong { color: var(--sg-ink, #022f32); }

    @keyframes fadeUp {
      from { opacity: 0; transform: translateY(12px); }
      to { opacity: 1; transform: translateY(0); }
    }
    @keyframes pulseSoft {
      0%, 100% { opacity: 0.75; }
      50% { opacity: 1; }
    }

    @media (prefers-reduced-motion: reduce) {
      .hero-panel,
      .road-card,
      .hero-pulse li {
        animation: none;
      }
      .cat-card:hover,
      .alert-card:hover,
      .jump a:hover {
        transform: none;
      }
    }

    @media (max-width: 960px) {
      .hero-layout,
      .highlight-grid,
      .cat-grid {
        grid-template-columns: 1fr;
      }
      .hero-panel { max-width: 420px; }
    }
    @media (max-width: 800px) {
      .alert-grid,
      .intro-points {
        grid-template-columns: 1fr;
      }
      .jump-wrap { position: static; }
    }
  `,
})
export class LiveTrackingPage implements OnInit, AfterViewInit {
  readonly heroImageCss = websiteCssUrl('/website/product/live-map.png');

  private readonly seo = inject(WebsiteSeoService);
  private readonly route = inject(ActivatedRoute);

  readonly demoMapCoordinates = WEBSITE_DEMO_MAP_COORDINATES;
  readonly demoMapAddressLine = WEBSITE_DEMO_MAP_ADDRESS_SHORT;

  readonly groups: FeatureGroup[] = [
    {
      id: 'map',
      title: 'Fleet Map',
      kicker: 'See the whole operation',
      lead: 'One interactive map for every tracked vehicle — zoom out for the fleet or focus on a single unit.',
      icon: 'map',
      accent: 'map',
      count: 4,
      items: [
        {
          title: 'Real-Time Location',
          icon: 'my_location',
          what: 'Vehicle positions update on the map as GPS devices and supported apps report new fixes.',
          why: 'Dispatch and owners work from the same live picture instead of chasing phone updates.',
        },
        {
          title: 'Multi-Vehicle View',
          icon: 'layers',
          what: 'Clustered markers, KPI tiles and filters for moving, idle, parked, offline and more.',
          why: 'Spot gaps in coverage or unusual patterns across rental, logistics and staff transport fleets.',
        },
        {
          title: 'Follow Vehicle',
          icon: 'gps_fixed',
          what: 'Lock the map on a selected vehicle while it moves — or pause live updates when you need a steady view.',
          why: 'Track one high-priority trip without losing the rest of the fleet in the sidebar.',
        },
        {
          title: 'Map Controls',
          icon: 'fullscreen',
          what: 'Refresh, fullscreen, map theme and search by plate, driver name or device identifier.',
          why: 'Operators find the right unit quickly during busy shifts.',
        },
      ],
    },
    {
      id: 'status',
      title: 'Speed & Status',
      kicker: 'Context at a glance',
      lead: 'Operational state, speed and device health — so you know whether a vehicle is working, waiting or unreachable.',
      icon: 'speed',
      accent: 'status',
      count: 4,
      items: [
        {
          title: 'Movement States',
          icon: 'directions_car',
          what: 'Moving, idle, parked and offline labels derived from speed, ignition and last reported time.',
          why: 'Answer “is it still on the road?” without opening a spreadsheet.',
        },
        {
          title: 'Speed & Ignition',
          icon: 'key',
          what: 'Current speed and ignition on/off when the device reports them, shown in the map popup and detail panel.',
          why: 'Separate a parked engine-off stop from idle time with the engine running.',
        },
        {
          title: 'Connectivity',
          icon: 'wifi',
          what: 'Online and offline indicators when a vehicle stops sharing location for longer than you allow.',
          why: 'Catch stale fixes, dead batteries or coverage gaps before a customer calls.',
        },
        {
          title: 'Fleet Health',
          icon: 'monitor_heart',
          what: 'A fleet-level health snapshot from real tracker signals — GPS freshness, battery, GSM and open critical alerts.',
          why: 'Prioritize which vehicles need attention before they drop off the map entirely.',
        },
      ],
    },
    {
      id: 'review',
      title: 'Geofences & History',
      kicker: 'Review and investigate',
      lead: 'Overlay zones on the map, then open history when you need stops, routes and playback — not just the last pin.',
      icon: 'history',
      accent: 'review',
      count: 4,
      items: [
        {
          title: 'Geofence Layers',
          icon: 'crop_free',
          what: 'Toggle active geofence boundaries on the live map to see enter and exit context visually.',
          why: 'Confirm site arrivals and restricted-area compliance while you watch the fleet.',
        },
        {
          title: 'GPS History',
          icon: 'route',
          what: 'Jump from a vehicle to GPS History for a chosen date range — positions, stops and trip detail.',
          why: 'Settle disputes, audit driver routes and document customer visits with evidence.',
        },
        {
          title: 'Trip Playback',
          icon: 'play_circle',
          what: 'Replay recorded movement on the map to see how a journey unfolded over time.',
          why: 'Understand delays, detours and idle time without reconstructing the day manually.',
        },
        {
          title: 'Works With GPS Alerts',
          icon: 'notifications_active',
          what: 'Live map focuses on visibility; GPS Alerts handles SOS, speed, geofence, ignition and vehicle-health notifications.',
          why: 'Watch the fleet in real time and get pinged when rules break — two views, one platform.',
        },
      ],
    },
  ];

  ngOnInit(): void {
    this.seo.set(
      'Live Tracking',
      'SheikhGo Live Tracking — real-time fleet map, speed and status, geofence layers, history playback and fleet health for GPS-tracked vehicles.',
      '/live-tracking',
    );
  }

  ngAfterViewInit(): void {
    this.route.fragment.subscribe(fragment => {
      if (!fragment) return;
      setTimeout(() =>
        document.getElementById(fragment)?.scrollIntoView({ behavior: 'smooth', block: 'start' }),
      );
    });
  }
}
