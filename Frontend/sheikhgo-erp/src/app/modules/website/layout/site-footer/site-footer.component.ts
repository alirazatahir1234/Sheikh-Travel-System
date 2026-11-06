import { Component, OnInit, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import {
  WEBSITE_BRAND,
  WEBSITE_CONTACT_PHONES,
  WEBSITE_OFFICE_ADDRESS,
  WEBSITE_OFFICE_LOCATION_LABEL,
  WEBSITE_OFFICE_MAPS_URL,
  WebsiteContactPhone,
  resolveWebsiteOfficeAddress,
} from '../../core/brand';
import { PublicWebsiteSettings, WebsitePublicContentService } from '../../core/website-public-content.service';
import { CookieConsentService } from '../../core/cookie-consent.service';
import { SERVICE_ITEMS } from '../../core/site-catalog';
import { WebsiteI18nService } from '../../core/website-i18n.service';
import { WebsiteTranslatePipe } from '../../shared/website-translate.pipe';
import { SgLogoComponent } from '../../../../shared/components/logo/sg-logo.component';

@Component({
  selector: 'app-site-footer',
  standalone: true,
  imports: [RouterLink, MatIconModule, SgLogoComponent, WebsiteTranslatePipe],
  templateUrl: './site-footer.component.html',
  styleUrl: './site-footer.component.scss',
})
export class SiteFooterComponent implements OnInit {
  readonly brand = WEBSITE_BRAND;
  readonly i18n = inject(WebsiteI18nService);
  readonly serviceLinks = SERVICE_ITEMS;
  private readonly content = inject(WebsitePublicContentService);
  private readonly cookies = inject(CookieConsentService);

  salesEmail: string = WEBSITE_BRAND.salesEmail;
  supportEmail: string = WEBSITE_BRAND.supportEmail;
  siteName: string = WEBSITE_BRAND.productName;
  readonly contactPhones: readonly WebsiteContactPhone[] = WEBSITE_CONTACT_PHONES;
  officeAddress: string = WEBSITE_OFFICE_ADDRESS;
  readonly officeLocationLabel = WEBSITE_OFFICE_LOCATION_LABEL;
  readonly officeMapsUrl = WEBSITE_OFFICE_MAPS_URL;

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
      this.officeAddress = resolveWebsiteOfficeAddress(s.address);
    });
  }

  openCookieSettings(): void {
    this.cookies.openCookieSettings();
  }
}
