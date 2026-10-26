import { AfterViewInit, Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { CtaBandComponent } from '../../shared/cta-band.component';
import { WebsiteSeoService } from '../../core/seo.service';

interface AlertItem {
  title: string;
  icon: string;
  what: string;
  why: string;
}

interface AlertGroup {
  id: string;
  title: string;
  kicker: string;
  lead: string;
  icon: string;
  accent: 'safety' | 'tracking' | 'health';
  count: number;
  items: AlertItem[];
}

@Component({
  selector: 'app-gps-alerts-page',
  standalone: true,
  imports: [RouterLink, MatIconModule, CtaBandComponent],
  template: `
    <section class="page-hero">
      <div class="container hero-layout">
        <div class="hero-copy">
          <span class="section-kicker" style="color:#99f6e4">GPS Tracking</span>
          <h1>GPS Alerts</h1>
          <p class="hero-tagline">Know the moment something needs your attention.</p>
          <p class="hero-lead">
            SheikhGo watches your vehicles around the clock and notifies owners and managers when something needs
            attention — speeding, leaving a zone, device issues, panic, fuel and more. Alerts reach you on the app,
            dashboard, email or SMS so you can act before a small issue becomes a costly one.
          </p>
          <div class="hero-actions">
            <a routerLink="/request-demo" class="btn btn-primary">Request a Demo</a>
            <a routerLink="/live-tracking" class="btn btn-ghost">Live Tracking →</a>
          </div>
        </div>
        <aside class="hero-panel" aria-label="Alert coverage at a glance">
          <div class="hero-panel-glow" aria-hidden="true"></div>
          <div class="stat-chip">
            <span class="stat-num">13</span>
            <span class="stat-label">Alert types</span>
          </div>
          <div class="stat-chip">
            <span class="stat-num">3</span>
            <span class="stat-label">Categories</span>
          </div>
          <div class="stat-chip wide">
            <mat-icon aria-hidden="true">notifications_active</mat-icon>
            <span class="stat-label">App · Dashboard · Email · SMS</span>
          </div>
          <ul class="hero-pulse" aria-hidden="true">
            <li><span class="dot danger"></span> SOS Panic</li>
            <li><span class="dot warn"></span> Road Speed</li>
            <li><span class="dot ok"></span> Vehicle Online</li>
          </ul>
        </aside>
      </div>
    </section>

    <section class="section categories">
      <div class="container">
        <header class="section-head">
          <span class="section-kicker">Coverage</span>
          <h2 class="section-title">Three ways alerts protect your fleet</h2>
          <p class="section-lead">
            Live map is not enough. SheikhGo turns critical moments into clear notifications — so you know which
            vehicle, what happened, and when.
          </p>
        </header>
        <div class="cat-grid">
          @for (g of groups; track g.id) {
            <a class="cat-card" [attr.data-accent]="g.accent" [routerLink]="[]" [fragment]="g.id">
              <span class="cat-icon" aria-hidden="true"><mat-icon>{{ g.icon }}</mat-icon></span>
              <div class="cat-meta">
                <span class="cat-count">{{ g.count }} alerts</span>
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
            <span>13 alert types covering safety, tracking and vehicle health</span>
          </li>
          <li>
            <mat-icon aria-hidden="true">check_circle</mat-icon>
            <span>Know which vehicle, what happened, and when — without calling the driver first</span>
          </li>
          <li>
            <mat-icon aria-hidden="true">check_circle</mat-icon>
            <span>Fewer surprises for rental, transport, logistics and staff transport fleets</span>
          </li>
          <li>
            <mat-icon aria-hidden="true">check_circle</mat-icon>
            <span>Turn on what matters to your operation; leave the rest off</span>
          </li>
        </ul>
      </div>
    </section>

    <nav class="jump-wrap" aria-label="Alert groups">
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
                    <span class="label">What it does</span>
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
            <mat-icon>add_road</mat-icon>
            <div class="road-meta">
              <strong>Road Speed Alert</strong>
              <span>City street · 50 km/h</span>
            </div>
            <span class="road-badge">+ buffer</span>
          </div>
          <div class="road-card dim">
            <mat-icon>speed</mat-icon>
            <div class="road-meta">
              <strong>Fixed Speed Limit</strong>
              <span>Fleet rule · 100 km/h</span>
            </div>
          </div>
        </div>
        <div class="highlight-copy">
          <span class="section-kicker">Road Speed Alert</span>
          <h2 class="section-title">Legal road limit — not one fixed number</h2>
          <p>
            Fixed Speed Limit alerts when a vehicle crosses a limit you set (for example 100 km/h everywhere).
            <strong> Road Speed Alert</strong> goes further: it checks the vehicle against the posted speed limit of
            the road it is on right now. City street, highway, or service road — the alert follows the actual limit
            for that stretch, with a buffer you can set so minor overspeeds do not flood your inbox.
          </p>
        </div>
      </div>
    </section>

    <app-cta-band
      title="Ready to see GPS Alerts on your fleet?"
      lead="Book a short demo. We will walk through live tracking and the alert types that matter for your vehicles."
      primaryText="Request a Demo"
      primaryUrl="/request-demo"
      secondaryText="Contact Us →"
      secondaryUrl="/contact"
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
    .cat-card[data-accent='safety'] .cat-icon {
      background: rgba(15, 118, 110, 0.12);
      color: var(--sg-teal, #0f766e);
    }
    .cat-card[data-accent='tracking'] .cat-icon {
      background: rgba(24, 201, 135, 0.15);
      color: #0b8f6a;
    }
    .cat-card[data-accent='health'] .cat-icon {
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
    .group[data-accent='tracking'] .alert-icon {
      background: rgba(24, 201, 135, 0.14);
      color: #0b8f6a;
    }
    .group[data-accent='health'] .alert-icon {
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
export class GpsAlertsPage implements OnInit, AfterViewInit {
  private readonly seo = inject(WebsiteSeoService);
  private readonly route = inject(ActivatedRoute);

  readonly groups: AlertGroup[] = [
    {
      id: 'safety',
      title: 'Safety Alerts',
      kicker: 'Protect people',
      lead:
        'Protect drivers, passengers and your reputation. These alerts surface the moments that need a quick response.',
      icon: 'health_and_safety',
      accent: 'safety',
      count: 5,
      items: [
        {
          title: 'SOS Panic',
          icon: 'sos',
          what: 'Sends an urgent alert when the driver presses the panic button in the vehicle or on the app.',
          why: 'Gives your team a chance to respond immediately to threats, accidents or medical emergencies.',
        },
        {
          title: 'Fixed Speed Limit',
          icon: 'speed',
          what: 'Alerts when a vehicle goes faster than a speed you set for the fleet or that vehicle (for example 100 km/h).',
          why: 'Simple company rule — one clear ceiling for drivers who ignore your policy.',
        },
        {
          title: 'Road Speed Alert',
          icon: 'add_road',
          what:
            'Compares the vehicle’s current speed with the legal speed limit of the road it is on, plus a small buffer you can set.',
          why:
            'City roads and highways have different limits. You catch real overspeeding on that street — not just against one blanket number.',
        },
        {
          title: 'Geofence Enter',
          icon: 'login',
          what: 'Notifies when a vehicle enters an area you defined (depot, customer site, restricted area, or city boundary).',
          why: 'Confirm arrivals and site visits without waiting for a driver phone call.',
        },
        {
          title: 'Geofence Exit',
          icon: 'logout',
          what: 'Notifies when a vehicle leaves an allowed or restricted area.',
          why: 'Spot unauthorized movement, early departures or vehicles leaving a job site before the trip is done.',
        },
      ],
    },
    {
      id: 'tracking',
      title: 'Tracking Alerts',
      kicker: 'Stay in control',
      lead: 'Stay in control of where each vehicle is — and whether it is still visible on the map.',
      icon: 'my_location',
      accent: 'tracking',
      count: 5,
      items: [
        {
          title: 'Vehicle Online',
          icon: 'wifi',
          what: 'Tells you when a vehicle reconnects and starts sharing live location again.',
          why: 'You know it is back on the map after night parking, a tunnel, or a temporary signal gap.',
        },
        {
          title: 'Vehicle Offline',
          icon: 'wifi_off',
          what: 'Alerts when a vehicle stops sharing location for longer than you allow.',
          why: 'Catch dead batteries, covered devices, network issues or suspicious disconnects early.',
        },
        {
          title: 'Ignition On',
          icon: 'power_settings_new',
          what: 'Fires when the vehicle’s engine is switched on.',
          why: 'See when a trip actually starts — useful for duty hours, rental handovers and night movements.',
        },
        {
          title: 'Ignition Off',
          icon: 'power_off',
          what: 'Fires when the engine is switched off.',
          why: 'Confirm parking, end of trip, or unexpected stops during an active job.',
        },
        {
          title: 'GPS Lost',
          icon: 'gps_off',
          what: 'Warns when the device cannot get a clear location from satellites (signal blocked or weak).',
          why: 'You will not trust an old pin on the map when the vehicle is no longer reporting a real position.',
        },
      ],
    },
    {
      id: 'vehicle-health',
      title: 'Vehicle Health Alerts',
      kicker: 'Protect the asset',
      lead: 'Protect the vehicle itself — power, battery and fuel — before a roadside failure or theft attempt.',
      icon: 'directions_car',
      accent: 'health',
      count: 3,
      items: [
        {
          title: 'Low Battery',
          icon: 'battery_alert',
          what: 'Alerts when the backup battery or vehicle battery drops below a safe level.',
          why: 'Charge or replace it before you lose visibility mid-route.',
        },
        {
          title: 'Power Cut',
          icon: 'bolt',
          what: 'Notifies when main power to the tracking device is disconnected.',
          why: 'A common theft or tampering warning — act while there is still time to respond.',
        },
        {
          title: 'Fuel',
          icon: 'local_gas_station',
          what: 'Flags unusual fuel drops or refill patterns when your setup supports fuel monitoring.',
          why: 'Help reduce fuel theft and unexplained consumption across commercial fleets.',
        },
      ],
    },
  ];

  ngOnInit(): void {
    this.seo.set(
      'GPS Alerts',
      'SheikhGo GPS Alerts for fleet owners — safety, tracking and vehicle health notifications including road speed, geofence, SOS, ignition, fuel and more.',
      '/gps-alerts',
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
