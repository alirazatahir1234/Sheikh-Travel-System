import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject } from '@angular/core';
import { environment } from '../../../../environments/environment';

declare global {
  interface Window {
    dataLayer?: unknown[];
    gtag?: (...args: unknown[]) => void;
  }
}

/** Loads GA4 gtag.js only when a measurement ID is configured and analytics consent is granted. */
@Injectable({ providedIn: 'root' })
export class WebsiteGoogleAnalyticsService {
  private readonly document = inject(DOCUMENT);
  private readonly platformId = inject(PLATFORM_ID);

  private measurementId = '';
  private loaded = false;

  /** Prefer CMS `analyticsId`; fall back to `environment.googleAnalyticsMeasurementId`. */
  configure(measurementIdFromCms?: string | null): void {
    const cms = (measurementIdFromCms ?? '').trim();
    const envId = (environment.googleAnalyticsMeasurementId ?? '').trim();
    this.measurementId = cms || envId;
  }

  /** Call after cookie consent allows analytics (or on shell init when consent already stored). */
  enableIfConfigured(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    if (!this.isValidMeasurementId(this.measurementId)) return;
    if (this.loaded) return;

    this.injectGtagScript();
    this.loaded = true;
    this.trackPageView(this.document.location?.pathname ?? '/');
  }

  trackPageView(path: string): void {
    if (!isPlatformBrowser(this.platformId) || !this.loaded || !window.gtag) return;
    if (!this.isValidMeasurementId(this.measurementId)) return;
    window.gtag('config', this.measurementId, { page_path: path });
  }

  /**
   * GA4 custom event (no-op when analytics is not loaded / consent denied).
   * Known website events:
   * `mobile_promo_video_open`, `mobile_app_store_click`,
   * `cta_book_demo_click`, `cta_fleet_assessment_click`,
   * `demo_request_submit`, `fleet_assessment_submit`.
   */
  trackEvent(eventName: string, params?: Record<string, string | number | boolean | null | undefined>): void {
    if (!isPlatformBrowser(this.platformId) || !this.loaded || !window.gtag) return;
    if (!this.isValidMeasurementId(this.measurementId)) return;
    const cleaned: Record<string, string | number | boolean> = {};
    if (params) {
      for (const [key, value] of Object.entries(params)) {
        if (value === null || value === undefined || value === '') continue;
        cleaned[key] = value;
      }
    }
    window.gtag('event', eventName, cleaned);
  }

  private isValidMeasurementId(id: string): boolean {
    return /^G-[A-Z0-9]+$/i.test(id.trim());
  }

  private injectGtagScript(): void {
    const id = this.measurementId;
    window.dataLayer = window.dataLayer ?? [];
    window.gtag = function gtag(...args: unknown[]) {
      window.dataLayer!.push(args);
    };
    window.gtag('js', new Date());
    window.gtag('config', id, { send_page_view: false });

    const script = this.document.createElement('script');
    script.async = true;
    script.src = `https://www.googletagmanager.com/gtag/js?id=${encodeURIComponent(id)}`;
    this.document.head.appendChild(script);
  }
}
