import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { APP_LOGO_PATH, APP_PRODUCT_NAME } from '../../../core/constants/app-brand';

@Component({
  selector: 'app-splash',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="splash-screen"
      [class.splash-screen--exit]="hiding()"
      role="status"
      [attr.aria-label]="'Loading ' + productName"
    >
      <div class="splash-screen__satellite" aria-hidden="true">
        <svg viewBox="0 0 64 64" class="splash-screen__satellite-icon">
          <rect x="26" y="28" width="12" height="8" rx="2" fill="#118177" />
          <rect x="10" y="30" width="14" height="4" rx="1" fill="#118177" opacity="0.85" />
          <rect x="40" y="30" width="14" height="4" rx="1" fill="#118177" opacity="0.85" />
          <circle cx="32" cy="24" r="4" fill="#f0fffd" />
        </svg>
        <svg viewBox="0 0 80 48" class="splash-screen__satellite-waves">
          <path class="splash-wave splash-wave--1" d="M8 40 Q24 8 40 40" fill="none" stroke="#118177" stroke-width="2" />
          <path class="splash-wave splash-wave--2" d="M20 40 Q36 16 52 40" fill="none" stroke="#118177" stroke-width="2" />
          <path class="splash-wave splash-wave--3" d="M32 40 Q48 24 64 40" fill="none" stroke="#118177" stroke-width="2" />
        </svg>
      </div>

      <div class="splash-screen__center">
        <div class="splash-screen__gps" aria-hidden="true">
          <span class="splash-screen__gps-ring"></span>
          <span class="splash-screen__gps-ring"></span>
          <span class="splash-screen__gps-ring"></span>
        </div>

        <img class="splash-screen__logo" [src]="logoPath" [alt]="productName + ' logo'" />

        <p class="splash-screen__label">{{ productName }}</p>
      </div>

      <div class="splash-screen__road" aria-hidden="true">
        <div class="splash-screen__road-line"></div>
        <svg viewBox="0 0 96 40" class="splash-screen__car">
          <path fill="#118177" d="M6 22h72c6 0 10 4 10 10v4c0 4-3 8-8 9l-6 3H8c-8 0-14-6-14-14v-6c0-6 5-6 12-6z" />
          <path fill="#f0fffd" d="M14 14h18l8 8H12l2-8zm42 0h18l2 8H58l-8-8z" />
          <rect x="24" y="24" width="40" height="8" rx="2" fill="#f0fffd" />
          <circle cx="18" cy="38" r="8" fill="#113537" />
          <circle cx="18" cy="38" r="4" fill="#f0fffd" />
          <circle cx="72" cy="38" r="8" fill="#113537" />
          <circle cx="72" cy="38" r="4" fill="#f0fffd" />
        </svg>
      </div>
    </div>
  `,
  styles: [`
    .splash-screen {
      position: fixed;
      inset: 0;
      z-index: 10000;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      background: #113537;
      overflow: hidden;
      transition: opacity 0.5s ease, visibility 0.5s ease;
    }

    .splash-screen--exit {
      opacity: 0;
      visibility: hidden;
      pointer-events: none;
    }

    .splash-screen__center {
      position: relative;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 16px;
      z-index: 2;
    }

    .splash-screen__logo {
      width: min(240px, 52vw);
      height: auto;
      object-fit: contain;
      animation: splash-logo-in 0.8s ease-out both, splash-logo-breathe 2.4s ease-in-out 0.8s infinite;
    }

    .splash-screen__label {
      margin: 0;
      font-family: Inter, system-ui, sans-serif;
      font-size: 0.95rem;
      font-weight: 600;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      color: rgba(240, 255, 253, 0.72);
      animation: splash-label-in 0.6s ease-out 0.4s both;
    }

    .splash-screen__gps {
      position: absolute;
      top: 58%;
      left: 50%;
      width: 120px;
      height: 120px;
      transform: translate(-50%, -50%);
      pointer-events: none;
    }

    .splash-screen__gps-ring {
      position: absolute;
      inset: 0;
      border: 2px solid rgba(17, 129, 119, 0.55);
      border-radius: 50%;
      animation: splash-gps-pulse 2s ease-out infinite;
    }

    .splash-screen__gps-ring:nth-child(2) {
      animation-delay: 0.55s;
    }

    .splash-screen__gps-ring:nth-child(3) {
      animation-delay: 1.1s;
    }

    .splash-screen__satellite {
      position: absolute;
      top: clamp(16px, 8vh, 48px);
      right: clamp(16px, 6vw, 64px);
      width: 72px;
      height: 72px;
      opacity: 0;
      animation: splash-satellite-in 0.6s ease-out 0.3s both;
    }

    .splash-screen__satellite-icon {
      width: 48px;
      height: 48px;
      display: block;
      animation: splash-satellite-blink 2.2s ease-in-out 0.9s infinite;
    }

    .splash-screen__satellite-waves {
      position: absolute;
      left: -24px;
      bottom: -8px;
      width: 80px;
      height: 48px;
    }

    .splash-wave {
      opacity: 0;
      animation: splash-wave-emit 2.2s ease-out infinite;
    }

    .splash-wave--2 {
      animation-delay: 0.35s;
    }

    .splash-wave--3 {
      animation-delay: 0.7s;
    }

    .splash-screen__road {
      position: absolute;
      bottom: clamp(24px, 8vh, 72px);
      left: 50%;
      transform: translateX(-50%);
      width: min(320px, 80vw);
      height: 56px;
      opacity: 0;
      animation: splash-road-in 0.7s ease-out 0.5s both;
    }

    .splash-screen__road-line {
      position: absolute;
      left: 0;
      right: 0;
      top: 50%;
      height: 3px;
      background: linear-gradient(90deg, transparent, #118177 12%, #118177 88%, transparent);
      transform: translateY(-50%);
    }

    .splash-screen__road-line::after {
      content: '';
      position: absolute;
      inset: -1px 20%;
      background: repeating-linear-gradient(
        90deg,
        transparent 0 12px,
        rgba(240, 255, 253, 0.75) 12px 22px
      );
      animation: splash-road-dash 1.2s linear infinite;
    }

    .splash-screen__car {
      position: absolute;
      left: 0;
      top: 4px;
      width: 72px;
      height: 40px;
      animation: splash-car-drive 2.4s ease-in-out 0.8s infinite;
    }

    @keyframes splash-logo-in {
      from { opacity: 0; transform: scale(0.92); }
      to { opacity: 1; transform: scale(1); }
    }

    @keyframes splash-logo-breathe {
      0%, 100% { transform: scale(1); }
      50% { transform: scale(1.03); }
    }

    @keyframes splash-label-in {
      from { opacity: 0; transform: translateY(8px); }
      to { opacity: 1; transform: translateY(0); }
    }

    @keyframes splash-gps-pulse {
      0% { transform: scale(0.45); opacity: 0.75; }
      100% { transform: scale(1.35); opacity: 0; }
    }

    @keyframes splash-satellite-in {
      from { opacity: 0; transform: translateY(-8px); }
      to { opacity: 1; transform: translateY(0); }
    }

    @keyframes splash-satellite-blink {
      0%, 100% { opacity: 1; }
      50% { opacity: 0.55; }
    }

    @keyframes splash-wave-emit {
      0% { opacity: 0; stroke-dashoffset: 40; }
      20% { opacity: 0.85; }
      100% { opacity: 0; stroke-dashoffset: 0; }
    }

    .splash-wave {
      stroke-dasharray: 40;
      stroke-dashoffset: 40;
    }

    @keyframes splash-road-in {
      from { opacity: 0; transform: translateX(-50%) translateY(12px); }
      to { opacity: 1; transform: translateX(-50%) translateY(0); }
    }

    @keyframes splash-road-dash {
      from { background-position: 0 0; }
      to { background-position: 34px 0; }
    }

    @keyframes splash-car-drive {
      0%, 100% { left: 4%; transform: translateY(0); }
      50% { left: calc(100% - 76px); transform: translateY(-2px); }
    }

    @media (max-width: 600px) {
      .splash-screen__logo {
        width: min(180px, 58vw);
      }

      .splash-screen__satellite {
        width: 56px;
        height: 56px;
        top: 12px;
        right: 12px;
      }

      .splash-screen__satellite-icon {
        width: 40px;
        height: 40px;
      }
    }

    @media (prefers-reduced-motion: reduce) {
      .splash-screen__logo,
      .splash-screen__label,
      .splash-screen__gps-ring,
      .splash-screen__satellite,
      .splash-screen__satellite-icon,
      .splash-wave,
      .splash-screen__road,
      .splash-screen__road-line::after,
      .splash-screen__car {
        animation: none !important;
      }

      .splash-screen__logo,
      .splash-screen__label,
      .splash-screen__satellite,
      .splash-screen__road {
        opacity: 1;
      }
    }
  `]
})
export class AppSplashComponent {
  readonly hiding = input(false);
  readonly productName = APP_PRODUCT_NAME;
  readonly logoPath = APP_LOGO_PATH;
}
