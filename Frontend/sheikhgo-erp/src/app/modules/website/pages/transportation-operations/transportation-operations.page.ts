import { Component, OnInit, inject } from '@angular/core';
import { SolutionLandingComponent } from '../../shared/solution-landing/solution-landing.component';
import { TRANSPORTATION_OPS_SOLUTION } from '../../core/channel-c-solutions';
import { WebsiteSeoService } from '../../core/seo.service';

@Component({
  selector: 'app-transportation-operations-page',
  standalone: true,
  imports: [SolutionLandingComponent],
  template: `
    <app-solution-landing
      [page]="page"
      [relatedLinks]="relatedLinks"
      ctaTitle="Run transport ops on Nexus"
      ctaLead="Book a demo of bookings, trips, drivers, and live GPS — or request a fleet assessment of your dispatch flow."
    />
  `,
})
export class TransportationOperationsPage implements OnInit {
  private readonly seo = inject(WebsiteSeoService);

  readonly page = TRANSPORTATION_OPS_SOLUTION;
  readonly relatedLinks = [
    { label: 'Fleet Management →', url: '/fleet-management' },
    { label: 'Live Tracking →', url: '/live-tracking' },
    { label: 'AI Fleet Insights →', url: '/ai-fleet-insights' },
  ];

  ngOnInit(): void {
    this.seo.set(this.page.seoTitle, this.page.seoDescription, this.page.path);
  }
}
