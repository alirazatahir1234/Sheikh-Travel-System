import { Component, OnInit, inject } from '@angular/core';
import { SolutionLandingComponent } from '../../shared/solution-landing/solution-landing.component';
import { GPS_TRACKING_SOLUTION } from '../../core/channel-c-solutions';
import { WebsiteSeoService } from '../../core/seo.service';
import { WebsitePublicContentService } from '../../core/website-public-content.service';

@Component({
  selector: 'app-gps-page',
  standalone: true,
  imports: [SolutionLandingComponent],
  template: `
    <app-solution-landing
      [page]="page"
      [relatedLinks]="relatedLinks"
      ctaTitle="See live GPS on your fleet?"
      ctaLead="Book a demo of live tracking, geofences, and alerts — or request a fleet assessment of device coverage."
    />
  `,
})
export class GpsPage implements OnInit {
  private readonly seo = inject(WebsiteSeoService);
  private readonly content = inject(WebsitePublicContentService);

  readonly page = { ...GPS_TRACKING_SOLUTION };
  readonly relatedLinks = [
    { label: 'Live Tracking →', url: '/live-tracking' },
    { label: 'GPS Alerts →', url: '/gps-alerts' },
    { label: 'Fleet Management →', url: '/fleet-management' },
  ];

  ngOnInit(): void {
    this.seo.set(this.page.seoTitle, this.page.seoDescription, this.page.path);

    this.content.getPage('gps-tracking').subscribe(cms => {
      if (!cms?.page) return;
      if (cms.page.title) this.page.title = cms.page.title;
      if (cms.page.description) this.page.lead = cms.page.description;
      if (cms.page.metaTitle || cms.page.metaDescription) {
        this.seo.set(
          cms.page.metaTitle || this.page.seoTitle,
          cms.page.metaDescription || this.page.seoDescription,
          this.page.path,
        );
      }
    });
  }
}
