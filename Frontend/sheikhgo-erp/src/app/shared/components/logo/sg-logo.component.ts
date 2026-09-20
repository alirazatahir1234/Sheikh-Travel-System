import { Component, Input } from '@angular/core';
import { NgClass } from '@angular/common';
import { APP_LOGIN_LOGO_PATH, APP_LOGO_PATH } from '../../../core/constants/app-brand';

/**
 * Shared SheikhGo mark.
 * - Login (`login-hero` / `login-card`): APP_LOGIN_LOGO_PATH only
 * - Elsewhere: APP_LOGO_PATH (`/brand/sheikhgo-logo.png`)
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
  /**
   * `login-hero` — left brand on /auth/login (96px)
   * `login-card` — form card on /auth/login (116px)
   * `header` / `footer` — site chrome
   */
  @Input() variant: 'login-hero' | 'login-card' | 'header' | 'footer' = 'header';

  /** Website chrome shows wordmark; login hero keeps its own title block. */
  @Input() showWordmark = false;

  @Input() wordmark = 'SheikhGo';

  @Input() alt = 'SheikhGo';

  get src(): string {
    return this.variant === 'login-hero' || this.variant === 'login-card'
      ? APP_LOGIN_LOGO_PATH
      : APP_LOGO_PATH;
  }
}
