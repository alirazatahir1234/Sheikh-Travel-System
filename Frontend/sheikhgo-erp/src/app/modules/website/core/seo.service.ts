import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { WEBSITE_BRAND } from './brand';

const SITE_ORIGIN = 'https://www.sheikhgo.com';
const DEFAULT_OG_IMAGE = `${SITE_ORIGIN}/brand/og-share.png`;

@Injectable({ providedIn: 'root' })
export class WebsiteSeoService {
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly doc = inject(DOCUMENT);

  set(pageTitle: string, description: string, path?: string): void {
    // If caller already supplies a full "Brand | Topic" title, use it as-is.
    const full = pageTitle.includes('|')
      ? pageTitle
      : `${pageTitle} | ${WEBSITE_BRAND.companyName}`;
    this.title.setTitle(full);
    this.meta.updateTag({ name: 'description', content: description });
    this.meta.updateTag({ property: 'og:title', content: full });
    this.meta.updateTag({ property: 'og:description', content: description });
    this.meta.updateTag({ property: 'og:type', content: 'website' });
    this.meta.updateTag({ property: 'og:site_name', content: WEBSITE_BRAND.companyName });
    this.meta.updateTag({ property: 'og:image', content: DEFAULT_OG_IMAGE });
    this.meta.updateTag({ property: 'og:image:width', content: '1200' });
    this.meta.updateTag({ property: 'og:image:height', content: '630' });
    this.meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this.meta.updateTag({ name: 'twitter:title', content: full });
    this.meta.updateTag({ name: 'twitter:description', content: description });
    this.meta.updateTag({ name: 'twitter:image', content: DEFAULT_OG_IMAGE });
    this.meta.updateTag({ name: 'robots', content: 'index,follow' });

    if (path != null) {
      const normalized = path.startsWith('/') ? path : `/${path}`;
      const url = `${SITE_ORIGIN}${normalized === '/' ? '/' : normalized}`;
      this.meta.updateTag({ property: 'og:url', content: url });
      this.setCanonical(url);
    }

    this.ensureOrganizationJsonLd();
  }

  private setCanonical(url: string): void {
    let link = this.doc.querySelector<HTMLLinkElement>('link[rel="canonical"]');
    if (!link) {
      link = this.doc.createElement('link');
      link.setAttribute('rel', 'canonical');
      this.doc.head.appendChild(link);
    }
    link.setAttribute('href', url);
  }

  private ensureOrganizationJsonLd(): void {
    const id = 'sg-org-jsonld';
    if (this.doc.getElementById(id)) return;

    const script = this.doc.createElement('script');
    script.id = id;
    script.type = 'application/ld+json';
    script.text = JSON.stringify({
      '@context': 'https://schema.org',
      '@graph': [
        {
          '@type': 'Organization',
          name: WEBSITE_BRAND.companyName,
          url: SITE_ORIGIN,
          logo: `${SITE_ORIGIN}/brand/sheikhgo-logo.png`,
          email: WEBSITE_BRAND.salesEmail,
          description: WEBSITE_BRAND.footerBlurb,
        },
        {
          '@type': 'SoftwareApplication',
          name: 'SheikhGo Fleet',
          applicationCategory: 'BusinessApplication',
          operatingSystem: 'Web, Android, iOS',
          url: `${SITE_ORIGIN}/fleet-management`,
          description:
            'Fleet management platform with live GPS, vehicles, drivers, trips and operational reporting.',
        },
      ],
    });
    this.doc.head.appendChild(script);
  }
}
