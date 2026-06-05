import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

/**
 * Loader for the Google Maps JavaScript API using the official
 * "Dynamic Library Import" bootstrap loader pattern.
 *
 * This registers `google.maps.importLibrary` synchronously so that
 * `@angular/google-maps` components (which call `importLibrary('maps')`
 * internally) work without extra setup. Individual libraries (`places`,
 * `geometry`, etc.) are fetched on first use via `importLibrary()`.
 *
 * Reference: https://developers.google.com/maps/documentation/javascript/load-maps-js-api#dynamic-library-import
 */
@Injectable({ providedIn: 'root' })
export class GoogleMapsLoaderService {
  private bootstrapPromise: Promise<boolean> | null = null;
  private readonly libraryCache = new Map<string, Promise<unknown>>();

  /** Ensures the bootstrap loader has registered `google.maps.importLibrary`. */
  load(): Promise<boolean> {
    if (this.bootstrapPromise) return this.bootstrapPromise;

    const key = environment.googleMapsApiKey;
    if (!key) {
      this.bootstrapPromise = Promise.resolve(false);
      return this.bootstrapPromise;
    }

    this.bootstrapPromise = new Promise<boolean>((resolve) => {
      try {
        this.installBootstrap({ key, v: 'weekly' });
        // importLibrary is now defined synchronously; actual SDK fetch is deferred.
        resolve(true);
      } catch {
        resolve(false);
      }
    });
    return this.bootstrapPromise;
  }

  /**
   * Lazily loads a specific Maps library (e.g. 'maps', 'places', 'geometry').
   * Results are cached so repeated calls are free.
   */
  async importLibrary<T = unknown>(name: string): Promise<T> {
    await this.load();
    let pending = this.libraryCache.get(name);
    if (!pending) {
      pending = (google.maps as unknown as {
        importLibrary: (n: string) => Promise<unknown>;
      }).importLibrary(name);
      this.libraryCache.set(name, pending);
    }
    return pending as Promise<T>;
  }

  get isConfigured(): boolean {
    return !!environment.googleMapsApiKey;
  }

  /**
   * Registers `google.maps.importLibrary` and queues requested libraries.
   * Adapted from Google's official bootstrap loader snippet.
   */
  private installBootstrap(g: Record<string, string>): void {
    const w = window as unknown as { google?: { maps?: Record<string, unknown> } };
    w.google = w.google || {};
    const maps = (w.google.maps = w.google.maps || {}) as Record<string, unknown>;
    if (typeof maps['importLibrary'] === 'function') return; // already installed

    const requested = new Set<string>();
    let fetchPromise: Promise<void> | null = null;

    const runFetch = (): Promise<void> => {
      if (fetchPromise) return fetchPromise;
      fetchPromise = new Promise<void>((resolve, reject) => {
        const params = new URLSearchParams();
        params.set('libraries', Array.from(requested).join(','));
        Object.keys(g).forEach((k) => {
          const snake = k.replace(/[A-Z]/g, (c) => '_' + c.toLowerCase());
          params.set(snake, g[k]);
        });
        params.set('callback', 'google.maps.__ib__');
        const script = document.createElement('script');
        script.src = `https://maps.googleapis.com/maps/api/js?${params.toString()}`;
        script.async = true;
        script.onerror = () => reject(new Error('Google Maps JavaScript API could not load.'));
        (maps as Record<string, unknown>)['__ib__'] = resolve;
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
}
