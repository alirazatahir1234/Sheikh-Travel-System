import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

export type AuthMode = 'cookie' | 'bearer';

export interface AppRuntimeConfig {
  apiUrl: string;
  authMode: AuthMode;
}

/**
 * Hosts under these suffixes belong to different owners, so two of them are never the
 * "same site" even though their last two labels match (public suffixes).
 */
const SHARED_SUFFIXES = [
  'vercel.app', 'sslip.io', 'nip.io', 'github.io', 'pages.dev', 'workers.dev',
  'azurewebsites.net', 'trycloudflare.com', 'railway.app', 'netlify.app'
];

/** The site a host belongs to: its last two labels, or the whole host when that cannot be trusted. */
function siteOf(hostname: string): string {
  const host = hostname.toLowerCase();
  const isIp = /^[\d.]+$/.test(host) || host.includes(':');
  if (isIp || !host.includes('.')) return host;
  if (SHARED_SUFFIXES.some(suffix => host === suffix || host.endsWith(`.${suffix}`))) return host;
  return host.split('.').slice(-2).join('.');
}

/**
 * True when the browser will send SameSite cookies from the page to the API:
 * a relative API URL, or the same scheme and the same site (www.x.com → api.x.com).
 */
export function isApiSameSite(apiUrl: string, pageOrigin: string): boolean {
  try {
    const page = new URL(pageOrigin);
    const api = new URL(apiUrl, pageOrigin);
    return api.protocol === page.protocol && siteOf(api.hostname) === siteOf(page.hostname);
  } catch {
    return false;
  }
}

/**
 * Runtime configuration, read once at startup from assets/config.json (APP_INITIALIZER).
 * The API host lives in that file, not in the JavaScript bundle (SG-09), so moving the
 * API is a config change rather than a rebuild.
 *
 * Existing code reads `environment.apiUrl`; this service overwrites that value before any
 * other service is created, so those readers need no change.
 */
@Injectable({ providedIn: 'root' })
export class AppConfigService {
  private config: AppRuntimeConfig = { apiUrl: environment.apiUrl, authMode: 'bearer' };

  get apiUrl(): string {
    return this.config.apiUrl;
  }

  get authMode(): AuthMode {
    return this.config.authMode;
  }

  async load(fetchFn: typeof fetch = (input, init) => fetch(input, init)): Promise<void> {
    try {
      const response = await fetchFn('assets/config.json', { cache: 'no-store' });
      if (!response.ok) return;
      this.apply(await response.json());
    } catch {
      /* No config file (or unreadable): keep the build default, same-origin /api. */
    }
  }

  apply(raw: Partial<AppRuntimeConfig> | null | undefined, pageOrigin: string = window.location.origin): void {
    const configured = typeof raw?.apiUrl === 'string' ? raw.apiUrl.trim().replace(/\/+$/, '') : '';
    const apiUrl = configured || environment.apiUrl;
    // Cookie sessions need the API on the same site as the page. If it is not, the browser would
    // drop the cookies and nobody could sign in — so fall back to bearer instead.
    const authMode: AuthMode = raw?.authMode === 'cookie' && isApiSameSite(apiUrl, pageOrigin) ? 'cookie' : 'bearer';

    this.config = { apiUrl, authMode };
    environment.apiUrl = apiUrl;
  }
}
