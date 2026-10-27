import { ElementRef, Component, HostListener, NgZone, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { DOCUMENT, NgClass } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { filter } from 'rxjs/operators';
import { WEBSITE_BRAND } from '../../core/brand';
import { INDUSTRY_ITEMS, SERVICE_ITEMS } from '../../core/site-catalog';
import { WebsitePublicContentService } from '../../core/website-public-content.service';
import { WebsiteI18nService } from '../../core/website-i18n.service';
import { WebsiteLanguageSwitcherComponent } from '../../shared/website-language-switcher/website-language-switcher.component';
import { WebsiteTranslatePipe } from '../../shared/website-translate.pipe';
import { SgLogoComponent } from '../../../../shared/components/logo/sg-logo.component';

@Component({
  selector: 'app-site-header',
  standalone: true,
  imports: [
    RouterLink,
    RouterLinkActive,
    NgClass,
    MatIconModule,
    SgLogoComponent,
    WebsiteLanguageSwitcherComponent,
    WebsiteTranslatePipe,
  ],
  templateUrl: './site-header.component.html',
  styleUrl: './site-header.component.scss',
})
export class SiteHeaderComponent implements OnInit, OnDestroy {
  readonly brand = WEBSITE_BRAND;
  readonly i18n = inject(WebsiteI18nService);
  private readonly content = inject(WebsitePublicContentService);
  private readonly router = inject(Router);
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly ngZone = inject(NgZone);
  private readonly doc = inject(DOCUMENT);

  /** Chrome wordmark — prefer short mark so nav never shades logo text */
  siteName: string = WEBSITE_BRAND.productName;

  /** Header lockup text — compact when CMS/product name is long */
  get chromeWordmark(): string {
    const name = (this.siteName || WEBSITE_BRAND.productName).trim();
    if (name.length > 14) return WEBSITE_BRAND.shortMark;
    return name || WEBSITE_BRAND.shortMark;
  }

  readonly menuOpen = signal(false);
  readonly servicesOpen = signal(false);
  readonly industriesOpen = signal(false);
  scrolled = false;
  /** Always light/white sticky header on marketing pages. */
  readonly light = true;

  readonly serviceLinks = SERVICE_ITEMS;
  readonly industryLinks = INDUSTRY_ITEMS;

  private scrollHandler?: () => void;

  ngOnInit(): void {
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe(() => this.closeMenu());

    this.content.getSettings().subscribe(s => {
      if (!s) return;
      if (s.siteName) this.siteName = s.siteName;
    });

    // Scroll must stay outside NgZone — HostListener('window:scroll') re-ran CD
    // every frame (impure wsTranslate pipe on the whole marketing shell → flicker).
    this.ngZone.runOutsideAngular(() => {
      this.scrollHandler = () => {
        const y =
          window.scrollY ||
          this.doc.documentElement.scrollTop ||
          this.doc.body.scrollTop ||
          0;
        const next = y > 12;
        if (this.scrolled === next) return;
        this.ngZone.run(() => {
          this.scrolled = next;
        });
      };
      window.addEventListener('scroll', this.scrollHandler, { passive: true });
    });
  }

  ngOnDestroy(): void {
    if (this.scrollHandler) {
      window.removeEventListener('scroll', this.scrollHandler);
      this.scrollHandler = undefined;
    }
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

  toggleServices(event: MouseEvent): void {
    event.stopPropagation();
    this.servicesOpen.update(v => !v);
    this.industriesOpen.set(false);
  }

  toggleIndustries(event: MouseEvent): void {
    event.stopPropagation();
    this.industriesOpen.update(v => !v);
    this.servicesOpen.set(false);
  }
}
