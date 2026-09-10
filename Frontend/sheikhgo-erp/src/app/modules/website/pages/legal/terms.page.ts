import { Component, OnInit, inject } from '@angular/core';
import { WebsiteSeoService } from '../../core/seo.service';
import { WEBSITE_BRAND } from '../../core/brand';

@Component({
  selector: 'app-terms-page',
  standalone: true,
  imports: [],
  templateUrl: './terms.page.html',
  styleUrl: './terms.page.scss',
})
export class TermsPage implements OnInit {
  readonly brand = WEBSITE_BRAND;
  private readonly seo = inject(WebsiteSeoService);

  readonly version = '1.0';
  readonly lastUpdated = '10 September 2026';
  readonly websiteUrl = 'https://www.sheikhgo.com';

  ngOnInit(): void {
    this.seo.set(
      'Terms & Conditions',
      'Terms of use for SheikhGo Fleet and related services.',
      '/terms-and-conditions',
    );
  }
}
