import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';
import { WEBSITE_MESSAGES } from './i18n/messages';
import {
  DEFAULT_WEBSITE_LANGUAGE,
  WEBSITE_LANGUAGE_STORAGE_KEY,
  WebsiteLanguage,
  WebsiteMessageDictionary,
} from './i18n/website-language';

@Injectable({ providedIn: 'root' })
export class WebsiteI18nService {
  private readonly document = inject(DOCUMENT);

  /** Current UI language — read in templates/computed for reactive copy. */
  readonly lang = signal<WebsiteLanguage>(DEFAULT_WEBSITE_LANGUAGE);

  constructor() {
    this.lang.set(this.readStoredLanguage());
    this.applyDocumentLanguage(this.lang());
  }

  t(key: string, fallback?: string): string {
    this.lang();
    const dict = WEBSITE_MESSAGES[this.lang()];
    return dict[key] ?? WEBSITE_MESSAGES.en[key] ?? fallback ?? key;
  }

  setLanguage(code: WebsiteLanguage): void {
    if (this.lang() === code) return;
    this.lang.set(code);
    try {
      localStorage.setItem(WEBSITE_LANGUAGE_STORAGE_KEY, code);
    } catch {
      /* private mode / blocked storage */
    }
    this.applyDocumentLanguage(code);
  }

  catalogField(slug: string, field: 'title' | 'text'): string {
    return this.t(`catalog.${slug}.${field}`);
  }

  industryField(i18nKey: string, field: 'title' | 'text'): string {
    return this.t(`industry.${i18nKey}.${field}`);
  }

  private readStoredLanguage(): WebsiteLanguage {
    try {
      const raw = localStorage.getItem(WEBSITE_LANGUAGE_STORAGE_KEY);
      if (raw === 'en' || raw === 'ar' || raw === 'de') return raw;
    } catch {
      /* ignore */
    }
    return DEFAULT_WEBSITE_LANGUAGE;
  }

  private applyDocumentLanguage(code: WebsiteLanguage): void {
    const root = this.document.documentElement;
    root.lang = code === 'ar' ? 'ar' : code === 'de' ? 'de' : 'en';
    root.dir = code === 'ar' ? 'rtl' : 'ltr';
  }
}

export type { WebsiteLanguage, WebsiteMessageDictionary };
