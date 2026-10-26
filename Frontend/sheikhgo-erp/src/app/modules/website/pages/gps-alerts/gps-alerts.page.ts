import { AfterViewInit, Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CtaBandComponent } from '../../shared/cta-band.component';
import { WebsiteSeoService } from '../../core/seo.service';

interface AlertItem {
  title: string;
  what: string;
  why: string;
}

interface AlertGroup {
  id: string;
  title: string;
  kicker: string;
  lead: string;
  items: AlertItem[];
}

@Component({
  selector: 'app-gps-alerts-page',
  standalone: true,
  imports: [RouterLink, CtaBandComponent],
  template: `
    <section class="page-hero">
      <div class="container">
        <span class="section-kicker" style="color:#99f6e4">GPS Tracking</span>
        <h1>GPS Alerts</h1>
        <p class="hero-tagline">Know the moment something needs your attention.</p>
        <p>
          SheikhGo watches your vehicles around the clock and notifies owners and managers when something needs
          attention — speeding, leaving a zone, device issues, panic, fuel and more. Alerts reach you on the app,
          dashboard, email or SMS so you can act before a small issue becomes a costly one.
        </p>
        <div class="hero-actions">
          <a routerLink="/request-demo" class="btn btn-primary">Request a Demo</a>
          <a routerLink="/gps-tracking" class="btn btn-ghost">GPS Tracking →</a>
        </div>
      </div>
    </section>

    <section class="section intro">
      <div class="container intro-grid">
        <div>
          <span class="section-kicker">Why it matters</span>
          <h2 class="section-title">Live map is not enough</h2>
          <p class="section-lead">
            Seeing where a vehicle is helps — but fleet owners also need to know when it goes offline, leaves an
            allowed area, exceeds a safe speed, or when a driver hits panic. SheikhGo GPS Alerts turn those moments
            into clear notifications you can act on.
          </p>
        </div>
        <ul class="intro-points">
          <li>13 alert types covering safety, tracking and vehicle health</li>
          <li>Know which vehicle, what happened, and when — without calling the driver first</li>
          <li>Fewer surprises for rental, transport, logistics and staff transport fleets</li>
          <li>Turn on what matters to your operation; leave the rest off</li>
        </ul>
      </div>
    </section>

    <nav class="section jump-wrap" aria-label="Alert groups">
      <div class="container jump">
        @for (g of groups; track g.id) {
          <a [routerLink]="[]" [fragment]="g.id">{{ g.title }}</a>
        }
      </div>
    </nav>

    @for (g of groups; track g.id) {
      <section class="section group" [id]="g.id">
        <div class="container">
          <header class="group-head">
            <span class="section-kicker">{{ g.kicker }}</span>
            <h2 class="section-title">{{ g.title }}</h2>
            <p class="section-lead">{{ g.lead }}</p>
          </header>
          <div class="alert-grid">
            @for (a of g.items; track a.title) {
              <article class="alert-card">
                <h3>{{ a.title }}</h3>
                <p class="what"><strong>What it does:</strong> {{ a.what }}</p>
                <p class="why"><strong>Why it matters:</strong> {{ a.why }}</p>
              </article>
            }
          </div>
        </div>
      </section>
    }

    <section class="section highlight">
      <div class="container highlight-inner">
        <span class="section-kicker">Road Speed Alert</span>
        <h2 class="section-title">Legal road limit — not one fixed number</h2>
        <p>
          Fixed Speed Limit alerts when a vehicle crosses a limit you set (for example 100 km/h everywhere).
          <strong> Road Speed Alert</strong> goes further: it checks the vehicle against the posted speed limit of
          the road it is on right now. City street, highway, or service road — the alert follows the actual limit
          for that stretch, with a buffer you can set so minor overspeeds do not flood your inbox.
        </p>
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
    .hero-tagline {
      font-size: 1.15rem;
      color: #99f6e4;
      margin: 0.75rem 0 1rem;
      font-weight: 600;
    }
    .hero-actions {
      margin-top: 1.4rem;
      display: flex;
      gap: 0.75rem;
      flex-wrap: wrap;
    }

    .intro { background: #fff; }
    .intro-grid {
      display: grid;
      grid-template-columns: 1.4fr 1fr;
      gap: 2rem;
      align-items: start;
    }
    .intro-points {
      margin: 0;
      padding: 0;
      list-style: none;
      display: grid;
      gap: 0.65rem;
    }
    .intro-points li {
      background: var(--sg-mist, #e9faf4);
      border-radius: 12px;
      padding: 0.9rem 1rem;
      font-weight: 600;
      color: var(--sg-ink, #022f32);
      font-size: 0.95rem;
    }

    .jump-wrap {
      padding: 1.25rem 0;
      background: var(--sg-page, #f5fbf9);
      border-bottom: 1px solid var(--sg-line);
    }
    .jump {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
    }
    .jump a {
      padding: 0.45rem 0.9rem;
      border-radius: 999px;
      font-size: 0.85rem;
      font-weight: 600;
      background: rgba(15, 118, 110, 0.08);
      color: var(--sg-teal, #0f766e);
    }
    .jump a:hover {
      background: var(--sg-mist, #e9faf4);
    }

    .group {
      scroll-margin-top: 96px;
      border-bottom: 1px solid var(--sg-line);
    }
    .group:nth-of-type(even) {
      background: #fff;
    }
    .group-head {
      max-width: 42rem;
      margin-bottom: 1.5rem;
    }

    .alert-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 1rem;
    }
    .alert-card {
      background: #fff;
      border: 1px solid var(--sg-line);
      border-radius: 14px;
      padding: 1.25rem 1.35rem;
    }
    .group:nth-of-type(even) .alert-card {
      background: var(--sg-page, #f5fbf9);
    }
    .alert-card h3 {
      font-size: 1.1rem;
      margin-bottom: 0.65rem;
    }
    .what,
    .why {
      color: var(--sg-muted);
      font-size: 0.95rem;
      margin: 0;
    }
    .what {
      margin-bottom: 0.45rem;
    }
    .what strong,
    .why strong {
      color: var(--sg-ink, #022f32);
      font-weight: 600;
    }

    .highlight {
      background: linear-gradient(135deg, #e9faf4 0%, #f5fbf9 55%, #fff 100%);
    }
    .highlight-inner {
      max-width: 46rem;
    }
    .highlight-inner p {
      color: var(--sg-muted);
      margin-top: 0.75rem;
    }

    @media (max-width: 800px) {
      .intro-grid,
      .alert-grid {
        grid-template-columns: 1fr;
      }
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
      items: [
        {
          title: 'SOS Panic',
          what: 'Sends an urgent alert when the driver presses the panic button in the vehicle or on the app.',
          why: 'Gives your team a chance to respond immediately to threats, accidents or medical emergencies.',
        },
        {
          title: 'Fixed Speed Limit',
          what: 'Alerts when a vehicle goes faster than a speed you set for the fleet or that vehicle (for example 100 km/h).',
          why: 'Simple company rule — one clear ceiling for drivers who ignore your policy.',
        },
        {
          title: 'Road Speed Alert',
          what:
            'Compares the vehicle’s current speed with the legal speed limit of the road it is on, plus a small buffer you can set.',
          why:
            'City roads and highways have different limits. You catch real overspeeding on that street — not just against one blanket number.',
        },
        {
          title: 'Geofence Enter',
          what: 'Notifies when a vehicle enters an area you defined (depot, customer site, restricted area, or city boundary).',
          why: 'Confirm arrivals and site visits without waiting for a driver phone call.',
        },
        {
          title: 'Geofence Exit',
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
      items: [
        {
          title: 'Vehicle Online',
          what: 'Tells you when a vehicle reconnects and starts sharing live location again.',
          why: 'You know it is back on the map after night parking, a tunnel, or a temporary signal gap.',
        },
        {
          title: 'Vehicle Offline',
          what: 'Alerts when a vehicle stops sharing location for longer than you allow.',
          why: 'Catch dead batteries, covered devices, network issues or suspicious disconnects early.',
        },
        {
          title: 'Ignition On',
          what: 'Fires when the vehicle’s engine is switched on.',
          why: 'See when a trip actually starts — useful for duty hours, rental handovers and night movements.',
        },
        {
          title: 'Ignition Off',
          what: 'Fires when the engine is switched off.',
          why: 'Confirm parking, end of trip, or unexpected stops during an active job.',
        },
        {
          title: 'GPS Lost',
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
      items: [
        {
          title: 'Low Battery',
          what: 'Alerts when the backup battery or vehicle battery drops below a safe level.',
          why: 'Charge or replace it before you lose visibility mid-route.',
        },
        {
          title: 'Power Cut',
          what: 'Notifies when main power to the tracking device is disconnected.',
          why: 'A common theft or tampering warning — act while there is still time to respond.',
        },
        {
          title: 'Fuel',
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
