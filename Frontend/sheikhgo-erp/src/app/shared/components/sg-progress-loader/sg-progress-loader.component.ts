import { ChangeDetectionStrategy, Component, input } from '@angular/core';

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
          <radialGradient id="sgCoreGlow" cx="50%" cy="45%" r="55%">
            <stop offset="0%" stop-color="#14544f" />
            <stop offset="100%" stop-color="#0E3D3A" />
          </radialGradient>
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

        <circle class="sg-loader__core" cx="60" cy="60" r="30" fill="url(#sgCoreGlow)" />

        <circle
          cx="60"
          cy="60"
          r="30"
          fill="none"
          stroke="rgba(15, 175, 159, 0.22)"
          stroke-width="1"
        />

        <path
          class="sg-loader__globe"
          d="M44 52c6-8 14-12 16-12s10 4 16 12M76 68c-6 8-14 12-16 12s-10-4-16-12M48 60h24"
          fill="none"
          stroke="rgba(15, 175, 159, 0.28)"
          stroke-width="1.2"
        />

        <text
          class="sg-loader__mark"
          x="60"
          y="67"
          text-anchor="middle"
          fill="#ffffff"
        >SG</text>
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

    .sg-loader__mark {
      font-family: Georgia, 'Times New Roman', serif;
      font-size: 24px;
      font-weight: 700;
      letter-spacing: -0.04em;
    }

    .sg-loader--sm .sg-loader__mark {
      font-size: 16px;
    }

    .sg-loader--lg .sg-loader__mark {
      font-size: 34px;
    }

    @keyframes sg-arc-spin {
      to { transform: rotate(360deg); }
    }

    @keyframes sg-arc-pulse {
      0%, 100% { stroke-dasharray: 24 76; opacity: 0.92; }
      50% { stroke-dasharray: 34 66; opacity: 1; }
    }

    @media (prefers-reduced-motion: reduce) {
      .sg-loader__arc-wrap,
      .sg-loader__arc {
        animation: none !important;
      }

      .sg-loader__arc {
        stroke-dasharray: 30 70;
        opacity: 1;
      }
    }
  `]
})
export class SgProgressLoaderComponent {
  readonly size = input<SgLoaderSize>('md');
}
