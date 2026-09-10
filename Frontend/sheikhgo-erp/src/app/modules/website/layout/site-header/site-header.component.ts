import { Component, HostListener, OnInit, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { NgClass } from '@angular/common';
import { filter } from 'rxjs/operators';
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
  private readonly router = inject(Router);

  /** Chrome wordmark — SheikhGo Technologies */
  siteName: string = WEBSITE_BRAND.productName;

  readonly menuOpen = signal(false);
  readonly servicesOpen = signal(false);
  readonly industriesOpen = signal(false);
  scrolled = false;
  /** Always light/white sticky header on marketing pages. */
  readonly light = true;

  readonly serviceLinks = [
    { label: 'Web Development', fragment: 'services' },
    { label: 'Mobile App Development', fragment: 'services' },
    { label: 'UI/UX Design', fragment: 'services' },
    { label: 'Cloud Solutions', fragment: 'services' },
    { label: 'IT Consulting', fragment: 'services' },
    { label: 'Custom Software', fragment: 'services' },
  ] as const;

  readonly industryLinks: ReadonlyArray<{ label: string; href: string; fragment: string | null }> = [
    { label: 'Logistics & Fleet', href: WEBSITE_BRAND.loginPath, fragment: null },
    { label: 'Travel & Tourism', href: '/', fragment: 'industries' },
    { label: 'Healthcare', href: '/', fragment: 'industries' },
    { label: 'Retail & E-Commerce', href: '/', fragment: 'industries' },
    { label: 'Finance', href: '/', fragment: 'industries' },
    { label: 'Education', href: '/', fragment: 'industries' },
    { label: 'Real Estate', href: '/', fragment: 'industries' },
    { label: 'Manufacturing', href: '/', fragment: 'industries' },
  ];

  ngOnInit(): void {
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe(() => this.closeMenu());

    this.content.getSettings().subscribe(s => {
      if (!s) return;
      if (s.siteName) this.siteName = s.siteName;
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
    this.servicesOpen.set(false);
    this.industriesOpen.set(false);
  }

  toggleServices(): void {
    this.servicesOpen.update(v => !v);
    this.industriesOpen.set(false);
  }

  toggleIndustries(): void {
    this.industriesOpen.update(v => !v);
    this.servicesOpen.set(false);
  }
}
