import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';
import { environment } from '../../../environments/environment';

const GOOGLE_AUTH_PHRASE = "This page can't load Google Maps correctly";

/**
 * Turns Google's auth-failure documentation link into an operator-facing reason.
 * The native popup is removed separately; this text is what the form shows.
 */
export function mapsAuthMessageFromDocsUrl(url: string | null): string {
  const lead = 'Address search and routing are temporarily unavailable.';
  if (!url) {
    return (
      `${lead} Google Maps could not be initialized. ` +
      'Enable Cloud billing and the Maps JavaScript API, Places API, and Directions API for this key, then retry.'
    );
  }

  const normalized = url.toLowerCase();
  if (normalized.includes('billing')) {
    return `${lead} Google Cloud billing is not enabled for this Maps API key.`;
  }
  if (normalized.includes('referer')) {
    return `${lead} This site is blocked by the API key HTTP referrer restrictions.`;
  }
  if (normalized.includes('invalid-key') || normalized.includes('missing-key')) {
    return `${lead} The Google Maps API key is missing or invalid.`;
  }
  if (
    normalized.includes('api-not-activated') ||
    normalized.includes('api-target-blocked') ||
    normalized.includes('not-activated')
  ) {
    return `${lead} Maps JavaScript API, Places API, or Directions API is not enabled for this key.`;
  }
  return `${lead} Google Maps could not be initialized. Check the Maps configuration and retry.`;
}

type MapsWindow = Window & {
  gm_authFailure?: () => void;
  google?: { maps?: Record<string, unknown> };
};

/**
 * Single bootstrap for the Google Maps JavaScript API.
 * `@angular/google-maps` calls `google.maps.importLibrary` and must reuse this loader.
 */
@Injectable({ providedIn: 'root' })
export class GoogleMapsLoaderService {
  private bootstrapPromise: Promise<boolean> | null = null;
  private readonly libraryCache = new Map<string, Promise<unknown>>();
  private authFailedFlag = false;
  private authDecided = false;
  private authWaiters: Array<() => void> = [];
  private dialogObserver: MutationObserver | null = null;
  private dismissingDialogs = false;
  private docsUrl: string | null = null;
  private readonly authFailureSubject = new Subject<string>();

  /** Emits when Google rejects the key, billing, or required APIs. */
  readonly authFailures$ = this.authFailureSubject.asObservable();

  failureMessage: string | null = null;

  get authFailed(): boolean {
    return this.authFailedFlag;
  }

  /** Ensures the bootstrap loader has registered `google.maps.importLibrary`. */
  load(): Promise<boolean> {
    if (this.bootstrapPromise) return this.bootstrapPromise;

    const key = environment.googleMapsApiKey;
    if (!key) {
      this.failureMessage = 'Google Maps API key is not configured.';
      this.bootstrapPromise = Promise.resolve(false);
      return this.bootstrapPromise;
    }

    this.bootstrapPromise = new Promise<boolean>(resolve => {
      try {
        this.installBootstrap({ key, v: 'weekly' });
        resolve(true);
      } catch {
        this.failureMessage = mapsAuthMessageFromDocsUrl(null);
        resolve(false);
      }
    });
    return this.bootstrapPromise;
  }

  /**
   * Loads one Maps library through the shared bootstrap.
   * Rejects when Google reports an authentication/configuration failure
   * so callers do not construct extra Places or Directions widgets.
   */
  async importLibrary<T = unknown>(name: string): Promise<T> {
    const loaded = await this.load();
    if (!loaded) {
      throw new Error(this.failureMessage || 'Google Maps API key is not configured.');
    }

    let pending = this.libraryCache.get(name);
    if (!pending) {
      pending = (
        google.maps as unknown as { importLibrary: (n: string) => Promise<unknown> }
      ).importLibrary(name);
      this.libraryCache.set(name, pending);
    }

    const lib = await pending;
    await this.waitForAuthDecision();
    if (this.authFailedFlag) {
      throw new Error(this.failureMessage || mapsAuthMessageFromDocsUrl(this.docsUrl));
    }
    return lib as T;
  }

  /**
   * Drops the failed script and loads once more.
   * If Google left a non-deletable `google.maps` global, reloads the page.
   */
  async retry(): Promise<boolean> {
    this.dialogObserver?.disconnect();
    this.dialogObserver = null;
    document
      .querySelectorAll('script[src*="maps.googleapis.com/maps/api/js"]')
      .forEach(node => node.remove());

    const w = window as MapsWindow;
    let cleared = !w.google?.maps;
    if (w.google?.maps) {
      try {
        delete w.google.maps;
        cleared = !w.google.maps;
      } catch {
        cleared = false;
      }
    }

    if (!cleared) {
      window.location.reload();
      return false;
    }

    this.bootstrapPromise = null;
    this.libraryCache.clear();
    this.authFailedFlag = false;
    this.authDecided = false;
    this.authWaiters = [];
    this.docsUrl = null;
    this.failureMessage = null;

    try {
      await this.importLibrary('maps');
      return true;
    } catch {
      return false;
    }
  }

  get isConfigured(): boolean {
    return !!environment.googleMapsApiKey;
  }

  private waitForAuthDecision(): Promise<void> {
    if (this.authDecided || this.authFailedFlag) {
      this.authDecided = true;
      return Promise.resolve();
    }

    return new Promise(resolve => {
      this.authWaiters.push(resolve);
      window.setTimeout(() => this.finishAuthWait(), 500);
    });
  }

  private finishAuthWait(): void {
    if (this.authDecided) return;
    this.authDecided = true;
    const waiters = this.authWaiters;
    this.authWaiters = [];
    waiters.forEach(resolve => resolve());
  }

  private noteAuthFailure(docsUrl: string | null): void {
    if (docsUrl) this.docsUrl = docsUrl;
    const alreadyFailed = this.authFailedFlag && this.failureMessage === mapsAuthMessageFromDocsUrl(this.docsUrl);
    this.authFailedFlag = true;
    this.failureMessage = mapsAuthMessageFromDocsUrl(this.docsUrl);
    this.finishAuthWait();
    if (!alreadyFailed && this.failureMessage) {
      this.authFailureSubject.next(this.failureMessage);
    }
    this.observeAndDismissDialogs();
  }

  private installBootstrap(g: Record<string, string>): void {
    const w = window as MapsWindow;
    w.google = w.google || {};
    const maps = (w.google.maps = w.google.maps || {}) as Record<string, unknown>;
    this.installAuthHook();
    if (typeof maps['importLibrary'] === 'function') return;

    const requested = new Set<string>();
    let fetchPromise: Promise<void> | null = null;

    const runFetch = (): Promise<void> => {
      if (fetchPromise) return fetchPromise;
      fetchPromise = new Promise<void>((resolve, reject) => {
        const params = new URLSearchParams();
        params.set('libraries', Array.from(requested).join(','));
        Object.keys(g).forEach(k => {
          const snake = k.replace(/[A-Z]/g, c => '_' + c.toLowerCase());
          params.set(snake, g[k]);
        });
        params.set('callback', 'google.maps.__ib__');
        const script = document.createElement('script');
        script.id = 'sheikhgo-google-maps';
        script.src = `https://maps.googleapis.com/maps/api/js?${params.toString()}`;
        script.async = true;
        script.onerror = () => {
          this.failureMessage = 'Google Maps JavaScript API could not load.';
          this.authFailedFlag = true;
          this.finishAuthWait();
          reject(new Error(this.failureMessage));
        };
        maps['__ib__'] = () => {
          this.observeAndDismissDialogs();
          resolve();
        };
        document.head.appendChild(script);
      });
      return fetchPromise;
    };

    maps['importLibrary'] = (name: string, ...rest: unknown[]) => {
      requested.add(name);
      return runFetch().then(() =>
        (maps['importLibrary'] as (n: string, ...r: unknown[]) => Promise<unknown>)(name, ...rest)
      );
    };
  }

  private installAuthHook(): void {
    const w = window as MapsWindow;
    const previous = w.gm_authFailure;
    if (previous && (previous as { __sheikhGo?: boolean }).__sheikhGo) return;

    const hook = () => {
      this.noteAuthFailure(null);
      previous?.();
    };
    (hook as { __sheikhGo?: boolean }).__sheikhGo = true;
    w.gm_authFailure = hook;
  }

  /**
   * Google injects one "can't load Google Maps correctly" dialog per widget.
   * The form shows the same failure once. The dialog is removed only after its
   * documentation link is read, so the underlying configuration error stays visible.
   */
  private observeAndDismissDialogs(): void {
    this.dismissNativeGoogleAuthDialogs();
    if (this.dialogObserver || typeof MutationObserver === 'undefined') return;

    this.dialogObserver = new MutationObserver(() => this.dismissNativeGoogleAuthDialogs());
    this.dialogObserver.observe(document.body, { childList: true, subtree: true });
    window.setTimeout(() => {
      this.dialogObserver?.disconnect();
      this.dialogObserver = null;
      this.dismissNativeGoogleAuthDialogs();
    }, 4000);
  }

  private dismissNativeGoogleAuthDialogs(): void {
    if (this.dismissingDialogs) return;
    this.dismissingDialogs = true;
    try {
      const hits: HTMLElement[] = [];
      document.querySelectorAll('div').forEach(node => {
        if (!(node instanceof HTMLElement)) return;
        const text = node.innerText || '';
        if (!text.includes(GOOGLE_AUTH_PHRASE) || text.length > 700) return;
        hits.push(node);
      });

      hits.sort((a, b) => (b.innerText || '').length - (a.innerText || '').length);
      let docsUrl: string | null = null;
      for (const el of hits) {
        if (!el.isConnected) continue;
        const link = el.querySelector('a[href*="developers.google.com"]');
        if (link instanceof HTMLAnchorElement) docsUrl = link.href;
        el.remove();
      }

      document.querySelectorAll('.gm-err-container, .gm-err-autocomplete').forEach(node => node.remove());
      if (hits.length) this.noteAuthFailure(docsUrl);
    } finally {
      this.dismissingDialogs = false;
    }
  }
}
