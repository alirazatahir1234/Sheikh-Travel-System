import { Component, OnInit, inject } from '@angular/core';
import { SolutionLandingComponent } from '../../shared/solution-landing/solution-landing.component';
import { LOGISTICS_FLEET_SOLUTION } from '../../core/channel-c-solutions';
import { WebsiteSeoService } from '../../core/seo.service';

@Component({
  selector: 'app-logistics-fleet-page',
  standalone: true,
  imports: [SolutionLandingComponent],
  template: `
    <app-solution-landing
      [page]="page"
      [relatedLinks]="relatedLinks"
      ctaTitle="Run logistics on SheikhGo Nexus"
      ctaLead="Book a demo of fleet inventory, GPS, drivers, fuel, and maintenance — or request a fleet assessment of your logistics stack."
    />
  `,
})
export class LogisticsFleetPage implements OnInit {
  private readonly seo = inject(WebsiteSeoService);

  readonly page = LOGISTICS_FLEET_SOLUTION;
  readonly relatedLinks = [
    { label: 'Fleet Management →', url: '/fleet-management' },
    { label: 'GPS Vehicle Tracking →', url: '/gps-tracking' },
    { label: 'Transportation Operations →', url: '/transportation-operations' },
  ];

  ngOnInit(): void {
    this.seo.set(this.page.seoTitle, this.page.seoDescription, this.page.path);
  }
}
