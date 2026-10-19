import { Component, OnInit, inject } from '@angular/core';
import { NgClass } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { WebsiteSeoService } from '../../core/seo.service';
import { WEBSITE_BRAND } from '../../core/brand';
import { WebsitePublicContentService } from '../../core/website-public-content.service';
import { INDUSTRY_ITEMS, SERVICE_ITEMS } from '../../core/site-catalog';

@Component({
  selector: 'app-home-page',
  standalone: true,
  imports: [RouterLink, NgClass, MatIconModule],
  templateUrl: './home.page.html',
  styleUrl: './home.page.scss',
})
export class HomePage implements OnInit {
  readonly brand = WEBSITE_BRAND;
  private readonly seo = inject(WebsiteSeoService);
  private readonly content = inject(WebsitePublicContentService);

  heroEyebrow: string = WEBSITE_BRAND.companyName;
  heroTitlePrimary = 'Run your fleet with';
  heroTitleAccent = 'real-time control';
  heroLead: string = WEBSITE_BRAND.mission;
  heroPrimaryCta = { label: 'Book a Fleet Demo →', link: '/request-demo' };
  heroSecondaryCta = { label: 'Our Services', link: '/', fragment: 'services' as string | undefined };
  cmsFeatures: { title: string; text: string; link: string }[] = [];

  readonly softStats = [
    { label: 'Live GPS Tracking' },
    { label: 'Vehicles & Drivers' },
    { label: 'Trips & Dispatch' },
    { label: 'Custom Software' },
  ] as const;

  readonly capabilityChips = [
    { label: 'Fleet Management', icon: 'local_shipping' },
    { label: 'Live GPS', icon: 'gps_fixed' },
    { label: 'Driver App', icon: 'smartphone' },
    { label: 'Web Development', icon: 'language' },
    { label: 'Cloud Solutions', icon: 'cloud' },
    { label: 'AI Solutions', icon: 'auto_awesome' },
  ] as const;

  /** Honest operational outcomes — no fabricated customer claims. */
  readonly outcomes = [
    {
      title: 'Stop calling drivers for location',
      text: 'Live map and online status so dispatch sees where every vehicle is without a phone loop.',
      icon: 'near_me',
    },
    {
      title: 'One place for fleet truth',
      text: 'Vehicles, drivers, assignments, trips and GPS history in a single operational workspace.',
      icon: 'hub',
    },
    {
      title: 'Driver app in the field',
      text: 'Assigned trips, status updates and mobile GPS where your drivers actually work.',
      icon: 'phone_android',
    },
    {
      title: 'Built for multi-tenant ops',
      text: 'Role-based access and tenant isolation so each operator only sees their own fleet.',
      icon: 'security',
    },
  ] as const;

  readonly trustedTech = [
    '.NET',
    'Angular',
    'Flutter',
    'SQL Server',
    'Traccar',
    'Azure',
  ] as const;

  readonly services = SERVICE_ITEMS;

  readonly aiFeatures = [
    'Notification intelligence',
    'Operational insights',
    'Workflow automation',
    'Secure model integration',
  ] as const;

  readonly industries = [
    ...INDUSTRY_ITEMS.map(i => ({
      title: i.title,
      link: i.link,
      icon: i.icon,
      fragment: i.fragment,
    })),
    { title: 'More Industries', link: '/contact', icon: 'add_circle', fragment: null as string | null },
  ];

  readonly whyItems = [
    {
      title: 'Operator-first product',
      text: 'SheikhGo Fleet is built around real dispatch, driver and GPS workflows — not a generic ERP skin.',
      icon: 'local_shipping',
    },
    {
      title: 'Real product screens',
      text: 'What you see on the Fleet page is captured from the running platform, not marketing mockups.',
      icon: 'screenshot_monitor',
    },
    {
      title: 'Custom when you need it',
      text: 'Web, mobile and integrations when off-the-shelf fleet software is not enough.',
      icon: 'code',
    },
    {
      title: 'Practical AI only',
      text: 'We apply AI where it reduces noise or speeds decisions — not as a buzzword layer.',
      icon: 'auto_awesome',
    },
    {
      title: 'Clear delivery',
      text: 'Discovery, scope, milestones and demos so you know what is shipping and when.',
      icon: 'schedule',
    },
    {
      title: 'Long-term partnership',
      text: 'Support and iteration after launch so the system stays useful as your fleet grows.',
      icon: 'groups',
    },
  ] as const;

  readonly processSteps = [
    { step: '01', title: 'Discover', text: 'Understand fleet size, ops pain and goals.', icon: 'search' },
    { step: '02', title: 'Demo', text: 'Walk through SheikhGo Fleet on real screens.', icon: 'play_circle' },
    { step: '03', title: 'Pilot', text: 'Start with a focused vehicle and driver set.', icon: 'flag' },
    { step: '04', title: 'Roll out', text: 'Expand modules, GPS devices and teams.', icon: 'rocket_launch' },
    { step: '05', title: 'Support', text: 'Iterate with training and ongoing support.', icon: 'support_agent' },
  ] as const;

  /** Real product captures — same assets as /fleet-management. */
  readonly productShots = [
    {
      src: '/website/product/live-map.png',
      alt: 'SheikhGo Fleet live GPS map',
      title: 'Live GPS map',
    },
    {
      src: '/website/product/dashboard.png',
      alt: 'SheikhGo Fleet operations dashboard',
      title: 'Operations dashboard',
    },
    {
      src: '/website/product/trips.png',
      alt: 'SheikhGo Fleet trip management',
      title: 'Trips',
    },
  ] as const;

  /** Fallback icons for CMS-managed "Featured capabilities" cards (no icon field on that model). */
  private readonly cmsFeatureIcons = ['bolt', 'hub', 'insights', 'auto_awesome'] as const;

  orbitCoreLabel = 'GPS';

  cmsFeatureIcon(index: number): string {
    return this.cmsFeatureIcons[index % this.cmsFeatureIcons.length];
  }

  readonly fleetCapabilities = [
    'Live GPS Tracking',
    'Vehicle Management',
    'Driver Management',
    'Trip Management',
    'Fleet Analytics',
    'GPS Route History',
    'AI-ready architecture',
  ] as const;

  ngOnInit(): void {
    this.seo.set(
      'SheikhGo Technologies | Fleet Management & Software',
      'SheikhGo Fleet brings live GPS, vehicles, drivers and trips into one platform — plus custom software and AI for transport and logistics operators.',
      '/',
    );

    this.content.getHome().subscribe(home => {
      if (!home) return;

      if (home.settings?.defaultMetaTitle || home.settings?.defaultMetaDescription) {
        this.seo.set(
          home.settings.defaultMetaTitle ||
            'SheikhGo Technologies | Fleet Management & Software',
          home.settings.defaultMetaDescription ||
            'SheikhGo Fleet brings live GPS, vehicles, drivers and trips into one platform — plus custom software and AI for transport and logistics operators.',
          '/',
        );
      }

      const hero = this.content.sectionByType(home.sections, 'Hero');
      if (hero) {
        if (hero.subtitle?.trim()) this.heroEyebrow = hero.subtitle.trim();
        if (hero.title?.trim()) {
          const parts = hero.title.trim().split(/\s+/);
          if (parts.length > 2) {
            this.heroTitlePrimary = parts.slice(0, Math.ceil(parts.length / 2)).join(' ');
            this.heroTitleAccent = parts.slice(Math.ceil(parts.length / 2)).join(' ');
          } else {
            this.heroTitlePrimary = hero.title.trim();
            this.heroTitleAccent = '';
          }
        }
        if (hero.content?.trim()) this.heroLead = hero.content.trim();
        if (hero.buttonText?.trim() && hero.buttonUrl?.trim()) {
          this.heroPrimaryCta = {
            label: hero.buttonText.trim(),
            link: hero.buttonUrl.trim(),
          };
        }
        if (hero.secondaryButtonText?.trim() && hero.secondaryButtonUrl?.trim()) {
          const url = hero.secondaryButtonUrl.trim();
          const hash = url.includes('#') ? url.split('#')[1] : undefined;
          this.heroSecondaryCta = {
            label: hero.secondaryButtonText.trim(),
            link: url.startsWith('#') ? '/' : url.split('#')[0] || '/',
            fragment: hash,
          };
        }
      }

      if (home.features?.length) {
        this.cmsFeatures = home.features.map(f => ({
          title: f.title,
          text: f.description || '',
          link: f.linkUrl || '/features',
        }));
      }
    });
  }
}
