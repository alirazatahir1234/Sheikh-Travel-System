import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { WEBSITE_BRAND } from './brand';

/** Canonical marketing origin for Open Graph URLs (production site). */
export const WEBSITE_PUBLIC_ORIGIN = 'https://www.sheikhgo.com';

@Injectable({ providedIn: 'root' })
export class WebsiteSeoService {
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly document = inject(DOCUMENT);

  /** Object form used by newer marketing pages (accident-risk, etc.). */
  setPage(opts: { title: string; description: string; path?: string }): void {
    this.set(opts.title, opts.description, opts.path);
  }

  set(pageTitle: string, description: string, path?: string): void {
    // If caller already supplies a full "Brand | Topic" title, use it as-is.
    const full = pageTitle.includes('|')
      ? pageTitle
      : `${pageTitle} | ${WEBSITE_BRAND.companyName}`;
    const pagePath = path?.startsWith('/') ? path : path ? `/${path}` : this.document.location?.pathname ?? '/';
    const pageUrl = `${WEBSITE_PUBLIC_ORIGIN}${pagePath}`;

    this.title.setTitle(full);
    this.upsertMeta({ name: 'description' }, { name: 'description', content: description });
    this.upsertMeta({ property: 'og:title' }, { property: 'og:title', content: full });
    this.upsertMeta({ property: 'og:description' }, { property: 'og:description', content: description });
    this.upsertMeta({ property: 'og:type' }, { property: 'og:type', content: 'website' });
    this.upsertMeta({ property: 'og:site_name' }, { property: 'og:site_name', content: WEBSITE_BRAND.companyName });
    this.upsertMeta({ property: 'og:url' }, { property: 'og:url', content: pageUrl });
    this.upsertMeta({ name: 'robots' }, { name: 'robots', content: 'index,follow' });
    this.upsertMeta({ name: 'twitter:card' }, { name: 'twitter:card', content: 'summary_large_image' });
    this.upsertMeta({ name: 'twitter:title' }, { name: 'twitter:title', content: full });
    this.upsertMeta({ name: 'twitter:description' }, { name: 'twitter:description', content: description });
    const ogImage = `${WEBSITE_PUBLIC_ORIGIN}/brand/sheikhgo-logo.png`;
    this.upsertMeta({ property: 'og:image' }, { property: 'og:image', content: ogImage });
    this.setCanonical(pageUrl);
  }

  private setCanonical(pageUrl: string): void {
    let link = this.document.querySelector('link[rel="canonical"]') as HTMLLinkElement | null;
    if (!link) {
      link = this.document.createElement('link');
      link.setAttribute('rel', 'canonical');
      this.document.head.appendChild(link);
    }
    link.setAttribute('href', pageUrl);
  }

  private upsertMeta(
    selector: { name?: string; property?: string },
    tag: { name?: string; property?: string; content: string },
  ): void {
    const existing = selector.name
      ? this.meta.getTag(`name="${selector.name}"`)
      : selector.property
        ? this.meta.getTag(`property="${selector.property}"`)
        : null;
    if (existing) {
      this.meta.updateTag(tag);
    } else {
      this.meta.addTag(tag);
    }
  }
}
