import { environment } from '../../../../environments/environment';

/**
 * Marketing media via Cloudflare Worker → Azure Blob (`sheikhtravelblob`).
 *
 * Logical paths stay `/website/...` in source; this helper maps to:
 *   {websiteMediaBaseUrl}/website-images/{rel}
 *   {websiteMediaBaseUrl}/website-videos/{rel}
 *
 * Default base: `https://sheikhgo-media.sheikhgo.workers.dev` (same-path proxy).
 * Leave `environment.websiteMediaBaseUrl` empty to serve local `/website/`.
 */
export const WEBSITE_IMAGES_CONTAINER = 'website-images';
export const WEBSITE_VIDEOS_CONTAINER = 'website-videos';

const VIDEO_EXT = /\.(mp4|webm|mov)(?:$|\?)/i;

/** Absolute Blob/CDN URL for a marketing asset. Pass `/website/...` or `website/...`. */
export function websiteAssetUrl(logicalPath: string): string {
  const raw = (logicalPath || '').trim();
  if (!raw) return raw;
  if (/^https?:\/\//i.test(raw)) return raw;

  const [pathOnly, query = ''] = raw.split('?');
  const rel = pathOnly.replace(/^\//, '').replace(/^website\//, '');
  const q = query ? `?${query}` : '';

  const base = (environment.websiteMediaBaseUrl || '').replace(/\/$/, '');
  if (!base) {
    return `/website/${rel}${q}`;
  }

  const container = VIDEO_EXT.test(rel) ? WEBSITE_VIDEOS_CONTAINER : WEBSITE_IMAGES_CONTAINER;
  return `${base}/${container}/${rel}${q}`;
}

/** CSS `url('...')` wrapper for hero backgrounds / SCSS-driven style bindings. */
export function websiteCssUrl(logicalPath: string): string {
  const url = websiteAssetUrl(logicalPath).replace(/'/g, "\\'");
  return `url('${url}')`;
}
