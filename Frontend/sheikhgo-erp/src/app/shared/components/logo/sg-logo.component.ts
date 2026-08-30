import { Component, Input } from '@angular/core';
import { NgClass } from '@angular/common';
import { APP_LOGO_PATH } from '../../../core/constants/app-brand';

/**
 * Shared SheikhGo mark — identical asset + sizing as /auth/login.
 * Asset: APP_LOGO_PATH (`/brand/sheikhgo-logo.png`), transparent RGBA.
 *
 * Sizing rules copied from login SCSS (do not invent widths):
 * - login-hero / header / footer → `.brand-logo` (96 / 72 / 56)
 * - login-card → `.card-logo img` (116 / 88 / 72)
 */
@Component({
  selector: 'app-sg-logo',
  standalone: true,
  imports: [NgClass],
  templateUrl: './sg-logo.component.html',
  styleUrl: './sg-logo.component.scss',
})
export class SgLogoComponent {
  readonly src = APP_LOGO_PATH;

  /**
   * `login-hero` — left brand on /auth/login (96px)
   * `login-card` — form card on /auth/login (116px)
   * `header` / `footer` — same mark size as login-hero
   */
  @Input() variant: 'login-hero' | 'login-card' | 'header' | 'footer' = 'header';

  /** Website chrome shows wordmark; login hero keeps its own title block. */
  @Input() showWordmark = false;

  @Input() wordmark = 'SheikhGo';

  @Input() alt = 'SheikhGo';
}
