import { AfterViewInit, Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { CtaBandComponent } from '../../shared/cta-band.component';
import { WebsiteSeoService } from '../../core/seo.service';
import { WEBSITE_BRAND } from '../../core/brand';
import {
  NEXUS_DASHBOARD_IMAGE,
  NEXUS_MODULE_SHOTS,
  NEXUS_PORTFOLIO_ITEMS,
  NEXUS_VALUE_PILLARS,
} from '../../core/site-catalog';

@Component({
  selector: 'app-services-page',
  standalone: true,
  imports: [RouterLink, MatIconModule, CtaBandComponent],
  templateUrl: './services.page.html',
  styleUrl: './services.page.scss',
})
export class ServicesPage implements OnInit, AfterViewInit {
  private readonly seo = inject(WebsiteSeoService);
  private readonly route = inject(ActivatedRoute);

  readonly brand = WEBSITE_BRAND;
  readonly portfolio = NEXUS_PORTFOLIO_ITEMS;
  readonly valuePillars = NEXUS_VALUE_PILLARS;
  readonly moduleShots = NEXUS_MODULE_SHOTS;
  readonly dashboardImage = NEXUS_DASHBOARD_IMAGE;

  readonly pageTitle = 'SheikhGo Nexus';
  readonly pageLead =
    'Portfolio showcase of the flagship fleet intelligence and operations platform — modules, GPS depth, and SheikhGo AI in one client-ready product.';

  dashboardImageError = false;

  get moduleSection() {
    return this.portfolio.find(s => s.slug === 'platform-modules')!;
  }

  /** Alternate band backgrounds for readable rhythm. */
  detailAlt(slug: string): boolean {
    const order = ['fleet-intelligence', 'operations-modules'] as const;
    return (order as readonly string[]).includes(slug);
  }

  ngOnInit(): void {
    this.seo.set(
      this.pageTitle,
      'SheikhGo Nexus portfolio — fleet intelligence dashboard, GPS suite, operations modules, product UI gallery, and SheikhGo AI for transport teams.',
      '/services',
    );
  }

  ngAfterViewInit(): void {
    this.route.fragment.subscribe(fragment => {
      if (!fragment) return;
      setTimeout(() => document.getElementById(fragment)?.scrollIntoView({ behavior: 'smooth', block: 'start' }));
    });
  }
}
