import { Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-cta-band',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="cta-band">
      <div class="cta-bg" aria-hidden="true"></div>
      <div class="container inner">
        <div class="copy">
          <h2>{{ title }}</h2>
          <p>{{ lead }}</p>
        </div>
        <div class="actions">
          <a [routerLink]="primaryUrl" class="btn btn-primary">{{ primaryText }}</a>
          <a [routerLink]="secondaryUrl" class="btn btn-ghost">{{ secondaryText }}</a>
        </div>
      </div>
    </section>
  `,
  styles: `
    .cta-band {
      position: relative;
      overflow: hidden;
      padding: clamp(4.5rem, 8vw, 6.5rem) 0;
      color: #fff;
    }
    .cta-bg {
      position: absolute;
      inset: 0;
      background:
        linear-gradient(105deg, rgba(4, 31, 36, 0.92) 0%, rgba(4, 31, 36, 0.78) 45%, rgba(0, 95, 73, 0.55) 100%),
        radial-gradient(circle at 80% 50%, rgba(0, 223, 130, 0.18), transparent 45%),
        linear-gradient(135deg, #041f24, #0a2f36 55%, #005f49);
    }
    .inner {
      position: relative;
      z-index: 1;
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 2.5rem;
      flex-wrap: wrap;
    }
    .copy {
      max-width: 36rem;
    }
    h2 {
      font-size: var(--type-section-title, clamp(2.25rem, 3.6vw, 2.75rem));
      margin-bottom: 0.85rem;
      max-width: 16ch;
      line-height: 1.12;
    }
    p {
      color: rgba(255, 255, 255, 0.78);
      font-size: var(--type-section-lead, 1.125rem);
      line-height: 1.65;
      max-width: 46ch;
    }
    .actions {
      display: flex;
      gap: 0.85rem;
      flex-wrap: wrap;
    }
    .actions .btn {
      font-size: var(--type-btn, 0.9375rem);
      padding: 0.95rem 1.55rem;
    }
    @media (max-width: 720px) {
      .inner {
        flex-direction: column;
        align-items: flex-start;
      }
      h2 {
        max-width: none;
      }
    }
  `,
})
export class CtaBandComponent {
  @Input() title = 'Ready to manage your fleet smarter?';
  @Input() lead =
    'Bring vehicles, drivers, trips, GPS tracking, maintenance and analytics together in SheikhGo.';
  @Input() primaryText = 'Request a Demo';
  @Input() primaryUrl = '/request-demo';
  @Input() secondaryText = 'Contact Sales →';
  @Input() secondaryUrl = '/contact';
}
