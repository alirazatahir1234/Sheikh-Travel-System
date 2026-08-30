import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { WEBSITE_BRAND } from './brand';

@Injectable({ providedIn: 'root' })
export class WebsiteSeoService {
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);

  set(pageTitle: string, description: string, _path?: string): void {
    const full = `${pageTitle} | ${WEBSITE_BRAND.productName}`;
    this.title.setTitle(full);
    this.meta.updateTag({ name: 'description', content: description });
    this.meta.updateTag({ property: 'og:title', content: full });
    this.meta.updateTag({ property: 'og:description', content: description });
    this.meta.updateTag({ property: 'og:type', content: 'website' });
    this.meta.updateTag({ property: 'og:site_name', content: WEBSITE_BRAND.productName });
    this.meta.updateTag({ name: 'robots', content: 'index,follow' });
  }
}
