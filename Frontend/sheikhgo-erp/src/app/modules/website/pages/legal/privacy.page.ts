import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { WebsiteSeoService } from '../../core/seo.service';
import { WEBSITE_BRAND } from '../../core/brand';

@Component({
  selector: 'app-privacy-page',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './privacy.page.html',
  styleUrl: './privacy.page.scss',
})
export class PrivacyPage implements OnInit {
  readonly brand = WEBSITE_BRAND;
  private readonly seo = inject(WebsiteSeoService);

  readonly version = '1.0';
  readonly effectiveDate = '29 August 2026';
  readonly lastUpdated = '10 September 2026';
  readonly websiteUrl = 'https://www.sheikhgo.com';

  ngOnInit(): void {
    this.seo.set(
      'Privacy Policy',
      'How SheikhGo Fleet collects, uses, protects, and shares information — including GPS location data.',
      '/privacy-policy',
    );
  }
}
