import { Component, OnInit, inject } from '@angular/core';
import { SolutionLandingComponent } from '../../shared/solution-landing/solution-landing.component';
import { FLEET_MANAGEMENT_SOLUTION } from '../../core/channel-c-solutions';
import { WebsiteSeoService } from '../../core/seo.service';
import { WebsitePublicContentService } from '../../core/website-public-content.service';

@Component({
  selector: 'app-fleet-page',
  standalone: true,
  imports: [SolutionLandingComponent],
  template: `
    <app-solution-landing
      [page]="page"
      [relatedLinks]="relatedLinks"
      ctaTitle="Ready to run your fleet on Nexus?"
      ctaLead="Book a demo of fleet management, GPS, and ops modules — or request a fleet assessment before you spend on ads."
    />
  `,
})
export class FleetPage implements OnInit {
  private readonly seo = inject(WebsiteSeoService);
  private readonly content = inject(WebsitePublicContentService);

  readonly page = { ...FLEET_MANAGEMENT_SOLUTION };
  readonly relatedLinks = [
    { label: 'GPS Vehicle Tracking →', url: '/gps-tracking' },
    { label: 'Live Tracking →', url: '/live-tracking' },
    { label: 'Platform Features →', url: '/features' },
  ];

  ngOnInit(): void {
    this.seo.set(this.page.seoTitle, this.page.seoDescription, this.page.path);

    this.content.getPage('fleet-management').subscribe(cms => {
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
