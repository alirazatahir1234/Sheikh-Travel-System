import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject } from '@angular/core';
import { environment } from '../../../../environments/environment';

/**
 * Optional Plausible Analytics loader.
 * Loads only when `environment.plausibleDomain` (or CMS override) is set and analytics consent is granted.
 * Never commit a live domain as a hard requirement — leave empty until configured.
 */
@Injectable({ providedIn: 'root' })
export class WebsitePlausibleAnalyticsService {
  private readonly document = inject(DOCUMENT);
  private readonly platformId = inject(PLATFORM_ID);

  private domain = '';
  private scriptSrc = '';
  private loaded = false;

  /** Prefer CMS override; fall back to environment placeholders. */
  configure(domainFromCms?: string | null, scriptSrcFromCms?: string | null): void {
    const cmsDomain = (domainFromCms ?? '').trim();
    const envDomain = (environment.plausibleDomain ?? '').trim();
    this.domain = cmsDomain || envDomain;

    const cmsSrc = (scriptSrcFromCms ?? '').trim();
    const envSrc = (environment.plausibleScriptSrc ?? '').trim();
    this.scriptSrc = cmsSrc || envSrc || 'https://plausible.io/js/script.js';
  }

  enableIfConfigured(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    if (!this.isValidDomain(this.domain)) return;
    if (this.loaded) return;

    this.injectScript();
    this.loaded = true;
  }

  trackEvent(eventName: string, props?: Record<string, string | number | boolean | null | undefined>): void {
    if (!isPlatformBrowser(this.platformId) || !this.loaded) return;
    if (!this.isValidDomain(this.domain)) return;

    const w = window as Window & {
      plausible?: (event: string, options?: { props?: Record<string, string | number | boolean> }) => void;
    };
    if (typeof w.plausible !== 'function') return;

    const cleaned: Record<string, string | number | boolean> = {};
    if (props) {
      for (const [key, value] of Object.entries(props)) {
        if (value === null || value === undefined || value === '') continue;
        cleaned[key] = value;
      }
    }
    w.plausible(eventName, Object.keys(cleaned).length ? { props: cleaned } : undefined);
  }

  private isValidDomain(domain: string): boolean {
    // Hostname only — no protocol, no path. Reject placeholders.
    const d = domain.trim().toLowerCase();
    if (!d || d.includes('://') || d.includes('/') || d.includes(' ')) return false;
    if (d === 'example.com' || d.startsWith('your-')) return false;
    return /^[a-z0-9.-]+\.[a-z]{2,}$/i.test(d);
  }

  private injectScript(): void {
    const existing = this.document.querySelector('script[data-sg-plausible="1"]');
    if (existing) return;

    const script = this.document.createElement('script');
    script.async = true;
    script.defer = true;
    script.src = this.scriptSrc;
    script.setAttribute('data-domain', this.domain);
    script.setAttribute('data-sg-plausible', '1');
    this.document.head.appendChild(script);
  }
}
