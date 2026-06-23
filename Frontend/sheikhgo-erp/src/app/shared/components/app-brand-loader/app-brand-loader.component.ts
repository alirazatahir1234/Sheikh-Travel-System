import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { APP_LOGO_PATH, APP_PRODUCT_NAME } from '../../../core/constants/app-brand';

export type BrandLoaderSize = 'sm' | 'md' | 'lg';

@Component({
  selector: 'app-brand-loader',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="brand-loader"
      [class.brand-loader--sm]="size() === 'sm'"
      [class.brand-loader--lg]="size() === 'lg'"
      role="status"
      [attr.aria-label]="ariaLabel()"
    >
      <div class="brand-loader__visual" aria-hidden="true">
        <div class="brand-loader__gps">
          <span class="brand-loader__gps-ring"></span>
          <span class="brand-loader__gps-ring"></span>
          <span class="brand-loader__gps-ring"></span>
        </div>
        <div class="brand-loader__logo-wrap">
          <img class="brand-loader__logo" [src]="logoPath" alt="" />
        </div>
      </div>
      @if (message()) {
        <p class="brand-loader__message">{{ message() }}</p>
      }
    </div>
  `,
  styles: [`
    .brand-loader {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 14px;
      padding: 48px 24px;
      width: 100%;
      box-sizing: border-box;
    }

    .brand-loader--sm {
      padding: 28px 16px;
      gap: 10px;
    }

    .brand-loader--lg {
      padding: 64px 24px;
      gap: 16px;
    }

    .brand-loader__visual {
      position: relative;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .brand-loader__logo-wrap {
      position: relative;
      z-index: 1;
      border-radius: 14px;
      background: #113537;
      border: 1px solid rgba(17, 129, 119, 0.35);
      box-shadow: 0 2px 8px rgba(2, 6, 23, 0.12);
      overflow: hidden;
      animation: brand-loader-breathe 2.2s ease-in-out infinite;
    }

    .brand-loader__logo {
      display: block;
      width: 88px;
      height: 88px;
      object-fit: contain;
    }

    .brand-loader--sm .brand-loader__logo {
      width: 56px;
      height: 56px;
    }

    .brand-loader--lg .brand-loader__logo {
      width: 112px;
      height: 112px;
    }

    .brand-loader__gps {
      position: absolute;
      inset: -18px;
      pointer-events: none;
    }

    .brand-loader--sm .brand-loader__gps {
      inset: -12px;
    }

    .brand-loader--lg .brand-loader__gps {
      inset: -22px;
    }

    .brand-loader__gps-ring {
      position: absolute;
      inset: 0;
      border: 2px solid rgba(17, 129, 119, 0.5);
      border-radius: 50%;
      animation: brand-loader-gps 2s ease-out infinite;
    }

    .brand-loader__gps-ring:nth-child(2) {
      animation-delay: 0.55s;
    }

    .brand-loader__gps-ring:nth-child(3) {
      animation-delay: 1.1s;
    }

    .brand-loader__message {
      margin: 0;
      font-family: Inter, system-ui, sans-serif;
      font-size: 0.875rem;
      font-weight: 500;
      color: var(--stb-text-muted, #64748b);
      text-align: center;
    }

    @keyframes brand-loader-breathe {
      0%, 100% { transform: scale(1); }
      50% { transform: scale(1.04); }
    }

    @keyframes brand-loader-gps {
      0% { transform: scale(0.5); opacity: 0.7; }
      100% { transform: scale(1.25); opacity: 0; }
    }

    @media (prefers-reduced-motion: reduce) {
      .brand-loader__logo-wrap,
      .brand-loader__gps-ring {
        animation: none !important;
      }
    }
  `]
})
export class AppBrandLoaderComponent {
  readonly size = input<BrandLoaderSize>('md');
  readonly message = input<string | undefined>(undefined);

  readonly logoPath = APP_LOGO_PATH;

  readonly ariaLabel = computed(() => {
    const text = this.message();
    return text ? text : `Loading ${APP_PRODUCT_NAME}`;
  });
}
