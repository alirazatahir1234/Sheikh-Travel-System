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
          <linearGradient id="sgArcGlow" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stop-color="#0FAF9F" stop-opacity="0.35" />
            <stop offset="55%" stop-color="#0FAF9F" />
            <stop offset="100%" stop-color="#5eead4" />
          </linearGradient>
        </defs>

        <circle
          class="sg-loader__track"
          cx="60"
          cy="60"
          r="46"
          fill="none"
          stroke="#0FAF9F"
          stroke-width="7"
          stroke-opacity="0.18"
        />

        <g class="sg-loader__arc-wrap">
          <circle
            class="sg-loader__arc"
            cx="60"
            cy="60"
            r="46"
            fill="none"
            stroke="url(#sgArcGlow)"
            stroke-width="7"
            stroke-linecap="round"
            pathLength="100"
            stroke-dasharray="28 72"
          />
        </g>

        <circle
          cx="60"
          cy="60"
          r="34"
          fill="none"
          stroke="rgba(15, 175, 159, 0.12)"
          stroke-width="1"
        />

        <g class="sg-loader__logo-wrap">
          <image
            class="sg-loader__logo"
            [attr.href]="logoPath"
            x="32"
            y="28"
            width="56"
            height="54"
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

    .sg-loader__arc-wrap {
      transform-origin: 60px 60px;
      animation: sg-arc-spin 1.15s linear infinite;
    }

    .sg-loader__arc {
      transform-origin: 60px 60px;
      animation: sg-arc-pulse 1.15s ease-in-out infinite;
    }

    .sg-loader__logo-wrap {
      transform-origin: 60px 55px;
      animation: sg-logo-pulse 1.85s ease-in-out infinite;
    }

    @keyframes sg-arc-spin {
      to { transform: rotate(360deg); }
    }

    @keyframes sg-arc-pulse {
      0%, 100% { stroke-dasharray: 24 76; opacity: 0.92; }
      50% { stroke-dasharray: 34 66; opacity: 1; }
    }

    @keyframes sg-logo-pulse {
      0%, 100% { opacity: 0.88; transform: scale(0.94); }
      50% { opacity: 1; transform: scale(1); }
    }

    @media (prefers-reduced-motion: reduce) {
      .sg-loader__arc-wrap,
      .sg-loader__arc,
      .sg-loader__logo-wrap {
        animation: none !important;
      }

      .sg-loader__arc {
        stroke-dasharray: 30 70;
        opacity: 1;
      }

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
