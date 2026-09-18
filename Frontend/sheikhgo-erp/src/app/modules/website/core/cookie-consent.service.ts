import { Injectable, signal } from '@angular/core';

export type CookieCategory = 'necessary' | 'analytics' | 'functional' | 'marketing';

export interface CookieConsentState {
  necessary: true;
  analytics: boolean;
  functional: boolean;
  marketing: boolean;
  decidedAt: string | null;
}

const STORAGE_KEY = 'sg_cookie_consent_v1';

const DEFAULT_STATE: CookieConsentState = {
  necessary: true,
  analytics: false,
  functional: false,
  marketing: false,
  decidedAt: null,
};

@Injectable({ providedIn: 'root' })
export class CookieConsentService {
  readonly consent = signal<CookieConsentState>(this.read());
  readonly bannerVisible = signal(!this.consent().decidedAt);
  readonly preferencesOpen = signal(false);

  hasDecided(): boolean {
    return !!this.consent().decidedAt;
  }

  allows(category: Exclude<CookieCategory, 'necessary'>): boolean {
    return this.consent()[category] === true;
  }

  acceptAll(): void {
    this.persist({
      necessary: true,
      analytics: true,
      functional: true,
      marketing: true,
      decidedAt: new Date().toISOString(),
    });
    this.bannerVisible.set(false);
    this.preferencesOpen.set(false);
  }

  rejectNonEssential(): void {
    this.persist({
      necessary: true,
      analytics: false,
      functional: false,
      marketing: false,
      decidedAt: new Date().toISOString(),
    });
    this.bannerVisible.set(false);
    this.preferencesOpen.set(false);
  }

  savePreferences(partial: Partial<Pick<CookieConsentState, 'analytics' | 'functional' | 'marketing'>>): void {
    this.persist({
      necessary: true,
      analytics: !!partial.analytics,
      functional: !!partial.functional,
      marketing: !!partial.marketing,
      decidedAt: new Date().toISOString(),
    });
    this.bannerVisible.set(false);
    this.preferencesOpen.set(false);
  }

  openPreferences(): void {
    this.preferencesOpen.set(true);
  }

  closePreferences(): void {
    this.preferencesOpen.set(false);
  }

  openCookieSettings(): void {
    this.bannerVisible.set(false);
    this.preferencesOpen.set(true);
  }

  /** Apply scripts only after consent — call from app bootstrap / shell. */
  applyConsentScripts(): void {
    const c = this.consent();
    if (!c.decidedAt) return;

    // Hook for future analytics / marketing loaders.
    // Do not load non-essential scripts until the matching flag is true.
    if (c.analytics) {
      // e.g. loadGoogleAnalytics();
    }
    if (c.marketing) {
      // e.g. loadMarketingPixels();
    }
  }

  private persist(state: CookieConsentState): void {
    this.consent.set(state);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
    } catch {
      /* private mode / quota */
    }
    this.applyConsentScripts();
  }

  private read(): CookieConsentState {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return { ...DEFAULT_STATE };
      const parsed = JSON.parse(raw) as Partial<CookieConsentState>;
      return {
        necessary: true,
        analytics: !!parsed.analytics,
        functional: !!parsed.functional,
        marketing: !!parsed.marketing,
        decidedAt: typeof parsed.decidedAt === 'string' ? parsed.decidedAt : null,
      };
    } catch {
      return { ...DEFAULT_STATE };
    }
  }
}
