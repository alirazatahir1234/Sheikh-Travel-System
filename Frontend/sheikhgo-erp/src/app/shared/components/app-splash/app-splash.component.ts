import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { APP_PRODUCT_NAME } from '../../../core/constants/app-brand';
import { SgProgressLoaderComponent } from '../sg-progress-loader/sg-progress-loader.component';

@Component({
  selector: 'app-splash',
  standalone: true,
  imports: [SgProgressLoaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="splash-screen"
      [class.splash-screen--exit]="hiding()"
      role="status"
      [attr.aria-label]="'Loading ' + productName"
    >
      <div class="splash-screen__content">
        <sg-progress-loader size="lg" />
        <p class="splash-screen__label">{{ productName }}</p>
      </div>
    </div>
  `,
  styles: [`
    .splash-screen {
      position: fixed;
      inset: 0;
      z-index: 10000;
      display: flex;
      align-items: center;
      justify-content: center;
      background: linear-gradient(180deg, #f8fffe 0%, #eef8f6 100%);
      transition: opacity 0.5s ease, visibility 0.5s ease;
    }

    .splash-screen--exit {
      opacity: 0;
      visibility: hidden;
      pointer-events: none;
    }

    .splash-screen__content {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 18px;
      animation: splash-fade-in 0.45s ease-out both;
    }

    .splash-screen__label {
      margin: 0;
      font-family: Inter, system-ui, sans-serif;
      font-size: 0.95rem;
      font-weight: 600;
      letter-spacing: 0.04em;
      color: #0E3D3A;
    }

    @keyframes splash-fade-in {
      from { opacity: 0; transform: translateY(8px); }
      to { opacity: 1; transform: translateY(0); }
    }

    @media (prefers-reduced-motion: reduce) {
      .splash-screen__content {
        animation: none;
      }
    }
  `]
})
export class AppSplashComponent {
  readonly hiding = input(false);
  readonly productName = APP_PRODUCT_NAME;
}
