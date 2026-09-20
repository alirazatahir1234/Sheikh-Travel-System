import { ElementRef, Component, HostListener, OnInit, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { NgClass } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { filter } from 'rxjs/operators';
import { WEBSITE_BRAND } from '../../core/brand';
import { INDUSTRY_ITEMS, SERVICE_ITEMS } from '../../core/site-catalog';
import { WebsitePublicContentService } from '../../core/website-public-content.service';
import { SgLogoComponent } from '../../../../shared/components/logo/sg-logo.component';

@Component({
  selector: 'app-site-header',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, NgClass, MatIconModule, SgLogoComponent],
  templateUrl: './site-header.component.html',
  styleUrl: './site-header.component.scss',
})
export class SiteHeaderComponent implements OnInit {
  readonly brand = WEBSITE_BRAND;
  private readonly content = inject(WebsitePublicContentService);
  private readonly router = inject(Router);
  private readonly host = inject(ElementRef<HTMLElement>);

  /** Chrome wordmark — SheikhGo Technologies */
  siteName: string = WEBSITE_BRAND.productName;

  readonly menuOpen = signal(false);
  readonly servicesOpen = signal(false);
  readonly industriesOpen = signal(false);
  scrolled = false;
  /** Always light/white sticky header on marketing pages. */
  readonly light = true;

  readonly serviceLinks = SERVICE_ITEMS;
  readonly industryLinks = INDUSTRY_ITEMS;

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

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeMenu();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.servicesOpen() && !this.industriesOpen()) return;
    if (this.host.nativeElement.contains(event.target as Node)) return;
    this.servicesOpen.set(false);
    this.industriesOpen.set(false);
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
