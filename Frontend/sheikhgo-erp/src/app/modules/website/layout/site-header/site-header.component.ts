import { Component, HostListener, OnInit, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { NgClass } from '@angular/common';
import { WEBSITE_BRAND } from '../../core/brand';
import { WebsitePublicContentService } from '../../core/website-public-content.service';
import { SgLogoComponent } from '../../../../shared/components/logo/sg-logo.component';

@Component({
  selector: 'app-site-header',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, NgClass, SgLogoComponent],
  templateUrl: './site-header.component.html',
  styleUrl: './site-header.component.scss',
})
export class SiteHeaderComponent implements OnInit {
  readonly brand = WEBSITE_BRAND;
  private readonly content = inject(WebsitePublicContentService);

  siteName: string = WEBSITE_BRAND.productName;

  readonly menuOpen = signal(false);
  readonly platformOpen = signal(false);
  readonly solutionsOpen = signal(false);
  scrolled = false;

  ngOnInit(): void {
    this.content.getSettings().subscribe(s => {
      if (!s) return;
      if (s.siteName) this.siteName = s.siteName;
      // Logo asset is locked to APP_LOGO_PATH via app-sg-logo — ignore CMS logoUrl.
    });
  }

  @HostListener('window:scroll')
  onScroll(): void {
    this.scrolled = window.scrollY > 12;
  }

  toggleMenu(): void {
    this.menuOpen.update(v => !v);
  }

  closeMenu(): void {
    this.menuOpen.set(false);
    this.platformOpen.set(false);
    this.solutionsOpen.set(false);
  }

  togglePlatform(): void {
    this.platformOpen.update(v => !v);
    this.solutionsOpen.set(false);
  }

  toggleSolutions(): void {
    this.solutionsOpen.update(v => !v);
    this.platformOpen.set(false);
  }
}
