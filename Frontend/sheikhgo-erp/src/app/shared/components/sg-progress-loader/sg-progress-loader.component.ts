import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { APP_LOGO_PATH } from '../../../core/constants/app-brand';

export type SgLoaderSize = 'sm' | 'md' | 'lg';

@Component({
  selector: 'sg-progress-loader',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="sg-loader"
      [class.sg-loader--sm]="size() === 'sm'"
      [class.sg-loader--lg]="size() === 'lg'"
      aria-hidden="true"
    >
      <svg class="sg-loader__svg" viewBox="0 0 120 120" xmlns="http://www.w3.org/2000/svg">
        <defs>
          <linearGradient id="sgNexusGlow" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stop-color="#0FAF9F" stop-opacity="0.4" />
            <stop offset="55%" stop-color="#0FAF9F" />
            <stop offset="100%" stop-color="#5eead4" />
          </linearGradient>
          <radialGradient id="sgHubGlow" cx="50%" cy="50%" r="50%">
            <stop offset="0%" stop-color="#5eead4" stop-opacity="0.35" />
            <stop offset="100%" stop-color="#0FAF9F" stop-opacity="0" />
          </radialGradient>
        </defs>

        <circle cx="60" cy="60" r="28" fill="url(#sgHubGlow)" />

        <g class="sg-loader__nexus">
          <polygon
            points="60,12 101.57,36 101.57,84 60,108 18.43,84 18.43,36"
            fill="none"
            stroke="#0FAF9F"
            stroke-width="1.5"
            stroke-opacity="0.22"
          />

          <g stroke="url(#sgNexusGlow)" stroke-width="2" stroke-linecap="round" fill="none">
            <line class="sg-loader__link" x1="60" y1="60" x2="60" y2="12" />
            <line class="sg-loader__link" x1="60" y1="60" x2="101.57" y2="36" />
            <line class="sg-loader__link" x1="60" y1="60" x2="101.57" y2="84" />
            <line class="sg-loader__link" x1="60" y1="60" x2="60" y2="108" />
            <line class="sg-loader__link" x1="60" y1="60" x2="18.43" y2="84" />
            <line class="sg-loader__link" x1="60" y1="60" x2="18.43" y2="36" />
          </g>

          <g stroke="#0FAF9F" stroke-width="1.75" stroke-opacity="0.55" stroke-linecap="round" fill="none">
            <line class="sg-loader__link" x1="60" y1="12" x2="101.57" y2="36" />
            <line class="sg-loader__link" x1="101.57" y1="36" x2="101.57" y2="84" />
            <line class="sg-loader__link" x1="101.57" y1="84" x2="60" y2="108" />
            <line class="sg-loader__link" x1="60" y1="108" x2="18.43" y2="84" />
            <line class="sg-loader__link" x1="18.43" y1="84" x2="18.43" y2="36" />
            <line class="sg-loader__link" x1="18.43" y1="36" x2="60" y2="12" />
          </g>

          <g fill="#0FAF9F">
            <circle class="sg-loader__node" cx="60" cy="12" r="4.5" />
            <circle class="sg-loader__node" cx="101.57" cy="36" r="4.5" />
            <circle class="sg-loader__node" cx="101.57" cy="84" r="4.5" />
            <circle class="sg-loader__node" cx="60" cy="108" r="4.5" />
            <circle class="sg-loader__node" cx="18.43" cy="84" r="4.5" />
            <circle class="sg-loader__node" cx="18.43" cy="36" r="4.5" />
          </g>

          <g class="sg-loader__hub">
            <circle cx="60" cy="60" r="11" fill="none" stroke="url(#sgNexusGlow)" stroke-width="2.5" />
            <circle cx="60" cy="60" r="4" fill="#0E3D3A" />
          </g>
        </g>

        <g class="sg-loader__logo-wrap">
          <image
            class="sg-loader__logo"
            [attr.href]="logoPath"
            x="36"
            y="33"
            width="48"
            height="46"
            preserveAspectRatio="xMidYMid meet"
          />
        </g>
      </svg>
    </div>
  `,
  styles: [`
    .sg-loader {
      --sg-size: 72px;
      width: var(--sg-size);
      height: var(--sg-size);
      display: inline-flex;
      align-items: center;
      justify-content: center;
      flex: 0 0 auto;
    }

    .sg-loader--sm {
      --sg-size: 48px;
    }

    .sg-loader--lg {
      --sg-size: 112px;
    }

    .sg-loader__svg {
      width: 100%;
      height: 100%;
      display: block;
      overflow: visible;
      filter: drop-shadow(0 4px 14px rgba(14, 61, 58, 0.18));
    }

    .sg-loader__nexus {
      transform-origin: 60px 60px;
      animation: sg-nexus-spin 2.8s linear infinite;
    }

    .sg-loader__link {
      stroke-dasharray: 10 18;
      animation: sg-nexus-flow 1.4s linear infinite;
    }

    .sg-loader__node {
      transform-box: fill-box;
      transform-origin: center;
      animation: sg-nexus-node 1.6s ease-in-out infinite;
    }

    .sg-loader__node:nth-child(1) { animation-delay: 0s; }
    .sg-loader__node:nth-child(2) { animation-delay: 0.12s; }
    .sg-loader__node:nth-child(3) { animation-delay: 0.24s; }
    .sg-loader__node:nth-child(4) { animation-delay: 0.36s; }
    .sg-loader__node:nth-child(5) { animation-delay: 0.48s; }
    .sg-loader__node:nth-child(6) { animation-delay: 0.6s; }

    .sg-loader__hub {
      transform-origin: 60px 60px;
      animation: sg-hub-pulse 1.85s ease-in-out infinite;
    }

    .sg-loader__logo-wrap {
      transform-origin: 60px 55px;
      animation: sg-logo-pulse 1.85s ease-in-out infinite;
    }

    @keyframes sg-nexus-spin {
      to { transform: rotate(360deg); }
    }

    @keyframes sg-nexus-flow {
      to { stroke-dashoffset: -28; }
    }

    @keyframes sg-nexus-node {
      0%, 100% { opacity: 0.55; transform: scale(0.85); }
      50% { opacity: 1; transform: scale(1.15); }
    }

    @keyframes sg-hub-pulse {
      0%, 100% { opacity: 0.7; }
      50% { opacity: 1; }
    }

    @keyframes sg-logo-pulse {
      0%, 100% { opacity: 0.88; transform: scale(0.94); }
      50% { opacity: 1; transform: scale(1); }
    }

    @media (prefers-reduced-motion: reduce) {
      .sg-loader__nexus,
      .sg-loader__link,
      .sg-loader__node,
      .sg-loader__hub,
      .sg-loader__logo-wrap {
        animation: none !important;
      }

      .sg-loader__node,
      .sg-loader__logo-wrap {
        opacity: 1;
        transform: scale(1);
      }
    }
  `]
})
export class SgProgressLoaderComponent {
  readonly size = input<SgLoaderSize>('md');
  readonly logoPath = APP_LOGO_PATH;
}
