import { Component, OnInit, inject } from '@angular/core';
import { SolutionLandingComponent } from '../../shared/solution-landing/solution-landing.component';
import { AI_FLEET_INSIGHTS_SOLUTION } from '../../core/channel-c-solutions';
import { WebsiteSeoService } from '../../core/seo.service';

@Component({
  selector: 'app-ai-fleet-insights-page',
  standalone: true,
  imports: [SolutionLandingComponent],
  template: `
    <app-solution-landing
      [page]="page"
      [relatedLinks]="relatedLinks"
      ctaTitle="See AI grounded in your fleet data"
      ctaLead="Book a demo of SheikhGo AI on Nexus — or request a fleet assessment of which insight workflows matter first."
    />
  `,
})
export class AiFleetInsightsPage implements OnInit {
  private readonly seo = inject(WebsiteSeoService);

  readonly page = AI_FLEET_INSIGHTS_SOLUTION;
  readonly relatedLinks = [
    { label: 'Full AI Solutions inventory →', url: '/ai-solutions' },
    { label: 'Fleet Management →', url: '/fleet-management' },
    { label: 'GPS Vehicle Tracking →', url: '/gps-tracking' },
  ];

  ngOnInit(): void {
    this.seo.set(this.page.seoTitle, this.page.seoDescription, this.page.path);
  }
}
