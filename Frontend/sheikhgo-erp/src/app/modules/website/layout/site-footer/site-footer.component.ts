import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { WEBSITE_BRAND } from '../../core/brand';
import { PublicWebsiteSettings, WebsitePublicContentService } from '../../core/website-public-content.service';
import { SgLogoComponent } from '../../../../shared/components/logo/sg-logo.component';

@Component({
  selector: 'app-site-footer',
  standalone: true,
  imports: [RouterLink, SgLogoComponent],
  templateUrl: './site-footer.component.html',
  styleUrl: './site-footer.component.scss',
})
export class SiteFooterComponent implements OnInit {
  readonly brand = WEBSITE_BRAND;
  private readonly content = inject(WebsitePublicContentService);

  salesEmail: string = WEBSITE_BRAND.salesEmail;
  supportEmail: string = WEBSITE_BRAND.supportEmail;
  siteName: string = WEBSITE_BRAND.productName;

  /** Single contact line — avoid listing the same address twice. */
  get contactEmail(): string {
    return this.salesEmail || this.supportEmail || WEBSITE_BRAND.salesEmail;
  }

  ngOnInit(): void {
    this.content.getSettings().subscribe((s: PublicWebsiteSettings | null) => {
      if (!s) return;
      this.salesEmail = s.salesEmail || this.salesEmail;
      this.supportEmail = s.supportEmail || this.supportEmail;
      this.siteName = s.siteName || this.siteName;
    });
  }
}
