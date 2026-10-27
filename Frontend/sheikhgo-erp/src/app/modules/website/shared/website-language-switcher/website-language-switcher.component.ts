import { Component, inject } from '@angular/core';
import { WebsiteI18nService } from '../../core/website-i18n.service';
import { WEBSITE_LANGUAGES, WebsiteLanguage } from '../../core/i18n/website-language';

@Component({
  selector: 'app-website-language-switcher',
  standalone: true,
  template: `
    <div class="lang-switch" [attr.aria-label]="i18n.t('lang.switchLabel')">
      @for (opt of options; track opt.code) {
        <button
          type="button"
          class="lang-btn"
          [class.active]="i18n.lang() === opt.code"
          [attr.aria-pressed]="i18n.lang() === opt.code"
          [attr.title]="convertLabel(opt.code)"
          (click)="pick(opt.code, $event)"
        >
          {{ opt.nativeLabel }}
        </button>
      }
    </div>
  `,
  styles: `
    :host {
      display: inline-flex;
      flex-shrink: 0;
      position: relative;
      z-index: 2;
      isolation: isolate;
    }

    .lang-switch {
      display: inline-flex;
      align-items: center;
      gap: 0.15rem;
      padding: 0.2rem;
      border-radius: 999px;
      background: rgba(15, 118, 110, 0.08);
      border: 1px solid rgba(15, 118, 110, 0.14);
    }
    .lang-btn {
      font: inherit;
      font-size: 0.6875rem;
      font-weight: 700;
      letter-spacing: 0.04em;
      line-height: 1;
      padding: 0.4rem 0.55rem;
      border: none;
      border-radius: 999px;
      cursor: pointer;
      color: var(--sg-ink, #022f32);
      background: transparent;
      transition: background 0.15s ease, color 0.15s ease;
    }
    .lang-btn:hover {
      background: rgba(15, 118, 110, 0.12);
    }
    .lang-btn.active {
      background: var(--sg-teal, #0f766e);
      color: #fff;
    }
    :host-context([dir='rtl']) .lang-switch {
      flex-direction: row-reverse;
    }
  `,
})
export class WebsiteLanguageSwitcherComponent {
  readonly i18n = inject(WebsiteI18nService);
  readonly options = WEBSITE_LANGUAGES;

  pick(code: WebsiteLanguage, event: MouseEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.i18n.setLanguage(code);
  }

  convertLabel(code: WebsiteLanguage): string {
    return this.i18n.t(`lang.convertTo.${code}`);
  }
}
