import { Component, OnInit, inject } from '@angular/core';
import { SolutionLandingComponent } from '../../shared/solution-landing/solution-landing.component';
import { VEHICLE_RENTAL_SOLUTION } from '../../core/channel-c-solutions';
import { WebsiteSeoService } from '../../core/seo.service';

@Component({
  selector: 'app-vehicle-rental-page',
  standalone: true,
  imports: [SolutionLandingComponent],
  template: `
    <app-solution-landing
      [page]="page"
      [relatedLinks]="relatedLinks"
      ctaTitle="Rental ops on SheikhGo Nexus"
      ctaLead="Book a demo of fleet + GPS for rental today, and discuss the upcoming rental rates & bookings module."
    />
  `,
})
export class VehicleRentalPage implements OnInit {
  private readonly seo = inject(WebsiteSeoService);

  readonly page = VEHICLE_RENTAL_SOLUTION;
  readonly relatedLinks = [
    { label: 'Fleet Management →', url: '/fleet-management' },
    { label: 'GPS Vehicle Tracking →', url: '/gps-tracking' },
    { label: 'Transportation Operations →', url: '/transportation-operations' },
  ];

  ngOnInit(): void {
    this.seo.set(this.page.seoTitle, this.page.seoDescription, this.page.path);
  }
}
